using System;
using KrasCore.Mosaic.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public partial struct TilemapMeshDataSystem
    {
        private struct Data : IDisposable
        {
            public NativeList<int2> Positions;
            public NativeList<SpriteMesh> SpriteMeshes;
            public NativeList<Vertex> Vertices;
            public NativeList<int> Indices;
            public NativeList<int> LayerPointers;
        
            public NativeList<Hash128> TilemapHashesToUpdate;
            public NativeList<TilemapRendererData> DirtyTilemapsRendererData;
            public NativeList<OffsetData> DirtyOffsetCounts;
        
            public Data(int capacity, Allocator allocator)
            {
                Positions = new NativeList<int2>(capacity, allocator);
                SpriteMeshes = new NativeList<SpriteMesh>(capacity, allocator);
                Vertices = new NativeList<Vertex>(capacity, allocator);
                Indices = new NativeList<int>(capacity, allocator);
                LayerPointers = new NativeList<int>(capacity, allocator);

                TilemapHashesToUpdate = new NativeList<Hash128>(8, allocator);
                DirtyTilemapsRendererData = new NativeList<TilemapRendererData>(8, allocator);
                DirtyOffsetCounts = new NativeList<OffsetData>(8, allocator);
            }

            public void Dispose()
            {
                Positions.Dispose();
                SpriteMeshes.Dispose();
                Vertices.Dispose();
                Indices.Dispose();
                LayerPointers.Dispose();

                TilemapHashesToUpdate.Dispose();
                DirtyTilemapsRendererData.Dispose();
                DirtyOffsetCounts.Dispose();
            }
        }
    }
}