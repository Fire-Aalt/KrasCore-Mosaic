using Unity.Collections;
using Unity.Entities;

namespace KrasCore.Mosaic.Data
{
    public struct IntGridData : IComponentData, IEnableableComponent
    {
        public Hash128 Hash;
        public FixedString128Bytes DebugName;
        
        public bool DualGrid;
    }
}