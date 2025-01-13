namespace KrasCore.Mosaic
{
    public static class MosaicExtensions
    {
        public static bool IsMirroredX(this RuleTransform ruleTransform) =>
            ruleTransform == RuleTransform.MirrorX || ruleTransform == RuleTransform.MirrorXY;
        
        public static bool IsMirroredY(this RuleTransform ruleTransform) =>
            ruleTransform == RuleTransform.MirrorY || ruleTransform == RuleTransform.MirrorXY;
    }
}