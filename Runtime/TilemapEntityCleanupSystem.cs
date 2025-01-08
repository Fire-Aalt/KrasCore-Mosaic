using Unity.Burst;
using Unity.Entities;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct TilemapEntityCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TilemapDataSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            var list = singleton.PositionToRemoveList;
            var layers = singleton.IntGridLayers;
            
            if (list.Length == 0) return;

            foreach (var positionToRemove in list)
            {
                var spawnedEntities = layers[positionToRemove.IntGridHash].SpawnedEntities;

                if (spawnedEntities.ContainsKey(positionToRemove.Position))
                {
                    foreach (var entity in spawnedEntities.GetValuesForKey(positionToRemove.Position))
                    {
                        state.EntityManager.DestroyEntity(entity);
                    }
                    spawnedEntities.Remove(positionToRemove.Position);
                }
            }
            list.Clear();
        }
    }
}