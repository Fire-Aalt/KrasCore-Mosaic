using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapRendererData : IComponentData
    {
        public float3 CellSize;
        public Orientation Orientation;
        public Swizzle Swizzle;
    }
}