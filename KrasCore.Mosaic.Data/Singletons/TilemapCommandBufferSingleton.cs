using System;
using KrasCore.NZCore;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapCommandBufferSingleton : IComponentData, IDisposable
    {
        public struct IntGridLayer : IDisposable
        {
            public ParallelList<SetCommand> SetCommands;
            public NativeReference<bool> ClearCommand;

            public IntGridLayer(int capacity, Allocator allocator)
            {
                SetCommands = new ParallelList<SetCommand>(capacity, allocator);
                ClearCommand = new NativeReference<bool>(allocator);
            }

            public void Dispose()
            {
                SetCommands.Dispose();
                ClearCommand.Dispose();
            }
        }

        public struct ParallelWriter
        {
            public struct IntGridLayerParallelWriter
            {
                public ParallelList<SetCommand>.ThreadWriter SetCommands;
                
                internal IntGridLayerParallelWriter(ref IntGridLayer layer)
                {
                    SetCommands = layer.SetCommands.AsThreadWriter();
                }
            }

            [NativeDisableContainerSafetyRestriction]
            private NativeHashMap<Hash128, IntGridLayerParallelWriter> _layers;
            
            [NativeSetThreadIndex] 
            private int _threadIndex;
            
            internal ParallelWriter(ref NativeHashMap<Hash128, IntGridLayerParallelWriter> layers)
            {
                _layers = layers;
                _threadIndex = 0;
            }
            
            public void SetIntGridValue(in Hash128 intGridHash, int2 position, IntGridValue intGridValue)
            {
                var layer = _layers[intGridHash].SetCommands;
                layer.Begin(_threadIndex);
                layer.Write(new SetCommand 
                    { Position = position, IntGridValue = intGridValue });
            }
        }
        
        [NativeDisableContainerSafetyRestriction]
        internal NativeHashMap<Hash128, IntGridLayer> IntGridLayers;
        [NativeDisableContainerSafetyRestriction]
        private NativeHashMap<Hash128, ParallelWriter.IntGridLayerParallelWriter> _parallelWriteLayers;
        
        internal NativeReference<uint> GlobalSeed;
        internal NativeReference<AABB2D> CullingBounds;
        internal NativeReference<AABB2D> PrevCullingBounds;

        private readonly Allocator _allocator;
        
        public TilemapCommandBufferSingleton(int layersCapacity, Allocator allocator)
        {
            _allocator = allocator;

            IntGridLayers = new NativeHashMap<Hash128, IntGridLayer>(layersCapacity, allocator);
            _parallelWriteLayers =
                new NativeHashMap<Hash128, ParallelWriter.IntGridLayerParallelWriter>(layersCapacity, allocator);
            GlobalSeed = new NativeReference<uint>(allocator);
            CullingBounds = new NativeReference<AABB2D>(allocator);
            PrevCullingBounds = new NativeReference<AABB2D>(allocator);
            
            CullingBounds.Value = new AABB2D { Extents = new float2(float.MaxValue, float.MaxValue) / 2f };
        }

        public ParallelWriter AsParallelWriter() => new(ref _parallelWriteLayers);
        
        public void SetIntGridValue(in Hash128 intGridHash, in int2 position, short intGridValue)
        {
            var layerList = IntGridLayers[intGridHash].SetCommands.GetUnsafeList(0);
            layerList.Add(new SetCommand { Position = position, IntGridValue = intGridValue });
        }

        public void ClearAll()
        {
            foreach (var kvp in IntGridLayers)
            {
                Clear(kvp.Key);
            }
        }
        
        public void Clear(Hash128 intGridHash)
        {
            var clearCommand = IntGridLayers[intGridHash].ClearCommand;
            clearCommand.Value = true;
        }
        
        public void SetCullingBounds(AABB2D bounds)
        {
            CullingBounds.Value = bounds;
        }

        public void SetGlobalSeed(uint seed)
        {
            GlobalSeed.Value = seed;
        }
        
        internal bool TryRegisterIntGridLayer(in Hash128 intGridHash)
        {
            if (IntGridLayers.ContainsKey(intGridHash)) return false;
            
            var layer = new IntGridLayer(256, _allocator);
            var parallelLayer = new ParallelWriter.IntGridLayerParallelWriter(ref layer);
            IntGridLayers.Add(intGridHash, layer);
            _parallelWriteLayers.Add(intGridHash, parallelLayer);
            return true;
        }

        public void Dispose()
        {
            foreach (var layer in IntGridLayers)
            {
                layer.Value.Dispose();
            }
            IntGridLayers.Dispose();
            _parallelWriteLayers.Dispose();
            GlobalSeed.Dispose();
            CullingBounds.Dispose();
            PrevCullingBounds.Dispose();
        }
    }
}