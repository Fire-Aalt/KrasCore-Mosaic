using System;
using System.Runtime.InteropServices;
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
	[UpdateAfter(typeof(RuleEngineSystem))]
	[UpdateInGroup(typeof(TilemapUpdateSystemGroup))]
    public partial struct TilemapMeshDataSystem : ISystem
    {
	    public struct Singleton : IComponentData, IDisposable
	    {
		    public struct Tilemap : IDisposable
		    {
			    public UnsafeHashMap<int2, SpriteMesh> SpriteMeshes;
	            
			    public Tilemap(int capacity, Allocator allocator)
			    {
				    SpriteMeshes = new UnsafeHashMap<int2, SpriteMesh>(capacity, allocator);
			    }
	            
			    public void Dispose()
			    {
				    SpriteMeshes.Dispose();
			    }
		    }
		    
		    public NativeArray<VertexAttributeDescriptor> Layout;
		    public NativeList<Hash128> HashesToUpdate;
		    public MeshDataArrayWrapper MeshDataArray;
		    public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;
		    public NativeList<Entity> RenderingEntities;

		    public NativeList<TilemapRendererData> TilemapsRendererData;
		    public NativeHashMap<Hash128, Tilemap> Tilemaps;
		    
		    public Singleton(int capacity, Allocator allocator)
		    {
			    Layout = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Persistent);
			    Layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
			    Layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
			    Layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

			    HashesToUpdate = new NativeList<Hash128>(capacity, allocator);
			    MeshDataArray = default;
			    UpdatedMeshBoundsMap = new NativeParallelHashMap<Hash128, AABB>(capacity, allocator);
			    RenderingEntities = new NativeList<Entity>(capacity, allocator);

			    TilemapsRendererData = new NativeList<TilemapRendererData>(capacity, allocator);
			    Tilemaps = new NativeHashMap<Hash128, Tilemap>(capacity, allocator);
		    }

		    public void Dispose()
		    {
			    Layout.Dispose();
			    HashesToUpdate.Dispose();
			    UpdatedMeshBoundsMap.Dispose();
			    RenderingEntities.Dispose();
			    
			    TilemapsRendererData.Dispose();
			    foreach (var kvp in Tilemaps)
			    {
				    kvp.Value.Dispose();
			    }
			    Tilemaps.Dispose();
		    }
	    }
	    
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        state.EntityManager.CreateSingleton(new Singleton(8, Allocator.Persistent));
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
	        
	        singleton.TilemapsRendererData.Clear();
	        
	        state.Dependency = new FindHashesToUpdateJob
	        {
		        HashesToUpdate = singleton.HashesToUpdate,
		        IntGridLayers = dataSingleton.IntGridLayers,
		        CullingBoundsChanged = cullingBoundsChanged,
		        Tilemaps = singleton.Tilemaps,
		        TilemapRendererData = singleton.TilemapsRendererData,
	        }.Schedule(state.Dependency);
            
            state.Dependency = new PrepareAndCullSpriteMeshDataJob
            {
	            CullingBounds = tcb.CullingBounds.Value,
	            IntGridLayers = dataSingleton.IntGridLayers,
	            HashesToUpdate = singleton.HashesToUpdate.AsDeferredJobArray(),
	            Tilemaps = singleton.Tilemaps,
            }.Schedule(singleton.HashesToUpdate, 1, state.Dependency);
            
            state.Dependency = new GenerateTilemapMeshDataJob
            {
	            Layout = singleton.Layout,
	            RendererData = singleton.TilemapsRendererData.AsDeferredJobArray(),
	            Tilemaps = singleton.Tilemaps,
	            HashesToUpdate = singleton.HashesToUpdate.AsDeferredJobArray(),
	            MeshDataArray = singleton.MeshDataArray.Array,
	            UpdatedMeshBoundsMapWriter = singleton.UpdatedMeshBoundsMap.AsParallelWriter(),
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
	        public NativeHashMap<Hash128, Singleton.Tilemap> Tilemaps;
            
	        private void Execute(in TilemapData tilemapData, in TilemapRendererInitData tilemapRendererInitData, in TilemapRendererData rendererData)
	        {
		        if (!Tilemaps.ContainsKey(tilemapRendererInitData.MeshHash))
		        {
			        var terrain = new Singleton.Tilemap(256, Allocator.Persistent);
                    
			        Tilemaps.Add(tilemapRendererInitData.MeshHash, terrain);
		        }
                
		        ref var dataLayer = ref IntGridLayers.GetValueAsRef(tilemapData.IntGridHash);
		        
		        if (!dataLayer.RefreshedPositions.IsEmpty || CullingBoundsChanged || dataLayer.Cleared)
		        {
			        HashesToUpdate.Add(tilemapRendererInitData.MeshHash);
			        TilemapRendererData.Add(rendererData);
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
	        [ReadOnly]
	        public NativeHashMap<Hash128, Singleton.Tilemap> Tilemaps;

	        public AABB2D CullingBounds;
	        
	        public void Execute(int index)
	        {
		        var data = IntGridLayers[HashesToUpdate[index]];
		        ref var tilemap = ref Tilemaps.GetValueAsRef(HashesToUpdate[index]);
                
		        tilemap.SpriteMeshes.Clear();
		        foreach (var kvp in data.RenderedSprites)
		        {
			        if (!CullingBounds.Contains(kvp.Key)) continue;
	                    
			        tilemap.SpriteMeshes.Add(kvp.Key, kvp.Value);
		        }
	        }
        }
        
        [BurstCompile]
        private struct GenerateTilemapMeshDataJob : IJobParallelForDefer
        {
	        [ReadOnly]
	        public NativeArray<VertexAttributeDescriptor> Layout;
	        
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
	        [ReadOnly]
	        public NativeArray<TilemapRendererData> RendererData;
	        [ReadOnly]
	        public NativeHashMap<Hash128, Singleton.Tilemap> Tilemaps;

	        public NativeParallelHashMap<Hash128, AABB>.ParallelWriter UpdatedMeshBoundsMapWriter;
	        public Mesh.MeshDataArray MeshDataArray;
	        
        	public void Execute(int index)
	        {
		        var hash = HashesToUpdate[index];
		        var meshData = MeshDataArray[index];
		        var rendererData = RendererData[index];
		        ref var tilemap = ref Tilemaps.GetValueAsRef(hash);
		     
				var quadCount = tilemap.SpriteMeshes.Count;
                
				var vertexCount = quadCount * 4;
				var indexCount = quadCount * 6;
				
				PrepareMeshData(meshData, vertexCount, indexCount);
				
		        var vertices = meshData.GetVertexData<Vertex>();
				var indices = meshData.GetIndexData<int>();
				
				var quadIndex = 0;

				var minPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
				var maxPos = new float3(float.MinValue, float.MinValue, float.MinValue);
				
		        foreach (var kvp in tilemap.SpriteMeshes)
		        {
			        var spriteMesh = kvp.Value;
			        var orientation = rendererData.Orientation;
			        
			        MosaicUtils.GetSpriteMeshTranslation(spriteMesh, out var meshTranslation);

			        var worldPos = MosaicUtils.ToWorldSpace(kvp.Key, rendererData)
			                         + MosaicUtils.ApplyOrientation(meshTranslation, orientation);

			        var pivotPoint = MosaicUtils.ApplyOrientation(spriteMesh.RectScale * spriteMesh.NormalizedPivot, orientation);
			        
			        var rotatedSize = MosaicUtils.ApplyOrientation(spriteMesh.RectScale, orientation);
			        
			        var normal = MosaicUtils.ApplyOrientation(new float3(0, 0, 1), orientation);
			        var up = MosaicUtils.ApplyOrientation(new float3(0, 1, 0), orientation) * rotatedSize;
			        var right = MosaicUtils.ApplyOrientation(new float3(1, 0, 0), orientation) * rotatedSize;

        			var vc = 4 * quadIndex;
			        var tc = 6 * quadIndex;

			        var minUv = new float2(
				        spriteMesh.Flip.x ? spriteMesh.MaxUv.x : spriteMesh.MinUv.x,
				        spriteMesh.Flip.y ? spriteMesh.MaxUv.y : spriteMesh.MinUv.y);
			        var maxUv = new float2(
				        spriteMesh.Flip.x ? spriteMesh.MinUv.x : spriteMesh.MaxUv.x,
				        spriteMesh.Flip.y ? spriteMesh.MinUv.y : spriteMesh.MaxUv.y);

			        var minVertexPos = worldPos + MosaicUtils.Rotate(-pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint;
			        var maxVertexPos = worldPos + MosaicUtils.Rotate(up + right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint;
			        
			        minPos = math.min(minPos, minVertexPos);
			        maxPos = math.max(maxPos, maxVertexPos);
			        
			        vertices[vc + 0] = new Vertex
        			{
        				Position = worldPos + MosaicUtils.Rotate(up - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
				        Normal = normal,
        				TexCoord0 = new float2(minUv.x, maxUv.y)
        			};

			        vertices[vc + 1] = new Vertex
        			{
        				Position = maxVertexPos,
				        Normal = normal,
        				TexCoord0 = new float2(maxUv.x, maxUv.y)
        			};

			        vertices[vc + 2] = new Vertex
        			{
        				Position = worldPos + MosaicUtils.Rotate(right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
				        Normal = normal,
        				TexCoord0 = new float2(maxUv.x, minUv.y)
        			};

			        vertices[vc + 3] = new Vertex
        			{
        				Position = minVertexPos,
				        Normal = normal,
        				TexCoord0 = new float2(minUv.x, minUv.y)
        			};
				        
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