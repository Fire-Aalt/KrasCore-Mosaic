using Unity.Entities;

namespace KrasCore.Mosaic.Data
{
    [InternalBufferCapacity(0)]
    public struct RuleBlobReferenceElement : IBufferElementData
    {
        public bool Enabled;
        public BlobAssetReference<RuleBlob> Value;
    }
}