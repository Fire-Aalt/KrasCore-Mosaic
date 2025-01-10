using System;
using Game;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public class TilemapAuthoring : MonoBehaviour
    {
        [SerializeField] private IntGrid _intGrid;
        [SerializeField] private Material _material;
        [SerializeField] private Orientation _orientation;

        public IntGrid IntGrid => _intGrid;
        
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
                    foreach (var rule in group.rules)
                    {
                        var blob = Create(rule, entityCount, refreshPositions);
                        AddBlobAsset(ref blob, out _);

                        ruleBlobBuffer.Add(new RuleBlobReferenceElement
                        {
                            Enabled = rule.enabled.HasFlag(RuleGroup.Enabled.Enabled),
                            Value = blob
                        });
                        
                        for (var i = 0; i < rule.TileEntities.Count; i++)
                        {
                            var go = rule.TileEntities[i].entityResult;
                            
                            var target = go;
                            if (go.TryGetComponent(out HybridPrefab hybridPrefab))
                            {
                                target = hybridPrefab.BakingPrefabReference.editorAsset as GameObject;
                            }
                            
                            weightedEntityBuffer.Add(new WeightedEntityElement
                            {
                                Value = GetEntity(target, TransformUsageFlags.None)
                            });
                        }

                        for (int i = 0; i < rule.TileSprites.Count; i++)
                        {
                            var spriteTexture = rule.TileSprites[i].spriteResult.texture;

                            if (refTexture == null)
                                refTexture = spriteTexture;
                            else if (refTexture != spriteTexture)
                            {
                                throw new Exception("Different textures in one tilemap. This is not supported yet");
                            }
                        }

                        entityCount += rule.TileEntities.Count;
                    }
                }
                
                AddComponent(entity, new TilemapData
                {
                    IntGridReference = authoring._intGrid,
                    Orientation = authoring._orientation,
                    Material = refTexture != null ? MaterialAssetsStorage.GetOrCreateMaterialAsset(authoring._material, refTexture) : null
                });

                var refreshPositionsBuffer = AddBuffer<RefreshPositionElement>(entity);
                refreshPositionsBuffer.AddRange(refreshPositions.ToNativeArray(Allocator.Temp).Reinterpret<RefreshPositionElement>());
            }
        }
        
        private static BlobAssetReference<RuleBlob> Create(RuleGroup.Rule rule, int entityCount, NativeHashSet<int2> refreshPositions)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<RuleBlob>();

            root.Chance = rule.ruleChance;
            root.Mirror = rule.mirror;
            root.ResultType = rule.ResultType;
            
            var usedCellCount = 0;
            foreach (var intGridValue in rule.RuleMatrix)
            {
                usedCellCount += intGridValue == 0 ? 0 : 1;
            }

            var xMirror = rule.mirror.HasFlag(RuleGroup.Mirror.MirrorX) ? 2 : 1;
            var yMirror = rule.mirror.HasFlag(RuleGroup.Mirror.MirrorY) ? 2 : 1;
            var combinedMirroredCellCount = usedCellCount * xMirror * yMirror;
            var cells = builder.Allocate(ref root.Cells, usedCellCount);

            var cnt = 0;
            for (var index = 0; index < rule.RuleMatrix.Length; index++)
            {
                var intGridValue = rule.RuleMatrix[index];
                if (intGridValue == 0) continue;
                ref var cell = ref cells[cnt];

                var pos = RuleGroup.Rule.GetOffsetFromCenter(index);
                refreshPositions.Add(pos);
                
                cell = new RuleCell
                {
                    IntGridValue = intGridValue,
                    Offset = pos
                };
                cnt++;
            }

            var weightedEntities = builder.Allocate(ref root.WeightedEntities, rule.TileEntities.Count);

            for (int i = 0; i < weightedEntities.Length; i++)
            {
                ref var weightedEntity = ref weightedEntities[i];

                weightedEntity.Weight = rule.TileEntities[i].weight;
                weightedEntity.EntityBufferIndex = entityCount + i;
            }
            
            var weightedSprites = builder.Allocate(ref root.WeightedSprites, rule.TileSprites.Count);
            for (int i = 0; i < weightedSprites.Length; i++)
            {
                ref var weightedEntity = ref weightedSprites[i];

                weightedEntity.Weight = rule.TileSprites[i].weight;
                weightedEntity.SpriteMesh = new SpriteMesh(rule.TileSprites[i].spriteResult);
            }

            return builder.CreateBlobAssetReference<RuleBlob>(Allocator.Persistent);
        }
    }


    

    public struct RuleBlobReferenceElement : IBufferElementData
    {
        public bool Enabled;
        public RuleResultType ResultType;
        public BlobAssetReference<RuleBlob> Value;
    }
    
    public struct WeightedEntityElement : IBufferElementData
    {
        public Entity Value;
    }
    
    public struct RefreshPositionElement : IBufferElementData
    {
        public int2 Position;
    }
    
    public struct RuleBlob
    {
        public BlobArray<RuleCell> Cells;

        public BlobArray<WeightedEntity> WeightedEntities;
        public BlobArray<WeightedSprite> WeightedSprites;

        public RuleResultType ResultType;
        public float Chance;
        public RuleGroup.Mirror Mirror;
    }
    
    public struct WeightedEntity
    {
        public int Weight;
        public int EntityBufferIndex;
    }
    
    public struct WeightedSprite
    {
        public int Weight;
        public SpriteMesh SpriteMesh;
    }

    public struct TilemapData : IComponentData
    {
        public UnityObjectRef<IntGrid> IntGridReference;
        public Orientation Orientation;
        
        public GridData GridData;

        public UnityObjectRef<Material> Material;
        public Swizzle Swizzle => GridData.CellSwizzle;
    }

    // public struct WeightedSprite
    // {
    //     public int Weight;
    //     public SpriteProperties Properties;
    // }
    

    public struct RuleCell
    {
        public int2 Offset;
        public int IntGridValue;
    }
}
