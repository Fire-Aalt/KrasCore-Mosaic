using System;
using KrasCore.Mosaic.Data;

namespace KrasCore.Mosaic.Authoring
{
    [Serializable]
    public class IntGridMatrix
    {
        public IntGridValue[] singleGridMatrix;
        public IntGridValue[] dualGridMatrix;
        public IntGridDefinition intGrid;

        private int _singleGridSingleGridSize;
        private int _dualGridSize;
        
        public IntGridValue[] GetCurrentMatrix() => intGrid.useDualGrid ? dualGridMatrix : singleGridMatrix;
        public int GetCurrentSize() => intGrid.useDualGrid ? _dualGridSize : _singleGridSingleGridSize;
        
        public IntGridMatrix(int singleGridSize)
        {
            _singleGridSingleGridSize = singleGridSize;
            _dualGridSize = singleGridSize + 1;
            
            singleGridMatrix = new IntGridValue[_singleGridSingleGridSize * _singleGridSingleGridSize];
            dualGridMatrix = new IntGridValue[_dualGridSize * _dualGridSize];
        }
    }
}