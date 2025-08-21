using System;
using System.Runtime.InteropServices;
using BovineLabs.Core.Extensions;
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

namespace KrasCore.Mosaic
{
	[UpdateAfter(typeof(TilemapMeshDataSystem))]
	[UpdateInGroup(typeof(TilemapUpdateSystemGroup))]
    public partial struct TerrainMeshDataSystem : ISystem
    {
	    public struct Singleton : IComponentData, IDisposable
	    {
	        public struct Terrain : IDisposable
	        {
	            public const int MaxLayersBlend = 4;
	            
	            public FixedList512Bytes<Hash128> Layers;
	            public float2 TileSize;
		        
	            public UnsafeHashMap<int2, FixedList64Bytes<GpuTerrainTile>> RawTilesToBlend;

	            public UnsafeList<GpuTerrainTile> TileBuffer;
	            public UnsafeList<GpuTerrainIndex> IndexBuffer;
	            
	            public Terrain(int capacity, Allocator allocator)
	            {
	                Layers = default;
	                TileSize = default;
	                
	                RawTilesToBlend = new UnsafeHashMap<int2, FixedList64Bytes<GpuTerrainTile>>(capacity, allocator);

	                var list = new FixedList64Bytes<GpuTerrainTile>();
	                if (list.Capacity < MaxLayersBlend)
	                {
	                    throw new Exception($"{nameof(Terrain)} has MaxLayersBlend set to {Singleton.Terrain.MaxLayersBlend}, but the capacity of a fixed list is {list.Capacity}");
	                }
	                
	                TileBuffer = new UnsafeList<GpuTerrainTile>(capacity, allocator);
	                IndexBuffer = new UnsafeList<GpuTerrainIndex>(capacity, allocator);
	            }
	            
	            public void Dispose()
	            {
	                RawTilesToBlend.Dispose();

	                TileBuffer.Dispose();
	                IndexBuffer.Dispose();
	            }
	        }
	        
	        public NativeArray<VertexAttributeDescriptor> Layout;
	        public NativeList<Hash128> HashesToUpdate;
	        public MeshDataArrayWrapper MeshDataArray;
	        public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;
	        public NativeList<Entity> RenderingEntities;
	        
	        public NativeList<TilemapRendererData> TilemapRendererData;
	        public NativeHashMap<Hash128, Terrain> Terrains;

	        public Singleton(int capacity, Allocator allocator)
	        {
	            Layout = new NativeArray<VertexAttributeDescriptor>(3, allocator);
	            Layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
	            Layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
	            Layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

	            HashesToUpdate = new NativeList<Hash128>(capacity, allocator);
	            MeshDataArray = default;
	            UpdatedMeshBoundsMap = new NativeParallelHashMap<Hash128, AABB>(capacity, allocator);
	            RenderingEntities = new NativeList<Entity>(capacity, allocator);
	            
	            TilemapRendererData = new NativeList<TilemapRendererData>(capacity, allocator);
	            Terrains = new NativeHashMap<Hash128, Terrain>(capacity, allocator);
	        }

	        public void Dispose()
	        {
	            Layout.Dispose();
	            HashesToUpdate.Dispose();
	            UpdatedMeshBoundsMap.Dispose();
	            RenderingEntities.Dispose();
	            
	            TilemapRendererData.Dispose();
	            foreach (var kvp in Terrains)
	            {
	                kvp.Value.Dispose();
	            }
	            Terrains.Dispose();
	        }
	    }
	    
	    
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        state.EntityManager.CreateSingleton(new Singleton(1, Allocator.Persistent));
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
	        SystemAPI.GetSingleton<Singleton>().Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dataSingleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();
            
            var singleton = SystemAPI.GetSingletonRW<Singleton>().ValueRW;

            var cullingBoundsChanged = !tcb.PrevCullingBounds.Value.Equals(tcb.CullingBounds.Value);
            tcb.PrevCullingBounds.Value = tcb.CullingBounds.Value;

            singleton.TilemapRendererData.Clear();
            
            state.Dependency = new FindHashesToUpdateJob
            {
	            IntGridLayers = dataSingleton.IntGridLayers,
	            HashesToUpdate = singleton.HashesToUpdate,
	            TilemapRendererData = singleton.TilemapRendererData,
	            CullingBoundsChanged = cullingBoundsChanged,
	            Terrains = singleton.Terrains
            }.Schedule(state.Dependency);

