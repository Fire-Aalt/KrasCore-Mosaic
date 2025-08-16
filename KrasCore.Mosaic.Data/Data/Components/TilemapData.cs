using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapData : IComponentData, IEnableableComponent
    {
        public Hash128 IntGridHash;
        public FixedString128Bytes DebugName;
        
        public bool DualGrid;
    }
}