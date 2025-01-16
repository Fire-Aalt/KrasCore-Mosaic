using Sirenix.OdinInspector;
using UnityEngine;

namespace KrasCore.Mosaic
{
    [DontApplyToListElements] 
    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class MatrixAttribute : PropertyAttribute
    {
        public float Padding = 0.01f;
        public string DrawCellMethod;
        public string MatrixRectMethod;

        public MatrixAttribute(string drawCellMethod)
        {
            DrawCellMethod = drawCellMethod;
        }
    }
}