using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
	[UpdateAfter(typeof(TilemapCommandBufferSystem))]
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct TilemapRendererVertexDataSystem : ISystem
    {
	    private static readonly quaternion RotY90 = quaternion.RotateY(90f * math.TORADIANS);
	    private static readonly quaternion RotY180 = quaternion.RotateY(180f * math.TORADIANS);
	    private static readonly quaternion RotY270 = quaternion.RotateY(270f * math.TORADIANS);

	    private static readonly quaternion RotZ90 = quaternion.RotateZ(90f * math.TORADIANS);
	    private static readonly quaternion RotZ180 = quaternion.RotateZ(180f * math.TORADIANS);
	    private static readonly quaternion RotZ270 = quaternion.RotateZ(270f * math.TORADIANS);
	    
        private NativeList<JobHandle> _jobHandles;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        state.RequireForUpdate<TilemapRendererSingleton>();
            state.RequireForUpdate<TilemapDataSingleton>();
	        state.EntityManager.CreateSingleton(new TilemapRendererSingleton(8, Allocator.Persistent));
            _jobHandles = new NativeList<JobHandle>(8, Allocator.Persistent);
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
	        SystemAPI.GetSingleton<TilemapRendererSingleton>().Dispose();
	        _jobHandles.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
	        ref var rendererSingleton = ref SystemAPI.GetSingletonRW<TilemapRendererSingleton>().ValueRW;
            var dataSingleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            rendererSingleton.JobHandle = default;
            dataSingleton.JobHandle.Complete();
            
            foreach (var kvp in dataSingleton.IntGridLayers)
            {
	            var dataLayer = kvp.Value;
	            if (!rendererSingleton.IntGridLayers.TryGetValue(kvp.Key, out var rendererLayer))
	            {
		            rendererLayer = new TilemapRendererSingleton.IntGridLayer(256, Allocator.Persistent);
		            rendererSingleton.IntGridLayers.Add(kvp.Key, rendererLayer);
	            }
	            
	            rendererLayer.IsDirty.Value = false;
	            if (dataLayer.PositionToRemove.List.Length == 0 && dataLayer.SpriteCommands.List.Length == 0) continue;
	            rendererLayer.IsDirty.Value = true;

	            var handle = new PrepareVertexDataJob
	            {
		            RenderedSprites = dataLayer.RenderedSprites,
		            PositionsToRemove = dataLayer.PositionToRemove.List,
		            SpriteCommands = dataLayer.SpriteCommands.List,
		            Positions = rendererLayer.Positions,
		            SpriteMeshes = rendererLayer.SpriteMeshes,
		            Vertices = rendererLayer.Vertices,
		            Triangles = rendererLayer.Triangles,
	            }.Schedule(dataSingleton.JobHandle);
	            
	            handle = new GenerateVertexDataJob
	            {
		            Positions = rendererLayer.Positions.AsDeferredJobArray(),
		            SpriteMeshes = rendererLayer.SpriteMeshes.AsDeferredJobArray(),
		            Vertices = rendererLayer.Vertices.AsDeferredJobArray(),
		            Triangles = rendererLayer.Triangles.AsDeferredJobArray(),
		            GridCellSize = dataLayer.TilemapData.GridData.CellSize,
		            Orientation = dataLayer.TilemapData.Orientation,
		            Swizzle = dataLayer.TilemapData.GridData.CellSwizzle
	            }.Schedule(rendererLayer.SpriteMeshes, 32, handle);
	            _jobHandles.Add(handle);
            }
            
	        rendererSingleton.JobHandle = JobHandle.CombineDependencies(_jobHandles.AsArray());
	        _jobHandles.Clear();
        }

        [BurstCompile]
        private struct PrepareVertexDataJob : IJob
        {
	        public NativeParallelHashMap<int2, SpriteMesh> RenderedSprites;
	        
	        [ReadOnly]
	        public NativeList<SpriteCommand> SpriteCommands;
	        [ReadOnly]
	        public NativeList<PositionToRemove> PositionsToRemove;
	        
	        public NativeList<int2> Positions;
	        public NativeList<SpriteMesh> SpriteMeshes;
	        
	        public NativeList<Vertex> Vertices;
	        public NativeList<int> Triangles;
	        
	        public void Execute()
	        {
		        foreach (var positionToRemove in PositionsToRemove)
		        {
			        RenderedSprites.Remove(positionToRemove.Position);
		        }
            
		        foreach (var command in SpriteCommands)
		        {
			        RenderedSprites[command.Position] = command.SpriteMesh;
		        }
	            
		        RenderedSprites.ToNativeLists(ref Positions, ref SpriteMeshes);
		        
		        var meshesCount = Positions.Length;
                
		        var vertexCount = meshesCount * 4;
		        var indexCount = meshesCount * 6;

		        Vertices.Clear();
		        Triangles.Clear();
                
		        if (Vertices.Capacity < vertexCount)
		        {
			        Vertices.Capacity = vertexCount;
			        Triangles.Capacity = indexCount;
		        }

		        Vertices.SetLengthNoClear(vertexCount);
		        Triangles.SetLengthNoClear(indexCount);
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

        	public Swizzle Swizzle;
	        public float3 GridCellSize;
	        public Orientation Orientation;
	        
        	public void Execute(int index)
	        {
		        var spriteMesh = SpriteMeshes[index];
		        var pos = Positions[index];
		        
		        MosaicUtils.GetSpriteMeshTranslation(spriteMesh, out var meshTranslation);

		        var rotatedPos = MosaicUtils.ToWorldSpace(pos, GridCellSize, Swizzle)
		                         + MosaicUtils.ApplyOrientation(meshTranslation, Orientation);

		        var pivotPoint = MosaicUtils.ApplyOrientation(spriteMesh.RectScale * spriteMesh.NormalizedPivot, Orientation);
		        
		        var rotatedSize = MosaicUtils.ApplyOrientation(spriteMesh.RectScale, Orientation);
		        
		        var normal = MosaicUtils.ApplyOrientation(new float3(0, 0, 1), Orientation);
		        
		        var up = MosaicUtils.ApplyOrientation(new float3(0, 1, 0), Orientation) * rotatedSize;
		        var right = MosaicUtils.ApplyOrientation(new float3(1, 0, 0), Orientation) * rotatedSize;

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
        			position = rotatedPos + Rotate(up - pivotPoint, spriteMesh.Rotation) + pivotPoint,
			        normal = normal,
        			uv = new float2(minUv.x, maxUv.y)
        		};

        		Vertices[vc + 1] = new Vertex
        		{
        			position = rotatedPos + Rotate(up + right - pivotPoint, spriteMesh.Rotation) + pivotPoint,
			        normal = normal,
        			uv = new float2(maxUv.x, maxUv.y)
        		};

        		Vertices[vc + 2] = new Vertex
        		{
        			position = rotatedPos + Rotate(right - pivotPoint, spriteMesh.Rotation) + pivotPoint,
			        normal = normal,
        			uv = new float2(maxUv.x, minUv.y)
        		};

        		Vertices[vc + 3] = new Vertex
        		{
        			position = rotatedPos + Rotate(-pivotPoint, spriteMesh.Rotation) + pivotPoint,
			        normal = normal,
        			uv = new float2(minUv.x, minUv.y)
        		};

        		Triangles[tc + 0] = (vc + 0);
        		Triangles[tc + 1] = (vc + 1);
        		Triangles[tc + 2] = (vc + 2);

        		Triangles[tc + 3] = (vc + 0);
        		Triangles[tc + 4] = (vc + 2);
        		Triangles[tc + 5] = (vc + 3);
        	}
	        
	        private float3 Rotate(in float3 direction, in int rotation)
	        {
		        return rotation switch
		        {
			        0 => direction,
			        1 => math.mul(Orientation == Orientation.XY ? RotZ90 : RotY90, direction),
			        2 => math.mul(Orientation == Orientation.XY ? RotZ180 : RotY180, direction),
			        3 => math.mul(Orientation == Orientation.XY ? RotZ270 : RotY270, direction),
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