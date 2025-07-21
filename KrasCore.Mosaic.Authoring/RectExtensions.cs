using UnityEngine;

namespace KrasCore.Mosaic.Authoring
{
    public static class RectExtensions
    {
        public static void Subdivide(this Rect rect, out Rect leftBottom, out Rect rightBottom, out Rect leftTop, out Rect rightTop)
        {
            var size = rect.size / 2f;
            
            leftBottom = new Rect(rect.min, size);
            rightBottom = new Rect(rect.min + new Vector2(size.x, 0), size);
            leftTop = new Rect(rect.min + new Vector2(0, size.y), size);
            rightTop = new Rect(rect.min + size, size);
        }

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