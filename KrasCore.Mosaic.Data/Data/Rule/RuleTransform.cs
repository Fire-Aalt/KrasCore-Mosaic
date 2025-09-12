using System;

namespace KrasCore.Mosaic.Data
{
    // Exists for migration
    [Obsolete]
    public enum RuleTransform
    {
        None,
        MirrorX,
        MirrorY,
        MirrorXY,
        Rotated,
        Migrated
    }
}