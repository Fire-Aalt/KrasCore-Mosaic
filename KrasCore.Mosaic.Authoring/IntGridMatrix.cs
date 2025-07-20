using System;
using KrasCore.Mosaic.Data;

namespace KrasCore.Mosaic.Authoring
{
    [Serializable]
    public class IntGridMatrix
    {
        public IntGridValue[] matrix;
        public IntGridDefinition intGrid;

        public IntGridMatrix(int size)
        {
            matrix = new IntGridValue[size * size];
        }
    }
}