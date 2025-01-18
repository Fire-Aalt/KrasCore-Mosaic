using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public struct TilemapRendererSingleton : IComponentData, IDisposable
    {
        public struct IntGridLayer : IDisposable
        {
            public NativeList<int2> Positions;
            public NativeList<SpriteMesh> SpriteMeshes;
            public NativeList<Vertex> Vertices;
            public NativeList<int> Triangles;

            public NativeReference<bool> IsDirty;

            public IntGridLayer(int capacity, Allocator allocator)
            {
                Positions = new NativeList<int2>(capacity, allocator);
                SpriteMeshes = new NativeList<SpriteMesh>(capacity, allocator);
                Vertices = new NativeList<Vertex>(capacity, allocator);
                Triangles = new NativeList<int>(capacity, allocator);
                IsDirty = new NativeReference<bool>(allocator);
            }

            public void Dispose()
            {
                Positions.Dispose();
                SpriteMeshes.Dispose();
                Vertices.Dispose();
                Triangles.Dispose();
                IsDirty.Dispose();
            }
        }
		
        public NativeHashMap<int, IntGridLayer> IntGridLayers;
		
        public TilemapRendererSingleton(int capacity, Allocator allocator)
        {
            IntGridLayers = new NativeHashMap<int, IntGridLayer>(capacity, allocator);
        }

        public void Dispose()
        {
            foreach (var layer in IntGridLayers)
            {
                layer.Value.Dispose();
            }
            IntGridLayers.Dispose();
        }
    }
}