using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KrasCore.Mosaic;
using KrasCore.NZCore;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

[assembly: RegisterGenericJobType(typeof(ParallelList<TilemapCommandBufferSystem.DeferredCommand>.UnsafeParallelListToArraySingleThreaded))]

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
    
    public partial struct TilemapCommandBufferSystem : ISystem
    {
        private NativeHashMap<int, IntGridLayer> _intGridLayers;
            
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
            state.EntityManager.CreateSingleton(new TilemapCommandBufferSingleton
            {
                Tcb = new TilemapCommandBuffer(64, Allocator.Persistent)
            });

            _intGridLayers = new NativeHashMap<int, IntGridLayer>(8, Allocator.Persistent);
            _commandsParallelList = new ParallelList<DeferredCommand>(256, Allocator.Persistent);
            _commandsList = new NativeList<DeferredCommand>(256, Allocator.Persistent);
            _jobHandles = new NativeList<JobHandle>(8, Allocator.Persistent);
            state.RequireForUpdate<TilemapCommandBufferSingleton>();
        }

        public struct DeferredCommand
        {
            public Entity SrcEntity;
            public int2 Position;
        }
        
        private struct DeferredCommandComparer : IComparer<DeferredCommand>
        {
            public int Compare(DeferredCommand x, DeferredCommand y)
            {
                return x.SrcEntity.Index.CompareTo(y.SrcEntity.Index);
            }
        }

        private ParallelList<DeferredCommand> _commandsParallelList;
        private NativeList<DeferredCommand> _commandsList;
        private NativeList<JobHandle> _jobHandles;

        private static readonly ProfilerMarker ProcessCommands = new ProfilerMarker("Process Commands");
        private static readonly ProfilerMarker ScheduleJobs = new ProfilerMarker("Schedule Jobs");
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();
            var tcb = singleton.Tcb;

            var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            
            foreach (var (intGridRef, rulesBuffer, refreshPositionsBuffer, entityBuffer) in SystemAPI.Query<RefRO<IntGridReference>, DynamicBuffer<RuleBlobReferenceElement>, DynamicBuffer<RefreshPositionElement>, DynamicBuffer<WeightedEntityElement>>())
            {
                var intGridHash = intGridRef.ValueRO.Value.GetHashCode();

                if (!_intGridLayers.ContainsKey(intGridHash))
                {
                    _intGridLayers.Add(intGridHash, new IntGridLayer(64, Allocator.Persistent));
                }
                var layer = _intGridLayers[intGridHash];
                
                var commandsQueue = tcb.Layers[intGridHash];
                if (commandsQueue.IsEmpty()) continue;
                
                // ProcessCommandsJob
                var processCommandsJob = new ProcessCommandsJob
                {
                    IntGrid = layer.IntGrid,
                    ChangedPositions = layer.ChangedPositions,
                    SetCommandsQueue = tcb.Layers[intGridHash]
                };
                var jobDependency = processCommandsJob.Schedule();
                //
                // Find and filter refresh positions
                var findRefreshPositionsJob = new FindRefreshPositionsJob
                {
                    RefreshOffsets = refreshPositionsBuffer.AsNativeArray().Reinterpret<int2>(),
                    ChangedPositions = layer.ChangedPositions,
                    PositionsToRefresh = layer.PositionsToRefresh,
                    PositionsToRefreshList = layer.PositionsToRefreshList
                };
                jobDependency = findRefreshPositionsJob.Schedule(jobDependency);
                
                // Apply rules
                var processRulesJob = new ProcessRulesJob
                {
                    IntGrid = layer.IntGrid,
                    PositionsToRefresh = layer.PositionsToRefreshList.AsDeferredJobArray(),
                    EntityBuffer = entityBuffer.AsNativeArray(),
                    RulesBuffer = rulesBuffer.AsNativeArray(),
                    Writer = _commandsParallelList.AsThreadWriter()
                };
                
                jobDependency = JobHandle.CombineDependencies(state.Dependency, jobDependency);
                _jobHandles.Add(processRulesJob.Schedule(layer.PositionsToRefreshList, 16, jobDependency));
            }

            var combinedDependency = JobHandle.CombineDependencies(_jobHandles.AsArray());
            if (combinedDependency == default)
            {
                return;
            }
            
            combinedDependency = _commandsParallelList.CopyToArraySingle(ref _commandsList, combinedDependency);

            combinedDependency = new CreateBatchedEntities
            {
                Ecb = ecb,
                LocalTransform = SystemAPI.GetComponentLookup<LocalTransform>(true),
                CommandsList = _commandsList
            }.Schedule(combinedDependency);

            state.Dependency = new ClearBuffersJob
            {
                CommandsParallelList = _commandsParallelList,
                CommandsList = _commandsList
            }.Schedule(combinedDependency);
            
            _jobHandles.Clear();
        }
        
        [BurstCompile]
        private struct ClearBuffersJob : IJob
        {
            public ParallelList<DeferredCommand> CommandsParallelList;
            public NativeList<DeferredCommand> CommandsList;
            
            public void Execute()
            {
                CommandsParallelList.Clear();
                CommandsList.Clear();
            }
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
        private struct CreateBatchedEntities : IJob
        {
            [ReadOnly]
            public ComponentLookup<LocalTransform> LocalTransform;
            public NativeList<DeferredCommand> CommandsList;
            public EntityCommandBuffer Ecb;
            
            public void Execute()
            {
                if (CommandsList.Length == 0) return;
                
                CommandsList.Sort(new DeferredCommandComparer());
                
                var beginBatchIndex = 0;
                for (int i = 0; i < CommandsList.Length - 1; i++)
                {
                    var currentCommand = CommandsList[i];
                    var nextCommand = CommandsList[i + 1];
                    if (currentCommand.SrcEntity == nextCommand.SrcEntity) continue;
                
                    UploadBatch(ref Ecb, beginBatchIndex, i, currentCommand.SrcEntity);
                    beginBatchIndex = i + 1;
                }
                UploadBatch(ref Ecb, beginBatchIndex, CommandsList.Length, CommandsList[^1].SrcEntity);
            }
            
            private void UploadBatch(ref EntityCommandBuffer ecb, int beginIndex, int endIndex, Entity srcEntity)
            {
                var length = endIndex - beginIndex;
                if (length <= 0) return;
                
                var srcTransform = LocalTransform.GetRefRO(srcEntity).ValueRO;
                
                var instances = new NativeArray<Entity>(length, Allocator.Temp);
                ecb.Instantiate(srcEntity, instances);
                
                for (var i = 0; i < instances.Length; i++)
                {
                    var currentCommand = CommandsList[beginIndex + i];
                    var instance = instances[i];
                    
                    var position = currentCommand.Position;
                    
                    ecb.SetComponent(instance, new LocalTransform
                    {
                        Position = new float3(position.x, 0f, position.y) + srcTransform.Position, 
                        Scale = srcTransform.Scale,
                        Rotation = srcTransform.Rotation
                    });
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

            public ParallelList<DeferredCommand>.ThreadWriter Writer;
            
            public void Execute(int index)
            {
                var posToRefresh = PositionsToRefresh[index];
                Writer.Begin();
                
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
                        Writer.Write(new DeferredCommand
                        {
                            SrcEntity = EntityBuffer[rule.WeightedEntities[0].EntityBufferIndex].Value,
                            Position = posToRefresh
                        });

                        break;
                    }
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
        public void OnDestroy(ref SystemState state)
        {
            SystemAPI.GetSingleton<TilemapCommandBufferSingleton>().Dispose();
            foreach (var layer in _intGridLayers)
            {
                layer.Value.Dispose();
            }
            _commandsParallelList.Dispose();
            _commandsList.Dispose();
            _jobHandles.Dispose();
        }
    }

    public struct TilemapCommandBuffer : IDisposable
    {
        public NativeHashMap<int, NativeQueue<SetCommand>> Layers;

        private readonly Allocator _allocator;
        
        public TilemapCommandBuffer(int capacity, Allocator allocator)
        {
            _allocator = allocator;
            Layers = new NativeHashMap<int, NativeQueue<SetCommand>>(capacity, allocator);
        }
        
        public void Set(IntGrid intGrid, int2 position, int intGridValue)
        {
            Set(intGrid.GetHashCode(), position, intGridValue);
        }
        
        public void Set(UnityObjectRef<IntGrid> intGrid, int2 position, int intGridValue)
        {
            Set(intGrid.GetHashCode(), position, intGridValue);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int intGridHash, int2 position, int intGridValue)
        {
            if (!Layers.ContainsKey(intGridHash))
            {
                Layers[intGridHash] = new NativeQueue<SetCommand>(_allocator);
            }
            Layers[intGridHash].Enqueue(new SetCommand { Position = position, IntGridValue = intGridValue });
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
        }
    }
}