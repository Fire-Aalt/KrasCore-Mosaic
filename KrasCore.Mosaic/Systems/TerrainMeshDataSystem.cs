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
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        state.EntityManager.CreateSingleton(new TilemapTerrainMeshDataSingleton(1, Allocator.Persistent));
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
	        SystemAPI.GetSingleton<TilemapTerrainMeshDataSingleton>().Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dataSingleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            var meshDataSingleton = SystemAPI.GetSingletonRW<TilemapMeshDataSingleton>().ValueRW;
            var terrainData = SystemAPI.GetSingletonRW<TilemapTerrainMeshDataSingleton>().ValueRW;
            var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();

            var cullingBoundsChanged = !tcb.PrevCullingBounds.Value.Equals(tcb.CullingBounds.Value);
            tcb.PrevCullingBounds.Value = tcb.CullingBounds.Value;

            state.Dependency = new FindHashesToUpdateJob
            {
	            IntGridLayers = dataSingleton.IntGridLayers,
	            TilemapRendererData = terrainData.TilemapRendererData,
	            CullingBoundsChanged = cullingBoundsChanged,
	            TerrainHashesToUpdate = meshDataSingleton.TerrainHashesToUpdate,
	            MeshHashesToUpdate = meshDataSingleton.HashesToUpdate,
	            Terrains = terrainData.Terrains
            }.Schedule(state.Dependency);

            state.Dependency = new PrepareAndCullSpriteMeshDataJob
            {
	            HashesToUpdate = meshDataSingleton.TerrainHashesToUpdate.AsDeferredJobArray(),
	            IntGridLayers = dataSingleton.IntGridLayers,
	            Terrains = terrainData.Terrains,
	            CullingBounds = tcb.CullingBounds.Value,
            }.Schedule(meshDataSingleton.TerrainHashesToUpdate, 1, state.Dependency);

            state.Dependency = new SetBufferParamsJob
            {
	            Layout = terrainData.Layout,
	            HashesToUpdate = meshDataSingleton.TerrainHashesToUpdate.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
	            Terrains = terrainData.Terrains,
            }.Schedule(meshDataSingleton.TerrainHashesToUpdate, 1, state.Dependency);
            
            state.Dependency = new GenerateVertexDataJob
            {
	            HashesToUpdate = meshDataSingleton.TerrainHashesToUpdate.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
	            Terrains = terrainData.Terrains,
	            LayerData = terrainData.TilemapRendererData.AsDeferredJobArray()
            }.Schedule(meshDataSingleton.TerrainHashesToUpdate, 1, state.Dependency);
            
            state.Dependency = new FinalizeMeshDataJob
            {
	            HashesToUpdate = meshDataSingleton.TerrainHashesToUpdate.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
	            UpdatedMeshBoundsMapWriter = meshDataSingleton.UpdatedMeshBoundsMap.AsParallelWriter()
            }.Schedule(meshDataSingleton.TerrainHashesToUpdate, 1, state.Dependency);
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
            public NativeHashMap<Hash128, TilemapTerrainMeshDataSingleton.Terrain> Terrains;
            
            private void Execute(in TilemapTerrainData tilemapTerrainData, in TilemapRendererInitData tilemapRendererInitData, in TilemapRendererData rendererData)
            {
                if (!Terrains.ContainsKey(tilemapRendererInitData.MeshHash))
                {
                    var terrain = new TilemapTerrainMeshDataSingleton.Terrain(256, Allocator.Persistent);
                    terrain.Layers = tilemapTerrainData.LayerHashes;
                    terrain.TileSize = tilemapTerrainData.TileSize;
                    
                    Terrains.Add(tilemapRendererInitData.MeshHash, terrain);
                }
                
                foreach (var intGridHash in tilemapTerrainData.LayerHashes)
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
            public NativeHashMap<Hash128, TilemapTerrainMeshDataSingleton.Terrain> Terrains;
	        
            public void Execute(int index)
            {
                var hash = HashesToUpdate[index];
                Debug.Log(index + " " + hash);
                ref var terrainData = ref Terrains.GetValueAsRef(hash);
                
                terrainData.SpriteMeshMap.Clear();
                terrainData.UniquePositionsSet.Clear();
                for (int stream = terrainData.Layers.Length - 1; stream >= 0; stream--)
                {
                    var intGridLayer = IntGridLayers[terrainData.Layers[stream]];

                    foreach (var kvp in intGridLayer.RenderedSprites)
                    {
                        if (CullingBounds.Contains(kvp.Key))
                        {
	                        ref var layers = ref terrainData.SpriteMeshMap.GetOrAddRef(kvp.Key);

	                        if (layers.Length + 1 <= layers.Capacity)
	                        {
		                        var spriteMesh = kvp.Value;
								layers.Add(new GpuTerrainTile(spriteMesh.MinUv, 1, spriteMesh.Flip, spriteMesh.Rotation));
								terrainData.UniquePositionsSet.Add(kvp.Key);
	                        }
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
            public NativeHashMap<Hash128, TilemapTerrainMeshDataSingleton.Terrain> Terrains;
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
	        public NativeHashMap<Hash128, TilemapTerrainMeshDataSingleton.Terrain> Terrains;
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
            
	        public Mesh.MeshDataArray MeshDataArray;
	        
	        [ReadOnly]
	        public NativeArray<TilemapRendererData> LayerData;
	        
        	public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var rendererData = LayerData[index];
		        ref var terrainData = ref Terrains.GetValueAsRef(HashesToUpdate[index]);

		        var orientation = rendererData.Orientation;
		        
		        var vertices = meshData.GetVertexData<Vertex>();
		        var indices = meshData.GetIndexData<int>();

		        terrainData.TileBuffer.Clear();
		        terrainData.IndexBuffer.Clear();
		        var quadIndex = 0;
		        
		        foreach (var pos in terrainData.UniquePositionsSet)
		        {
			        var worldPos = MosaicUtils.ToWorldSpace(pos, rendererData)
			                         + MosaicUtils.ApplyOrientation(float2.zero, orientation);

			        var rectSize = MosaicUtils.ApplySwizzle(rendererData.CellSize, rendererData.Swizzle).xy;
			        var rotatedSize = MosaicUtils.ApplyOrientation(rectSize, orientation);
			        
			        var normal = MosaicUtils.ApplyOrientation(new float3(0, 0, 1), orientation);
			        
			        var up = MosaicUtils.ApplyOrientation(new float3(0, 1, 0), orientation) * rotatedSize;
			        var right = MosaicUtils.ApplyOrientation(new float3(1, 0, 0), orientation) * rotatedSize;

			        var vc = 4 * quadIndex;
			        var tc = 6 * quadIndex;

			        vertices[vc + 0] = new Vertex
			        {
				        Position = worldPos + up,
				        Normal = normal,
				        TexCoord0 = new float2(0f, terrainData.TileSize.y)
			        };
			        vertices[vc + 1] = new Vertex
			        {
				        Position = worldPos + up + right,
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
				        Position = worldPos,
				        Normal = normal,
				        TexCoord0 = new float2(0f, 0f)
			        };


			        var startIndex = terrainData.TileBuffer.Length;
			        const int MAX_LAYERS = 4;
			        var layersBlended = 0;
			        foreach (var terrainTile in terrainData.SpriteMeshMap[pos])
			        {
				        terrainData.TileBuffer.Add(terrainTile);

				        layersBlended++;
				        if (layersBlended == MAX_LAYERS)
				        {
					        break;
				        }
			        }
			        
			        terrainData.IndexBuffer.Add(new GpuTerrainIndex
			        {
				        StartIndex = (uint)startIndex,
				        Count = (uint)layersBlended
			        });
			        
			        indices[tc + 0] = (vc + 0);
			        indices[tc + 1] = (vc + 1);
			        indices[tc + 2] = (vc + 2);
			        
			        indices[tc + 3] = (vc + 0);
			        indices[tc + 4] = (vc + 2);
			        indices[tc + 5] = (vc + 3);
			        
			        quadIndex++;
		        }
        	}
        }
        
	    [BurstCompile]
	    private struct FinalizeMeshDataJob : IJobParallelForDefer
	    {
		    [ReadOnly]
		    public NativeArray<Hash128> HashesToUpdate;
		    
		    public NativeParallelHashMap<Hash128, AABB>.ParallelWriter UpdatedMeshBoundsMapWriter;
		    public Mesh.MeshDataArray MeshDataArray;
		    
		    public void Execute(int index)
		    {
			    var meshData = MeshDataArray[index];
			    
			    var vertices = meshData.GetVertexData<Vertex>();
			    var indices = meshData.GetIndexData<int>();
			    
			    var minPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
			    var maxPos = new float3(float.MinValue, float.MinValue, float.MinValue);

			    for (int i = 0; i < vertices.Length; i++)
			    {
				    var position = vertices[i].Position;
				    minPos = math.min(minPos, position);
				    maxPos = math.max(maxPos, position);
			    }
			    
			    meshData.subMeshCount = 1;
			    meshData.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length));
			    
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
	    }
    }
}