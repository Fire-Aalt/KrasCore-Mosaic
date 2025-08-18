using System;
using System.Runtime.InteropServices;
using BovineLabs.Core.Collections;
using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Hash128 = Unity.Entities.Hash128;
using TerrainData = KrasCore.Mosaic.Data.TerrainData;

namespace KrasCore.Mosaic
{
	[UpdateAfter(typeof(TilemapMeshDataSystem))]
	[UpdateInGroup(typeof(TilemapUpdateSystemGroup))]
    public partial struct TerrainMeshDataSystem : ISystem
    {
        private NativeArray<VertexAttributeDescriptor> _layout;
        private NativeList<Hash128> _terrainHashesToUpdate;
        private NativeList<TilemapRendererData> _tilemapRendererData;
        private NativeHashMap<Hash128, Terrain> _terrains;
        
        public struct SpriteMeshWithStream
        {
            public SpriteMesh SpriteMesh;
            public byte Stream;
        }
        
        
        [StructLayout(LayoutKind.Sequential)]
        public struct OffsetData
        {
	        public half2 Layer1TexCoordOffset;
	        public half2 Layer2TexCoordOffset;
        }
        
        public struct Terrain : IDisposable
        {
            public FixedList512Bytes<Hash128> Layers;
            public UnsafeHashSet<int2> UniquePositionsSet;
            public UnsafeMultiHashMap<int2, SpriteMeshWithStream> SpriteMeshMap;

            public UnsafeArray<OffsetData> OffsetDataPixelArray;
            public UnsafeArray<byte> ControlDataPixelArray;
            
            public Terrain(int capacity, Allocator allocator)
            {
                Layers = default;
                OffsetDataPixelArray = default;
                ControlDataPixelArray = default;
                UniquePositionsSet = new UnsafeHashSet<int2>(capacity, allocator);
                SpriteMeshMap = new UnsafeMultiHashMap<int2, SpriteMeshWithStream>(capacity, allocator);
            }
            
            public void Dispose()
            {
                UniquePositionsSet.Dispose();
                SpriteMeshMap.Dispose();
            }
        }
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _layout = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Persistent);
            _layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
            _layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
            _layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
            _layout[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord4, VertexAttributeFormat.Float32, 2);

            _tilemapRendererData = new NativeList<TilemapRendererData>(1, Allocator.Persistent);
            _terrainHashesToUpdate = new NativeList<Hash128>(1, Allocator.Persistent);
            _terrains = new NativeHashMap<Hash128, Terrain>(1, Allocator.Persistent);
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _layout.Dispose();

            foreach (var kvp in _terrains)
            {
                kvp.Value.Dispose();
            }
            _terrains.Dispose();

            _tilemapRendererData.Dispose();
            _terrainHashesToUpdate.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dataSingleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            var meshDataSingleton = SystemAPI.GetSingletonRW<TilemapMeshDataSingleton>().ValueRW;
            var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();

            var cullingBoundsChanged = !tcb.PrevCullingBounds.Value.Equals(tcb.CullingBounds.Value);
            tcb.PrevCullingBounds.Value = tcb.CullingBounds.Value;
            
            _terrainHashesToUpdate.Clear();

            state.Dependency = new FindHashesToUpdateJob
            {
	            IntGridLayers = dataSingleton.IntGridLayers,
	            TilemapRendererData = _tilemapRendererData,
	            CullingBoundsChanged = cullingBoundsChanged,
	            TerrainHashesToUpdate = _terrainHashesToUpdate,
	            MeshHashesToUpdate = meshDataSingleton.HashesToUpdate,
	            Terrains = _terrains
            }.Schedule(state.Dependency);

            state.Dependency = new PrepareAndCullSpriteMeshDataJob
            {
	            HashesToUpdate = _terrainHashesToUpdate.AsDeferredJobArray(),
	            IntGridLayers = dataSingleton.IntGridLayers,
	            Terrains = _terrains,
	            CullingBounds = tcb.CullingBounds.Value,
            }.Schedule(_terrainHashesToUpdate, 1, state.Dependency);

