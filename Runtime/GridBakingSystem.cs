using Unity.Burst;
using Unity.Entities;

namespace KrasCore.Mosaic
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct GridBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (buffer, gridDataRO) in SystemAPI.Query<DynamicBuffer<GridBakingData>, RefRO<GridData>>())
            {
                foreach (var tilemapData in SystemAPI.Query<RefRW<TilemapData>>())
                {
                    foreach (var gridBakingData in buffer)
                    {
                        if (tilemapData.ValueRO.IntGridReference == gridBakingData.ToLink)
                        {
                            tilemapData.ValueRW.GridData = gridDataRO.ValueRO;
                        }
                    }
                }
            }
        }
    }
}