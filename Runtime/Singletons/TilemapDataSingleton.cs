using System;
using KrasCore.Mosaic;
using KrasCore.NZCore;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace KrasCore.Mosaic
{
    public struct TilemapDataSingleton : IComponentData, IDisposable
    {
        public struct IntGridLayer : IDisposable
        {
            public NativeParallelHashMap<int2, int> IntGrid;
            public NativeParallelHashMap<int2, int> RuleGrid;
            public NativeHashSet<int2> ChangedPositions;
            public NativeHashSet<int2> PositionsToRefresh;
            public NativeList<int2> PositionsToRefreshList;
        
            public NativeParallelHashMap<int2, Entity> SpawnedEntities;
            public NativeParallelHashMap<int2, SpriteMesh> RenderedSprites;
            public NativeReference<int> RenderedSpritesCount;
        
            public ParallelToListMapper<RuleCommand> RuleCommands;
            public ParallelToListMapper<SpriteCommand> SpriteCommands;
            public ParallelToListMapper<RemoveCommand> PositionToRemove;
        
            // Store data locally to simplify lookups
            public TilemapData TilemapData;
        
            public IntGridLayer(TilemapData tilemapData, int capacity, Allocator allocator)
            {
                IntGrid = new NativeParallelHashMap<int2, int>(capacity, allocator);
                RuleGrid = new NativeParallelHashMap<int2, int>(capacity, allocator);
                
                ChangedPositions = new NativeHashSet<int2>(capacity, allocator);
                PositionsToRefresh = new NativeHashSet<int2>(capacity, allocator);
                PositionsToRefreshList = new NativeList<int2>(capacity, allocator);
            
                SpawnedEntities = new NativeParallelHashMap<int2, Entity>(capacity, allocator);
                RenderedSprites = new NativeParallelHashMap<int2, SpriteMesh>(capacity, allocator);
                RenderedSpritesCount = new NativeReference<int>(allocator);

                RuleCommands = new ParallelToListMapper<RuleCommand>(capacity, allocator);
                SpriteCommands = new ParallelToListMapper<SpriteCommand>(capacity, allocator);
                PositionToRemove = new ParallelToListMapper<RemoveCommand>(capacity, allocator);
            
                TilemapData = tilemapData;
            }

            public void Dispose()
            {
                IntGrid.Dispose();
                RuleGrid.Dispose();
                
                ChangedPositions.Dispose();
                PositionsToRefresh.Dispose();
                PositionsToRefreshList.Dispose();
            
                RuleCommands.Dispose();
                SpawnedEntities.Dispose();
                RenderedSprites.Dispose();
                RenderedSpritesCount.Dispose();
            
                SpriteCommands.Dispose();
                PositionToRemove.Dispose();
            }
        }
        
        public NativeHashMap<Hash128, IntGridLayer> IntGridLayers;

        // Store entity commands on a singleton to sort it later and instantiate using batch API
        public ParallelToListMapper<EntityCommand> EntityCommands;
        
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