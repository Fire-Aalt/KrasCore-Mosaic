using System;
using UnityEngine;

namespace KrasCore.Mosaic.Data
{
    [Obsolete]
    public enum RuleTransform
    {
        [Tooltip("No transform operation")]
        None,
        
        [Tooltip("X mirror. Enable this to also check for match when mirrored horizontally")]
        MirrorX,

        [Tooltip("Y mirror. Enable this to also check for match when mirrored vertically")]
        MirrorY,
        
        [Tooltip("XY mirror. Enable this to also check for match when mirrored horizontally or vertically")]
        MirrorXY,
        
        [Tooltip("Rotate the pattern by 90 degrees 4 times to check for matches")]
        Rotated,
        
        Migrated
    }
    
    [Flags]
    public enum Transformation
    {
        [Tooltip("X mirror. Enable this to mark the rule as able to mirror the result horizontally")]
        MirrorX = 1,

        [Tooltip("Y mirror. Enable this to mark the rule as able to mirror the result vertically")]
        MirrorY = 2,
        
        [Tooltip("Enable this to mark the rule as able to rotate the result by 90 degrees up to 4 times")]
        Rotated = 4
    }
}