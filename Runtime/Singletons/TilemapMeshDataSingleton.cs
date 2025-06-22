using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    internal struct TilemapMeshDataSingleton : IComponentData, IDisposable
    {
        public NativeList<Hash128> IntGridHashesToUpdate;
        public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;
        
        public UnityEngine.Mesh.MeshDataArray MeshDataArray;

        public bool IsDirty => IntGridHashesToUpdate.Length > 0;
        
        public void Dispose()
        {
            IntGridHashesToUpdate.Dispose();
            UpdatedMeshBoundsMap.Dispose();
        }
    }
}