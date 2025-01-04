using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace Mosaic.Runtime
{
    public class IntGridAuthoring : MonoBehaviour
    {
        [SerializeField] private IntGrid _intGrid;
        
        public class Baker : Baker<IntGridAuthoring>
        {
            public override void Bake(IntGridAuthoring authoring)
            {
            
            }
        }
    }
//
    public struct RuleBlob
    {
        public BlobArray<BlobArray<RuleCell>> Cells;

        //public BlobArray<Sprite>
        
        public float Chance;
        public RuleGroup.Mirror Mirror;
    }

    public struct RuleCell
    {
        public int2 Offset;
        public int IntGridValue;
    }
}
