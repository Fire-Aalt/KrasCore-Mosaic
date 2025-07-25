using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(TilemapInitializationSystemGroup), OrderFirst = true)]
    public partial class TilemapInitializationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var uninitializedQuery = SystemAPI.QueryBuilder().WithAll<TilemapData, RuntimeMaterial>().WithNone<MaterialMeshInfo>().Build();
            if (!uninitializedQuery.IsEmpty)
            {
                var meshSingleton = SystemAPI.ManagedAPI.GetSingleton<TilemapMeshSingleton>();
                var entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();

                var entities = uninitializedQuery.ToEntityArray(Allocator.Temp);
                var tilemapsData = uninitializedQuery.ToComponentDataArray<TilemapData>(Allocator.Temp);
                var runtimeMaterials = uninitializedQuery.ToComponentDataArray<RuntimeMaterial>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    var tilemapData = tilemapsData[i];

                    var mesh = new Mesh { name = "Mosaic.TilemapMesh" };
                    mesh.MarkDynamic();
                    meshSingleton.MeshMap.Add(tilemapData.IntGridHash, mesh);

                    var meshId = entitiesGraphicsSystem.RegisterMesh(mesh);
                    var materialId = entitiesGraphicsSystem.RegisterMaterial(runtimeMaterials[i].Value);

                    var desc = new RenderMeshDescription(
                        tilemapData.ShadowCastingMode,
                        tilemapData.ReceiveShadows);
                    var materialMeshInfo = new MaterialMeshInfo(materialId, meshId);

                    RenderMeshUtility.AddComponents(entities[i], EntityManager, desc, materialMeshInfo);
                }
            }
            
            Dependency = new RegisterJob
            {
                Tcb = SystemAPI.GetSingletonRW<TilemapCommandBufferSingleton>().ValueRW,
                DataSingleton = SystemAPI.GetSingletonRW<TilemapDataSingleton>().ValueRW
            }.Schedule(Dependency);
            
            Dependency = new UpdateTilemapRendererDataJob
            {
                GridDataLookup = SystemAPI.GetComponentLookup<GridData>(true)
            }.Schedule(Dependency);
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