            state.Dependency = new PrepareAndCullSpriteMeshDataJob
            {
	            IntGridLayers = dataSingleton.IntGridLayers,
	            HashesToUpdate = singleton.HashesToUpdate.AsDeferredJobArray(),
	            Terrains = singleton.Terrains,
	            CullingBounds = tcb.CullingBounds.Value,
            }.Schedule(singleton.HashesToUpdate, 1, state.Dependency);
            
            state.Dependency = new GenerateTerrainMeshDataJob
            {
	            Layout = singleton.Layout,
	            HashesToUpdate = singleton.HashesToUpdate.AsDeferredJobArray(),
	            RendererData = singleton.TilemapRendererData.AsDeferredJobArray(),
	            Terrains = singleton.Terrains,
	            MeshDataArray = singleton.MeshDataArray.Array,
	            UpdatedMeshBoundsMapWriter = singleton.UpdatedMeshBoundsMap.AsParallelWriter()
            }.Schedule(singleton.HashesToUpdate, 1, state.Dependency);
        }

        [BurstCompile]
        private partial struct FindHashesToUpdateJob : IJobEntity
        {
            [ReadOnly]
            public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;
            
            public bool CullingBoundsChanged;
            public NativeList<Hash128> HashesToUpdate;
            public NativeList<TilemapRendererData> TilemapRendererData;
            public NativeHashMap<Hash128, Singleton.Terrain> Terrains;
            
