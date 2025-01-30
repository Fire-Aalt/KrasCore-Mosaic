using System;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;
using Hash128 = Unity.Entities.Hash128;
using Random = Unity.Mathematics.Random;

#if HYBRID_ECS
using KrasCore.HybridECS;
#endif

namespace KrasCore.Mosaic
{
    public class TilemapAuthoring : MonoBehaviour
    {
        [SerializeField] private IntGrid _intGrid;
        [SerializeField] private Orientation _orientation;
        
        [SerializeField] private Material _material;
        [SerializeField] private ShadowCastingMode _shadowCastingMode = ShadowCastingMode.TwoSided;
        [SerializeField] private bool _receiveShadows = true;

        public IntGrid IntGrid => _intGrid;

#if UNITY_EDITOR
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
                foreach (var group in authoring._intGrid.ruleGroups)
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
                    IntGridHash = authoring._intGrid.Hash,
                    Orientation = authoring._orientation,
                    ShadowCastingMode = authoring._shadowCastingMode,
                    ReceiveShadows = authoring._receiveShadows
                });
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
#if HYBRID_ECS
                    if (go.TryGetComponent(out HybridPrefab hybridPrefab))
                    {
                        go = hybridPrefab.BakingPrefabReference.editorAsset as GameObject;
                    }
#endif
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
#endif
    }

    public struct RuleBlobReferenceElement : IBufferElementData
    {
        public bool Enabled;
        public BlobAssetReference<RuleBlob> Value;
    }
    
    public struct WeightedEntityElement : IBufferElementData
    {
        public Entity Value;
    }
    
    public struct RefreshPositionElement : IBufferElementData
    {
        public int2 Value;
    }
    
    public struct RuleBlob
    {
        public BlobArray<RuleCell> Cells;
        public int CellsToCheckCount;

        public BlobArray<int> EntitiesWeights;
        public BlobArray<int> EntitiesPointers;
        public int EntitiesWeightSum;
        
        public BlobArray<int> SpritesWeights;
        public BlobArray<SpriteMesh> SpriteMeshes;
        public int SpritesWeightSum;

        public float Chance;
        public RuleTransform RuleTransform;
        public ResultTransform ResultTransform;

        public bool TryGetEntity(ref Random random, in DynamicBuffer<WeightedEntityElement> entityBuffer, out Entity entity)
        {
            if (EntitiesPointers.Length > 0)
            {
                var variant = RandomUtils.NextVariant(ref random, ref EntitiesWeights, EntitiesWeightSum);
                entity = entityBuffer[EntitiesPointers[variant]].Value;
                return true;
            }
            entity = default;
            return false;
        }
        
        public bool TryGetSpriteMesh(ref Random random, out SpriteMesh spriteMesh)
        {
            if (SpriteMeshes.Length > 0)
            {
                var variant = RandomUtils.NextVariant(ref random, ref SpritesWeights, SpritesWeightSum);
                spriteMesh = SpriteMeshes[variant];
                return true;
            }
            spriteMesh = default;
            return false;
        }
    }

    public struct TilemapData : IComponentData
    {
        public Hash128 IntGridHash;
        public Orientation Orientation;
        
        // Store data locally to simplify lookups
        public GridData GridData;
        
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        
        public Swizzle Swizzle => GridData.CellSwizzle;
    }

    public struct RuleCell
    {
        public int2 Offset;
        public int IntGridValue;
    }
}
