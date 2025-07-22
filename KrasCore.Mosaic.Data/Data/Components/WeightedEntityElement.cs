using Unity.Entities;

namespace KrasCore.Mosaic.Data
{
    [InternalBufferCapacity(0)]
    public struct WeightedEntityElement : IBufferElementData
    {
        public Entity Value;
    }
}