using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
    internal struct TilemapMeshDataSingleton : IComponentData, IDisposable
    {
        public NativeList<Hash128> HashesToUpdate;
        public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;
        
        public UnityEngine.Mesh.MeshDataArray MeshDataArray;

        public bool IsDirty => HashesToUpdate.Length > 0;
        
        public void Dispose()
        {
            HashesToUpdate.Dispose();
            UpdatedMeshBoundsMap.Dispose();
        }
    }
}