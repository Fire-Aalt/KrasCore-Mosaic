using System;
using Drawing;
using KrasCore.Mosaic;
using KrasCore.NZCore;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[assembly: RegisterGenericJobType(typeof(ParallelList<EntityCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<SpriteCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<PositionToRemove>.UnsafeParallelListToArraySingleThreaded))]



namespace KrasCore.Mosaic
{
    public struct TilemapCommandBufferSingleton : IComponentData, IDisposable
    {
        public TilemapCommandBuffer Tcb;

        public void Dispose()
        {
            Tcb.Dispose();
        }
    }

    public struct TilemapDataSingleton : IComponentData, IDisposable
    {
        public NativeHashMap<int, IntGridLayer> IntGridLayers;

        public NativeList<EntityCommand> EntityCommands;
        public NativeList<SpriteCommand> SpriteCommands;
        public NativeList<PositionToRemove> PositionToRemove;
        
        public JobHandle JobHandle;
        
        public void Dispose()
        {
            foreach (var layer in IntGridLayers)
            {
                layer.Value.Dispose();
            }
            IntGridLayers.Dispose();
            EntityCommands.Dispose();
            SpriteCommands.Dispose();
            PositionToRemove.Dispose();
        }
    }

    public struct IntGridLayer : IDisposable
    {
        public NativeParallelHashMap<int2, int> IntGrid;
        public NativeHashSet<int2> ChangedPositions;
        public NativeHashSet<int2> PositionsToRefresh;
        
        public NativeParallelHashMap<int2, Entity> SpawnedEntities;
        public NativeParallelHashMap<int2, SpriteMesh> RenderedSprites;

        public NativeList<int2> PositionsToRefreshList;

        public TilemapData TilemapData;
        public LocalTransform TilemapTransform;
        
        public IntGridLayer(TilemapData tilemapData, int capacity, Allocator allocator)
        {
            IntGrid = new NativeParallelHashMap<int2, int>(capacity, allocator);
            ChangedPositions = new NativeHashSet<int2>(capacity, allocator);
            PositionsToRefresh = new NativeHashSet<int2>(capacity, allocator);
            SpawnedEntities = new NativeParallelHashMap<int2, Entity>(capacity, allocator);
            RenderedSprites = new NativeParallelHashMap<int2, SpriteMesh>(capacity, allocator);
            
            PositionsToRefreshList = new NativeList<int2>(capacity, allocator);
            TilemapData = tilemapData;
            TilemapTransform = default;
        }

        public void Dispose()
        {
            IntGrid.Dispose();
            ChangedPositions.Dispose();
            PositionsToRefresh.Dispose();
            PositionsToRefreshList.Dispose();
            SpawnedEntities.Dispose();
            RenderedSprites.Dispose();
        }
    }

    public struct PositionToRemove
    {
        public int2 Position;
        public int IntGridHash;
    }
    
    public struct SpriteCommand
    {
        public SpriteMesh SpriteMesh;
        public int2 Position;
        public int IntGridHash;
    }
    
    [RequireMatchingQueriesForUpdate]
    public partial struct TilemapCommandBufferSystem : ISystem
    {
        private ParallelList<EntityCommand> _entityCommands;
        private ParallelList<SpriteCommand> _spriteCommands;
        private ParallelList<PositionToRemove> _entityPositionsToRemove;
        private NativeList<JobHandle> _jobHandles;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TilemapDataSingleton>();
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
            state.EntityManager.CreateSingleton(new TilemapCommandBufferSingleton
            {
                Tcb = new TilemapCommandBuffer(64, Allocator.Persistent)
            });
            state.EntityManager.CreateSingleton(new TilemapDataSingleton
            {
                IntGridLayers = new NativeHashMap<int, IntGridLayer>(8, Allocator.Persistent),
                EntityCommands = new NativeList<EntityCommand>(64, Allocator.Persistent),
                SpriteCommands = new NativeList<SpriteCommand>(64, Allocator.Persistent),
                PositionToRemove = new NativeList<PositionToRemove>(64, Allocator.Persistent)
            });

