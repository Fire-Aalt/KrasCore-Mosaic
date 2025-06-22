using Unity.Entities;
using Unity.Scenes;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    public partial class TilemapCleanupSystemGroup : ComponentSystemGroup
    {
    }
}