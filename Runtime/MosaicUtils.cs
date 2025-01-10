using System;
using Game;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace KrasCore.Mosaic
{
    public static class MosaicUtils
    {
        public static float3 TranslateAndScale(this LocalTransform transform, float3 point)
        {
            return transform.Position + point * transform.Scale;
        }
        
        public static void GetSpriteMeshTranslation(in SpriteMesh spriteMesh, in bool2 flip, out float2 translation)
        {
            var pivot = spriteMesh.NormalizedPivot;
            
            pivot.x = flip.x ? 1.0f - pivot.x : pivot.x;
            pivot.y = flip.y ? 1.0f - pivot.y : pivot.y;
                
            // Offset by from center (0.5f - x/y)
            translation = new float2((0.5f - pivot.x) * spriteMesh.RectScale.x, (0.5f - pivot.y) * spriteMesh.RectScale.y);
        }
        
        public static float3 ApplyOrientation(float2 pos, Orientation orientation)
        {
            return orientation == Orientation.XZ ? new float3(pos.x, 0f, pos.y) : new float3(pos.x, pos.y, 0f);
        }
        
        public static float3 ApplyOrientation(float3 pos, Orientation orientation)
        {
            return orientation == Orientation.XZ ? new float3(pos.x, pos.z, pos.y) : new float3(pos.x, pos.y, pos.z);
        }
        
        public static float3 ToWorldSpace(in int2 pos, in float3 gridCellSize, in Swizzle swizzle)
        {
            return ApplySwizzle(pos, swizzle) * ApplySwizzle(gridCellSize, swizzle);
        }
        
        public static float3 ApplySwizzle(in int2 pos, in Swizzle swizzle)
        {
            return swizzle switch
            {
                Swizzle.XYZ => new float3(pos.x, pos.y, 0f),
                Swizzle.XZY => new float3(pos.x, 0f, pos.y),
                _ => float3.zero
            };
        }
        
        public static float3 ApplySwizzle(in float3 pos, in Swizzle swizzle)
        {
            return swizzle switch
            {
                Swizzle.XYZ => pos.xyz,
                Swizzle.XZY => pos.xzy,
                _ => float3.zero
            };
        }
    }

    public struct SpriteMesh : IEquatable<SpriteMesh>
    {
        public float2 NormalizedPivot;
        public float2 RectScale;
        public float2 MinUv;
        public float2 MaxUv;

        public SpriteMesh(Sprite sprite)
        {
            if (sprite != null)
            {
                var uvAtlas = RendererUtility.GetUvAtlas(sprite);
                NormalizedPivot = RendererUtility.GetNormalizedPivot(sprite);
                RectScale = RendererUtility.GetRectScale(sprite, uvAtlas);
                RendererUtility.GetUVCorners(sprite, out MinUv, out MaxUv);
            }
            else
            {
                NormalizedPivot = new float2(0.5f, 0.5f);
                RectScale = new float2(1f, 1f);
                MinUv = float2.zero;
                MaxUv = new float2(1f, 1f);
            }
        }

        public bool Equals(SpriteMesh other)
        {
            return NormalizedPivot.Equals(other.NormalizedPivot) && RectScale.Equals(other.RectScale) && MinUv.Equals(other.MinUv) && MaxUv.Equals(other.MaxUv);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(NormalizedPivot, RectScale, MinUv, MaxUv);
        }
    }
    
    public enum Orientation
    {
        XY,
        XZ
    }
    
    public enum Swizzle
    {
        XYZ,
        XZY
    }
}