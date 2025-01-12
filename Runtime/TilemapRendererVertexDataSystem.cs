using System;
using Drawing;
using Game;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace KrasCore.Mosaic
{
	public struct TilemapRendererSingleton : IComponentData, IDisposable
	{
		public struct IntGridLayer : IDisposable
		{
			public NativeList<Vertex> Vertices;
			public NativeList<int> Triangles;

			public NativeReference<bool> IsDirty;

			public IntGridLayer(int capacity, Allocator allocator)
			{
				Vertices = new NativeList<Vertex>(capacity, allocator);
				Triangles = new NativeList<int>(capacity, allocator);
				IsDirty = new NativeReference<bool>(allocator);
			}

			public void Dispose()
			{
				Vertices.Dispose();
				Triangles.Dispose();
				IsDirty.Dispose();
			}
		}
		
		public NativeHashMap<int, IntGridLayer> IntGridLayers;
		public JobHandle JobHandle;
		
		public TilemapRendererSingleton(int capacity, Allocator allocator)
		{
			IntGridLayers = new NativeHashMap<int, IntGridLayer>(capacity, allocator);
			JobHandle = default;
		}

		public void Dispose()
		{
			foreach (var layer in IntGridLayers)
			{
				layer.Value.Dispose();
			}
			IntGridLayers.Dispose();
		}
	}
	
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
	    
        private NativeList<SpriteCommand> _commandsList;
        private NativeHashMap<int, IntGridLayer> _intGridLayers;
        private NativeList<JobHandle> _jobHandles;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        state.RequireForUpdate<TilemapRendererSingleton>();
	        state.EntityManager.CreateSingleton(new TilemapRendererSingleton(8, Allocator.Persistent));
            state.RequireForUpdate<TilemapDataSingleton>();
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
            _commandsList = dataSingleton.SpriteCommands;
            _intGridLayers = dataSingleton.IntGridLayers;
            rendererSingleton.JobHandle = default;
            dataSingleton.JobHandle.Complete();

            if (dataSingleton.PositionToRemove.Length == 0 && _commandsList.Length == 0) return;
            
            foreach (var positionToRemove in dataSingleton.PositionToRemove)
            {
                var dataLayer = _intGridLayers[positionToRemove.IntGridHash];
                var rendererLayer = GetOrAddRendererLayer(ref rendererSingleton, positionToRemove.IntGridHash);
                
                dataLayer.RenderedSprites.Remove(positionToRemove.Position);
                rendererLayer.IsDirty.Value = true;
            }
            
            foreach (var command in _commandsList)
            {
                var dataLayer = _intGridLayers[command.IntGridHash];
                var rendererLayer = GetOrAddRendererLayer(ref rendererSingleton, command.IntGridHash);
                
                dataLayer.RenderedSprites.Add(command.Position, command.SpriteMesh);
                rendererLayer.IsDirty.Value = true;
            }
            
            foreach (var layer in _intGridLayers)
            {
	            var rendererLayer = GetOrAddRendererLayer(ref rendererSingleton, layer.Key);
	            if (!rendererLayer.IsDirty.Value) continue;
	            
                var keyValueArrays = layer.Value.RenderedSprites.GetKeyValueArrays(Allocator.TempJob);
                
                var meshesCount = keyValueArrays.Values.Length;
                
                var vertexCount = meshesCount * 4;
                var indexCount = meshesCount * 6;

                rendererLayer.Vertices.Clear();
                rendererLayer.Triangles.Clear();
                
                if (rendererLayer.Vertices.Capacity < vertexCount)
                {
		            rendererLayer.Vertices.Capacity = vertexCount;
		            rendererLayer.Triangles.Capacity = indexCount;
                }

                rendererLayer.Vertices.SetLengthNoClear(vertexCount);
                rendererLayer.Triangles.SetLengthNoClear(indexCount);

	            var handle = new GenerateVertexDataJob
	            {
		            Positions = keyValueArrays.Keys,
		            SpriteMeshes = keyValueArrays.Values,
		            Vertices = rendererLayer.Vertices.AsArray(),
		            Triangles = rendererLayer.Triangles.AsArray(),
		            GridCellSize = layer.Value.TilemapData.GridData.CellSize,
		            Orientation = layer.Value.TilemapData.Orientation,
		            Swizzle = layer.Value.TilemapData.GridData.CellSwizzle
	            }.ScheduleParallel(meshesCount, 32, dataSingleton.JobHandle);
	            keyValueArrays.Dispose(handle);
	            _jobHandles.Add(handle);
            }
            
	        rendererSingleton.JobHandle = JobHandle.CombineDependencies(_jobHandles.AsArray());
	        _jobHandles.Clear();
        }

        private static TilemapRendererSingleton.IntGridLayer GetOrAddRendererLayer(ref TilemapRendererSingleton rendererSingleton, int intGridHash)
        {
	        if (!rendererSingleton.IntGridLayers.TryGetValue(intGridHash, out var rendererLayer))
	        {
		        rendererLayer = new TilemapRendererSingleton.IntGridLayer(256, Allocator.Persistent);
		        rendererSingleton.IntGridLayers.Add(intGridHash, rendererLayer);
	        }
	        return rendererLayer;
        }

        [BurstCompile]
        private struct GenerateVertexDataJob : IJobFor
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
		        MosaicUtils.GetSpriteMeshTranslation(spriteMesh, out var meshTranslation);

		        var rotatedPos = MosaicUtils.ToWorldSpace(Positions[index], GridCellSize, Swizzle)
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