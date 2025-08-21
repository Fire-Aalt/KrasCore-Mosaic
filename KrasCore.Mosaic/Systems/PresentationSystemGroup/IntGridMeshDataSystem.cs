using System;
using System.Runtime.InteropServices;
using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using Mesh = UnityEngine.Mesh;

namespace KrasCore.Mosaic
{
	[UpdateAfter(typeof(RuleEngineSystem))]
	[UpdateInGroup(typeof(TilemapUpdateSystemGroup))]
    public partial struct IntGridMeshDataSystem : ISystem
    {
	    public struct Singleton : IComponentData, IDisposable
	    {
		    public struct IntGrid : IDisposable
		    {
			    public Entity IntGridEntity;
			    
			    public UnsafeHashMap<int2, SpriteMesh> SpriteMeshes;
	            
			    public IntGrid(Entity intGridEntity, int capacity, Allocator allocator)
			    {
				    IntGridEntity = intGridEntity;
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

		    public NativeHashMap<Hash128, IntGrid> Tilemaps;
		    
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

			    Tilemaps = new NativeHashMap<Hash128, IntGrid>(capacity, allocator);
		    }

		    public void Dispose()
		    {
			    Layout.Dispose();
			    HashesToUpdate.Dispose();
			    UpdatedMeshBoundsMap.Dispose();
			    RenderingEntities.Dispose();
			    
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
	        var dataSingleton = SystemAPI.GetSingleton<RuleEngineSystem.Singleton>();
	        var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();
	        
	        var singleton = SystemAPI.GetSingletonRW<Singleton>().ValueRW;

	        var cullingBoundsChanged = !tcb.PrevCullingBounds.Value.Equals(tcb.CullingBounds.Value);
	        tcb.PrevCullingBounds.Value = tcb.CullingBounds.Value;
	        
	        state.Dependency = new FindHashesToUpdateJob
	        {
		        HashesToUpdate = singleton.HashesToUpdate,
		        IntGridLayers = dataSingleton.IntGridLayers,
		        CullingBoundsChanged = cullingBoundsChanged,
		        Tilemaps = singleton.Tilemaps,
	        }.Schedule(state.Dependency);
            
            state.Dependency = new PrepareAndCullSpriteMeshDataJob
            {
	            CullingBounds = tcb.CullingBounds.Value,
	            IntGridLayers = dataSingleton.IntGridLayers,
	            HashesToUpdate = singleton.HashesToUpdate.AsDeferredJobArray(),
	            Tilemaps = singleton.Tilemaps,
            }.Schedule(singleton.HashesToUpdate, 1, state.Dependency);
            
            state.Dependency = new GenerateIntGridMeshDataJob
            {
	            TilemapRendererDataLookup = SystemAPI.GetComponentLookup<TilemapRendererData>(true),
	            Layout = singleton.Layout,
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
	        public NativeHashMap<Hash128, RuleEngineSystem.IntGridLayer> IntGridLayers;
            
	        public bool CullingBoundsChanged;
	        
	        public NativeList<Hash128> HashesToUpdate;
	        public NativeHashMap<Hash128, Singleton.IntGrid> Tilemaps;
            
	        private void Execute(in IntGridData intGridData, in TilemapRendererData tilemapRendererData, Entity entity)
	        {
		        if (!Tilemaps.ContainsKey(tilemapRendererData.MeshHash))
		        {
			        var terrain = new Singleton.IntGrid(entity, 256, Allocator.Persistent);
                    
			        Tilemaps.Add(tilemapRendererData.MeshHash, terrain);
		        }
                
		        ref var dataLayer = ref IntGridLayers.GetValueAsRef(intGridData.Hash);
		        
		        if (!dataLayer.RefreshedPositions.IsEmpty || CullingBoundsChanged || dataLayer.Cleared)
		        {
			        HashesToUpdate.Add(tilemapRendererData.MeshHash);
		        }
	        }
        }
        
        [BurstCompile]
        private struct PrepareAndCullSpriteMeshDataJob : IJobParallelForDefer
        {
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
	        [ReadOnly]
	        public NativeHashMap<Hash128, RuleEngineSystem.IntGridLayer> IntGridLayers;
	        [ReadOnly]
	        public NativeHashMap<Hash128, Singleton.IntGrid> Tilemaps;

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
        private struct GenerateIntGridMeshDataJob : IJobParallelForDefer
        {
	        [ReadOnly]
	        public ComponentLookup<TilemapRendererData> TilemapRendererDataLookup;
	        
	        [ReadOnly]
	        public NativeArray<VertexAttributeDescriptor> Layout;
	        
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
	        [ReadOnly]
	        public NativeHashMap<Hash128, Singleton.IntGrid> Tilemaps;

	        public Mesh.MeshDataArray MeshDataArray;
	        public NativeParallelHashMap<Hash128, AABB>.ParallelWriter UpdatedMeshBoundsMapWriter;
	        
        	public void Execute(int index)
	        {
		        var hash = HashesToUpdate[index];
		        var meshData = MeshDataArray[index];
		        ref var intGrid = ref Tilemaps.GetValueAsRef(hash);
		        var rendererData = TilemapRendererDataLookup[intGrid.IntGridEntity];
		     
				var quadCount = intGrid.SpriteMeshes.Count;
                
				var vertexCount = quadCount * 4;
				var indexCount = quadCount * 6;
				
				PrepareMeshData(meshData, vertexCount, indexCount);
				
		        var vertices = meshData.GetVertexData<Vertex>();
				var indices = meshData.GetIndexData<int>();
				
				var quadIndex = 0;

				var minPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
				var maxPos = new float3(float.MinValue, float.MinValue, float.MinValue);
				
		        foreach (var kvp in intGrid.SpriteMeshes)
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