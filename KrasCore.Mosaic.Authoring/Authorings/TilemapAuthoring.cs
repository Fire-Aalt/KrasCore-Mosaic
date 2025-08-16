using System;
using UnityEngine;
using Unity.Entities;

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
                
                var materialTexture = BakerUtils.AddIntGridLayerData(this, entity, authoring.intGrid, null);
                BakerUtils.AddRenderingData(this, entity, authoring.intGrid.Hash, authoring.renderingData, gridAuthoring, materialTexture);
            }
        }
    }
}
