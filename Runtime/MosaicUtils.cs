using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public static class MosaicUtils
    {
        public static float3 ApplyOrientation(float2 pos, Orientation orientation)
        {
            return orientation == Orientation.XZ ? new float3(pos.x, 0f, pos.y) : new float3(pos.x, pos.y, 0f);
        }
        
        public static float3 ToWorldSpace(in int2 pos, float3 gridCellSize, in Swizzle swizzle)
        {
            return ApplySwizzle(pos, swizzle) * gridCellSize;
        }
        
        public static float3 ApplySwizzle(in int2 pos, in Swizzle swizzle)
        {
            return swizzle switch
            {
                Swizzle.XYZ => new float3(pos.x, pos.y, 0f),
                Swizzle.XZY => new float3(pos.x, 0f, pos.y),
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