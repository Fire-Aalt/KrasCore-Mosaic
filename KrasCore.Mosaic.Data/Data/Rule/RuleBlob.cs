using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
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
    
    public struct RuleCell
    {
        public int2 Offset;
        public int IntGridValue;
    }
}