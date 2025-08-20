using System.Runtime.InteropServices;

namespace KrasCore.Mosaic.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuTerrainIndex
    {
        public uint StartIndex;
        public uint Count;
    }
}