using Unity.Entities;
using Unity.Scenes;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(RuntimeBakingSystemGroup))]
    public partial class TilemapCleanupSystemGroup : ComponentSystemGroup
    {
    }
}