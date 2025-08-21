using System;
using KrasCore.Mosaic.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace KrasCore.Mosaic.Authoring
{
    public static class BakerUtils
    {
        public static void AddRenderingData(IBaker baker, Entity entity, Hash128 meshHash, RenderingData renderingData,
            GridAuthoring gridAuthoring, Texture2D materialTexture)
        {
            if (renderingData.material == null)
            {
                throw new Exception("Material is null");
            }
            
            baker.AddComponent(entity, new TilemapRendererData
            {
                Orientation = renderingData.orientation,
            });
            
            baker.AddComponent(entity, new TilemapRendererInitData
            {
                MeshHash = meshHash,
                GridEntity = baker.GetEntity(gridAuthoring, TransformUsageFlags.None),
                ShadowCastingMode = renderingData.shadowCastingMode,
                ReceiveShadows = renderingData.receiveShadows
            });
            
            baker.AddComponent(entity, new RuntimeMaterialLookup(renderingData.material, materialTexture));
            baker.AddComponent<RuntimeMaterial>(entity);
        }
        
        public static Texture2D AddIntGridLayerData(IBaker baker, Entity entity, IntGridDefinition intGrid,
            Texture2D materialTexture, bool constPivotAndSize, ref float2 tilePivot, ref float2 tileSize)
        {
            var ruleBlobBuffer = baker.AddBuffer<RuleBlobReferenceElement>(entity);
            var weightedEntityBuffer = baker.AddBuffer<WeightedEntityElement>(entity);

            var refreshPositions = new NativeHashSet<int2>(64, Allocator.Temp);

            var entityCount = 0;
            baker.DependsOn(intGrid);
            foreach (var group in intGrid.ruleGroups)
            {
                baker.DependsOn(group);
                
                foreach (var rule in group.rules)
                {
                    var blob = RuleBlobCreator.Create(rule, entityCount, refreshPositions);
                    baker.AddBlobAsset(ref blob, out _);

                    ruleBlobBuffer.Add(new RuleBlobReferenceElement
                    {
                        Enabled = rule.enabled.HasFlag(RuleGroup.Enabled.Enabled),
                        Value = blob
                    });
                    
                    materialTexture = AddResults(baker, rule, weightedEntityBuffer, materialTexture, constPivotAndSize, ref tilePivot, ref tileSize);
                    entityCount += rule.TileEntities.Count;
                }
            }
            
            baker.AddComponent(entity, new IntGridData
            {
                Hash = intGrid.Hash,
                DebugName = intGrid.name,
                DualGrid = intGrid.useDualGrid
            });
            baker.SetComponentEnabled<IntGridData>(entity, false);
            
            var refreshPositionsBuffer = baker.AddBuffer<RefreshPositionElement>(entity);
            refreshPositionsBuffer.AddRange(refreshPositions.ToNativeArray(Allocator.Temp).Reinterpret<RefreshPositionElement>());
            return materialTexture;
        }
        
        private static Texture2D AddResults(IBaker baker, RuleGroup.Rule rule,
            DynamicBuffer<WeightedEntityElement> weightedEntityBuffer, Texture2D materialTexture,
            bool constPivotAndSize, ref float2 tilePivot, ref float2 tileSize)
        {
            for (var i = 0; i < rule.TileEntities.Count; i++)
            {
                var go = rule.TileEntities[i].result;

                weightedEntityBuffer.Add(new WeightedEntityElement
                {
                    Value = baker.GetEntity(go, TransformUsageFlags.None)
                });
            }

            for (int i = 0; i < rule.TileSprites.Count; i++)
            {
                var sprite = rule.TileSprites[i].result;
                var spriteTexture = sprite.texture;

                if (constPivotAndSize)
                {
                    var spriteMesh = new SpriteMesh(sprite);
                    var uvPivot = spriteMesh.NormalizedPivot;
                    var uvTileSize = spriteMesh.MaxUv - spriteMesh.MinUv;
                    
                    if (math.all(tilePivot == float2.zero))
                    {
                        tilePivot = uvPivot;
                    }
                    if (math.all(tileSize == float2.zero))
                    {
                        tileSize = uvTileSize;
                    }

                    if (math.any(tilePivot != uvPivot))
                    {
                        throw new Exception("Different pivots in one tilemap terrain. This is not supported");
                    }
                    if (math.any(tileSize != uvTileSize))
                    {
                        throw new Exception("Different tile sizes in one tilemap terrain. This is not supported");
                    }
                }

                if (materialTexture == null)
                {
                    materialTexture = spriteTexture;
                }
                else if (materialTexture != spriteTexture)
                {
                    throw new Exception("Different textures in one tilemap. This is not supported");
                }
            }
            return materialTexture;
        }
    }
}