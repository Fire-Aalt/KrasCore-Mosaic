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
		    public NativeArray<VertexAttributeDescriptor> Layout;
		    public NativeList<Hash128> HashesToUpdate;
		    public MeshDataArrayWrapper MeshDataArray;
		    public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;
		    public NativeList<Entity> RenderingEntities;
		    
		    public NativeList<int2> Positions;
		    public NativeList<SpriteMesh> SpriteMeshes;
		    public NativeList<Vertex> Vertices;
		    public NativeList<int> Indices;
		    public NativeList<int> LayerPointers;
        
		    public NativeList<TilemapRendererData> DirtyTilemapsRendererData;
		    public NativeList<OffsetData> DirtyOffsetCounts;
        
		    public Singleton(int capacity, Allocator allocator)
		    {
			    Layout = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Persistent);
			    Layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
			    Layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
			    Layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

			    HashesToUpdate = new NativeList<Hash128>(8, allocator);
			    MeshDataArray = default;
			    UpdatedMeshBoundsMap = new NativeParallelHashMap<Hash128, AABB>(8, allocator);
			    RenderingEntities = new NativeList<Entity>(capacity, allocator);
			    
			    Positions = new NativeList<int2>(capacity, allocator);
			    SpriteMeshes = new NativeList<SpriteMesh>(capacity, allocator);
			    Vertices = new NativeList<Vertex>(capacity, allocator);
			    Indices = new NativeList<int>(capacity, allocator);
			    LayerPointers = new NativeList<int>(capacity, allocator);

			    DirtyTilemapsRendererData = new NativeList<TilemapRendererData>(8, allocator);
			    DirtyOffsetCounts = new NativeList<OffsetData>(8, allocator);
		    }

		    public void Dispose()
		    {
			    Layout.Dispose();
			    HashesToUpdate.Dispose();
			    UpdatedMeshBoundsMap.Dispose();
			    RenderingEntities.Dispose();
			    
			    Positions.Dispose();
			    SpriteMeshes.Dispose();
			    Vertices.Dispose();
			    Indices.Dispose();
			    LayerPointers.Dispose();

			    DirtyTilemapsRendererData.Dispose();
			    DirtyOffsetCounts.Dispose();
		    }
	    }
	    
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        state.EntityManager.CreateSingleton(new Singleton(256, Allocator.Persistent));
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
	        
	        state.Dependency = new FindHashesToUpdateJob
	        {
		        HashesToUpdate = singleton.HashesToUpdate,
		        IntGridLayers = dataSingleton.IntGridLayers,
		        CullingBoundsChanged = cullingBoundsChanged,
		        DirtyOffsetCounts = singleton.DirtyOffsetCounts,
		        DirtyTilemapsRendererData = singleton.DirtyTilemapsRendererData,
		        LayerPointers = singleton.LayerPointers,
		        TilemapRendererDataLookup = SystemAPI.GetComponentLookup<TilemapRendererData>(true),
	        }.Schedule(state.Dependency);
	        
            state.Dependency = new ResizeLargeListsJob
            {
	            HashesToUpdate = singleton.HashesToUpdate.AsDeferredJobArray(),
	            IntGridLayers = dataSingleton.IntGridLayers,
	            DirtyOffsetCounts = singleton.DirtyOffsetCounts,
	            Positions = singleton.Positions,
	            SpriteMeshes = singleton.SpriteMeshes,
	            Vertices = singleton.Vertices,
	            Indices = singleton.Indices,
	            LayerPointers = singleton.LayerPointers,
            }.Schedule(state.Dependency);
            
            state.Dependency = new PrepareAndCullSpriteMeshDataJob
            {
	            HashesToUpdate = singleton.HashesToUpdate.AsDeferredJobArray(),
	            IntGridLayers = dataSingleton.IntGridLayers,
	            OffsetData = singleton.DirtyOffsetCounts.AsDeferredJobArray(),
	            Positions = singleton.Positions.AsDeferredJobArray(),
	            SpriteMeshes = singleton.SpriteMeshes.AsDeferredJobArray(),
	            CullingBounds = tcb.CullingBounds.Value,
            }.Schedule(singleton.HashesToUpdate, 1, state.Dependency);
            
            state.Dependency = new PatchLayerPointersJob
            {
	            HashesToUpdate = singleton.HashesToUpdate.AsDeferredJobArray(),
	            Offsets = singleton.DirtyOffsetCounts.AsDeferredJobArray(),
	            LayerPointers = singleton.LayerPointers
            }.Schedule(state.Dependency);
            
            var setBufferParamsHandle = new SetBufferParamsJob
            {
	            Offsets = singleton.DirtyOffsetCounts.AsDeferredJobArray(),
	            MeshDataArray = singleton.MeshDataArray.Array,
	            Layout = singleton.Layout,
            }.Schedule(singleton.DirtyOffsetCounts, 1, state.Dependency);
            
            state.Dependency = new GenerateVertexDataJob
            {
	            LayerData = singleton.DirtyTilemapsRendererData.AsDeferredJobArray(),
	            OffsetCount = singleton.DirtyOffsetCounts.AsDeferredJobArray(),
	            Positions = singleton.Positions.AsDeferredJobArray(),
	            SpriteMeshes = singleton.SpriteMeshes.AsDeferredJobArray(),
	            LayerPointer = singleton.LayerPointers.AsDeferredJobArray(),
	            Vertices = singleton.Vertices.AsDeferredJobArray(),
	            Indices = singleton.Indices.AsDeferredJobArray(),
            }.Schedule(singleton.LayerPointers, 128, state.Dependency);
            
            state.Dependency = new FinalizeMeshDataJob
            {
	            HashesToUpdate = singleton.HashesToUpdate.AsDeferredJobArray(),
	            Offsets = singleton.DirtyOffsetCounts.AsDeferredJobArray(),
	            Vertices = singleton.Vertices.AsDeferredJobArray(),
	            Indices = singleton.Indices.AsDeferredJobArray(),
	            UpdatedMeshBoundsMapWriter = singleton.UpdatedMeshBoundsMap.AsParallelWriter(),
	            MeshDataArray = singleton.MeshDataArray.Array,
            }.Schedule(singleton.DirtyOffsetCounts, 1, JobHandle.CombineDependencies(setBufferParamsHandle, state.Dependency));
        }
        
        [BurstCompile]
        private struct FindHashesToUpdateJob : IJob
        {
	        [ReadOnly]
	        public ComponentLookup<TilemapRendererData> TilemapRendererDataLookup;
	        
	        [ReadOnly]
	        public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;
	        
	        public bool CullingBoundsChanged;
	        
	        public NativeList<Hash128> HashesToUpdate;
	        public NativeList<TilemapRendererData> DirtyTilemapsRendererData;
	        public NativeList<OffsetData> DirtyOffsetCounts;
	        public NativeList<int> LayerPointers;
	        
	        public void Execute()
	        {
		        DirtyTilemapsRendererData.Clear();
		        DirtyOffsetCounts.Clear();
		        LayerPointers.Clear();
	        
		        foreach (var kvp in IntGridLayers)
		        {
			        var intGridHash = kvp.Key;
			        ref var dataLayer = ref kvp.Value;
                
			        if (dataLayer.IsTerrainLayer) continue;
			        
			        if (dataLayer.RefreshedPositions.IsEmpty
			            && !CullingBoundsChanged && !dataLayer.Cleared)
			        {
				        continue;
			        }
		        
			        HashesToUpdate.Add(intGridHash);
			        DirtyTilemapsRendererData.Add(TilemapRendererDataLookup[dataLayer.IntGridEntity]);
			        DirtyOffsetCounts.Add(default);
		        }
	        }
        }
        
        [BurstCompile]
        private struct ResizeLargeListsJob : IJob
        {
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
	        [ReadOnly]
	        public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;

	        public NativeList<OffsetData> DirtyOffsetCounts;
	        
	        public NativeList<int2> Positions;
	        public NativeList<SpriteMesh> SpriteMeshes;
	        public NativeList<Vertex> Vertices;
	        public NativeList<int> Indices;
	        public NativeList<int> LayerPointers;
	        
	        public void Execute()
	        {
		        if (HashesToUpdate.Length == 0) return;
		        
		        var spriteMeshesCount = 0;
		        for (var i = 0; i < HashesToUpdate.Length; i++)
		        {
			        var data = IntGridLayers[HashesToUpdate[i]];

			        var offset = spriteMeshesCount;
			        var count = data.RenderedSprites.Count;
			        spriteMeshesCount += count;
			        
			        DirtyOffsetCounts[i] = new OffsetData
			        {
				        DataOffset = offset,
				        Count = count
			        };
		        }

		        var vertexCount = spriteMeshesCount * 4;
		        var triangleCount = spriteMeshesCount * 6;		
		        
		        Positions.EnsureCapacity(spriteMeshesCount, true);
		        SpriteMeshes.EnsureCapacity(spriteMeshesCount, true);
		        Vertices.EnsureCapacity(vertexCount, true);
		        Indices.EnsureCapacity(triangleCount, true);
		        LayerPointers.EnsureCapacity(spriteMeshesCount, true);
	        }
        }
        
        [BurstCompile]
        private struct PrepareAndCullSpriteMeshDataJob : IJobParallelForDefer
        {
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
	        [ReadOnly]
	        public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;

	        [NativeDisableParallelForRestriction]
	        public NativeArray<OffsetData> OffsetData;
	        [NativeDisableParallelForRestriction]
	        public NativeArray<int2> Positions;
	        [NativeDisableParallelForRestriction]
	        public NativeArray<SpriteMesh> SpriteMeshes;

	        public AABB2D CullingBounds;
	        
	        public void Execute(int index)
	        {
		        var data = IntGridLayers[HashesToUpdate[index]];
		        var offsetData = OffsetData[index];

		        var visibleCount = 0;
		        foreach (var kvp in data.RenderedSprites)
		        {
			        if (CullingBounds.Contains(kvp.Key))
			        {
				        Positions[offsetData.DataOffset + visibleCount] = kvp.Key;
				        SpriteMeshes[offsetData.DataOffset + visibleCount] = kvp.Value;
				        visibleCount++;
			        }
		        }
		        offsetData.Count = visibleCount;
		        OffsetData[index] = offsetData;
	        }
        }
		
        [BurstCompile]
        private unsafe struct PatchLayerPointersJob : IJob
        {
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
	        
	        public NativeArray<OffsetData> Offsets;
	        
	        public NativeList<int> LayerPointers;
	        
	        public void Execute()
	        {
		        if (HashesToUpdate.Length == 0) return;
		        
		        var pointerOffset = 0;
		        for (int i = 0; i < Offsets.Length; i++)
		        {
			        var newOffsetData = Offsets[i]; 
			        newOffsetData.PointerOffset = pointerOffset;
			        
			        pointerOffset += newOffsetData.Count;

			        Offsets[i] = newOffsetData;
		        }
		        
		        LayerPointers.SetLengthNoClear(pointerOffset);
		        for (int i = 0; i < Offsets.Length; i++)
		        {
			        var offset = Offsets[i];
			        
			        UnsafeUtility.MemCpyReplicate(
				        (byte*)LayerPointers.GetUnsafePtr() + offset.PointerOffset * UnsafeUtility.SizeOf<int>(), 
				        UnsafeUtility.AddressOf(ref i), UnsafeUtility.SizeOf<int>(), offset.Count);
		        }
	        }
        }
        
        [BurstCompile]
        private struct SetBufferParamsJob : IJobParallelForDefer
        {
	        [ReadOnly]
	        public NativeArray<VertexAttributeDescriptor> Layout;
	        [ReadOnly]
	        public NativeArray<OffsetData> Offsets;
	        
	        public Mesh.MeshDataArray MeshDataArray;
	        
	        public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var offset = Offsets[index];
		        
		        var vertexCount = offset.Count * 4;
		        var indexCount = offset.Count * 6;
		        
		        meshData.SetVertexBufferParams(vertexCount, Layout);
		        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
	        }
        }
        
        [BurstCompile]
        private struct GenerateVertexDataJob : IJobParallelForDefer
        {
	        [ReadOnly]
	        public NativeArray<int2> Positions;
	        [ReadOnly]
	        public NativeArray<SpriteMesh> SpriteMeshes;
	        [ReadOnly]
	        public NativeArray<int> LayerPointer;
	        
	        [NativeDisableParallelForRestriction]
	        public NativeArray<Vertex> Vertices;
	        
	        [NativeDisableParallelForRestriction]
	        public NativeArray<int> Indices;

	        [ReadOnly]
	        public NativeArray<TilemapRendererData> LayerData;
	        [ReadOnly]
	        public NativeArray<OffsetData> OffsetCount;
	        
        	public void Execute(int index)
	        {
		        var layerPointer = LayerPointer[index];
		        var data = LayerData[layerPointer];
		        var offset = OffsetCount[layerPointer];

		        index += offset.DataOffset - offset.PointerOffset;
		        
		        var spriteMesh = SpriteMeshes[index];
		        var pos = Positions[index];
		        
		        var orientation = data.Orientation;
		        
		        MosaicUtils.GetSpriteMeshTranslation(spriteMesh, out var meshTranslation);

		        var rotatedPos = MosaicUtils.ToWorldSpace(pos, data)
		                         + MosaicUtils.ApplyOrientation(meshTranslation, orientation);

		        var pivotPoint = MosaicUtils.ApplyOrientation(spriteMesh.RectScale * spriteMesh.NormalizedPivot, orientation);
		        
		        var rotatedSize = MosaicUtils.ApplyOrientation(spriteMesh.RectScale, orientation);
		        
		        var normal = MosaicUtils.ApplyOrientation(new float3(0, 0, 1), orientation);
		        
		        var up = MosaicUtils.ApplyOrientation(new float3(0, 1, 0), orientation) * rotatedSize;
		        var right = MosaicUtils.ApplyOrientation(new float3(1, 0, 0), orientation) * rotatedSize;

        		int vc = 4 * index;
        		int tc = 2 * 3 * index;

		        var minUv = new float2(
			        spriteMesh.Flip.x ? spriteMesh.MaxUv.x : spriteMesh.MinUv.x,
			        spriteMesh.Flip.y ? spriteMesh.MaxUv.y : spriteMesh.MinUv.y);
		        var maxUv = new float2(
			        spriteMesh.Flip.x ? spriteMesh.MinUv.x : spriteMesh.MaxUv.x,
			        spriteMesh.Flip.y ? spriteMesh.MinUv.y : spriteMesh.MaxUv.y);
		        
		        Vertices[vc + 0] = new Vertex
        		{
        			Position = rotatedPos + MosaicUtils.Rotate(up - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			TexCoord0 = new float2(minUv.x, maxUv.y)
        		};

		        Vertices[vc + 1] = new Vertex
        		{
        			Position = rotatedPos + MosaicUtils.Rotate(up + right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			TexCoord0 = new float2(maxUv.x, maxUv.y)
        		};

		        Vertices[vc + 2] = new Vertex
        		{
        			Position = rotatedPos + MosaicUtils.Rotate(right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			TexCoord0 = new float2(maxUv.x, minUv.y)
        		};

		        Vertices[vc + 3] = new Vertex
        		{
        			Position = rotatedPos + MosaicUtils.Rotate(-pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			TexCoord0 = new float2(minUv.x, minUv.y)
        		};
		        
		        vc -= offset.DataOffset * 4;
			        
        		Indices[tc + 0] = (vc + 0);
        		Indices[tc + 1] = (vc + 1);
        		Indices[tc + 2] = (vc + 2);

        		Indices[tc + 3] = (vc + 0);
        		Indices[tc + 4] = (vc + 2);
        		Indices[tc + 5] = (vc + 3);
        	}
        }

        [BurstCompile]
        private struct FinalizeMeshDataJob : IJobParallelForDefer
        {
	        [ReadOnly]
	        public NativeArray<Hash128> HashesToUpdate;
	        [ReadOnly]
	        public NativeArray<OffsetData> Offsets;
	        
	        [ReadOnly]
	        public NativeArray<Vertex> Vertices;
	        [ReadOnly]
	        public NativeArray<int> Indices;
	        
	        public NativeParallelHashMap<Hash128, AABB>.ParallelWriter UpdatedMeshBoundsMapWriter;
	        public Mesh.MeshDataArray MeshDataArray;
	        
	        public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var offset = Offsets[index];
		        
		        var vertexCount = offset.Count * 4;
		        var indexCount = offset.Count * 6;

		        var vertexData = meshData.GetVertexData<Vertex>();
		        var indexData = meshData.GetIndexData<int>();
		        
		        Vertices.CopyToUnsafe(vertexData, vertexCount, offset.DataOffset * 4, 0);
		        Indices.CopyToUnsafe(indexData, indexCount, offset.DataOffset * 6, 0);
		        
		        meshData.subMeshCount = 1;
		        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));
		        
		        var minPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
		        var maxPos = new float3(float.MinValue, float.MinValue, float.MinValue);

		        for (int i = 0; i < vertexData.Length; i++)
		        {
			        var position = vertexData[i].Position;
			        minPos = math.min(minPos, position);
			        maxPos = math.max(maxPos, position);
		        }
		        
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