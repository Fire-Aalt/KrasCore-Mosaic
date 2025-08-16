using KrasCore.Mosaic.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic.Authoring
{
    [System.Serializable]
    public class RenderingData
    {
        public Orientation orientation = Orientation.XZ;
        public Material material;
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.TwoSided;
        public bool receiveShadows = true;
    }
}