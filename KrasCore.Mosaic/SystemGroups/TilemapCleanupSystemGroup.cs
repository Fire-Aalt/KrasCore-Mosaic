using Unity.Entities;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TilemapCleanupSystemGroup : ComponentSystemGroup
    {
    }
}