using UnityEngine;

namespace KrasCore.Mosaic.Editor
{
    public static class RectExtensions
    {
        public static Rect EncapsulateWithin(this Rect inner, Rect outer)
        {
            var result = inner;

            result.xMin = Mathf.Clamp(result.xMin, outer.xMin, outer.xMax);
            result.yMin = Mathf.Clamp(result.yMin, outer.yMin, outer.yMax);
             
            result.xMax = Mathf.Clamp(result.xMax, outer.xMin, outer.xMax);
            result.yMax = Mathf.Clamp(result.yMax, outer.yMin, outer.yMax);
            
            return result;
        }
    }
}