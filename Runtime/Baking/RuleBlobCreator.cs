using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public struct RuleBlobCreator
    {
        public static BlobAssetReference<RuleBlob> Create(RuleGroup.Rule rule, int entityCount, NativeHashSet<int2> refreshPositions)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<RuleBlob>();

            root.Chance = rule.ruleChance;
            root.RuleTransform = rule.ruleTransform;
            root.ResultTransform = rule.resultTransform;
            root.RandomBehavior = root.RandomBehavior;
            
            AddPatterns(ref root, rule, refreshPositions, builder);
            AddResults(ref root, rule, entityCount, builder);

            return builder.CreateBlobAssetReference<RuleBlob>(Allocator.Persistent);
        }

        private static void AddPatterns(ref RuleBlob root, RuleGroup.Rule rule, NativeHashSet<int2> refreshPositions, BlobBuilder builder)
        {
            var usedCellCount = 0;
            foreach (var intGridValue in rule.ruleMatrix)
            {
                usedCellCount += intGridValue == 0 ? 0 : 1;
            }
            
            var combinedMirroredCellCount = usedCellCount * MosaicUtils.GetCellsToCheckBucketsCount(rule.ruleTransform);
            var cells = builder.Allocate(ref root.Cells, combinedMirroredCellCount);

            var currentCell = 0;
            AddMirrorPattern(rule, cells, refreshPositions, ref currentCell, default);
            root.CellsToCheckCount = currentCell;
            
            if (rule.ruleTransform.IsMirroredX()) AddMirrorPattern(rule, cells, refreshPositions, ref currentCell, new bool2(true, false));
            if (rule.ruleTransform.IsMirroredY()) AddMirrorPattern(rule, cells, refreshPositions, ref currentCell, new bool2(false, true));
            if (rule.ruleTransform == RuleTransform.MirrorXY) AddMirrorPattern(rule, cells, refreshPositions, ref currentCell, new bool2(true, true));
            if (rule.ruleTransform == RuleTransform.Rotated) AddRotatedPattern(rule, cells, refreshPositions, ref currentCell);
        }

        private static void AddResults(ref RuleBlob root, RuleGroup.Rule rule, int entityCount, BlobBuilder builder)
        {
            if (rule.TileSprites != null)
            {
                var spritesWeights = builder.Allocate(ref root.SpritesWeights, rule.TileSprites.Count);
                var spriteMeshes = builder.Allocate(ref root.SpriteMeshes, rule.TileSprites.Count);
                
                var sum = 0;
                for (int i = 0; i < spriteMeshes.Length; i++)
                {
                    spritesWeights[i] = rule.TileSprites[i].weight;
                    spriteMeshes[i] = new SpriteMesh(rule.TileSprites[i].result);
                    sum += spritesWeights[i];
                }
                root.SpritesWeightSum = sum;
            }
            
            if (rule.TileEntities != null)
            {
                var entitiesWeights = builder.Allocate(ref root.EntitiesWeights, rule.TileEntities.Count);
                var entitiesPointers = builder.Allocate(ref root.EntitiesPointers, rule.TileEntities.Count);

                var sum = 0;
                for (int i = 0; i < entitiesPointers.Length; i++)
                {
                    entitiesWeights[i] = rule.TileEntities[i].weight;
                    entitiesPointers[i] = entityCount + i;
                    sum += entitiesWeights[i];
                }
                root.EntitiesWeightSum = sum;
            }
        }
        
        private static void AddMirrorPattern(RuleGroup.Rule rule, BlobBuilderArray<RuleCell> cells,
            NativeHashSet<int2> refreshPositions, ref int cnt, bool2 mirror)
        {
            for (var index = 0; index < rule.ruleMatrix.Length; index++)
            {
                var intGridValue = rule.ruleMatrix[index];
                if (intGridValue == 0) continue;
                ref var cell = ref cells[cnt];
                
                var pos = RuleGroup.Rule.GetOffsetFromCenterMirrored(index, mirror);
                refreshPositions.Add(pos);
                
                cell = new RuleCell
                {
                    IntGridValue = intGridValue,
                    Offset = pos
                };
                cnt++;
            }
        }
        
        private static void AddRotatedPattern(RuleGroup.Rule rule, BlobBuilderArray<RuleCell> cells,
            NativeHashSet<int2> refreshPositions, ref int cnt)
        {
            for (int rotation = 1; rotation < 4; rotation++)
            {
                for (var index = 0; index < rule.ruleMatrix.Length; index++)
                {
                    var intGridValue = rule.ruleMatrix[index];
                    if (intGridValue == 0) continue;
                    ref var cell = ref cells[cnt];

                    var pos = RuleGroup.Rule.GetOffsetFromCenterRotated(index, rotation);
                    refreshPositions.Add(pos);
                    
                    cell = new RuleCell
                    {
                        IntGridValue = intGridValue,
                        Offset = pos
                    };
                    cnt++;
                }
            }
        }
    }
}