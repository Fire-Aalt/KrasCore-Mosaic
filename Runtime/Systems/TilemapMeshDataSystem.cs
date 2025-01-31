using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic
{
	[UpdateAfter(typeof(TilemapCommandBufferSystem))]
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct TilemapMeshDataSystem : ISystem
    {
	    private static readonly quaternion RotY90 = quaternion.RotateY(90f * math.TORADIANS);
	    private static readonly quaternion RotY180 = quaternion.RotateY(180f * math.TORADIANS);
	    private static readonly quaternion RotY270 = quaternion.RotateY(270f * math.TORADIANS);

	    private static readonly quaternion RotZ90 = quaternion.RotateZ(90f * math.TORADIANS);
	    private static readonly quaternion RotZ180 = quaternion.RotateZ(180f * math.TORADIANS);
	    private static readonly quaternion RotZ270 = quaternion.RotateZ(270f * math.TORADIANS);
	    
        private NativeArray<VertexAttributeDescriptor> _layout;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        state.RequireForUpdate<TilemapCommandBufferSingleton>();
	        state.RequireForUpdate<TilemapMeshDataSingleton>();
	        state.RequireForUpdate<TilemapDataSingleton>();
	        state.RequireForUpdate<TilemapRendererSingleton>();
	        state.EntityManager.CreateSingleton(new TilemapRendererSingleton(8, Allocator.Persistent));

            _layout = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Persistent);
            _layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
            _layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
            _layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
	        SystemAPI.GetSingleton<TilemapRendererSingleton>().Dispose();
	        _layout.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
	        state.EntityManager.CompleteDependencyBeforeRO<TilemapDataSingleton>();
	        
            var dataSingleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            ref var meshDataSingleton = ref SystemAPI.GetSingletonRW<TilemapMeshDataSingleton>().ValueRW;
	        var rendererSingleton = SystemAPI.GetSingleton<TilemapRendererSingleton>();
	        
	        // Set Culling Bounds
	        var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>().Tcb;
	        rendererSingleton.CullingBounds = tcb.CullingBounds.Value;
            
	        rendererSingleton.DirtyIntGridLayers.Clear();
	        rendererSingleton.DirtyTilemapsRendererData.Clear();
	        rendererSingleton.DirtyOffsetCounts.Clear();
	        
	        foreach (var kvp in dataSingleton.IntGridLayers)
	        {
		        var intGridHash = kvp.Key;
		        var dataLayer = kvp.Value;
                
		        if (dataLayer.PositionToRemove.List.Length == 0 && dataLayer.SpriteCommands.List.Length == 0)
		        {
			        continue;
		        }
		        meshDataSingleton.IntGridHashesToUpdate.Add(intGridHash);
	            rendererSingleton.DirtyIntGridLayers.Add(dataLayer);
	            rendererSingleton.DirtyTilemapsRendererData.Add(new TilemapRendererData(dataLayer.TilemapData));
	            rendererSingleton.DirtyOffsetCounts.Add(default);
	        }
	        if (!meshDataSingleton.IsDirty) return;
	        
	        meshDataSingleton.MeshDataArray = Mesh.AllocateWritableMeshData(meshDataSingleton.IntGridHashesToUpdate.Length);

            var layersCount = rendererSingleton.DirtyIntGridLayers.Length;
            
            var handle = new UpdateRenderedSpritesJob
            {
	            IntGridLayers = rendererSingleton.DirtyIntGridLayers
            }.ScheduleParallel(layersCount, 1, state.Dependency);
            
            handle = new ResizeLargeListsJob
            {
	            IntGridLayers = rendererSingleton.DirtyIntGridLayers,
	            Offsets = rendererSingleton.DirtyOffsetCounts,
	            Positions = rendererSingleton.Positions,
	            SpriteMeshes = rendererSingleton.SpriteMeshes,
	            Vertices = rendererSingleton.Vertices,
	            Indices = rendererSingleton.Indices,
	            LayerPointers = rendererSingleton.LayerPointers,
            }.Schedule(handle);
            
            handle = new PrepareAndCullSpriteMeshDataJob
            {
	            IntGridLayers = rendererSingleton.DirtyIntGridLayers,
	            OffsetData = rendererSingleton.DirtyOffsetCounts,
	            CullingBounds = rendererSingleton.CullingBounds,
	            Positions = rendererSingleton.Positions.AsDeferredJobArray(),
	            SpriteMeshes = rendererSingleton.SpriteMeshes.AsDeferredJobArray()
            }.ScheduleParallel(layersCount, 1, handle);
            
            handle = new PatchCulledLayerPointersListJob
            {
	            OffsetData = rendererSingleton.DirtyOffsetCounts,
	            LayerPointers = rendererSingleton.LayerPointers
            }.Schedule(handle);
            
            var setBufferParamsHandle = new SetBufferParamsJob
            {
	            Layout = _layout,
	            Offsets = rendererSingleton.DirtyOffsetCounts,
	            MeshDataArray = meshDataSingleton.MeshDataArray
            }.ScheduleParallel(layersCount, 1, handle);
            
            handle = new PrepareLayerPointersJob
            {
	            OffsetData = rendererSingleton.DirtyOffsetCounts,
	            LayerPointers = rendererSingleton.LayerPointers.AsDeferredJobArray()
            }.ScheduleParallel(layersCount, 1, handle);
            
            handle = new GenerateVertexDataJob
            {
	            LayerData = rendererSingleton.DirtyTilemapsRendererData,
	            OffsetCount = rendererSingleton.DirtyOffsetCounts,
	            Positions = rendererSingleton.Positions.AsDeferredJobArray(),
	            SpriteMeshes = rendererSingleton.SpriteMeshes.AsDeferredJobArray(),
	            LayerPointer = rendererSingleton.LayerPointers.AsDeferredJobArray(),
	            Vertices = rendererSingleton.Vertices.AsDeferredJobArray(),
	            Indices = rendererSingleton.Indices.AsDeferredJobArray(),
            }.Schedule(rendererSingleton.LayerPointers, 32, handle);

            var meshDataHandle = JobHandle.CombineDependencies(setBufferParamsHandle, handle);
			
            var vertexHandle = new CopyVertexDataJob
            {
	            Offsets = rendererSingleton.DirtyOffsetCounts,
	            Vertices = rendererSingleton.Vertices.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
            }.ScheduleParallel(layersCount, 1, meshDataHandle);
            
            var indexHandle = new CopyIndexDataJob
            {
	            Offsets = rendererSingleton.DirtyOffsetCounts,
	            Indices = rendererSingleton.Indices.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray,
            }.ScheduleParallel(layersCount, 1, meshDataHandle);
            
            handle = new SetSubMeshJob
            {
	            Offsets = rendererSingleton.DirtyOffsetCounts,
	            MeshDataArray = meshDataSingleton.MeshDataArray,
            }.ScheduleParallel(layersCount, 1, JobHandle.CombineDependencies(vertexHandle, indexHandle));
            
            state.Dependency = handle;
        }

        [BurstCompile]
        private struct UpdateRenderedSpritesJob : IJobFor
        {
	        [NativeDisableContainerSafetyRestriction]
	        public NativeList<TilemapDataSingleton.IntGridLayer> IntGridLayers;
	        
	        public void Execute(int index)
	        {
		        var data = IntGridLayers[index];

		        foreach (var positionToRemove in data.PositionToRemove.List)
		        {
			        data.RenderedSprites.Remove(positionToRemove.Position);
		        }
            
		        foreach (var command in data.SpriteCommands.List)
		        {
			        data.RenderedSprites[command.Position] = command.SpriteMesh;
		        }

		        data.RenderedSpritesCount.Value = data.RenderedSprites.Count();
	        }
        }
        
        [BurstCompile]
        private struct ResizeLargeListsJob : IJob
        {
	        [ReadOnly]
	        [NativeDisableContainerSafetyRestriction]
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
			        var count = data.RenderedSpritesCount.Value;
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
        			position = rotatedPos + Rotate(up - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        normal = normal,
        			uv = new float2(minUv.x, maxUv.y)
        		};

		        Vertices[vc + 1] = new Vertex
        		{
        			position = rotatedPos + Rotate(up + right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        normal = normal,
        			uv = new float2(maxUv.x, maxUv.y)
        		};

		        Vertices[vc + 2] = new Vertex
        		{
        			position = rotatedPos + Rotate(right - pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        normal = normal,
        			uv = new float2(maxUv.x, minUv.y)
        		};

		        Vertices[vc + 3] = new Vertex
        		{
        			position = rotatedPos + Rotate(-pivotPoint, spriteMesh.Rotation, orientation) + pivotPoint,
			        normal = normal,
        			uv = new float2(minUv.x, minUv.y)
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
    }

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct Vertex
    {
        public float3 position;
        public float3 normal;
        public float2 uv;
    }
}