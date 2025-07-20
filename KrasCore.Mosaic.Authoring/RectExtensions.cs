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
        
        /// <summary>
        /// Returns a new Rect based on <paramref name="inner"/> that is guaranteed to lie fully
        /// within <paramref name="outer"/>. If <paramref name="inner"/> is larger than <paramref name="outer"/>,
        /// its width/height will be reduced; if it's partially or fully outside, it will be shifted inside.
        /// </summary>
        public static Rect EncapsulateWithin(this Rect inner, Rect outer)
        {
            // Start with original
            Rect result = inner;

            // 2) Clamp position so it never leaves the outer bounds
            //    We clamp the min (x/y) between outer.xMin and (outer.xMax - result.width),
            //    so the max edge never goes beyond outer.xMax
            result.xMin = Mathf.Clamp(result.xMin, outer.xMin, outer.xMax);
            result.yMin = Mathf.Clamp(result.yMin, outer.yMin, outer.yMax);
             
            result.xMax = Mathf.Clamp(result.xMax, outer.xMin, outer.xMax);
            result.yMax = Mathf.Clamp(result.yMax, outer.yMin, outer.yMax);
            
            return result;
        }
    }
}