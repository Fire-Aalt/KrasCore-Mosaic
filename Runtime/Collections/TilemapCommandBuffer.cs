using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public struct TilemapCommandBuffer : IDisposable
    {
        public struct IntGridLayer : IDisposable
        {
            public NativeQueue<SetCommand> SetQueue;
            public NativeReference<bool> ClearCommand;

            public IntGridLayer(int capacity, Allocator allocator)
            {
                SetQueue = new NativeQueue<SetCommand>(allocator);
                ClearCommand = new NativeReference<bool>(allocator);
            }

            public void Dispose()
            {
                SetQueue.Dispose();
                ClearCommand.Dispose();
            }
        }
        
        public struct SetCommand
        {
            public int2 Position;
            public int IntGridValue;
        }
        
        public NativeHashMap<Hash128, IntGridLayer> Layers;
        public NativeReference<uint> GlobalSeed;

        private readonly Allocator _allocator;
        
        public TilemapCommandBuffer(int capacity, Allocator allocator)
        {
            _allocator = allocator;
            Layers = new NativeHashMap<Hash128, IntGridLayer>(capacity, allocator);
            GlobalSeed = new NativeReference<uint>(allocator);
        }
        
        public void SetIntGridValue(IntGrid intGrid, int2 position, int intGridValue)
        {
            SetIntGridValue(intGrid.Hash, position, intGridValue);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetIntGridValue(in Hash128 intGridHash, in int2 position, int intGridValue)
        {
            var layer = GetOrAddLayer(intGridHash);
            layer.SetQueue.Enqueue(new SetCommand { Position = position, IntGridValue = intGridValue });
        }

        public void ClearAll()
        {
            foreach (var kvp in Layers)
            {
                Clear(kvp.Key);
            }
        }
        
        public void Clear(IntGrid intGrid)
        {
            Clear(intGrid.Hash);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(Hash128 intGridHash)
        {
            var layer = GetOrAddLayer(intGridHash);
            layer.ClearCommand.Value = true;
        }

        public void SetGlobalSeed(uint seed)
        {
            GlobalSeed.Value = seed;
        }
        
        private IntGridLayer GetOrAddLayer(Hash128 intGridHash)
        {
            if (!Layers.TryGetValue(intGridHash, out var layer))
            {
                layer = new IntGridLayer(256, _allocator);
                Layers[intGridHash] = layer;
            }
            return layer;
        }

        public void Dispose()
        {
            foreach (var layer in Layers)
            {
                layer.Value.Dispose();
            }
            Layers.Dispose();
            GlobalSeed.Dispose();
        }
    }
}