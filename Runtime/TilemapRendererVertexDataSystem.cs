using System;
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

			public IntGridLayer(int capacity, Allocator allocator)
			{
				Vertices = new NativeList<Vertex>(capacity, allocator);
				Triangles = new NativeList<int>(capacity, allocator);
			}

			public void Dispose()
			{
				Vertices.Dispose();
				Triangles.Dispose();
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
    public partial struct TilemapRendererVertexDataSystem : ISystem
    {
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

            foreach (var positionToRemove in dataSingleton.PositionToRemove)
            {
                var layer = _intGridLayers[positionToRemove.IntGridHash];
                
                layer.RenderedSprites.Remove(positionToRemove.Position);
            }
            
            foreach (var command in _commandsList)
            {
                var layer = _intGridLayers[command.IntGridHash];
                
                layer.RenderedSprites.Add(command.Position, command.SpriteMesh);
            }
            
            foreach (var layer in _intGridLayers)
            {
	            if (!rendererSingleton.IntGridLayers.TryGetValue(layer.Key, out var rendererLayer))
	            {
		            rendererLayer = new TilemapRendererSingleton.IntGridLayer(256, Allocator.Persistent);
		            rendererSingleton.IntGridLayers.Add(layer.Key, rendererLayer);
	            }
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
		            verts = rendererLayer.Vertices.AsArray(),
		            tris = rendererLayer.Triangles.AsArray(),
		            GridCellSize = layer.Value.TilemapData.GridData.CellSize,
		            Orientation = layer.Value.TilemapData.Orientation,
		            Swizzle = layer.Value.TilemapData.GridData.CellSwizzle,
		            TilemapTransform = layer.Value.TilemapTransform
	            }.ScheduleParallel(meshesCount, 32, dataSingleton.JobHandle);
	            keyValueArrays.Dispose(handle);
	            
	            _jobHandles.Add(handle);
            }
            
	        rendererSingleton.JobHandle = JobHandle.CombineDependencies(_jobHandles.AsArray());
	        _jobHandles.Clear();
        }
        
		[BurstCompile]
        public struct GenerateVertexDataJob : IJobFor
        {
	        [ReadOnly]
	        public NativeArray<int2> Positions;
	        [ReadOnly]
	        public NativeArray<SpriteMesh> SpriteMeshes;

	        [NativeDisableParallelForRestriction]
			[WriteOnly] public NativeArray<Vertex> verts;
	        [NativeDisableParallelForRestriction]
        	[WriteOnly] public NativeArray<int> tris;

        	public Swizzle Swizzle;
	        public float3 GridCellSize;
	        public Orientation Orientation;

	        public LocalTransform TilemapTransform;

        	public void Execute(int index)
	        {
		        var spriteMesh = SpriteMeshes[index];
		        MosaicUtils.GetSpriteMeshTranslation(spriteMesh, default, out var meshTranslation);

		        var rotatedPos = TilemapTransform.TransformPoint(MosaicUtils.ToWorldSpace(Positions[index], GridCellSize, Swizzle)
		                                                            + MosaicUtils.ApplyOrientation(meshTranslation, Orientation));

		        var rotatedSize = MosaicUtils.ApplyOrientation(spriteMesh.RectScale, Orientation);
		        
		        var normal = TilemapTransform.TransformDirection(MosaicUtils.ApplyOrientation(new float3(0, 0, 1), Orientation));
		        
		        var up = TilemapTransform.TransformDirection(MosaicUtils.ApplyOrientation(new float3(0, 1, 0), Orientation) * rotatedSize);
		        var right = TilemapTransform.TransformDirection(MosaicUtils.ApplyOrientation(new float3(1, 0, 0), Orientation) * rotatedSize);

        		int vc = 4 * index;
        		int tc = 2 * 3 * index;

        		verts[vc + 0] = new Vertex
        		{
        			position = (rotatedPos + up),
			        normal = normal,
        			uv = new float2(spriteMesh.MinUv.x, spriteMesh.MaxUv.y)
        		};

        		verts[vc + 1] = new Vertex
        		{
        			position = (rotatedPos + up + right),
			        normal = normal,
        			uv = new float2(spriteMesh.MaxUv.x, spriteMesh.MaxUv.y)
        		};

        		verts[vc + 2] = new Vertex
        		{
        			position = (rotatedPos + right),
			        normal = normal,
        			uv = new float2(spriteMesh.MaxUv.x, spriteMesh.MinUv.y)
        		};

        		verts[vc + 3] = new Vertex
        		{
        			position = (rotatedPos),
			        normal = normal,
        			uv = new float2(spriteMesh.MinUv.x, spriteMesh.MinUv.y)
        		};

        		tris[tc + 0] = (vc + 0);
        		tris[tc + 1] = (vc + 1);
        		tris[tc + 2] = (vc + 2);

        		tris[tc + 3] = (vc + 0);
        		tris[tc + 4] = (vc + 2);
        		tris[tc + 5] = (vc + 3);
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