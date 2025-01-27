using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace KrasCore.Mosaic
{
    public struct TilemapMeshDataSingleton : IComponentData, IDisposable
    {
        public NativeList<Hash128> IntGridHashesToUpdate;
        public Mesh.MeshDataArray MeshDataArray;

        public bool IsDirty => IntGridHashesToUpdate.Length > 0;
        
        public void Dispose()
        {
            IntGridHashesToUpdate.Dispose();
        }
    }
    
    [UpdateAfter(typeof(TilemapCommandBufferSystem))]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class TilemapAllocateMeshDataSystem : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.CreateSingleton(new TilemapMeshDataSingleton
            {
                IntGridHashesToUpdate = new NativeList<Hash128>(8, Allocator.Persistent)
            });
        }

        protected override void OnDestroy()
        {
            SystemAPI.GetSingleton<TilemapMeshDataSingleton>().Dispose();
        }

        protected override void OnUpdate()
        {
            EntityManager.CompleteDependencyBeforeRO<TilemapDataSingleton>();
            var dataSingleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            
            ref var meshDataSingleton = ref SystemAPI.GetSingletonRW<TilemapMeshDataSingleton>().ValueRW;
            var meshesSingleton = SystemAPI.ManagedAPI.GetSingleton<TilemapMeshesSingleton>();
            
            foreach (var kvp in dataSingleton.IntGridLayers)
            {
                var intGridHash = kvp.Key;
                var dataLayer = kvp.Value;

                if (!meshesSingleton.Meshes.TryGetValue(intGridHash, out var mesh))
                {
                    mesh = new Mesh { name = "Mosaic.TilemapMesh" };
                    meshesSingleton.Meshes.Add(intGridHash, mesh);
                }
                
                if (dataLayer.PositionToRemove.List.Length == 0 && dataLayer.SpriteCommands.List.Length == 0)
                {
                    continue;
                }
                meshDataSingleton.IntGridHashesToUpdate.Add(intGridHash);
                meshesSingleton.MeshesToUpdate.Add(mesh);
            }
            
            if (meshDataSingleton.IsDirty)
                meshDataSingleton.MeshDataArray = Mesh.AllocateWritableMeshData(meshesSingleton.MeshesToUpdate.Count);
        }
    }
}