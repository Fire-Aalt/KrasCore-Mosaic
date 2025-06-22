using Unity.Entities;

namespace KrasCore.Mosaic.Data
{
    public struct RuleBlobReferenceElement : IBufferElementData
    {
        public bool Enabled;
        public BlobAssetReference<RuleBlob> Value;
    }
}