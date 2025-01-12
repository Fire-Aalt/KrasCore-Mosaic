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
            root.ResultType = rule.ResultType;
            
            var usedCellCount = 0;
            foreach (var intGridValue in rule.RuleMatrix)
            {
                usedCellCount += intGridValue == 0 ? 0 : 1;
            }

            root.RuleTransform = rule.ruleTransform;
            
            var combinedMirroredCellCount = usedCellCount * MosaicUtils.GetCellsToCheckBucketsCount(rule.ruleTransform);
            var cells = builder.Allocate(ref root.Cells, combinedMirroredCellCount);

            var cnt = 0;
            AddMirrorPattern(rule, cells, refreshPositions, ref cnt, default);
            root.CellsToCheckCount = cnt;
            
            if (rule.ruleTransform.IsMirroredX()) AddMirrorPattern(rule, cells, refreshPositions, ref cnt, new bool2(true, false));
            if (rule.ruleTransform.IsMirroredY()) AddMirrorPattern(rule, cells, refreshPositions, ref cnt, new bool2(false, true));
            if (rule.ruleTransform == RuleTransform.MirrorXY) AddMirrorPattern(rule, cells, refreshPositions, ref cnt, new bool2(true, true));
            if (rule.ruleTransform == RuleTransform.Rotated) AddRotatedPattern(rule, cells, refreshPositions, ref cnt);

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

        private static void AddMirrorPattern(RuleGroup.Rule rule, BlobBuilderArray<RuleCell> cells,
            NativeHashSet<int2> refreshPositions, ref int cnt, bool2 mirror)
        {
                var s = "";
            for (var index = 0; index < rule.RuleMatrix.Length; index++)
            {
                var intGridValue = rule.RuleMatrix[index];
                if (intGridValue == 0) continue;
                ref var cell = ref cells[cnt];

                var pos = RuleGroup.Rule.GetOffsetFromCenterMirrored(index, mirror);
                refreshPositions.Add(pos);
                s += "Pos: " + pos.ToString() + " val: " + intGridValue + " | ";
                
                cell = new RuleCell
                {
                    IntGridValue = intGridValue,
                    Offset = pos
                };
                cnt++;
            }
            Debug.Log(s + " at: " + rule.BoundIntGrid.name + " mirror: " + mirror);
        }
        
        private static void AddRotatedPattern(RuleGroup.Rule rule, BlobBuilderArray<RuleCell> cells,
            NativeHashSet<int2> refreshPositions, ref int cnt)
        {
            for (int rotation = 1; rotation < 4; rotation++)
            {
                var s = "";
                for (var index = 0; index < rule.RuleMatrix.Length; index++)
                {
                    var intGridValue = rule.RuleMatrix[index];
                    if (intGridValue == 0) continue;
                    ref var cell = ref cells[cnt];

                    var pos = RuleGroup.Rule.GetOffsetFromCenterRotated(index, rotation);
                    s += "Pos: " + pos.ToString() + " val: " + intGridValue + " | ";
                    refreshPositions.Add(pos);
                    
                    cell = new RuleCell
                    {
                        IntGridValue = intGridValue,
                        Offset = pos
                    };
                    cnt++;
                }
                Debug.Log(s + " at: " + rule.BoundIntGrid.name + " rot: " + rotation);
            }
        }
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
        public int2 Position;
    }
    
    public struct RuleBlob
    {
        public BlobArray<RuleCell> Cells;
        public int CellsToCheckCount;

        public BlobArray<WeightedEntity> WeightedEntities;
        public BlobArray<WeightedSprite> WeightedSprites;

        public RuleResultType ResultType;
        public float Chance;
        public RuleTransform RuleTransform;
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
