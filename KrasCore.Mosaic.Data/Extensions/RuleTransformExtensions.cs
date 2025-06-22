namespace KrasCore.Mosaic.Data
{
    public static class RuleTransformExtensions
    {
        public static bool IsMirroredX(this RuleTransform ruleTransform) =>
            ruleTransform == RuleTransform.MirrorX || ruleTransform == RuleTransform.MirrorXY;
        
        public static bool IsMirroredY(this RuleTransform ruleTransform) =>
            ruleTransform == RuleTransform.MirrorY || ruleTransform == RuleTransform.MirrorXY;
        
        public static bool HasFlagBurst(this ResultTransform flags, ResultTransform flag)
        {
            return (flags & flag) != 0;
        }
    }
}