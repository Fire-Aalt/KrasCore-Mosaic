using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(TilemapCleanupSystemGroup))]
    public partial struct TilemapEntityCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            
            foreach (var kvp in singleton.IntGridLayers)
            {
                ref var dataLayer = ref kvp.Value;
                ref var spawnedEntities = ref dataLayer.SpawnedEntities;

                if (dataLayer.RuleGrid.Count == 0 && dataLayer.SpawnedEntities.Count != 0)
                {
                    foreach (var kvPair in dataLayer.SpawnedEntities)
                    {
                        state.EntityManager.DestroyEntity(kvPair.Value);
                    }
                    dataLayer.SpawnedEntities.Clear();
                }
                else
                {
                    foreach (var removedPos in dataLayer.RefreshedPositions)
                    {
                        spawnedEntities.TryGetValue(removedPos, out var entity);
                        
                        state.EntityManager.DestroyEntity(entity);
                        spawnedEntities.Remove(removedPos);
                    }
                }
            }
        }
    }
}