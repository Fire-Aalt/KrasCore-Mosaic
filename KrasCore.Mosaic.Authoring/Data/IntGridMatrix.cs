using System;
using KrasCore.Mosaic.Data;
using UnityEngine;

namespace KrasCore.Mosaic.Authoring
{
    [Serializable]
    public class IntGridMatrix
    {
        public IntGridValue[] singleGridMatrix;
        public IntGridValue[] dualGridMatrix;
        
        public IntGridValue[] GetCurrentMatrix(IntGridDefinition intGrid) => intGrid.useDualGrid ? dualGridMatrix : singleGridMatrix;
        public int GetCurrentSize(IntGridDefinition intGrid) => intGrid.useDualGrid ? (int)Mathf.Sqrt(dualGridMatrix.Length) : (int)Mathf.Sqrt(singleGridMatrix.Length);
        
        public IntGridMatrix(int singleGridSize)
        {
            var dualGridSize = singleGridSize + 1;
            
            singleGridMatrix = new IntGridValue[singleGridSize * singleGridSize];
            dualGridMatrix = new IntGridValue[dualGridSize * dualGridSize];
        }
    }
}