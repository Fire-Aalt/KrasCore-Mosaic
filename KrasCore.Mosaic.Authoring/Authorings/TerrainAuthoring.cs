using System;
using System.Collections.Generic;
using KrasCore.Mosaic.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic.Authoring
{
    public class TerrainAuthoring : MonoBehaviour
    {
        public List<IntGridDefinition> intGridLayers;

        public Orientation orientation;
        public Material material;
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.TwoSided;
        public bool receiveShadows = true;
        
        private void OnValidate()
        {
            if (intGridLayers.Count > 8)
            {
                var exceed = intGridLayers.Count - 8;
                for (var index = 0; index < exceed; index++)
                {
                    intGridLayers.RemoveAtSwapBack(8);
                }
            }
        }

        private class Baker : Baker<TerrainAuthoring>
        {
            public override void Bake(TerrainAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
            }
        }
    }
}