using System;
using System.Collections.Generic;
using KrasCore.Mosaic.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using Random = UnityEngine.Random;
using TerrainData = KrasCore.Mosaic.Data.TerrainData;
using TerrainLayer = KrasCore.Mosaic.Data.TerrainLayer;

namespace KrasCore.Mosaic.Authoring
{
    public class TilemapTerrainAuthoring : MonoBehaviour
    {
        public List<IntGridDefinition> intGridLayers = new();
        public RenderingData renderingData;
        public Hash128 terrainHash;


        [MenuItem("CONTEXT/TilemapTerrainAuthoring/Randomize Terrain Hash")]
        private static void RandomizeTerrainHash(MenuCommand command)
        {
            var body = (TilemapTerrainAuthoring)command.context;
            body.terrainHash = new Hash128((uint)Random.Range(int.MinValue, int.MaxValue),
                (uint)Random.Range(int.MinValue, int.MaxValue),
                (uint)Random.Range(int.MinValue, int.MaxValue),
                (uint)Random.Range(int.MinValue, int.MaxValue));
        }
        
        private void OnValidate()
        {
            if (intGridLayers.Count > 8)
            {
                var exceed = intGridLayers.Count - 8;
                for (var index = 0; index < exceed; index++)
                {
                    intGridLayers.RemoveAtSwapBack(8);
                }
            }
        }

        private class Baker : Baker<TilemapTerrainAuthoring>
        {
            public override void Bake(TilemapTerrainAuthoring authoring)
            {
                if (authoring.intGridLayers.Count == 0) return;
                
                var gridAuthoring = GetComponentInParent<GridAuthoring>();
                if (gridAuthoring == null)
                {
                    throw new Exception("GridAuthoring not found");
                }
                
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var layersBuffer = AddBuffer<TilemapTerrainLayerElement>(entity);
                Texture2D materialTexture = null;
                
                // Bake layers
                var intGridLayersEntities = new NativeArray<Entity>(authoring.intGridLayers.Count, Allocator.Temp);
                CreateAdditionalEntities(intGridLayersEntities, TransformUsageFlags.None);
                
                var tilePivot = float2.zero;
                var tileSize = float2.zero;
                for (int i = 0; i < intGridLayersEntities.Length; i++)
                {
                    materialTexture = BakerUtils.AddIntGridLayerData(this, intGridLayersEntities[i], authoring.intGridLayers[i],
                        materialTexture, true, ref tilePivot, ref tileSize);
                    layersBuffer.Add(new TilemapTerrainLayerElement { IntGridHash = authoring.intGridLayers[i].Hash });
                    AddComponent(intGridLayersEntities[i], new TerrainLayer { TerrainEntity = entity });
                }
                
                // Bake terrain entity
                AddComponent(entity, new TerrainData
                {
                    TerrainHash = authoring.terrainHash,
                    TileSize = tileSize
                });

                BakerUtils.AddRenderingData(this, entity, authoring.terrainHash, authoring.renderingData, gridAuthoring, materialTexture);
            }
        }
    }
}