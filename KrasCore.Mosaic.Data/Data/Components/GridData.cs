using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
    public struct GridData : IComponentData
    {
        public float3 CellSize;
        public Swizzle CellSwizzle;
    }
}