            _entityCommands = new ParallelList<EntityCommand>(256, Allocator.Persistent);
            _spriteCommands = new ParallelList<SpriteCommand>(256, Allocator.Persistent);
            _entityPositionsToRemove = new ParallelList<PositionToRemove>(256, Allocator.Persistent);
            _jobHandles = new NativeList<JobHandle>(8, Allocator.Persistent);
            state.RequireForUpdate<TilemapCommandBufferSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            SystemAPI.GetSingleton<TilemapCommandBufferSingleton>().Dispose();
            SystemAPI.GetSingleton<TilemapDataSingleton>().Dispose();
            _entityCommands.Dispose();
            _spriteCommands.Dispose();
            _entityPositionsToRemove.Dispose();
            _jobHandles.Dispose();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>().Tcb;
            ref var singleton = ref SystemAPI.GetSingletonRW<TilemapDataSingleton>().ValueRW;
            singleton.JobHandle = default;
            var intGridLayers = singleton.IntGridLayers;
            
            singleton.EntityCommands.Clear();
            singleton.SpriteCommands.Clear();
            singleton.PositionToRemove.Clear();
            
            foreach (var (tilemapDataRO, transformRO, rulesBuffer, refreshPositionsBuffer, entityBuffer) in SystemAPI.Query<RefRO<TilemapData>, RefRO<LocalTransform>, DynamicBuffer<RuleBlobReferenceElement>, DynamicBuffer<RefreshPositionElement>, DynamicBuffer<WeightedEntityElement>>())
            {
                var intGridHash = tilemapDataRO.ValueRO.IntGridReference.GetHashCode();

                if (!intGridLayers.ContainsKey(intGridHash))
                {
                    intGridLayers.Add(intGridHash, new IntGridLayer(tilemapDataRO.ValueRO, 64, Allocator.Persistent));
                }
                var layer = intGridLayers[intGridHash];
                layer.TilemapTransform = transformRO.ValueRO;
                intGridLayers[intGridHash] = layer;
                
                var commandsQueue = tcb.Layers[intGridHash];
                if (commandsQueue.IsEmpty()) continue;
                
                // ProcessCommandsJob
                var jobDependency = new ProcessCommandsJob
                {
                    IntGrid = layer.IntGrid,
                    ChangedPositions = layer.ChangedPositions,
                    SetCommandsQueue = tcb.Layers[intGridHash]
                }.Schedule(state.Dependency);
                
                // Find and filter refresh positions
                jobDependency = new FindRefreshPositionsJob
                {
                    RefreshOffsets = refreshPositionsBuffer.AsNativeArray().Reinterpret<int2>(),
                    ChangedPositions = layer.ChangedPositions,
                    PositionsToRefresh = layer.PositionsToRefresh,
                    PositionsToRefreshList = layer.PositionsToRefreshList
                }.Schedule(jobDependency);
                
                // Apply rules
                var processRulesJob = new ProcessRulesJob
                {
                    IntGrid = layer.IntGrid,
                    PositionsToRefresh = layer.PositionsToRefreshList.AsDeferredJobArray(),
                    EntityBuffer = entityBuffer.AsNativeArray(),
                    RulesBuffer = rulesBuffer.AsNativeArray(),
                    SpawnedEntities = layer.SpawnedEntities,
                    RenderedSprites = layer.RenderedSprites,
                    EntityCommands = _entityCommands.AsThreadWriter(),
                    SpriteCommands = _spriteCommands.AsThreadWriter(),
                    PositionsToRemove = _entityPositionsToRemove.AsThreadWriter(),
                    IntGridHash = intGridHash
                };
                var handle = processRulesJob.Schedule(layer.PositionsToRefreshList, 16, jobDependency);
                _jobHandles.Add(handle);
            }

            if (_jobHandles.Length == 0) return;
            var combinedDependency = JobHandle.CombineDependencies(_jobHandles.AsArray());
            
