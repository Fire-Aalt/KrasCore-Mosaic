using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public struct TilemapCommandBuffer : IDisposable
    {
        public NativeHashMap<int, NativeQueue<SetCommand>> Layers;
        public NativeReference<uint> GlobalSeed;

        private readonly Allocator _allocator;
        
        public TilemapCommandBuffer(int capacity, Allocator allocator)
        {
            _allocator = allocator;
            Layers = new NativeHashMap<int, NativeQueue<SetCommand>>(capacity, allocator);
            GlobalSeed = new NativeReference<uint>(allocator);
        }
        
        public void SetIntGridValue(IntGrid intGrid, int2 position, int intGridValue)
        {
            SetIntGridValue(intGrid.GetHashCode(), position, intGridValue);
        }
        
        public void SetIntGridValue(UnityObjectRef<IntGrid> intGrid, int2 position, int intGridValue)
        {
            SetIntGridValue(intGrid.GetHashCode(), position, intGridValue);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetIntGridValue(int intGridHash, int2 position, int intGridValue)
        {
            if (!Layers.ContainsKey(intGridHash))
            {
                Layers[intGridHash] = new NativeQueue<SetCommand>(_allocator);
            }
            Layers[intGridHash].Enqueue(new SetCommand { Position = position, IntGridValue = intGridValue });
        }

        public void SetGlobalSeed(uint seed)
        {
            GlobalSeed.Value = seed;
        }
            
        public struct SetCommand
        {
            public int2 Position;
            public int IntGridValue;
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