using Game;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace KrasCore.Mosaic
{
    public partial struct TilemapRendererVertexDataSystem : ISystem
    {
        private NativeList<SpriteCommand> _commandsList;
        private NativeHashMap<int, IntGridLayer> _intGridLayers;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TilemapDataSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            _commandsList = singleton.SpriteCommands;
            _intGridLayers = singleton.IntGridLayers;

            foreach (var positionToRemove in singleton.PositionToRemove)
            {
                var layer = _intGridLayers[positionToRemove.IntGridHash];
                
                layer.RenderedSprites.Remove(positionToRemove.Position);
            }
            
            foreach (var command in _commandsList)
            {
                var layer = _intGridLayers[command.IntGridHash];
                
                layer.RenderedSprites.Add(command.Position, command.SpriteProperties);

            }

            foreach (var layer in _intGridLayers)
            {
                var keyValueArrays = layer.Value.RenderedSprites.GetKeyValueArrays(Allocator.TempJob);
                
            }
            
        }
        
		[BurstCompile]
        public struct JobGenerateMesh : IJobFor
        {
	        [ReadOnly]
	        public NativeArray<int2> Positions;
	        [ReadOnly]
	        public NativeArray<SpriteProperties> SpriteProperties;

			[WriteOnly] public NativeArray<Vertex> verts;
        	[WriteOnly] public NativeArray<int> tris;

        	public Swizzle Swizzle;
	        public float3 GridCellSize;
	        public Orientation Orientation;

        	public void Execute(int index)
	        {
		        var spritePos = MosaicUtils.ToWorldSpace(Positions[index], GridCellSize, Swizzle);
		        
        		// Create a square with the "forward" direction along the agent's velocity
        		float3 forward = transform.Forward() * shape.radius;
        		if (math.all(forward == 0)) forward = new float3(0, 0, shape.radius);
        		float3 right = math.cross(new float3(0, 1, 0), forward);
        		float3 orig = transform.Position + (float3)renderingOffset;

        		int vc = 4 * entityIndex;
        		int tc = 2 * 3 * entityIndex;

        		Color32 color = agentData.color;
        		verts[vc + 0] = new Vertex
        		{
        			position = (orig + forward - right),
        			uv = new float2(0, 1),
        			color = color,
        		};

        		verts[vc + 1] = new Vertex
        		{
        			position = (orig + forward + right),
        			uv = new float2(1, 1),
        			color = color,
        		};

        		verts[vc + 2] = new Vertex
        		{
        			position = (orig - forward + right),
        			uv = new float2(1, 0),
        			color = color,
        		};

        		verts[vc + 3] = new Vertex
        		{
        			position = (orig - forward - right),
        			uv = new float2(0, 0),
        			color = color,
        		};

        		tris[tc + 0] = (vc + 0);
        		tris[tc + 1] = (vc + 1);
        		tris[tc + 2] = (vc + 2);

        		tris[tc + 3] = (vc + 0);
        		tris[tc + 4] = (vc + 2);
        		tris[tc + 5] = (vc + 3);
        	}
        }
        
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

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