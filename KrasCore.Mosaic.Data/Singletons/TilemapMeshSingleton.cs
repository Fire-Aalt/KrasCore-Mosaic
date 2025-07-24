using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace KrasCore.Mosaic.Data
{
    internal class TilemapMeshSingleton : IComponentData, IDisposable
    {
        public Dictionary<Hash128, Mesh> MeshMap;
        
        public void Dispose()
        {
            foreach (var kvp in MeshMap)
            {
                UnityEngine.Object.Destroy(kvp.Value);
            }
        }
    }
}