            state.Dependency = new SetBufferParamsJob
            {
	            Layout = _layout,
	            HashesToUpdate = _terrainHashesToUpdate.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
	            Terrains = _terrains,
            }.Schedule(_terrainHashesToUpdate, 1, state.Dependency);
            
            state.Dependency = new GenerateVertexDataJob()
            {
	            HashesToUpdate = _terrainHashesToUpdate.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
	            Terrains = _terrains,
	            LayerData = _tilemapRendererData.AsDeferredJobArray()
            }.Schedule(_terrainHashesToUpdate, 1, state.Dependency);
            
            state.Dependency = new FinalizeMeshDataJob()
            {
	            HashesToUpdate = _terrainHashesToUpdate.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
	            Terrains = _terrains,
	            UpdatedMeshBoundsMapWriter = meshDataSingleton.UpdatedMeshBoundsMap.AsParallelWriter()
            }.Schedule(_terrainHashesToUpdate, 1, state.Dependency);
        }

        [BurstCompile]
        private partial struct FindHashesToUpdateJob : IJobEntity
        {
            [ReadOnly]
            public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;
            
            public bool CullingBoundsChanged;
            public NativeList<Hash128> TerrainHashesToUpdate; // For terrain only
            public NativeList<Hash128> MeshHashesToUpdate; // For presentation system
            public NativeList<TilemapRendererData> TilemapRendererData;//TODO: MeshDataArray is shared for Terrain and Tilemap -> CRASH!!!
            public NativeHashMap<Hash128, Terrain> Terrains;
            
            private void Execute(in TerrainData terrainData, in TilemapRendererInitData tilemapRendererInitData, in TilemapRendererData rendererData)
            {
                if (!Terrains.ContainsKey(tilemapRendererInitData.MeshHash))
                {
                    var terrain = new Terrain(256, Allocator.Persistent);
                    terrain.Layers = terrainData.LayerHashes;
                    Terrains.Add(tilemapRendererInitData.MeshHash, terrain);
                }
                
                foreach (var intGridHash in terrainData.LayerHashes)
                {
                    ref var dataLayer = ref IntGridLayers.GetValueAsRef(intGridHash);
                    
                    if (!dataLayer.RefreshedPositions.IsEmpty || CullingBoundsChanged || dataLayer.Cleared)
                    {
                        TerrainHashesToUpdate.Add(tilemapRendererInitData.MeshHash);
                        MeshHashesToUpdate.Add(tilemapRendererInitData.MeshHash);
                        TilemapRendererData.Add(rendererData);
                        return;
                    }
                }
            }
        }
        
        
        [BurstCompile]
        private struct PrepareAndCullSpriteMeshDataJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeArray<Hash128> HashesToUpdate;
            [ReadOnly]
            public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;
            
            public AABB2D CullingBounds;
            [ReadOnly]
            public NativeHashMap<Hash128, Terrain> Terrains;
	        
            public void Execute(int index)
            {
                var hash = HashesToUpdate[index];
                Debug.Log(index + " " + hash);
                ref var terrainData = ref Terrains.GetValueAsRef(hash);
                
                terrainData.SpriteMeshMap.Clear();
                terrainData.UniquePositionsSet.Clear();
                for (byte stream = 0; stream < terrainData.Layers.Length; stream++)
                {
                    var intGridLayer = IntGridLayers[terrainData.Layers[stream]];

                    foreach (var kvp in intGridLayer.RenderedSprites)
                    {
                        if (CullingBounds.Contains(kvp.Key))
                        {
                            terrainData.SpriteMeshMap.Add(kvp.Key, new SpriteMeshWithStream
                            {
                                SpriteMesh = kvp.Value,
                                Stream = stream
                            });
                            terrainData.UniquePositionsSet.Add(kvp.Key);

                        }
                    }
                }
            }
        }
        
        [BurstCompile]
        private struct SetBufferParamsJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeArray<VertexAttributeDescriptor> Layout;
            [ReadOnly]
            public NativeHashMap<Hash128, Terrain> Terrains;
            [ReadOnly]
            public NativeArray<Hash128> HashesToUpdate;
            
            public Mesh.MeshDataArray MeshDataArray;
	        
            public void Execute(int index)
            {
                var meshData = MeshDataArray[index];
                var terrainData = Terrains[HashesToUpdate[index]];

                var count = terrainData.UniquePositionsSet.Count;
                
                var vertexCount = count * 4;
                var indexCount = count * 6;
		        
                meshData.SetVertexBufferParams(vertexCount, Layout);
                meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            }
        }
        
	    [BurstCompile]
        private struct GenerateVertexDataJob : IJobParallelForDefer
        {
	        [ReadOnly]
	        public NativeHashMap<Hash128, Terrain> Terrains;
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
            
	        public Mesh.MeshDataArray MeshDataArray;
	        
	        [ReadOnly]
	        public NativeArray<TilemapRendererData> LayerData;
	        
	        
        	public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var rendererData = LayerData[index];
		        var terrainData = Terrains[HashesToUpdate[index]];

		        var orientation = rendererData.Orientation;
		        
		        var vertices = meshData.GetVertexData<Vertex>();
		        var indices = meshData.GetIndexData<int>();

		        var vertexIndex = 0;
		        foreach (var pos in terrainData.UniquePositionsSet)
		        {
			        var worldPos = MosaicUtils.ToWorldSpace(pos, rendererData)
			                         + MosaicUtils.ApplyOrientation(float2.zero, orientation);

			        var rectSize = MosaicUtils.ApplySwizzle(rendererData.CellSize, rendererData.Swizzle).xy;
			        
			        var pivotPoint = MosaicUtils.ApplyOrientation(rectSize * 0.5f, orientation);
			        var rotatedSize = MosaicUtils.ApplyOrientation(rectSize, orientation);
			        
			        var normal = MosaicUtils.ApplyOrientation(new float3(0, 0, 1), orientation);
			        
			        var up = MosaicUtils.ApplyOrientation(new float3(0, 1, 0), orientation) * rotatedSize;
			        var right = MosaicUtils.ApplyOrientation(new float3(1, 0, 0), orientation) * rotatedSize;

			        var vc = 4 * vertexIndex;
			        var tc = 6 * vertexIndex;

			        var v0Pos = worldPos + pivotPoint
			                             + up - pivotPoint;
			        var v1Pos = worldPos + pivotPoint
			                             + up + right - pivotPoint;
			        var v2Pos = worldPos + pivotPoint 
			                             + right - pivotPoint;
			        var v3Pos = worldPos + pivotPoint 
			                    - pivotPoint;
			         
			        // vertices[vc + 0] = new Vertex
			        // {
				       //  Position = v0Pos,
				       //  Normal = normal,
				       //  TexCoord0 = ,
				       //  TexCoord4 = 
			        // }
			        // SetNormal(vc + 0, ref normal);
			        // SetNormal(vc + 1, ref normal);
			        // SetNormal(vc + 2, ref normal);
			        // SetNormal(vc + 3, ref normal);
			        //
			        // SetPosition(vc + 0, ref v0Pos);
			        // SetPosition(vc + 1, ref v1Pos);
			        // SetPosition(vc + 2, ref v2Pos);
			        // SetPosition(vc + 3, ref v3Pos);
			        
			        foreach (var spriteMeshWithStream in terrainData.SpriteMeshMap.GetValuesForKey(pos))
			        {
						var spriteMesh = spriteMeshWithStream.SpriteMesh;
						var stream = spriteMeshWithStream.Stream;
			   if (stream != 0) continue;
						var minUv = (half2)new float2(
							spriteMesh.Flip.x ? spriteMesh.MaxUv.x : spriteMesh.MinUv.x,
							spriteMesh.Flip.y ? spriteMesh.MaxUv.y : spriteMesh.MinUv.y);
						var maxUv = (half2)new float2(
							spriteMesh.Flip.x ? spriteMesh.MinUv.x : spriteMesh.MaxUv.x,
							spriteMesh.Flip.y ? spriteMesh.MinUv.y : spriteMesh.MaxUv.y);
			   
						var texCoord0 = new half2(minUv.x, maxUv.y);
						var texCoord1 = new half2(maxUv.x, maxUv.y);
						var texCoord2 = new half2(maxUv.x, minUv.y);
						var texCoord3 = new half2(minUv.x, minUv.y);
												
						// SetTexCoord(vc + (0 + spriteMesh.Rotation) % 4, stream, ref texCoord0);
						// SetTexCoord(vc + (1 + spriteMesh.Rotation) % 4, stream, ref texCoord1);
						// SetTexCoord(vc + (2 + spriteMesh.Rotation) % 4, stream, ref texCoord2);
						// SetTexCoord(vc + (3 + spriteMesh.Rotation) % 4, stream, ref texCoord3);
			        }
			        
			        indices[tc + 0] = (vc + 0);
			        indices[tc + 1] = (vc + 1);
			        indices[tc + 2] = (vc + 2);
			        
			        indices[tc + 3] = (vc + 0);
			        indices[tc + 4] = (vc + 2);
			        indices[tc + 5] = (vc + 3);
			        
			        vertexIndex++;
		        }
        	}
        }
        
	    [BurstCompile]
	    private struct FinalizeMeshDataJob : IJobParallelForDefer
	    {
		    [ReadOnly]
		    public NativeArray<Hash128> HashesToUpdate;
		    [ReadOnly]
		    public NativeHashMap<Hash128, Terrain> Terrains;
		    
		    public NativeParallelHashMap<Hash128, AABB>.ParallelWriter UpdatedMeshBoundsMapWriter;
		    public Mesh.MeshDataArray MeshDataArray;
	        
		    private unsafe float3 GetPosition(in NativeArray<byte> vertices, int vertexSize, int vertexIndex)
		    {
			    var ptr = (byte*)vertices.GetUnsafePtr()
			              + vertexSize * vertexIndex; // Offset = 0

			    UnsafeUtility.CopyPtrToStructure(ptr, out float3 pos);
			    return pos;
		    }
		    
		    public void Execute(int index)
		    {
			    var meshData = MeshDataArray[index];
			    var terrainData = Terrains[HashesToUpdate[index]];

			    //var vertexSize = Layouts[terrainData.Layers.Length].ByteSize;
			    
			    var vertices = meshData.GetVertexData<byte>();
			    
			    var minPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
			    var maxPos = new float3(float.MinValue, float.MinValue, float.MinValue);

			    for (int i = 0; i < terrainData.UniquePositionsSet.Count * 4; i++)
			    {
				    // var position = GetPosition(vertices, vertexSize, i);
				    // minPos = math.min(minPos, position);
				    // maxPos = math.max(maxPos, position);
			    }
			    
			    meshData.subMeshCount = 1;
			    meshData.SetSubMesh(0, new SubMeshDescriptor(0, terrainData.UniquePositionsSet.Count * 6));
			    
			    UpdatedMeshBoundsMapWriter.TryAdd(HashesToUpdate[index], new AABB
			    {
				    Center = (maxPos + minPos) * 0.5f,
				    Extents = (maxPos - minPos) * 0.5f,
			    });
		    }
	    }
	    
	    
	    [StructLayout(LayoutKind.Sequential)]
	    public struct Vertex
	    {
		    public float3 Position;
		    public float3 Normal;
		    public float2 TexCoord0;
		    public float2 TexCoord4;
	    }
    }
}