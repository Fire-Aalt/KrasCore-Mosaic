using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public struct DeferredCommand
    {
        public Entity SrcEntity;
        public int2 Position;
        public int IntGridHash;
    }
    
    public struct DeferredCommandComparer : IComparer<DeferredCommand>
    {
        public int Compare(DeferredCommand x, DeferredCommand y)
        {
            return x.SrcEntity.Index.CompareTo(y.SrcEntity.Index);
        }
    }
}