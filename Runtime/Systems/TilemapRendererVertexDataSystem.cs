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
	[UpdateAfter(typeof(TilemapAllocateMeshDataSystem))]
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct TilemapRendererVertexDataSystem : ISystem
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
            var meshDataSingleton = SystemAPI.GetSingleton<TilemapMeshDataSingleton>();
	        var rendererSingleton = SystemAPI.GetSingleton<TilemapRendererSingleton>();
            
	        if (!meshDataSingleton.IsDirty) return;
	        
	        rendererSingleton.DirtyIntGridLayers.Clear();
	        rendererSingleton.DirtyTilemapsRendererData.Clear();
	        rendererSingleton.DirtyOffsetCounts.Clear();
	        
            foreach (var hash in meshDataSingleton.IntGridHashesToUpdate)
            {
	            var dataLayer = dataSingleton.IntGridLayers[hash];
	            
	            rendererSingleton.DirtyIntGridLayers.Add(dataLayer);
	            rendererSingleton.DirtyTilemapsRendererData.Add(new TilemapRendererData(dataLayer.TilemapData));
	            rendererSingleton.DirtyOffsetCounts.Add(default);
            }
            
            var verts = new NativeArray<NativeArray<Vertex>>(rendererSingleton.DirtyIntGridLayers.Length, Allocator.TempJob);
            var trigs = new NativeArray<NativeArray<int>>(rendererSingleton.DirtyIntGridLayers.Length, Allocator.TempJob);
            
            var handle = new UpdateRenderedSpritesJob
            {
	            IntGridLayers = rendererSingleton.DirtyIntGridLayers.AsArray()
            }.ScheduleParallel(rendererSingleton.DirtyIntGridLayers.Length, 1, state.Dependency);
            
            handle = new ResizeLargeListsJob
            {
	            IntGridLayers = rendererSingleton.DirtyIntGridLayers.AsArray(),
	            Positions = rendererSingleton.Positions,
	            SpriteMeshes = rendererSingleton.SpriteMeshes,
	            Vertices = rendererSingleton.Vertices,
	            Indices = rendererSingleton.Indices,
	            LayerPointers = rendererSingleton.LayerPointers,
	            Offsets = rendererSingleton.DirtyOffsetCounts
            }.Schedule(handle);
            
            handle = new PrepareSpriteMeshDataJob
            {
	            IntGridLayers = rendererSingleton.DirtyIntGridLayers.AsArray(),
	            Positions = rendererSingleton.Positions.AsDeferredJobArray(),
	            SpriteMeshes = rendererSingleton.SpriteMeshes.AsDeferredJobArray(),
	            LayerPointers = rendererSingleton.LayerPointers.AsDeferredJobArray(),
	            Offsets = rendererSingleton.DirtyOffsetCounts.AsDeferredJobArray()
            }.ScheduleParallel(rendererSingleton.DirtyIntGridLayers.Length, 1, handle);
            
            handle = new GenerateVertexDataJob
            {
	            Positions = rendererSingleton.Positions.AsDeferredJobArray(),
	            SpriteMeshes = rendererSingleton.SpriteMeshes.AsDeferredJobArray(),
	            Vertices = rendererSingleton.Vertices.AsDeferredJobArray(),
	            Triangles = rendererSingleton.Indices.AsDeferredJobArray(),
	            LayerPointer = rendererSingleton.LayerPointers.AsDeferredJobArray(),
	            OffsetCount = rendererSingleton.DirtyOffsetCounts.AsDeferredJobArray(),
	            LayerData = rendererSingleton.DirtyTilemapsRendererData.AsArray()
            }.Schedule(rendererSingleton.SpriteMeshes, 32, handle);
            
            
            handle = new CopyToMeshDataJob
            {
	            Layout = _layout,
	            Offsets = rendererSingleton.DirtyOffsetCounts.AsDeferredJobArray(),
	            Vertices = rendererSingleton.Vertices.AsDeferredJobArray(),
	            Indices = rendererSingleton.Indices.AsDeferredJobArray(),
	            Verts = verts,
	            Trigs = trigs,
	            MeshDataArray = meshDataSingleton.MeshDataArray
            }.ScheduleParallel(rendererSingleton.DirtyIntGridLayers.Length, 1, handle);

			
            handle = new CopyToMeshDataTestJob
            {
	            Layout = _layout,
	            VerticesPtr = verts,
	            IndicesPtr = trigs,
	            Offsets = rendererSingleton.DirtyOffsetCounts.AsDeferredJobArray(),
	            Vertices = rendererSingleton.Vertices.AsDeferredJobArray(),
	            Indices = rendererSingleton.Indices.AsDeferredJobArray(),
	            MeshDataArray = meshDataSingleton.MeshDataArray
            }.ScheduleParallel(rendererSingleton.DirtyIntGridLayers.Length, 1, handle);
            
            state.Dependency = handle;
        }

        [BurstCompile]
        private struct UpdateRenderedSpritesJob : IJobFor
        {
	        [NativeDisableContainerSafetyRestriction]
	        public NativeArray<TilemapDataSingleton.IntGridLayer> IntGridLayers;
	        
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
	        public NativeArray<TilemapDataSingleton.IntGridLayer> IntGridLayers;
	        
	        public NativeList<int2> Positions;
	        public NativeList<SpriteMesh> SpriteMeshes;
	        public NativeList<Vertex> Vertices;
	        public NativeList<int> Indices;

	        public NativeList<int> LayerPointers;
	        public NativeList<OffsetCount> Offsets;
	        
	        public void Execute()
	        {
		        var meshesCount = 0;		
		        for (var i = 0; i < IntGridLayers.Length; i++)
		        {
			        var data = IntGridLayers[i];

			        var offset = meshesCount;
			        var count = data.RenderedSpritesCount.Value;
			        meshesCount += count;
			        
			        Offsets[i] = new OffsetCount
			        {
				        Offset = offset,
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
        private struct PrepareSpriteMeshDataJob : IJobFor
        {
	        [ReadOnly]
	        [NativeDisableContainerSafetyRestriction]
	        public NativeArray<TilemapDataSingleton.IntGridLayer> IntGridLayers;
	        
	        [ReadOnly]
	        public NativeArray<OffsetCount> Offsets;
	        
	        [NativeDisableParallelForRestriction]
	        public NativeArray<int2> Positions;
	        [NativeDisableParallelForRestriction]
	        public NativeArray<SpriteMesh> SpriteMeshes;
	        [NativeDisableParallelForRestriction]
	        public NativeArray<int> LayerPointers;
	        
	        public unsafe void Execute(int index)
	        {
		        var data = IntGridLayers[index];
		        var offset = Offsets[index];
		        
		        data.RenderedSprites.ToNativeArrays(ref Positions, ref SpriteMeshes, offset.Offset);
		        
		        UnsafeUtility.MemCpyReplicate(
			        (byte*)LayerPointers.GetUnsafePtr() + offset.Offset * UnsafeUtility.SizeOf<int>(), 
			        UnsafeUtility.AddressOf(ref index), UnsafeUtility.SizeOf<int>(), offset.Count);
	        }
        }
		
        [BurstCompile]
        private struct CopyToMeshDataJob : IJobFor
        {
	        [ReadOnly]
	        public NativeArray<VertexAttributeDescriptor> Layout;
	        [ReadOnly]
	        public NativeArray<Vertex> Vertices;
	        [ReadOnly]
	        public NativeArray<int> Indices;
	        [ReadOnly]
	        public NativeArray<OffsetCount> Offsets;

	        public Mesh.MeshDataArray MeshDataArray;
	        [NativeDisableContainerSafetyRestriction]
	        public NativeArray<NativeArray<Vertex>> Verts;
	        [NativeDisableContainerSafetyRestriction]
	        public NativeArray<NativeArray<int>> Trigs;
	        
	        public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var offset = Offsets[index];
		        
		        var vertexCount = offset.Count * 4;
		        var indexCount = offset.Count * 6;
		        
		        meshData.SetVertexBufferParams(vertexCount, Layout);
		        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

		        Verts[index] = meshData.GetVertexData<Vertex>();
		        Trigs[index] = meshData.GetIndexData<int>();
		        // var vertexData = meshData.GetVertexData<Vertex>();
		        // var indexData = meshData.GetIndexData<int>();
		        //
		        // Vertices.CopyToUnsafe(vertexData, vertexCount, offset.Offset * 4, 0);
		        // Indices.CopyToUnsafe(indexData, indexCount, offset.Offset * 6, 0);
		        //
		        // meshData.subMeshCount = 1;
		        // meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));
	        }
        }
        
        [BurstCompile]
        private unsafe struct CopyToMeshDataTestJob : IJobFor
        {
	        [ReadOnly]
	        public NativeArray<VertexAttributeDescriptor> Layout;
	        //[ReadOnly]
	        [NativeDisableContainerSafetyRestriction]
	        public NativeArray<NativeArray<Vertex>> VerticesPtr;
	        //[ReadOnly]
	        [NativeDisableContainerSafetyRestriction]
	        public NativeArray<NativeArray<int>> IndicesPtr;
	        [ReadOnly]
	        public NativeArray<Vertex> Vertices;
	        [ReadOnly]
	        public NativeArray<int> Indices;
	        [ReadOnly]
	        public NativeArray<OffsetCount> Offsets;

	        public Mesh.MeshDataArray MeshDataArray;
	        
	        public void Execute(int index)
	        {
		        var meshData = MeshDataArray[index];
		        var offset = Offsets[index];
		        
		        var vertexCount = offset.Count * 4;
		        var indexCount = offset.Count * 6;

		        Vertices.CopyToUnsafe(VerticesPtr[index], vertexCount, offset.Offset * 4, 0);
		        Indices.CopyToUnsafe(IndicesPtr[index], indexCount, offset.Offset * 6, 0);
		        
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

	        [NativeDisableParallelForRestriction]
			[WriteOnly] public NativeArray<Vertex> Vertices;
	        [NativeDisableParallelForRestriction]
        	[WriteOnly] public NativeArray<int> Triangles;

	        [ReadOnly]
	        public NativeArray<int> LayerPointer;
	        [ReadOnly]
	        public NativeArray<TilemapRendererData> LayerData;
	        [ReadOnly]
	        public NativeArray<OffsetCount> OffsetCount;
	        
        	public void Execute(int index)
	        {
		        var spriteMesh = SpriteMeshes[index];
		        var pos = Positions[index];
		        var data = LayerData[LayerPointer[index]];
		        var offsetCount = OffsetCount[LayerPointer[index]];
		        
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

		        vc -= offsetCount.Offset * 4;
				
        		Triangles[tc + 0] = (vc + 0);
        		Triangles[tc + 1] = (vc + 1);
        		Triangles[tc + 2] = (vc + 2);

        		Triangles[tc + 3] = (vc + 0);
        		Triangles[tc + 4] = (vc + 2);
        		Triangles[tc + 5] = (vc + 3);
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