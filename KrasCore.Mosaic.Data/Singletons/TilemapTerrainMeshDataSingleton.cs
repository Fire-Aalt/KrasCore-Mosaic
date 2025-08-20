using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic.Data
{
    internal struct TilemapTerrainMeshDataSingleton : IComponentData, IDisposable
    {
        public struct Terrain : IDisposable
        {
            public FixedList512Bytes<Hash128> Layers;
            public float2 TileSize;
	        
            public UnsafeHashSet<int2> UniquePositionsSet;
            public UnsafeHashMap<int2, FixedList64Bytes<GpuTerrainTile>> SpriteMeshMap;

            public UnsafeList<GpuTerrainTile> TileBuffer;
            public UnsafeList<GpuTerrainIndex> IndexBuffer;
            
            public Terrain(int capacity, Allocator allocator)
            {
                Layers = default;
                TileSize = default;
                
                UniquePositionsSet = new UnsafeHashSet<int2>(capacity, allocator);
                SpriteMeshMap = new UnsafeHashMap<int2, FixedList64Bytes<GpuTerrainTile>>(capacity, allocator);
                
                TileBuffer = new UnsafeList<GpuTerrainTile>(capacity, allocator);
                IndexBuffer = new UnsafeList<GpuTerrainIndex>(capacity, allocator);
            }
            
            public void Dispose()
            {
                UniquePositionsSet.Dispose();
                SpriteMeshMap.Dispose();

                TileBuffer.Dispose();
                IndexBuffer.Dispose();
            }
        }
        
        public NativeArray<VertexAttributeDescriptor> Layout;
        public NativeList<TilemapRendererData> TilemapRendererData;
        public NativeHashMap<Hash128, Terrain> Terrains;

        public TilemapTerrainMeshDataSingleton(int capacity, Allocator allocator)
        {
            Layout = new NativeArray<VertexAttributeDescriptor>(3, allocator);
            Layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
            Layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
            Layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

            TilemapRendererData = new NativeList<TilemapRendererData>(capacity, allocator);
            Terrains = new NativeHashMap<Hash128, Terrain>(capacity, allocator);
        }

        public void Dispose()
        {
            Layout.Dispose();
            TilemapRendererData.Dispose();
            foreach (var kvp in Terrains)
            {
                kvp.Value.Dispose();
            }
            Terrains.Dispose();
        }
    }
}