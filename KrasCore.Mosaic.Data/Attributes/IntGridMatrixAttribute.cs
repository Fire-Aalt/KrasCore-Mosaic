using UnityEngine;

namespace KrasCore.Mosaic
{
    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class IntGridMatrixAttribute : PropertyAttribute
    {
        public float Padding = 0.01f;
        public bool IsReadonly;
        public string OnBeforeDrawCellMethod;
        public string MatrixRectMethod;

        public IntGridMatrixAttribute(string onBeforeDrawCellMethod = "")
        {
            OnBeforeDrawCellMethod = onBeforeDrawCellMethod;
        }
    }
}