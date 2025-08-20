using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapTerrainData : IComponentData
    {
        public FixedList512Bytes<Hash128> LayerHashes;
        public float2 TileSize;
    }
}