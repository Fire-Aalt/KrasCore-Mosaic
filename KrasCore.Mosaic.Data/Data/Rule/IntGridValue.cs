using System;
using System.Runtime.InteropServices;

namespace KrasCore.Mosaic.Data
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct IntGridValue
    {
        [FieldOffset(0)] public ulong AllValue;
        
        // Used for single grid setup
        [FieldOffset(0)] public short Solid;
        
        // Used for Dual-Grid setup
        [FieldOffset(0)] public short LeftBottom;
        [FieldOffset(2)] public short RightBottom;
        [FieldOffset(4)] public short LeftTop;
        [FieldOffset(6)] public short RightTop;
        
        public bool IsEmpty => AllValue == 0;
        
        public IntGridValue(short solid)
        {
            this = default;
            Solid = solid;
        }
    }
}