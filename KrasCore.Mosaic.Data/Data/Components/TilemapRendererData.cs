using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Hash128 = Unity.Entities.Hash128;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapRendererData : IComponentData
    {
        public Hash128 MeshHash;
        public Entity GridEntity;
        
        public RenderingLayerMask RenderingLayerMask;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        
        public float3 CellSize;
        public Orientation Orientation;
        public Swizzle Swizzle;
    }
}