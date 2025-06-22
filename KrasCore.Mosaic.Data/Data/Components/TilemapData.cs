using Unity.Entities;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapData : IComponentData, IEnableableComponent
    {
        public Hash128 IntGridHash;
        public Orientation Orientation;
        
        // Store data locally to simplify lookups
        public Entity GridEntity;
        public GridData GridData;
        
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        
        public Swizzle Swizzle => GridData.CellSwizzle;
    }
}