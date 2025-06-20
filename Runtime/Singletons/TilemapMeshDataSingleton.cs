using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace KrasCore.Mosaic
{
    internal struct TilemapMeshDataSingleton : IComponentData, IDisposable
    {
        public Mesh.MeshDataArray MeshDataArray;
        public NativeList<Hash128> IntGridHashesToUpdate;
        public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;

        public bool IsDirty => IntGridHashesToUpdate.Length > 0;
        
        public void Dispose()
        {
            IntGridHashesToUpdate.Dispose();
            UpdatedMeshBoundsMap.Dispose();
        }
    }
}