            var handle1 = _entityCommands.CopyToArraySingle(ref singleton.EntityCommands, combinedDependency);
            var handle2 = _spriteCommands.CopyToArraySingle(ref singleton.SpriteCommands, combinedDependency);
            var handle3 = _entityPositionsToRemove.CopyToArraySingle(ref singleton.PositionToRemove, combinedDependency);

            state.Dependency = new ClearBuffersJob
            {
                EntityCommands = _entityCommands,
                SpriteCommands = _spriteCommands,
                PositionsToRemoveParallelList = _entityPositionsToRemove
            }.Schedule(JobHandle.CombineDependencies(handle1, handle2, handle3));

            singleton.JobHandle = state.Dependency;
            
            _jobHandles.Clear();
        }

        [BurstCompile]
        private struct ProcessCommandsJob : IJob
        {
            public NativeQueue<TilemapCommandBuffer.SetCommand> SetCommandsQueue;
            public NativeParallelHashMap<int2, int> IntGrid;
            public NativeHashSet<int2> ChangedPositions;
            
            public void Execute()
            {
                while (SetCommandsQueue.TryDequeue(out var command))
                {
                    IntGrid[command.Position] = command.IntGridValue;
                    ChangedPositions.Add(command.Position);
                }
            }
        }

        [BurstCompile]
        private struct FindRefreshPositionsJob : IJob
        {
            [ReadOnly]
            public NativeArray<int2> RefreshOffsets;
            
            public NativeHashSet<int2> ChangedPositions;
            public NativeHashSet<int2> PositionsToRefresh;
            public NativeList<int2> PositionsToRefreshList;
            
            public void Execute()
            {
                foreach (var changedPosition in ChangedPositions)
                {
                    foreach (var refreshOffset in RefreshOffsets)
                    {
                        var pos = changedPosition + refreshOffset;
                        PositionsToRefresh.Add(pos);
                    }
                }
                PositionsToRefresh.ToNativeList(ref PositionsToRefreshList);
                
                PositionsToRefresh.Clear();
                ChangedPositions.Clear();
            }
        }

        [BurstCompile]
        private struct ProcessRulesJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeParallelHashMap<int2, int> IntGrid;
            [ReadOnly]
            public NativeArray<int2> PositionsToRefresh;
            [ReadOnly]
            public NativeArray<RuleBlobReferenceElement> RulesBuffer;
            [ReadOnly]
            public NativeArray<WeightedEntityElement> EntityBuffer;
            
            [ReadOnly]
            public NativeParallelHashMap<int2, Entity> SpawnedEntities;
            [ReadOnly]
            public NativeParallelHashMap<int2, SpriteMesh> RenderedSprites;

            [NativeDisableContainerSafetyRestriction]
            public ParallelList<EntityCommand>.ThreadWriter EntityCommands;
            [NativeDisableContainerSafetyRestriction]
            public ParallelList<SpriteCommand>.ThreadWriter SpriteCommands;
            [NativeDisableContainerSafetyRestriction]
            public ParallelList<PositionToRemove>.ThreadWriter PositionsToRemove;

            public int IntGridHash;
            
