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
	[UpdateAfter(typeof(TilemapRulesEngineSystem))]
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct TilemapMeshDataSystem : ISystem
    {
	    private static readonly quaternion RotY90 = quaternion.RotateY(90f * math.TORADIANS);
	    private static readonly quaternion RotY180 = quaternion.RotateY(180f * math.TORADIANS);
	    private static readonly quaternion RotY270 = quaternion.RotateY(270f * math.TORADIANS);

	    private static readonly quaternion RotZ90 = quaternion.RotateZ(90f * math.TORADIANS);
	    private static readonly quaternion RotZ180 = quaternion.RotateZ(180f * math.TORADIANS);
	    private static readonly quaternion RotZ270 = quaternion.RotateZ(270f * math.TORADIANS);
	    
        private Data _data;
        private NativeArray<VertexAttributeDescriptor> _layout;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        _data = new Data(8, Allocator.Persistent);

            _layout = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Persistent);
            _layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
            _layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
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
	        // TODO: move filtering into a job so that no Complete is necessary
	        state.EntityManager.CompleteDependencyBeforeRW<TilemapDataSingleton>();
	        state.EntityManager.CompleteDependencyBeforeRW<TilemapMeshDataSingleton>();
	        
            ref var dataSingleton = ref SystemAPI.GetSingletonRW<TilemapDataSingleton>().ValueRW;
            ref var meshDataSingleton = ref SystemAPI.GetSingletonRW<TilemapMeshDataSingleton>().ValueRW;
	        
	        // Set Culling Bounds
	        var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();
            
	        _data.DirtyIntGridLayers.Clear();
	        _data.DirtyTilemapsRendererData.Clear();
	        _data.DirtyOffsetCounts.Clear();
	        
	        foreach (var kvp in dataSingleton.IntGridLayers)
	        {
		        var intGridHash = kvp.Key;
		        var dataLayer = kvp.Value;
                
		        if (dataLayer.RefreshedPositions.Length == 0 
		            && tcb.PrevCullingBounds.Value.Equals(tcb.CullingBounds.Value))
		        {
			        continue;
		        }
		        meshDataSingleton.IntGridHashesToUpdate.Add(intGridHash);
		        _data.DirtyIntGridLayers.Add(dataLayer);
		        _data.DirtyTilemapsRendererData.Add(new TilemapRendererData(dataLayer.TilemapData));
		        _data.DirtyOffsetCounts.Add(default);
	        }
	        if (!meshDataSingleton.IsDirty) return;
	        tcb.PrevCullingBounds.Value = tcb.CullingBounds.Value;
	        
	        var meshesCount = meshDataSingleton.IntGridHashesToUpdate.Length;
	        meshDataSingleton.MeshDataArray = Mesh.AllocateWritableMeshData(meshesCount);

	        if (meshDataSingleton.UpdatedMeshBoundsMap.Capacity < meshesCount)
				meshDataSingleton.UpdatedMeshBoundsMap.Capacity = meshesCount;
	        
            var handle = new ResizeLargeListsJob
            {
	            IntGridLayers = _data.DirtyIntGridLayers,
	            Offsets = _data.DirtyOffsetCounts,
	            Positions = _data.Positions,
	            SpriteMeshes = _data.SpriteMeshes,
	            Vertices = _data.Vertices,
	            Indices = _data.Indices,
	            LayerPointers = _data.LayerPointers,
            }.Schedule(state.Dependency);
            
            handle = new PrepareAndCullSpriteMeshDataJob
            {
	            IntGridLayers = _data.DirtyIntGridLayers,
	            OffsetData = _data.DirtyOffsetCounts,
	            CullingBounds = tcb.CullingBounds.Value,
	            Positions = _data.Positions.AsDeferredJobArray(),
	            SpriteMeshes = _data.SpriteMeshes.AsDeferredJobArray()
            }.ScheduleParallel(meshesCount, 1, handle);
            
            handle = new PatchCulledLayerPointersListJob
            {
	            OffsetData = _data.DirtyOffsetCounts,
	            LayerPointers = _data.LayerPointers
            }.Schedule(handle);
            
            var setBufferParamsHandle = new SetBufferParamsJob
            {
	            Layout = _layout,
	            Offsets = _data.DirtyOffsetCounts,
	            MeshDataArray = meshDataSingleton.MeshDataArray
            }.ScheduleParallel(meshesCount, 1, handle);
            
            handle = new PrepareLayerPointersJob
            {
	            OffsetData = _data.DirtyOffsetCounts,
	            LayerPointers = _data.LayerPointers.AsDeferredJobArray()
            }.ScheduleParallel(meshesCount, 1, handle);
            
            handle = new GenerateVertexDataJob
            {
	            LayerData = _data.DirtyTilemapsRendererData,
	            OffsetCount = _data.DirtyOffsetCounts,
	            Positions = _data.Positions.AsDeferredJobArray(),
	            SpriteMeshes = _data.SpriteMeshes.AsDeferredJobArray(),
	            LayerPointer = _data.LayerPointers.AsDeferredJobArray(),
	            Vertices = _data.Vertices.AsDeferredJobArray(),
	            Indices = _data.Indices.AsDeferredJobArray(),
            }.Schedule(_data.LayerPointers, 32, handle);

            var meshDataHandle = JobHandle.CombineDependencies(setBufferParamsHandle, handle);
			
            var vertexHandle = new CopyVertexDataJob
            {
	            Offsets = _data.DirtyOffsetCounts,
	            Vertices = _data.Vertices.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
            }.ScheduleParallel(meshesCount, 1, meshDataHandle);
            
            var indexHandle = new CopyIndexDataJob
            {
	            Offsets = _data.DirtyOffsetCounts,
	            Indices = _data.Indices.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
            }.ScheduleParallel(meshesCount, 1, meshDataHandle);
            
            var boundsHandle = new CalculateBoundsJob
            {
	            IntGridHashesToUpdate = meshDataSingleton.IntGridHashesToUpdate,
	            UpdatedMeshBoundsMapWriter = meshDataSingleton.UpdatedMeshBoundsMap.AsParallelWriter(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
            }.ScheduleParallel(meshesCount, 1, vertexHandle);
            
            state.Dependency = new SetSubMeshJob
            {
	            Offsets = _data.DirtyOffsetCounts,
	            MeshDataArray = meshDataSingleton.MeshDataArray,
            }.ScheduleParallel(meshesCount, 1, JobHandle.CombineDependencies(indexHandle, boundsHandle));
        }
        
        [BurstCompile]
        private struct ResizeLargeListsJob : IJob
        {
	        [ReadOnly]
	        public NativeList<TilemapDataSingleton.IntGridLayer> IntGridLayers;
	        
	        public NativeList<int2> Positions;
	        public NativeList<SpriteMesh> SpriteMeshes;
	        public NativeList<Vertex> Vertices;
	        public NativeList<int> Indices;

	        public NativeList<int> LayerPointers;
	        public NativeList<OffsetData> Offsets;
	        
	        public void Execute()
	        {
		        var meshesCount = 0;		
		        for (var i = 0; i < IntGridLayers.Length; i++)
		        {
			        var data = IntGridLayers[i];

			        var offset = meshesCount;
			        var count = data.RenderedSprites.Count;
			        meshesCount += count;
			        
			        Offsets[i] = new OffsetData
			        {
				        DataOffset = offset,
				        Count = count
			        };
		        }

		        var vertexCount = meshesCount * 4;
		        var triangleCount = meshesCount * 6;		
		        
		        Positions.EnsureCapacity(meshesCount, true);
		        SpriteMeshes.EnsureCapacity(meshesCount, true);
		        Vertices.EnsureCapacity(vertexCount, true);
		        Indices.EnsureCapacity(triangleCount, true);
		        LayerPointers.EnsureCapacity(meshesCount, true);
	        }
        }
        
        [BurstCompile]
        private struct PrepareAndCullSpriteMeshDataJob : IJobFor
        {
	        [ReadOnly]
	        [NativeDisableContainerSafetyRestriction]
	        public NativeList<TilemapDataSingleton.IntGridLayer> IntGridLayers;

	        [NativeDisableParallelForRestriction]
	        public NativeList<OffsetData> OffsetData;
	        
	        [NativeDisableParallelForRestriction]
	        public NativeArray<int2> Positions;
	        [NativeDisableParallelForRestriction]
	        public NativeArray<SpriteMesh> SpriteMeshes;

	        public AABB2D CullingBounds;
	        
	        public void Execute(int index)
	        {
		        var data = IntGridLayers[index];
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
        private struct PatchCulledLayerPointersListJob : IJob
        {
	        public NativeList<OffsetData> OffsetData;
	        
	        public NativeList<int> LayerPointers;
	        
	        public void Execute()
	        {
		        var pointerOffset = 0;
		        for (int i = 0; i < OffsetData.Length; i++)
		        {
			        var newOffsetData = OffsetData[i]; 
			        newOffsetData.PointerOffset = pointerOffset;
			        
			        pointerOffset += newOffsetData.Count;

			        OffsetData[i] = newOffsetData;
		        }
		        
		        LayerPointers.SetLengthNoClear(pointerOffset);
	        }
        }
        
        [BurstCompile]
        private struct PrepareLayerPointersJob : IJobFor
        {
	        [ReadOnly]
	        public NativeList<OffsetData> OffsetData;
	        
	        [NativeDisableParallelForRestriction]
	        public NativeArray<int> LayerPointers;
	        
	        public unsafe void Execute(int index)
	        {
		        var offset = OffsetData[index];
		        
		        UnsafeUtility.MemCpyReplicate(
			        (byte*)LayerPointers.GetUnsafePtr() + offset.PointerOffset * UnsafeUtility.SizeOf<int>(), 
			        UnsafeUtility.AddressOf(ref index), UnsafeUtility.SizeOf<int>(), offset.Count);
	        }
        }
		
        [BurstCompile]
        private struct SetBufferParamsJob : IJobFor
        {
	        [ReadOnly]
	        public NativeArray<VertexAttributeDescriptor> Layout;
	        [ReadOnly]
	        public NativeList<OffsetData> Offsets;
	        
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
        private struct CopyVertexDataJob : IJobFor
        {
	        [ReadOnly]
	        public NativeArray<Vertex> Vertices;
	        [ReadOnly]
	        public NativeList<OffsetData> Offsets;

	        [NativeDisableContainerSafetyRestriction]
	        public Mesh.MeshDataArray MeshDataArray;
	        
	        public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var offset = Offsets[index];
		        
		        var vertexCount = offset.Count * 4;

		        Vertices.CopyToUnsafe(meshData.GetVertexData<Vertex>(), vertexCount, offset.DataOffset * 4, 0);
	        }
        }
        
        [BurstCompile]
        private struct CopyIndexDataJob : IJobFor
        {
	        [ReadOnly]
	        public NativeArray<int> Indices;
	        [ReadOnly]
	        public NativeList<OffsetData> Offsets;

	        [NativeDisableContainerSafetyRestriction]
	        public Mesh.MeshDataArray MeshDataArray;
	        
	        public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var offset = Offsets[index];
		        
		        var indexCount = offset.Count * 6;

		        Indices.CopyToUnsafe(meshData.GetIndexData<int>(), indexCount, offset.DataOffset * 6, 0);
	        }
        }
        
        [BurstCompile]
        private struct SetSubMeshJob : IJobFor
        {
	        [ReadOnly]
	        public NativeList<OffsetData> Offsets;

	        public Mesh.MeshDataArray MeshDataArray;
	        
	        public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var offset = Offsets[index];
		        
		        var indexCount = offset.Count * 6;
		        
		        meshData.subMeshCount = 1;
		        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));
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
	        public NativeList<TilemapRendererData> LayerData;
	        [ReadOnly]
	        public NativeList<OffsetData> OffsetCount;
	        
        	public void Execute(int index)
	        {
		        var layerPointer = LayerPointer[index];
		        var data = LayerData[layerPointer];
		        var offset = OffsetCount[layerPointer];

		        index += offset.DataOffset - offset.PointerOffset;
		        
		        var spriteMesh = SpriteMeshes[index];
		        var pos = Positions[index];
		        
		        var gridCellSize = data.GridCellSize;
		        var swizzle = data.Swizzle;
		        var orientation = data.Orientation;
		        
		        MosaicUtils.GetSpriteMeshTranslation(spriteMesh, out var meshTranslation);

		        var rotatedPos = MosaicUtils.ToWorldSpace(pos, gridCellSize, swizzle)
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
        			Position = rotatedPos + Rotate(up - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			UV = new float2(minUv.x, maxUv.y)
        		};

		        Vertices[vc + 1] = new Vertex
        		{
        			Position = rotatedPos + Rotate(up + right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			UV = new float2(maxUv.x, maxUv.y)
        		};

		        Vertices[vc + 2] = new Vertex
        		{
        			Position = rotatedPos + Rotate(right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        Normal = normal,
        			UV = new float2(maxUv.x, minUv.y)
        		};

		        Vertices[vc + 3] = new Vertex
        		{
        			Position = rotatedPos + Rotate(-pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
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
	        
	        private float3 Rotate(in float3 direction, in int rotation, in Orientation orientation)
	        {
		        return rotation switch
		        {
			        0 => direction,
			        1 => math.mul(orientation == Orientation.XY ? RotZ90 : RotY90, direction),
			        2 => math.mul(orientation == Orientation.XY ? RotZ180 : RotY180, direction),
			        3 => math.mul(orientation == Orientation.XY ? RotZ270 : RotY270, direction),
			        _ => default
		        };
	        }
        }
        
        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
        private struct CalculateBoundsJob : IJobFor
        {
	        [ReadOnly]
	        public NativeList<Hash128> IntGridHashesToUpdate;
	        
	        public Mesh.MeshDataArray MeshDataArray;
	        public NativeParallelHashMap<Hash128, AABB>.ParallelWriter UpdatedMeshBoundsMapWriter;

	        public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var vertices = meshData.GetVertexData<Vertex>();
		        
		        var minPos = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
		        var maxPos = new float3(float.MinValue, float.MinValue, float.MinValue);

		        for (int i = 0; i < vertices.Length; i++)
		        {
			        var position = vertices[i].Position;
			        minPos = math.min(minPos, position);
			        maxPos = math.max(maxPos, position);
		        }
		        
		        UpdatedMeshBoundsMapWriter.TryAdd(IntGridHashesToUpdate[index], new AABB
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