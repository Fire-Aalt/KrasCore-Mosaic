#if BL_DEBUG || UNITY_EDITOR
using BovineLabs.Core;
using BovineLabs.Core.Collections;
using BovineLabs.Quill;
using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using Random = Unity.Mathematics.Random;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct IntGridDebugSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            state.RequireForUpdate<TilemapDataSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var drawer = SystemAPI.GetSingleton<DrawSystem.Singleton>().CreateDrawer<IntGridDebugSystem>();
            if (!drawer.IsEnabled)
            {
                return;
            }
            
            var data = SystemAPI.GetSingleton<TilemapDataSingleton>();
            
            var arrays = data.IntGridLayers.GetKeyValueArrays(state.WorldUpdateAllocator);
            var ar = CollectionHelper.CreateNativeArray<NativeKeyValueArrays<int2, IntGridValue>>(arrays.Keys.Length, state.WorldUpdateAllocator);
            for (int i = 0; i < ar.Length; i++)
            {
                ar[i] = arrays.Values[i].IntGrid.GetKeyValueArrays(state.WorldUpdateAllocator);
            }
            
            
            var keysArray = data.IntGridLayers.GetKeyArray(state.WorldUpdateAllocator);
            
            state.Dependency = new DrawChunkBordersJob
            {
                Drawer = drawer,
                Keys = keysArray,
                IntGridLayers = ar,
            }.ScheduleParallel(keysArray.Length, 1, state.Dependency);
        }

        [BurstCompile]
        private struct DrawChunkBordersJob : IJobFor
        {
            public Drawer Drawer;

            [ReadOnly] 
            public NativeArray<Hash128> Keys;
            
            [ReadOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<NativeKeyValueArrays<int2, IntGridValue>> IntGridLayers;
            
            public void Execute(int index)
            {
                var key = Keys[index];
                var layer = IntGridLayers[index];

                var rnd = new Random((uint)key.GetHashCode());
                var rgb = rnd.NextFloat3();
                var color = new Color(rgb.x, rgb.y, rgb.z, 1f);
                
                for (var i = 0; i < layer.Keys.Length; i++)
                {
                    var pos = layer.Keys[i];
                    var value = layer.Values[i].value;
                    //var cellSize = layer.TilemapData.GridData.CellSize;

                    if (value != 0)
                    {
                        var str = new FixedString32Bytes();
                        str.Append(value);
                        
                        Drawer.RectangleXZ(new float3(pos.x, 0f, pos.y), new int2(1, 1), color);
                        Drawer.Text32(new float3(pos.x, 0.1f, pos.y), str, color);
                    }
                }
            }
        }
    }
}
#endif