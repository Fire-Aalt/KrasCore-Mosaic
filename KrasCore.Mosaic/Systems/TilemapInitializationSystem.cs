using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(TilemapInitializationSystemGroup), OrderFirst = true)]
    public partial struct TilemapInitializationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dataSingleton = SystemAPI.GetSingletonRW<TilemapDataSingleton>().ValueRW;
            
            state.Dependency = new RegisterJob
            {
                Tcb = SystemAPI.GetSingletonRW<TilemapCommandBufferSingleton>().ValueRW,
                DataSingleton = dataSingleton
            }.Schedule(state.Dependency);
            
            state.Dependency = new UpdateJob
            {
                GridData = SystemAPI.GetComponentLookup<GridData>(true),
                DataLayers = dataSingleton.IntGridLayers
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithDisabled(typeof(TilemapData))]
        private partial struct RegisterJob : IJobEntity
        {
            public TilemapCommandBufferSingleton Tcb;
            [NativeDisableContainerSafetyRestriction]
            public TilemapDataSingleton DataSingleton;
            
            private void Execute(ref TilemapData tilemapData, EnabledRefRW<TilemapData> enabled)
            {
                Tcb.TryRegisterIntGridLayer(tilemapData.IntGridHash);
                DataSingleton.TryRegisterIntGridLayer(tilemapData);
                enabled.ValueRW = true;
            }
        }
        
        [BurstCompile]
        private partial struct UpdateJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<GridData> GridData;
            
            [NativeDisableContainerSafetyRestriction]
            public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> DataLayers;
            
            private void Execute(ref TilemapData tilemapData)
            {
                tilemapData.GridData = GridData[tilemapData.GridEntity];
                ref var layer = ref DataLayers.GetValueAsRef(tilemapData.IntGridHash);
                layer.SetTilemapData(tilemapData);
            }
        }
    }
}