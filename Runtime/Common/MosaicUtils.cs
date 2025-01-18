using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public static class MosaicUtils
    {
        public static int GetCellsToCheckBucketsCount(RuleTransform transform)
        {
            return transform switch
            {
                RuleTransform.MirrorX => 2,
                RuleTransform.MirrorY => 2,
                RuleTransform.MirrorXY => 4,
                RuleTransform.Rotated => 4,
                _ => 1
            };
        }
        
        /// <summary>
        /// Hash function for seeded random seeds, unique for each position
        /// </summary>
        public static uint Hash(uint seed, in int2 pos)
        {
            // Combine position into a single 64-bit value for better hash distribution
            ulong combined = ((ulong)pos.x << 32) | (uint)pos.y;
            uint hash = seed;

            hash ^= (uint)(combined & 0xFFFFFFFF); 
            hash *= 0x85EBCA6B;
            hash ^= hash >> 13;
            hash ^= (uint)(combined >> 32);       
            hash *= 0xC2B2AE35;
            hash ^= hash >> 16;

            return hash + 1;
        }
        
        public static void GetSpriteMeshTranslation(in SpriteMesh spriteMesh, out float2 translation)
        {
            var pivot = spriteMesh.NormalizedPivot;
            
            pivot.x = spriteMesh.Flip.x ? 1.0f - pivot.x : pivot.x;
            pivot.y = spriteMesh.Flip.y ? 1.0f - pivot.y : pivot.y;
                
            // Offset by from center (0.5f - x/y)
            translation = new float2((0.5f - pivot.x) * spriteMesh.RectScale.x, (0.5f - pivot.y) * spriteMesh.RectScale.y);
        }
        
        public static float3 ApplyOrientation(float2 pos, Orientation orientation)
        {
            return orientation == Orientation.XZ ? new float3(pos.x, 0f, pos.y) : new float3(pos.x, pos.y, 0f);
        }
        
        public static float3 ApplyOrientation(float3 pos, Orientation orientation)
        {
            return orientation switch
            {
                Orientation.XY => new float3(pos.x, pos.y, -pos.z),
                Orientation.XZ => new float3(pos.x, pos.z, pos.y),
                _ => float3.zero
            };
        }
        
        public static float3 ToWorldSpace(in float2 pos, in float3 gridCellSize, in Swizzle swizzle)
        {
            return ApplySwizzle(pos, swizzle) * ApplySwizzle(gridCellSize, swizzle);
        }
        
        public static float3 ApplySwizzle(in float2 pos, in Swizzle swizzle)
        {
            return swizzle switch
            {
                Swizzle.XYZ => new float3(pos.x, pos.y, 0f),
                Swizzle.XZY => new float3(pos.x, 0f, pos.y),
                _ => float3.zero
            };
        }
        
        public static float3 ApplySwizzle(in float3 pos, in Swizzle swizzle)
        {
            return swizzle switch
            {
                Swizzle.XYZ => pos.xyz,
                Swizzle.XZY => pos.xzy,
                _ => float3.zero
            };
        }
    }

    public enum Orientation
    {
        XY,
        XZ
    }
    
    public enum Swizzle
    {
        XYZ,
        XZY
    }
}