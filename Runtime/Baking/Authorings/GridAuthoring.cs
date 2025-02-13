using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace KrasCore.Mosaic
{
    public class GridAuthoring : MonoBehaviour
    {
        [SerializeField] private float3 _cellSize = 1f;
        [SerializeField] private Swizzle _cellSwizzle = Swizzle.XZY;

        private void OnValidate()
        {
            _cellSize = math.max(0.005f, _cellSize);
        }

        private class GridBaker : Baker<GridAuthoring>
        {
            public override void Bake(GridAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GridData
                {
                    CellSize = authoring._cellSize,
                    CellSwizzle = authoring._cellSwizzle
                });

                var buffer = AddBuffer<GridBakingData>(entity);
                var tilemaps = authoring.GetComponentsInChildren<TilemapAuthoring>();
                foreach (var tilemap in tilemaps)
                {
                    buffer.Add(new GridBakingData { IntGridHash = tilemap.IntGrid.Hash });
                }
            }
        }
    }
    
    [BakingType]
    public struct GridBakingData : IBufferElementData
    {
        public Hash128 IntGridHash;
    }
    
    public struct GridData : IComponentData
    {
        public float3 CellSize;
        public Swizzle CellSwizzle;
    }
}