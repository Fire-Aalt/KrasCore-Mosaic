using System;
using System.Runtime.InteropServices;

namespace KrasCore.Mosaic.Data
{
    [Serializable]
    public struct IntGridValue : IEquatable<IntGridValue>
    {
        public short value;
     
        public IntGridValue(int value)
        {
            this.value = (short)value;
        }
        
        public static implicit operator IntGridValue(int value) => new(value);
        public static implicit operator short(IntGridValue value) => value.value;
        
        public static bool operator ==(IntGridValue left, IntGridValue right) => left.Equals(right);
        public static bool operator !=(IntGridValue left, IntGridValue right) => !(left == right);

        public bool Equals(IntGridValue other)
        {
            return value == other.value;
        }

        public override bool Equals(object obj)
        {
            return obj is IntGridValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
    }
}