using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Mesh = UnityEngine.Mesh;

namespace KrasCore.Mosaic.Data
{
    internal struct TilemapMeshDataSingleton : IComponentData, IDisposable
    {
        public NativeList<Hash128> HashesToUpdate;
        public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;

        public bool IsMeshDataArrayCreated;
        public Mesh.MeshDataArray MeshDataArray;
        
        public bool IsDirty => HashesToUpdate.Length > 0;

        public void AllocateWritableMeshData(int count)
        {
            if (IsMeshDataArrayCreated) MeshDataArray.Dispose();
            MeshDataArray = Mesh.AllocateWritableMeshData(count);
            IsMeshDataArrayCreated = true;
        }

        public void ApplyAndDisposeWritableMeshData(List<Mesh> meshesToUpdate)
        {
            Mesh.ApplyAndDisposeWritableMeshData(MeshDataArray, meshesToUpdate);
            IsMeshDataArrayCreated = false;
        }
        
        public void Dispose()
        {
            HashesToUpdate.Dispose();
            UpdatedMeshBoundsMap.Dispose();
        }
    }
}