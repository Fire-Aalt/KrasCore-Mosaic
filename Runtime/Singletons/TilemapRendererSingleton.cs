using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public struct TilemapRendererSingleton : IComponentData, IDisposable
    {
        public NativeList<int2> Positions;
        public NativeList<SpriteMesh> SpriteMeshes;
        public NativeList<Vertex> Vertices;
        public NativeList<int> Indices;
        public NativeList<int> LayerPointers;
        
        public NativeList<TilemapDataSingleton.IntGridLayer> DirtyIntGridLayers;
        public NativeList<TilemapRendererData> DirtyTilemapsRendererData;
        public NativeList<OffsetCount> DirtyOffsetCounts;
		
        public TilemapRendererSingleton(int capacity, Allocator allocator)
        {
            Positions = new NativeList<int2>(capacity, allocator);
            SpriteMeshes = new NativeList<SpriteMesh>(capacity, allocator);
            Vertices = new NativeList<Vertex>(capacity, allocator);
            Indices = new NativeList<int>(capacity, allocator);
            LayerPointers = new NativeList<int>(capacity, allocator);

            DirtyIntGridLayers = new NativeList<TilemapDataSingleton.IntGridLayer>(8, allocator);
            DirtyTilemapsRendererData = new NativeList<TilemapRendererData>(8, allocator);
            DirtyOffsetCounts = new NativeList<OffsetCount>(8, allocator);
        }

        public void Dispose()
        {
            Positions.Dispose();
            SpriteMeshes.Dispose();
            Vertices.Dispose();
            Indices.Dispose();
            LayerPointers.Dispose();
            
            DirtyIntGridLayers.Dispose();
            DirtyTilemapsRendererData.Dispose();
            DirtyOffsetCounts.Dispose();
        }
    }
}