            private void Execute(in TilemapTerrainData tilemapTerrainData, in TilemapRendererInitData tilemapRendererInitData, in TilemapRendererData rendererData)
            {
                if (!Terrains.ContainsKey(tilemapRendererInitData.MeshHash))
                {
                    var terrain = new Singleton.Terrain(256, Allocator.Persistent);
                    terrain.Layers = tilemapTerrainData.LayerHashes;
                    terrain.TileSize = tilemapTerrainData.TileSize;
                    
                    Terrains.Add(tilemapRendererInitData.MeshHash, terrain);
                }
                
                foreach (var intGridHash in tilemapTerrainData.LayerHashes)
                {
                    ref var dataLayer = ref IntGridLayers.GetValueAsRef(intGridHash);
                    
                    if (!dataLayer.RefreshedPositions.IsEmpty || CullingBoundsChanged || dataLayer.Cleared)
                    {
                        HashesToUpdate.Add(tilemapRendererInitData.MeshHash);
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
            public NativeHashMap<Hash128, Singleton.Terrain> Terrains;
	        
            public void Execute(int index)
            {
                ref var terrainData = ref Terrains.GetValueAsRef(HashesToUpdate[index]);
                
                terrainData.RawTilesToBlend.Clear();
                for (int stream = terrainData.Layers.Length - 1; stream >= 0; stream--)
                {
                    var intGridLayer = IntGridLayers[terrainData.Layers[stream]];

                    foreach (var kvp in intGridLayer.RenderedSprites)
                    {
	                    if (!CullingBounds.Contains(kvp.Key)) continue;
	                    
	                    ref var layers = ref terrainData.RawTilesToBlend.GetOrAddRef(kvp.Key);

	                    if (layers.Length == Singleton.Terrain.MaxLayersBlend)
	                    {
		                    continue;
	                    }
	                    
	                    var spriteMesh = kvp.Value;
	                    layers.Add(new GpuTerrainTile(spriteMesh.MinUv, 1, spriteMesh.Flip, spriteMesh.Rotation));
                    }
                }
            }
        }
        
	    [BurstCompile]
        private struct GenerateTerrainMeshDataJob : IJobParallelForDefer
        {
	        [ReadOnly]
	        public NativeArray<VertexAttributeDescriptor> Layout;
	        [ReadOnly]
	        public NativeHashMap<Hash128, Singleton.Terrain> Terrains;
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
	        
	        [ReadOnly]
	        public NativeArray<TilemapRendererData> RendererData;
	        
	        public Mesh.MeshDataArray MeshDataArray;
	        public NativeParallelHashMap<Hash128, AABB>.ParallelWriter UpdatedMeshBoundsMapWriter;
	        
        	public void Execute(int index)
	        {
		        var hash = HashesToUpdate[index];
		        var meshData = MeshDataArray[index];
		        var rendererData = RendererData[index];
		        ref var terrainData = ref Terrains.GetValueAsRef(hash);

		        var quadCount = terrainData.RawTilesToBlend.Count;
                
		        var vertexCount = quadCount * 4;
		        var indexCount = quadCount * 6;
		        
		        PrepareMeshData(meshData, vertexCount, indexCount);

		        terrainData.TileBuffer.Clear();
		        terrainData.IndexBuffer.Clear();
		        
		        var vertices = meshData.GetVertexData<Vertex>();
		        var indices = meshData.GetIndexData<int>();

		        var quadIndex = 0;
		        var orientation = rendererData.Orientation;
		        
		        var minPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
		        var maxPos = new float3(float.MinValue, float.MinValue, float.MinValue);
		        
		        foreach (var kvp in terrainData.RawTilesToBlend)
		        {
			        var worldPos = MosaicUtils.ToWorldSpace(kvp.Key, rendererData)
			                         + MosaicUtils.ApplyOrientation(float2.zero, orientation);

			        var rectSize = MosaicUtils.ApplySwizzle(rendererData.CellSize, rendererData.Swizzle).xy;
			        var rotatedSize = MosaicUtils.ApplyOrientation(rectSize, orientation);

			        var normal = MosaicUtils.ApplyOrientation(new float3(0, 0, 1), orientation);
			        var up = MosaicUtils.ApplyOrientation(new float3(0, 1, 0), orientation) * rotatedSize;
			        var right = MosaicUtils.ApplyOrientation(new float3(1, 0, 0), orientation) * rotatedSize;

			        var vc = 4 * quadIndex;
			        var tc = 6 * quadIndex;
			        
			        var minVertexPos = worldPos;
			        var maxVertexPos = worldPos + up + right;

			        minPos = math.min(minPos, minVertexPos);
			        maxPos = math.max(maxPos, maxVertexPos);
			        
			        vertices[vc + 0] = new Vertex
			        {
				        Position = worldPos + up,
				        Normal = normal,
				        TexCoord0 = new float2(0f, terrainData.TileSize.y)
			        };
			        vertices[vc + 1] = new Vertex
			        {
				        Position = maxVertexPos,
				        Normal = normal,
				        TexCoord0 = new float2(terrainData.TileSize.x, terrainData.TileSize.y)
			        };
			        vertices[vc + 2] = new Vertex
			        {
				        Position = worldPos + right,
				        Normal = normal,
				        TexCoord0 = new float2(terrainData.TileSize.x, 0f)
			        };
			        vertices[vc + 3] = new Vertex
			        {
				        Position = minVertexPos,
				        Normal = normal,
				        TexCoord0 = new float2(0f, 0f)
			        };

			        var startIndex = terrainData.TileBuffer.Length;
			        
			        foreach (var terrainTile in kvp.Value)
			        {
				        terrainData.TileBuffer.Add(terrainTile);
			        }
			        
			        terrainData.IndexBuffer.Add(new GpuTerrainIndex
			        {
				        StartIndex = (uint)startIndex,
				        EndIndex = (uint)terrainData.TileBuffer.Length
			        });
			        
			        indices[tc + 0] = (vc + 0);
			        indices[tc + 1] = (vc + 1);
			        indices[tc + 2] = (vc + 2);
			        
			        indices[tc + 3] = (vc + 0);
			        indices[tc + 4] = (vc + 2);
			        indices[tc + 5] = (vc + 3);
			        
			        quadIndex++;
		        }
		        
		        FinalizeMeshData(hash, meshData, indexCount, maxPos, minPos);
	        }
	        
	        private void PrepareMeshData(Mesh.MeshData meshData, int vertexCount, int indexCount)
	        {
		        meshData.SetVertexBufferParams(vertexCount, Layout);
		        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
	        }
	        
	        private void FinalizeMeshData(Hash128 hash, Mesh.MeshData meshData, int indexCount, float3 maxPos, float3 minPos)
	        {
		        meshData.subMeshCount = 1;
		        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));
		        
		        UpdatedMeshBoundsMapWriter.TryAdd(hash, new AABB
		        {
			        Center = (maxPos + minPos) * 0.5f,
			        Extents = (maxPos - minPos) * 0.5f,
		        });
	        }
        }
	    
	    [StructLayout(LayoutKind.Sequential)]
	    private struct Vertex
	    {
		    public float3 Position;
		    public float3 Normal;
		    public float2 TexCoord0;
	    }
    }
}