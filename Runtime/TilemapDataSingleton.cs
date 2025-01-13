using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace KrasCore.Mosaic
{
    public struct RuleCommand
    {
        public int2 Position;
        public int AppliedRuleHash;
    }
    
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
        
            public ParallelToListMapper<RuleCommand> RuleCommands;
            public ParallelToListMapper<SpriteCommand> SpriteCommands;
            public ParallelToListMapper<PositionToRemove> PositionToRemove;
        
            public TilemapData TilemapData;
            public LocalTransform TilemapTransform;
        
            public IntGridLayer(TilemapData tilemapData, int capacity, Allocator allocator)
            {
                IntGrid = new NativeParallelHashMap<int2, int>(capacity, allocator);
                RuleGrid = new NativeParallelHashMap<int2, int>(capacity, allocator);
                
                ChangedPositions = new NativeHashSet<int2>(capacity, allocator);
                PositionsToRefresh = new NativeHashSet<int2>(capacity, allocator);
                PositionsToRefreshList = new NativeList<int2>(capacity, allocator);
            
                SpawnedEntities = new NativeParallelHashMap<int2, Entity>(capacity, allocator);
                RenderedSprites = new NativeParallelHashMap<int2, SpriteMesh>(capacity, allocator);

                RuleCommands = new ParallelToListMapper<RuleCommand>(capacity, allocator);
                SpriteCommands = new ParallelToListMapper<SpriteCommand>(capacity, allocator);
                PositionToRemove = new ParallelToListMapper<PositionToRemove>(capacity, allocator);
            
                TilemapData = tilemapData;
                TilemapTransform = default;
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
            
                SpriteCommands.Dispose();
                PositionToRemove.Dispose();
            }
        }
        
        public NativeHashMap<int, IntGridLayer> IntGridLayers;

        public ParallelToListMapper<EntityCommand> EntityCommands;
        
        public JobHandle JobHandle;
        
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