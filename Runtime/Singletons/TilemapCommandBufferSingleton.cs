using System;
using KrasCore.NZCore;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace KrasCore.Mosaic
{
    public struct TilemapCommandBufferSingleton : IComponentData, IDisposable
    {
        public struct IntGridLayer : IDisposable
        {
            public ParallelToListMapper<SetCommand> SetCommandsMapper;
            public NativeReference<bool> ClearCommand;

            public IntGridLayer(int capacity, Allocator allocator)
            {
                SetCommandsMapper = new ParallelToListMapper<SetCommand>(capacity, allocator);
                ClearCommand = new NativeReference<bool>(allocator);
            }

            public void Dispose()
            {
                SetCommandsMapper.Dispose();
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
                    SetCommands = layer.SetCommandsMapper.AsThreadWriter();
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
            
            public void SetIntGridValue(in Hash128 intGridHash, in int2 position, int intGridValue)
            {
                var layer = _layers[intGridHash].SetCommands;
                layer.Begin(_threadIndex);
                layer.Write(new SetCommand 
                    { Position = position, IntGridValue = intGridValue });
            }
        }
        

        
        [NativeDisableContainerSafetyRestriction]
        public NativeHashMap<Hash128, IntGridLayer> Layers;
        [NativeDisableContainerSafetyRestriction]
        private NativeHashMap<Hash128, ParallelWriter.IntGridLayerParallelWriter> _parallelWriteLayers;
        
        internal NativeReference<uint> GlobalSeed;
        internal NativeReference<AABB2D> CullingBounds;
        internal NativeReference<AABB2D> PrevCullingBounds;

        private readonly Allocator _allocator;
        
        public TilemapCommandBufferSingleton(int layersCapacity, Allocator allocator)
        {
            _allocator = allocator;

            Layers = new NativeHashMap<Hash128, IntGridLayer>(layersCapacity, allocator);
            _parallelWriteLayers =
                new NativeHashMap<Hash128, ParallelWriter.IntGridLayerParallelWriter>(layersCapacity, allocator);
            GlobalSeed = new NativeReference<uint>(allocator);
            CullingBounds = new NativeReference<AABB2D>(allocator);
            PrevCullingBounds = new NativeReference<AABB2D>(allocator);
            
            CullingBounds.Value = new AABB2D { Extents = new float2(float.MaxValue, float.MaxValue) / 2f };
        }

        public ParallelWriter AsParallelWriter() => new(ref _parallelWriteLayers);

        public void SetIntGridValue(IntGrid intGrid, int2 position, int intGridValue)
        {
            SetIntGridValue(intGrid.Hash, position, intGridValue);
        }
        
        public void SetIntGridValue(in Hash128 intGridHash, in int2 position, int intGridValue)
        {
            var layerList = Layers[intGridHash].SetCommandsMapper.ParallelList.GetUnsafeList(0);
            layerList.Add(new SetCommand { Position = position, IntGridValue = intGridValue });
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
        
        public void Clear(in Hash128 intGridHash)
        {
            var clearCommand = Layers[intGridHash].ClearCommand;
            clearCommand.Value = true;
        }
        
        public void SetCullingBounds(in AABB2D bounds)
        {
            CullingBounds.Value = bounds;
        }

        public void SetGlobalSeed(uint seed)
        {
            GlobalSeed.Value = seed;
        }
        
        internal bool TryRegisterIntGridLayer(in Hash128 intGridHash)
        {
            if (Layers.ContainsKey(intGridHash)) return false;
            
            var layer = new IntGridLayer(256, _allocator);
            var parallelLayer = new ParallelWriter.IntGridLayerParallelWriter(ref layer);
            Layers.Add(intGridHash, layer);
            _parallelWriteLayers.Add(intGridHash, parallelLayer);
            return true;
        }

        public void Dispose()
        {
            foreach (var layer in Layers)
            {
                layer.Value.Dispose();
            }
            Layers.Dispose();
            _parallelWriteLayers.Dispose();
            GlobalSeed.Dispose();
            CullingBounds.Dispose();
            PrevCullingBounds.Dispose();
        }
    }
}