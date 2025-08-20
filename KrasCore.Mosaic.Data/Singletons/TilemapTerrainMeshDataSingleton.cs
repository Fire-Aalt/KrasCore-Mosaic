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
            public const int MaxLayersBlend = 4;
            
            public FixedList512Bytes<Hash128> Layers;
            public float2 TileSize;
	        
            public UnsafeHashMap<int2, FixedList64Bytes<GpuTerrainTile>> SpriteMeshMap;

            public UnsafeList<GpuTerrainTile> TileBuffer;
            public UnsafeList<GpuTerrainIndex> IndexBuffer;
            
            public Terrain(int capacity, Allocator allocator)
            {
                Layers = default;
                TileSize = default;
                
                SpriteMeshMap = new UnsafeHashMap<int2, FixedList64Bytes<GpuTerrainTile>>(capacity, allocator);

                var list = new FixedList64Bytes<GpuTerrainTile>();
                if (list.Capacity < MaxLayersBlend)
                {
                    throw new Exception($"{nameof(Terrain)} has MaxLayersBlend set to {TilemapTerrainMeshDataSingleton.Terrain.MaxLayersBlend}, but the capacity of a fixed list is {list.Capacity}");
                }
                
                TileBuffer = new UnsafeList<GpuTerrainTile>(capacity, allocator);
                IndexBuffer = new UnsafeList<GpuTerrainIndex>(capacity, allocator);
            }
            
            public void Dispose()
            {
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