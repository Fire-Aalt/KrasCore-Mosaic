using Unity.Burst;
using Unity.Entities;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(TilemapInitializationSystemGroup))]
    public partial struct TilemapEntityCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
                
            foreach (var kvp in singleton.IntGridLayers)
            {
                var dataLayer = kvp.Value;
                var spawnedEntities = dataLayer.SpawnedEntities;
                
                foreach (var positionToRemove in dataLayer.PositionToRemove.List)
                {
                    if (spawnedEntities.TryGetValue(positionToRemove.Position, out var entity))
                    {
                        state.EntityManager.DestroyEntity(entity);
                        spawnedEntities.Remove(positionToRemove.Position);
                    }
                }
            }
        }
    }
}