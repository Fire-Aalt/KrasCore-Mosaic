using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
    internal struct TilemapMeshDataSingleton : IComponentData, IDisposable
    {
        public NativeList<Hash128> TilemapHashesToUpdate;
        public NativeList<Hash128> TerrainHashesToUpdate;
        
        public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;

        public MeshDataArrayWrapper TilemapMeshDataArray;
        public MeshDataArrayWrapper TerrainMeshDataArray;
        
        public void Dispose()
        {
            TilemapHashesToUpdate.Dispose();
            TerrainHashesToUpdate.Dispose();
            UpdatedMeshBoundsMap.Dispose();
        }
    }
}