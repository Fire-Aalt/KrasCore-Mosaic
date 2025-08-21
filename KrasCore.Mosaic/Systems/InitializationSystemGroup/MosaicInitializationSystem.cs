using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using TerrainData = KrasCore.Mosaic.Data.TerrainData;
using TerrainLayer = KrasCore.Mosaic.Data.TerrainLayer;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(TilemapInitializationSystemGroup), OrderFirst = true)]
    public partial class MosaicInitializationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var uninitializedQuery = SystemAPI.QueryBuilder().WithAll<TilemapRendererData, RuntimeMaterial>().WithNone<MaterialMeshInfo>().Build();
            if (!uninitializedQuery.IsEmpty)
            {
                var presentationSingleton = SystemAPI.ManagedAPI.GetSingleton<MosaicPresentationSystem.Singleton>();
                var tilemapSingleton = SystemAPI.GetSingleton<IntGridMeshDataSystem.Singleton>();
                var terrainSingleton = SystemAPI.GetSingleton<TerrainMeshDataSystem.Singleton>();
                var entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();

                var entities = uninitializedQuery.ToEntityArray(Allocator.Temp);
                var rendererData = uninitializedQuery.ToComponentDataArray<TilemapRendererData>(Allocator.Temp);
                var runtimeMaterials = uninitializedQuery.ToComponentDataArray<RuntimeMaterial>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    var tilemapRenderingData = rendererData[i];
                    var entity = entities[i];
                    
                    var mesh = new Mesh { name = "Mosaic.TilemapMesh" };
                    mesh.MarkDynamic();
                    presentationSingleton.MeshMap.Add(tilemapRenderingData.MeshHash, mesh);

                    var material = runtimeMaterials[i].Value.Value;
                    if (EntityManager.HasComponent<TerrainData>(entity))
                    {
                        material = new Material(material); // Force unique for terrains
                        
                        presentationSingleton.TerrainMap.Add(tilemapRenderingData.MeshHash, new TilemapTerrainRenderingData
                        {
                            Material = material
                        });
                        terrainSingleton.RenderingEntities.Add(entity);
                    }
                    else
                    {
                        tilemapSingleton.RenderingEntities.Add(entity);
                    }
                    
                    var meshId = entitiesGraphicsSystem.RegisterMesh(mesh);
                    var materialId = entitiesGraphicsSystem.RegisterMaterial(material);

                    var desc = new RenderMeshDescription(
                        tilemapRenderingData.ShadowCastingMode,
                        tilemapRenderingData.ReceiveShadows);
                    var materialMeshInfo = new MaterialMeshInfo(materialId, meshId);

                    RenderMeshUtility.AddComponents(entity, EntityManager, desc, materialMeshInfo);
                }

                tilemapSingleton.UpdatedMeshBoundsMap.EnsureMinCapacity(tilemapSingleton.RenderingEntities.Length);
                terrainSingleton.UpdatedMeshBoundsMap.EnsureMinCapacity(terrainSingleton.RenderingEntities.Length);
            }
        
            Dependency = new RegisterJob
            {
                TilemapTerrainLayerTagLookup = SystemAPI.GetComponentLookup<TerrainLayer>(true),
                Tcb = SystemAPI.GetSingletonRW<TilemapCommandBufferSingleton>().ValueRW,
                DataSingleton = SystemAPI.GetSingletonRW<RuleEngineSystem.Singleton>().ValueRW,
            }.Schedule(Dependency);
            
            Dependency = new UpdateTilemapRendererDataJob
            {
                GridDataLookup = SystemAPI.GetComponentLookup<GridData>(true)
            }.Schedule(Dependency);
        }

        [BurstCompile]
        [WithDisabled(typeof(IntGridData))]
        private partial struct RegisterJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<TerrainLayer> TilemapTerrainLayerTagLookup;
            
            public TilemapCommandBufferSingleton Tcb;
            public RuleEngineSystem.Singleton DataSingleton;
            
            private void Execute(ref IntGridData intGridData, EnabledRefRW<IntGridData> enabled, Entity entity)
            {
                var isTerrainLayer = TilemapTerrainLayerTagLookup.HasComponent(entity);
                
                Tcb.TryRegisterIntGridLayer(intGridData.Hash);
                DataSingleton.TryRegisterIntGridLayer(intGridData, isTerrainLayer, entity);
                enabled.ValueRW = true;
            }
        }

        [BurstCompile]
        private partial struct UpdateTilemapRendererDataJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<GridData> GridDataLookup;
            
            private void Execute(ref TilemapRendererData rendererData)
            {
                var gridData = GridDataLookup[rendererData.GridEntity];
                rendererData.Swizzle = gridData.Swizzle;
                rendererData.CellSize = gridData.CellSize;
            }
        }
    }
}