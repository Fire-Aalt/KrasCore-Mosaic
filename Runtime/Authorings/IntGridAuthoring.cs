using Game;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public class IntGridAuthoring : MonoBehaviour
    {
        [SerializeField] private IntGrid _intGrid;
        
        public class Baker : Baker<IntGridAuthoring>
        {
            public override void Bake(IntGridAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var ruleBlobBuffer = AddBuffer<RuleBlobReferenceElement>(entity);
                var weightedEntityBuffer = AddBuffer<WeightedEntityElement>(entity);

                AddComponent(entity, new IntGridReference { Value = authoring._intGrid });

                var refreshPositions = new NativeHashSet<int2>(64, Allocator.Temp);
                
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

                        entityCount += rule.TileEntities.Count;
                    }
                }

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

            return builder.CreateBlobAssetReference<RuleBlob>(Allocator.Persistent);
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

        public BlobArray<WeightedEntity> WeightedEntities;
        
        public float Chance;
        public RuleGroup.Mirror Mirror;
    }
    
    public struct WeightedEntity
    {
        public int Weight;
        public int EntityBufferIndex;
    }

    public struct IntGridReference : IComponentData
    {
        public UnityObjectRef<IntGrid> Value;
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
