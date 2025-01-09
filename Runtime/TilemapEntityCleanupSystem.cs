using Drawing;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(TilemapEntityInitializationSystem))]
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
            var list = singleton.PositionToRemove;
            var layers = singleton.IntGridLayers;
            
            if (list.Length == 0) return;

            foreach (var positionToRemove in list)
            {
                var spawnedEntities = layers[positionToRemove.IntGridHash].SpawnedEntities;

                if (spawnedEntities.TryGetValue(positionToRemove.Position, out var entity))
                {
                    state.EntityManager.DestroyEntity(entity);
                    spawnedEntities.Remove(positionToRemove.Position);
                }
            }
            list.Clear();
        }
    }
}