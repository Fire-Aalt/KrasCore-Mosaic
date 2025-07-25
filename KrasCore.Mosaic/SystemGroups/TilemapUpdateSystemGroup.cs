using Unity.Entities;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial class TilemapUpdateSystemGroup : ComponentSystemGroup
    {
    }
}