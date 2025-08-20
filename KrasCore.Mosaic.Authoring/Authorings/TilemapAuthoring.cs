using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Authoring
{
    public class TilemapAuthoring : MonoBehaviour
    {
        public IntGridDefinition intGrid;
        public RenderingData renderingData;

        public class Baker : Baker<TilemapAuthoring>
        {
            public override void Bake(TilemapAuthoring authoring)
            {
                var gridAuthoring = GetComponentInParent<GridAuthoring>();
                if (gridAuthoring == null)
                {
                    throw new Exception("GridAuthoring not found");
                }
                
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                var tilePivot = float2.zero;
                var tileSize = float2.zero;
                var materialTexture = BakerUtils.AddIntGridLayerData(this, entity, authoring.intGrid,
                    null, false, ref tilePivot, ref tileSize);
                BakerUtils.AddRenderingData(this, entity, authoring.intGrid.Hash, authoring.renderingData, gridAuthoring, materialTexture);
            }
        }
    }
}
