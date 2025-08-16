using Unity.Collections;
using Unity.Entities;

namespace KrasCore.Mosaic.Data
{
    public struct TerrainData : IComponentData
    {
        public FixedList512Bytes<Hash128> LayerHashes;
    }
}