using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapCell : IComponentData
    {
        public int2 Cell;
    }
}