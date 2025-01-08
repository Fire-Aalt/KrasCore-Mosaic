using System;
using KrasCore.Mosaic;
using KrasCore.NZCore;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[assembly: RegisterGenericJobType(typeof(ParallelList<DeferredCommand>.UnsafeParallelListToArraySingleThreaded))]
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

        public NativeList<DeferredCommand> CommandsList;
        public NativeList<PositionToRemove> PositionToRemoveList;
        
        public void Dispose()
        {
            foreach (var layer in IntGridLayers)
            {
                layer.Value.Dispose();
            }
            IntGridLayers.Dispose();
            CommandsList.Dispose();
            PositionToRemoveList.Dispose();
        }
    }

    public struct IntGridLayer : IDisposable
    {
        public NativeHashMap<int2, int> IntGrid;
        public NativeHashSet<int2> ChangedPositions;
        public NativeHashSet<int2> PositionsToRefresh;
        public NativeParallelMultiHashMap<int2, Entity> SpawnedEntities;

        public NativeList<int2> PositionsToRefreshList;

        public IntGridLayer(int capacity, Allocator allocator)
        {
            IntGrid = new NativeHashMap<int2, int>(capacity, allocator);
            ChangedPositions = new NativeHashSet<int2>(capacity, allocator);
            PositionsToRefresh = new NativeHashSet<int2>(capacity, allocator);
            SpawnedEntities = new NativeParallelMultiHashMap<int2, Entity>(capacity, allocator);
            
            PositionsToRefreshList = new NativeList<int2>(capacity, allocator);
        }

        public void Dispose()
        {
            IntGrid.Dispose();
            ChangedPositions.Dispose();
            PositionsToRefresh.Dispose();
            PositionsToRefreshList.Dispose();
            SpawnedEntities.Dispose();
        }
    }

    public struct PositionToRemove
    {
        public int2 Position;
        public int IntGridHash;
    }
    
    public partial struct TilemapCommandBufferSystem : ISystem
    {
        private ParallelList<DeferredCommand> _commandsParallelList;
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
                CommandsList = new NativeList<DeferredCommand>(64, Allocator.Persistent),
                PositionToRemoveList = new NativeList<PositionToRemove>(64, Allocator.Persistent)
            });

            _commandsParallelList = new ParallelList<DeferredCommand>(256, Allocator.Persistent);
            _entityPositionsToRemove = new ParallelList<PositionToRemove>(256, Allocator.Persistent);
            _jobHandles = new NativeList<JobHandle>(8, Allocator.Persistent);
            state.RequireForUpdate<TilemapCommandBufferSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            SystemAPI.GetSingleton<TilemapCommandBufferSingleton>().Dispose();
            SystemAPI.GetSingleton<TilemapDataSingleton>().Dispose();
            _commandsParallelList.Dispose();
            _entityPositionsToRemove.Dispose();
            _jobHandles.Dispose();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>().Tcb;
            var singleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            var intGridLayers = singleton.IntGridLayers;
            
            foreach (var (intGridRef, rulesBuffer, refreshPositionsBuffer, entityBuffer) in SystemAPI.Query<RefRO<IntGridReference>, DynamicBuffer<RuleBlobReferenceElement>, DynamicBuffer<RefreshPositionElement>, DynamicBuffer<WeightedEntityElement>>())
            {
                var intGridHash = intGridRef.ValueRO.Value.GetHashCode();

                if (!intGridLayers.ContainsKey(intGridHash))
                {
                    intGridLayers.Add(intGridHash, new IntGridLayer(64, Allocator.Persistent));
                }
                var layer = intGridLayers[intGridHash];
                
                var commandsQueue = tcb.Layers[intGridHash];
                if (commandsQueue.IsEmpty()) continue;
                
                // ProcessCommandsJob
                var jobDependency = new ProcessCommandsJob
                {
                    IntGrid = layer.IntGrid,
                    ChangedPositions = layer.ChangedPositions,
                    SetCommandsQueue = tcb.Layers[intGridHash]
                }.Schedule();
                
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
                    Commands = _commandsParallelList.AsThreadWriter(),
                    EntityPositionsToRemove = _entityPositionsToRemove.AsThreadWriter(),
                    IntGridHash = intGridHash
                };
                
                jobDependency = JobHandle.CombineDependencies(state.Dependency, jobDependency);
                _jobHandles.Add(processRulesJob.Schedule(layer.PositionsToRefreshList, 16, jobDependency));
            }

            if (_jobHandles.Length == 0) return;
            var combinedDependency = JobHandle.CombineDependencies(_jobHandles.AsArray());
            
            var handle1 = _commandsParallelList.CopyToArraySingle(ref singleton.CommandsList, combinedDependency);
            var handle2 = _entityPositionsToRemove.CopyToArraySingle(ref singleton.PositionToRemoveList, combinedDependency);

            state.Dependency = new ClearBuffersJob
            {
                CommandsParallelList = _commandsParallelList,
                PositionsToRemoveParallelList = _entityPositionsToRemove
            }.Schedule(JobHandle.CombineDependencies(handle1, handle2));
            
            _jobHandles.Clear();
        }

        [BurstCompile]
        private struct ProcessCommandsJob : IJob
        {
            public NativeQueue<TilemapCommandBuffer.SetCommand> SetCommandsQueue;
            public NativeHashMap<int2, int> IntGrid;
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
                        PositionsToRefresh.Add(changedPosition + refreshOffset);
                    }
                }
                PositionsToRefresh.ToNativeList(ref PositionsToRefreshList);
                ChangedPositions.Clear();
            }
        }

        [BurstCompile]
        private struct ProcessRulesJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeHashMap<int2, int> IntGrid;
            [ReadOnly]
            public NativeArray<int2> PositionsToRefresh;
            [ReadOnly]
            public NativeArray<RuleBlobReferenceElement> RulesBuffer;
            [ReadOnly]
            public NativeArray<WeightedEntityElement> EntityBuffer;
            [ReadOnly]
            public NativeParallelMultiHashMap<int2, Entity> SpawnedEntities;

            public ParallelList<DeferredCommand>.ThreadWriter Commands;
            public ParallelList<PositionToRemove>.ThreadWriter EntityPositionsToRemove;

            public int IntGridHash;
            
            public void Execute(int index)
            {
                var posToRefresh = PositionsToRefresh[index];
                Commands.Begin();
                EntityPositionsToRemove.Begin();

                var rulePassed = false;
                
                foreach (var ruleElement in RulesBuffer)
                {
                    if (!ruleElement.Enabled) continue;

                    ref var rule = ref ruleElement.Value.Value;
                    var passed = true;

                    for (int i = 0; i < rule.Cells.Length; i++)
                    {
                        var cell = rule.Cells[i];

                        var posToCheck = posToRefresh + cell.Offset;
                            
                        IntGrid.TryGetValue(posToCheck, out var value);
                        passed = CanPlace(cell, value);

                        if (!passed)
                            break;
                    }
                    
                    if (passed)
                    {
                        Commands.Write(new DeferredCommand
                        {
                            SrcEntity = EntityBuffer[rule.WeightedEntities[0].EntityBufferIndex].Value,
                            Position = posToRefresh,
                            IntGridHash = IntGridHash
                        });
                        rulePassed = true;
                        break;
                    }
                }

                if (!rulePassed && SpawnedEntities.ContainsKey(posToRefresh))
                {
                    EntityPositionsToRemove.Write(new PositionToRemove
                    {
                        Position = posToRefresh,
                        IntGridHash = IntGridHash
                    });
                }
            }
            
            private static bool CanPlace(RuleCell cell, int value)
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
            public ParallelList<DeferredCommand> CommandsParallelList;
            public ParallelList<PositionToRemove> PositionsToRemoveParallelList;
            
            public void Execute()
            {
                CommandsParallelList.Clear();
                PositionsToRemoveParallelList.Clear();
            }
        }
    }
}