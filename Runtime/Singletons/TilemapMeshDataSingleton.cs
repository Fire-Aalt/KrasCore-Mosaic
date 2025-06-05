using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace KrasCore.Mosaic
{
    internal struct TilemapMeshDataSingleton : IComponentData, IDisposable
    {
        public NativeList<Hash128> IntGridHashesToUpdate;
        public Mesh.MeshDataArray MeshDataArray;

        public bool IsDirty => IntGridHashesToUpdate.Length > 0;
        
        public void Dispose()
        {
            IntGridHashesToUpdate.Dispose();
        }
    }
}