using KrasCore.Mosaic.Data;
using Unity.Entities;
using UnityEngine;

namespace KrasCore.Mosaic.Authoring
{
    /// <summary>
    /// Adds a TilemapCell component that is initialized when the entity is placed by Mosaic.
    /// The value of the TilemapCell corresponds to the cell on the spawning IntGrid.
    /// </summary>
    public class TilemapCellAuthoring : MonoBehaviour
    {
        private class Baker : Baker<TilemapCellAuthoring>
        {
            public override void Bake(TilemapCellAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<TilemapCell>(entity);
            }
        }
    }
}