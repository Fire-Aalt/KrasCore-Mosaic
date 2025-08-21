using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapRendererData : IComponentData
    {
        public Hash128 MeshHash;
        public Entity GridEntity;
        
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        
        public float3 CellSize;
        public Orientation Orientation;
        public Swizzle Swizzle;
    }
}