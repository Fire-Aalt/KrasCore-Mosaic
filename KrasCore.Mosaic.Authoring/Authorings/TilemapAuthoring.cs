using System;
using KrasCore.Mosaic.Data;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic.Authoring
{
    public class TilemapAuthoring : MonoBehaviour
    {
        [SerializeField] private IntGridDefinition intGrid;
        [SerializeField] private Orientation _orientation;

        [SerializeField] private Material _material;
        [SerializeField] private ShadowCastingMode _shadowCastingMode = ShadowCastingMode.TwoSided;
        [SerializeField] private bool _receiveShadows = true;

        public class Baker : Baker<TilemapAuthoring>
        {
            public override void Bake(TilemapAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var ruleBlobBuffer = AddBuffer<RuleBlobReferenceElement>(entity);
                var weightedEntityBuffer = AddBuffer<WeightedEntityElement>(entity);

                var refreshPositions = new NativeHashSet<int2>(64, Allocator.Temp);
                Texture2D refTexture = null;
                
                var entityCount = 0;
                foreach (var group in authoring.intGrid.ruleGroups)
                {
                    DependsOn(group);
                    
                    foreach (var rule in group.rules)
                    {
                        var blob = RuleBlobCreator.Create(rule, entityCount, refreshPositions);
                        AddBlobAsset(ref blob, out _);

                        ruleBlobBuffer.Add(new RuleBlobReferenceElement
                        {
                            Enabled = rule.enabled.HasFlag(RuleGroup.Enabled.Enabled),
                            Value = blob
                        });
                        
                        refTexture = AddResults(rule, weightedEntityBuffer, refTexture);

                        entityCount += rule.TileEntities.Count;
                    }
                }
                
                AddComponent(entity, new TilemapData
                {
                    IntGridHash = authoring.intGrid.Hash,
                    Orientation = authoring._orientation,
                    GridEntity = GetEntity(authoring.GetComponentInParent<GridAuthoring>(), TransformUsageFlags.None),
                    ShadowCastingMode = authoring._shadowCastingMode,
                    ReceiveShadows = authoring._receiveShadows
                });
                SetComponentEnabled<TilemapData>(entity, false);

                AddComponent(entity, new RuntimeMaterialLookup(authoring._material, refTexture));
                AddComponent<RuntimeMaterial>(entity);
                
                var refreshPositionsBuffer = AddBuffer<RefreshPositionElement>(entity);
                refreshPositionsBuffer.AddRange(refreshPositions.ToNativeArray(Allocator.Temp).Reinterpret<RefreshPositionElement>());
            }

            private Texture2D AddResults(RuleGroup.Rule rule, DynamicBuffer<WeightedEntityElement> weightedEntityBuffer, Texture2D refTexture)
            {
                for (var i = 0; i < rule.TileEntities.Count; i++)
                {
                    var go = rule.TileEntities[i].result;

                    weightedEntityBuffer.Add(new WeightedEntityElement
                    {
                        Value = GetEntity(go, TransformUsageFlags.None)
                    });
                }

                for (int i = 0; i < rule.TileSprites.Count; i++)
                {
                    var spriteTexture = rule.TileSprites[i].result.texture;

                    if (refTexture == null)
                        refTexture = spriteTexture;
                    else if (refTexture != spriteTexture)
                    {
                        throw new Exception("Different textures in one tilemap. This is not supported yet");
                    }
                }
                return refTexture;
            }
        }
    }
}
