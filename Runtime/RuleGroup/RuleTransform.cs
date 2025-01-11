using UnityEngine;

namespace KrasCore.Mosaic
{
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
        Rotated
    }
}