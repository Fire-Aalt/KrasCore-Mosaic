using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
    public struct RefreshPositionElement : IBufferElementData
    {
        public int2 Value;
    }
}