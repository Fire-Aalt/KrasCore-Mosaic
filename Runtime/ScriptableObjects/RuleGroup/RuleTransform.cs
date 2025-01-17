using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace KrasCore.Mosaic
{
    public enum RuleTransform
    {
        [Tooltip("No transform operation")]
        None,
        
        [Tooltip("X mirror. Enable this to also check for match when mirrored horizontally")]
        [LabelText(" ", SdfIconType.ArrowLeftRight)]
        MirrorX,

        [Tooltip("Y mirror. Enable this to also check for match when mirrored vertically")]
        [LabelText(" ", SdfIconType.ArrowDownUp)]
        MirrorY,
        
        [Tooltip("XY mirror. Enable this to also check for match when mirrored horizontally or vertically")]
        [LabelText(" ", SdfIconType.ArrowsFullscreen)]
        MirrorXY,
        
        [Tooltip("Rotate the pattern by 90 degrees 4 times to check for matches")]
        [LabelText(" ", SdfIconType.ArrowClockwise)]
        Rotated
    }
    
    [Flags]
    public enum ResultTransform
    {
        [Tooltip("X mirror. Enable this to mark the rule as able to mirror the result horizontally")]
        [LabelText(" ", SdfIconType.ArrowLeftRight)]
        MirrorX = 1,

        [Tooltip("Y mirror. Enable this to mark the rule as able to mirror the result vertically")]
        [LabelText(" ", SdfIconType.ArrowDownUp)]
        MirrorY = 2,
        
        [Tooltip("Enable this to mark the rule as able to rotate the result by 90 degrees up to 4 times")]
        [LabelText(" ", SdfIconType.ArrowClockwise)]
        Rotated = 4
    }
    

}