            public void Execute(int index)
            {
                var posToRefresh = PositionsToRefresh[index];
                EntityCommands.Begin();
                SpriteCommands.Begin();
                PositionsToRemove.Begin();
                
                foreach (var ruleElement in RulesBuffer)
                {
                    if (!ruleElement.Enabled) continue;
                    ref var rule = ref ruleElement.Value.Value;

                    var appliedRotation = 0;
                    var appliedMirror = new bool2(false, false);
                    var passedCheck = ExecuteRule(ref rule, posToRefresh, 0);
                    
                    if (rule.RuleTransform != RuleTransform.None)
                    {
                        if (!passedCheck && rule.RuleTransform.IsMirroredX())
                        {
                            appliedMirror = new bool2(true, false);
                            passedCheck = ExecuteRule(ref rule, posToRefresh, 1);
                        }
                        if (!passedCheck && rule.RuleTransform.IsMirroredY())
                        {
                            appliedMirror = new bool2(false, true);
                            passedCheck = ExecuteRule(ref rule, posToRefresh, 2);
                        }
                        if (!passedCheck && rule.RuleTransform == RuleTransform.MirrorXY)
                        {
                            appliedMirror = new bool2(true, true);
                            passedCheck = ExecuteRule(ref rule, posToRefresh, 3);
                        }
                        if (!passedCheck && rule.RuleTransform == RuleTransform.Rotated)
                        {
                            for (appliedRotation = 1; appliedRotation < 4; appliedRotation++)
                            {
                                passedCheck = ExecuteRule(ref rule, posToRefresh, appliedRotation);
                                if (passedCheck) break;
                            }
                        }
                    }
                    if (!passedCheck) continue;
                    
                    if (rule.ResultType == RuleResultType.Entity)
                    {
                        var newEntity = EntityBuffer[rule.WeightedEntities[0].EntityBufferIndex].Value;

                        var posOccupied = SpawnedEntities.TryGetValue(posToRefresh, out var presentEntity);
                        if (posOccupied && newEntity == presentEntity) return;
                        if (posOccupied) QueueRemovePos(posToRefresh);
                            
                        EntityCommands.Write(new EntityCommand
                        {
                            SrcEntity = newEntity,
                            Position = posToRefresh,
                            IntGridHash = IntGridHash
                        });
                        return;
                    }
                    else
                    {
                        var newSprite = rule.WeightedSprites[0].SpriteMesh;
                        newSprite.Flip = appliedMirror;
                        newSprite.Rotation = appliedRotation;
                        
                        var posOccupied = RenderedSprites.TryGetValue(posToRefresh, out var presentSprite);
                        if (posOccupied && newSprite.Equals(presentSprite)) return;
                        if (posOccupied) QueueRemovePos(posToRefresh);
                            
                        SpriteCommands.Write(new SpriteCommand
                        {
                            SpriteMesh = newSprite,
                            Position = posToRefresh,
                            IntGridHash = IntGridHash
                        });
                        return;
                    }
                }
                
                if (SpawnedEntities.ContainsKey(posToRefresh) || RenderedSprites.ContainsKey(posToRefresh))
                {
                    QueueRemovePos(posToRefresh);
                }
            }

            private bool ExecuteRule(ref RuleBlob rule, in int2 posToRefresh, int patternOffset)
            {
                var offset = patternOffset * rule.CellsToCheckCount;
                var passedCheck = true;
                
                for (int i = 0; i < rule.CellsToCheckCount; i++)
                {
                    var cell = rule.Cells[offset + i];

                    var posToCheck = posToRefresh + cell.Offset;
                            
                    IntGrid.TryGetValue(posToCheck, out var value);
                    passedCheck = CanPlace(cell, value);

                    if (!passedCheck)
                        break;
                }

                return passedCheck;
            }

            private void QueueRemovePos(int2 posToRefresh)
            {
                PositionsToRemove.Write(new PositionToRemove
                {
                    Position = posToRefresh,
                    IntGridHash = IntGridHash
                });
            }

            private static bool CanPlace(in RuleCell cell, int value)
            {
                if (cell.IntGridValue == -RuleGroup.Rule.AnyIntGridValue) 
                    return false;
                if (cell.IntGridValue < 0 && -cell.IntGridValue == value) 
                    return false;
                if (cell.IntGridValue != RuleGroup.Rule.AnyIntGridValue &&
                         (cell.IntGridValue > 0 && cell.IntGridValue != value)) 
                    return false;
                return true;
            }
        }

        [BurstCompile]
        private struct ClearBuffersJob : IJob
        {
            public ParallelList<EntityCommand> EntityCommands;
            public ParallelList<SpriteCommand> SpriteCommands;
            public ParallelList<PositionToRemove> PositionsToRemoveParallelList;
            
            public void Execute()
            {
                EntityCommands.Clear();
                SpriteCommands.Clear();
                PositionsToRemoveParallelList.Clear();
            }
        }
    }
}