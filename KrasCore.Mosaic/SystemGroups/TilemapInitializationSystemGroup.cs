using Unity.Entities;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TilemapCleanupSystemGroup))]
    public partial class TilemapInitializationSystemGroup : ComponentSystemGroup
    {
    }
}