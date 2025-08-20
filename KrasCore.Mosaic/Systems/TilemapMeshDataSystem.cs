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
        private Data _data;
        private NativeArray<VertexAttributeDescriptor> _layout;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        _data = new Data(8, Allocator.Persistent);
	        
            _layout = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Persistent);
            _layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
            _layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
            _layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
	        _data.Dispose();
	        _layout.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
	        var dataSingleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
	        var meshDataSingleton = SystemAPI.GetSingletonRW<TilemapMeshDataSingleton>().ValueRW;
	        var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();

	        var cullingBoundsChanged = !tcb.PrevCullingBounds.Value.Equals(tcb.CullingBounds.Value);
	        tcb.PrevCullingBounds.Value = tcb.CullingBounds.Value;
	        
	        state.Dependency = new FindHashesToUpdateJob
	        {
		        MeshHashesToUpdate = meshDataSingleton.TilemapHashesToUpdate,
		        IntGridLayers = dataSingleton.IntGridLayers,
		        CullingBoundsChanged = cullingBoundsChanged,
		        Data = _data,
		        TilemapRendererDataLookup = SystemAPI.GetComponentLookup<TilemapRendererData>(true),
	        }.Schedule(state.Dependency);
	        
            state.Dependency = new ResizeLargeListsJob
            {
	            IntGridLayers = dataSingleton.IntGridLayers,
	            Data = _data
            }.Schedule(state.Dependency);
            
            state.Dependency = new PrepareAndCullSpriteMeshDataJob
            {
	            HashesToUpdate = _data.TilemapHashesToUpdate.AsDeferredJobArray(),
	            IntGridLayers = dataSingleton.IntGridLayers,
	            OffsetData = _data.DirtyOffsetCounts.AsDeferredJobArray(),
	            Positions = _data.Positions.AsDeferredJobArray(),
	            SpriteMeshes = _data.SpriteMeshes.AsDeferredJobArray(),
	            CullingBounds = tcb.CullingBounds.Value,
            }.Schedule(_data.DirtyOffsetCounts, 1, state.Dependency);
            
            state.Dependency = new PatchLayerPointersJob
            {
	            HashesToUpdate = _data.TilemapHashesToUpdate.AsDeferredJobArray(),
	            Offsets = _data.DirtyOffsetCounts.AsDeferredJobArray(),
	            LayerPointers = _data.LayerPointers
            }.Schedule(state.Dependency);
            
            var setBufferParamsHandle = new SetBufferParamsJob
            {
	            Offsets = _data.DirtyOffsetCounts.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.TilemapMeshDataArray.Array,
	            Layout = _layout,
            }.Schedule(_data.DirtyOffsetCounts, 1, state.Dependency);
            
            state.Dependency = new GenerateVertexDataJob
            {
	            LayerData = _data.DirtyTilemapsRendererData.AsDeferredJobArray(),
	            OffsetCount = _data.DirtyOffsetCounts.AsDeferredJobArray(),
	            Positions = _data.Positions.AsDeferredJobArray(),
	            SpriteMeshes = _data.SpriteMeshes.AsDeferredJobArray(),
	            LayerPointer = _data.LayerPointers.AsDeferredJobArray(),
	            Vertices = _data.Vertices.AsDeferredJobArray(),
	            Indices = _data.Indices.AsDeferredJobArray(),
            }.Schedule(_data.LayerPointers, 128, state.Dependency);
            
            state.Dependency = new FinalizeMeshDataJob
            {
	            HashesToUpdate = _data.TilemapHashesToUpdate.AsDeferredJobArray(),
	            Offsets = _data.DirtyOffsetCounts.AsDeferredJobArray(),
	            Vertices = _data.Vertices.AsDeferredJobArray(),
	            Indices = _data.Indices.AsDeferredJobArray(),
	            UpdatedMeshBoundsMapWriter = meshDataSingleton.UpdatedMeshBoundsMap.AsParallelWriter(),
	            MeshDataArray = meshDataSingleton.TilemapMeshDataArray.Array,
            }.Schedule(_data.DirtyOffsetCounts, 1, JobHandle.CombineDependencies(setBufferParamsHandle, state.Dependency));
        }
        
        [BurstCompile]
        private struct FindHashesToUpdateJob : IJob
        {
	        [ReadOnly]
	        public ComponentLookup<TilemapRendererData> TilemapRendererDataLookup;
	        
	        [ReadOnly]
	        public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;
	        
	        public bool CullingBoundsChanged;
	        public Data Data;
	        public NativeList<Hash128> MeshHashesToUpdate;
	        
	        public void Execute()
	        {
		        Data.TilemapHashesToUpdate.Clear();
		        Data.DirtyTilemapsRendererData.Clear();
		        Data.DirtyOffsetCounts.Clear();
		        Data.LayerPointers.Clear();
	        
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
		        
			        Data.TilemapHashesToUpdate.Add(intGridHash);
			        MeshHashesToUpdate.Add(intGridHash);
			        Data.DirtyTilemapsRendererData.Add(TilemapRendererDataLookup[dataLayer.IntGridEntity]);
			        Data.DirtyOffsetCounts.Add(default);
		        }
	        }
        }
        
        [BurstCompile]
        private struct ResizeLargeListsJob : IJob
        {
	        [ReadOnly]
	        public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;

	        public Data Data;
	        
	        public void Execute()
	        {
		        if (Data.TilemapHashesToUpdate.Length == 0) return;
		        
		        var spriteMeshesCount = 0;
		        for (var i = 0; i < Data.TilemapHashesToUpdate.Length; i++)
		        {
			        var data = IntGridLayers[Data.TilemapHashesToUpdate[i]];

			        var offset = spriteMeshesCount;
			        var count = data.RenderedSprites.Count;
			        spriteMeshesCount += count;
			        
			        Data.DirtyOffsetCounts[i] = new OffsetData
			        {
				        DataOffset = offset,
				        Count = count
			        };
		        }

		        var vertexCount = spriteMeshesCount * 4;
		        var triangleCount = spriteMeshesCount * 6;		
		        
		        Data.Positions.EnsureCapacity(spriteMeshesCount, true);
		        Data.SpriteMeshes.EnsureCapacity(spriteMeshesCount, true);
		        Data.Vertices.EnsureCapacity(vertexCount, true);
		        Data.Indices.EnsureCapacity(triangleCount, true);
		        Data.LayerPointers.EnsureCapacity(spriteMeshesCount, true);
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
        			UV = new float2(minUv.x, maxUv.y)
        		};

		        Vertices[vc + 1] = new Vertex
        		{
        			Position = rotatedPos + MosaicUtils.Rotate(up + right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			UV = new float2(maxUv.x, maxUv.y)
        		};

		        Vertices[vc + 2] = new Vertex
        		{
        			Position = rotatedPos + MosaicUtils.Rotate(right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			UV = new float2(maxUv.x, minUv.y)
        		};

		        Vertices[vc + 3] = new Vertex
        		{
        			Position = rotatedPos + MosaicUtils.Rotate(-pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			UV = new float2(minUv.x, minUv.y)
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
    }

	[StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public float3 Position;
        public float3 Normal;
        public float2 UV;
    }
}