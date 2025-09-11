namespace KrasCore.Mosaic.Data
{
    public static class RuleTransformExtensions
    {
        public static bool IsMirroredX(this Transformation transformation) => transformation.HasFlagBurst(Transformation.MirrorX);
        
        public static bool IsMirroredY(this Transformation transformation) => transformation.HasFlagBurst(Transformation.MirrorY);
        
        public static bool HasFlagBurst(this Transformation flags, Transformation flag)
        {
            return (flags & flag) != 0;
        }
        
        public static void Add(this ref Transformation flags, Transformation flag)
        {
            flags |= flag;
        }
        
        public static void Remove(this ref Transformation flags, Transformation flag)
        {
            flags &= ~flag;
        }
    }
}