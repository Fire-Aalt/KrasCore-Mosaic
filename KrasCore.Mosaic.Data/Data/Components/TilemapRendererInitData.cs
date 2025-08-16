using Unity.Entities;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapRendererInitData : IComponentData
    {
        public Hash128 MeshHash;
        public Entity GridEntity;
        
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
    }
}