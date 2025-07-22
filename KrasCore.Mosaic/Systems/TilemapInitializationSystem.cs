using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(TilemapInitializationSystemGroup), OrderFirst = true)]
    public partial struct TilemapInitializationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new RegisterJob
            {
                Tcb = SystemAPI.GetSingletonRW<TilemapCommandBufferSingleton>().ValueRW,
                DataSingleton = SystemAPI.GetSingletonRW<TilemapDataSingleton>().ValueRW
            }.Schedule(state.Dependency);
            
            state.Dependency = new UpdateTilemapRendererDataJob
            {
                GridDataLookup = SystemAPI.GetComponentLookup<GridData>(true)
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithDisabled(typeof(TilemapData))]
        private partial struct RegisterJob : IJobEntity
        {
            public TilemapCommandBufferSingleton Tcb;
            public TilemapDataSingleton DataSingleton;
            
            private void Execute(ref TilemapData tilemapData, EnabledRefRW<TilemapData> enabled, Entity entity)
            {
                Tcb.TryRegisterIntGridLayer(tilemapData.IntGridHash);
                DataSingleton.TryRegisterIntGridLayer(tilemapData, entity);
                enabled.ValueRW = true;
            }
        }
        
        [BurstCompile]
        private partial struct UpdateTilemapRendererDataJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<GridData> GridDataLookup;
            
            private void Execute(in TilemapData data, ref TilemapRendererData rendererData)
            {
                var gridData = GridDataLookup[data.GridEntity];
                rendererData.Swizzle = gridData.Swizzle;
                rendererData.CellSize = gridData.CellSize;
            }
        }
    }
}