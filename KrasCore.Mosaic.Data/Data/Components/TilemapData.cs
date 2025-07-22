using Unity.Collections;
using Unity.Entities;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapData : IComponentData, IEnableableComponent
    {
        public Hash128 IntGridHash;
        public FixedString128Bytes DebugName;
        
        // Store data locally to simplify lookups
        public Entity GridEntity;
        public GridData GridData;
        public bool DualGrid;
        
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        
        public Orientation Orientation;
        public Swizzle Swizzle => GridData.CellSwizzle;
    }
}