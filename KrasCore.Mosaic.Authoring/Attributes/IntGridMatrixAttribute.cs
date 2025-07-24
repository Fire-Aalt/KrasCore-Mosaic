using System;
using UnityEngine;

namespace KrasCore.Mosaic.Authoring
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
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