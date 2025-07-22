using System;
using KrasCore.Mosaic;
using KrasCore.NZCore;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace KrasCore.Mosaic.Data
{
    public struct TilemapDataSingleton : IComponentData, IDisposable
    {
        public struct IntGridLayer : IDisposable
        {
            public UnsafeHashMap<int2, IntGridValue> IntGrid;
            public UnsafeHashMap<int2, int> RuleGrid;
        
            public UnsafeHashMap<int2, SpriteMesh> RenderedSprites;
            public UnsafeHashMap<int2, Entity> SpawnedEntities;

            public UnsafeHashSet<int2> ChangedPositions;
            public UnsafeHashSet<int2> PositionsToRefresh;
            public UnsafeList<int2> RefreshedPositions;

            public readonly bool DualGrid;
            
            // Store data locally to simplify lookups
            public TilemapData TilemapData;
            
            public IntGridLayer(int capacity, Allocator allocator, bool dualGrid)
            {
                IntGrid = new UnsafeHashMap<int2, IntGridValue>(capacity, allocator);
                RuleGrid = new UnsafeHashMap<int2, int>(capacity, allocator);
                
                ChangedPositions = new UnsafeHashSet<int2>(capacity, allocator);
                PositionsToRefresh = new UnsafeHashSet<int2>(capacity, allocator);
            
                SpawnedEntities = new UnsafeHashMap<int2, Entity>(capacity, allocator);
                RenderedSprites = new UnsafeHashMap<int2, SpriteMesh>(capacity, allocator);
            
                RefreshedPositions = new UnsafeList<int2>(capacity, allocator);
                
                DualGrid = dualGrid;
                TilemapData = default;
            }
            
            internal void SetTilemapData(in TilemapData data)
            {
                TilemapData = data;
            }

            public void Dispose()
            {
                IntGrid.Dispose();
                RuleGrid.Dispose();
                
                ChangedPositions.Dispose();
                PositionsToRefresh.Dispose();
            
                SpawnedEntities.Dispose();
                RenderedSprites.Dispose();

                RefreshedPositions.Dispose();
            }
        }
        
        public NativeHashMap<Hash128, IntGridLayer> IntGridLayers;

        // Store entity commands on a singleton to sort it later and instantiate using batch API
        public ParallelToListMapper<EntityCommand> EntityCommands;
        
        public bool TryRegisterIntGridLayer(TilemapData tilemapData)
        {
            if (IntGridLayers.ContainsKey(tilemapData.IntGridHash)) return false;
            
            IntGridLayers.Add(tilemapData.IntGridHash, new IntGridLayer(64, Allocator.Persistent, tilemapData.DualGrid));
            return true;
        }
        
        public void Dispose()
        {
            foreach (var layer in IntGridLayers)
            {
                layer.Value.Dispose();
            }
            IntGridLayers.Dispose();
            EntityCommands.Dispose();
        }
    }
}