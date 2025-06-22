using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapRendererData
    {
        public Swizzle Swizzle;
        public float3 GridCellSize;
        public Orientation Orientation;

        public TilemapRendererData(in TilemapData data)
        {
            GridCellSize = data.GridData.CellSize;
            Orientation = data.Orientation;
            Swizzle = data.GridData.CellSwizzle;
        }
    }
}