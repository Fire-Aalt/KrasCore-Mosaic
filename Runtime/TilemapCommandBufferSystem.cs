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
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
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

        public IntGridLayer(int capacity, Allocator allocator)
        {
            IntGrid = new NativeHashMap<int2, int>(capacity, allocator);
            ChangedPositions = new NativeHashSet<int2>(capacity, allocator);
            PositionsToRefresh = new NativeHashSet<int2>(capacity, allocator);
            SpawnedEntities = new NativeParallelMultiHashMap<int2, Entity>(capacity, allocator);
        }

        public void Dispose()
        {
            IntGrid.Dispose();
            ChangedPositions.Dispose();
            PositionsToRefresh.Dispose();
            SpawnedEntities.Dispose();
        }
    }
    
    public partial struct TilemapCommandBufferSystem : ISystem
    {
        private NativeHashMap<int, IntGridLayer> _intGridLayers;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeHashMap<int2, int> IntGrid(int intGridHash) => _intGridLayers[intGridHash].IntGrid;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeHashSet<int2> ChangedPositions(int intGridHash) => _intGridLayers[intGridHash].ChangedPositions;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeHashSet<int2> PositionsToRefresh(int intGridHash) => _intGridLayers[intGridHash].PositionsToRefresh;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeParallelMultiHashMap<int2, Entity> SpawnedEntities(int intGridHash) => _intGridLayers[intGridHash].SpawnedEntities;
            
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateSingleton(new TilemapCommandBufferSingleton
            {
                Tcb = new TilemapCommandBuffer(64, Allocator.Persistent)
            });
            _intGridLayers = new NativeHashMap<int, IntGridLayer>(8, Allocator.Persistent);

            _commandsParallelList = new ParallelList<DeferredCommand>(256, Allocator.Persistent);
            _commandsList = new NativeList<DeferredCommand>(256, Allocator.Persistent);
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
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();
            var tcb = singleton.Tcb;

            // Set NativeHashMap
            while (tcb.SetCommandsQueue.TryDequeue(out var command))
            {
                if (!_intGridLayers.ContainsKey(command.IntGridHash))
                {
                    _intGridLayers.Add(command.IntGridHash, new IntGridLayer(64, Allocator.Persistent));
                }

                var intGrid = IntGrid(command.IntGridHash);
                intGrid[command.Position] = command.IntGridValue;
                ChangedPositions(command.IntGridHash).Add(command.Position);
            }

            // Calculate refresh positions
            foreach (var kvp in _intGridLayers)
            {
                var changedPositions = kvp.Value.ChangedPositions;
                foreach (var changedPos in changedPositions)
                {
                    for (int x = -RuleGroup.Rule.MatrixSizeHalf; x < RuleGroup.Rule.MatrixSizeHalf + 1; x++)
                    {
                        for (int y = -RuleGroup.Rule.MatrixSizeHalf; y < RuleGroup.Rule.MatrixSizeHalf + 1; y++)
                        {
                            PositionsToRefresh(kvp.Key).Add(changedPos + new int2(x, y));
                        }
                    }
                }
                changedPositions.Clear();
            }

            // var toRefreshCountCombined = 0;
            // foreach (var layer in _intGridLayers)
            // {
            //     toRefreshCountCombined += layer.Value.PositionsToRefresh.Count;
            // }
            //
            // _stream = new NativeStream(toRefreshCountCombined, Allocator.TempJob);

            var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            JobHandle finalDep = default;
            
            // Apply rules
            foreach (var (intGridRef, rulesBuffer, entityBuffer) in SystemAPI.Query<RefRO<IntGridReference>, DynamicBuffer<RuleBlobReferenceElement>, DynamicBuffer<WeightedEntityElement>>())
            {
                var intGridHash = intGridRef.ValueRO.Value.GetHashCode();

                var positionsToRefreshTemp = PositionsToRefresh(intGridHash).ToNativeArray(Allocator.TempJob);
                var job = new RuleJob
                {
                    IntGrid = IntGrid(intGridHash),
                    PositionsToRefresh = positionsToRefreshTemp,
                    EntityBuffer = entityBuffer.AsNativeArray(),
                    RulesBuffer = rulesBuffer.AsNativeArray(),
                    Writer = _commandsParallelList.AsThreadWriter()
                };
                var dep = job.ScheduleParallel(positionsToRefreshTemp.Length, 16, state.Dependency);
                positionsToRefreshTemp.Dispose(dep);

                finalDep = dep;
            }
            
            finalDep = _commandsParallelList.CopyToArraySingle(ref _commandsList, finalDep);

            finalDep = new CreateBatchedEntities
            {
                Ecb = ecb,
                LocalTransform = SystemAPI.GetComponentLookup<LocalTransform>(true),
                CommandsList = _commandsList
            }.Schedule(finalDep);

            state.Dependency = new ClearBuffersJob
            {
                IntGridLayers = _intGridLayers,
                CommandsParallelList = _commandsParallelList,
                CommandsList = _commandsList
            }.Schedule(finalDep);
        }
        
        [BurstCompile]
        private struct ClearBuffersJob : IJob
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeHashMap<int, IntGridLayer> IntGridLayers;
            public ParallelList<DeferredCommand> CommandsParallelList;
            public NativeList<DeferredCommand> CommandsList;
            
            public void Execute()
            {
                foreach (var layer in IntGridLayers)
                {
                    layer.Value.PositionsToRefresh.Clear();
                }
                CommandsParallelList.Clear();
                CommandsList.Clear();
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
        private struct RuleJob : IJobFor
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
        }
    }

    public struct TilemapCommandBuffer : IDisposable
    {
        public NativeQueue<SetCommand> SetCommandsQueue;

        public TilemapCommandBuffer(int capacity, Allocator allocator)
        {
            SetCommandsQueue = new NativeQueue<SetCommand>(allocator);
        }
        
        public void Set(IntGrid intGrid, int2 position, int intGridValue)
        {
            SetCommandsQueue.Enqueue(new SetCommand { IntGridHash = intGrid.GetHashCode(), Position = position, IntGridValue = intGridValue });
        }
        
        public void Set(UnityObjectRef<IntGrid> intGrid, int2 position, int intGridValue)
        {
            SetCommandsQueue.Enqueue(new SetCommand { IntGridHash = intGrid.GetHashCode(), Position = position, IntGridValue = intGridValue });
        }
            
        public struct SetCommand
        {
            public int IntGridHash;
            public int2 Position;
            public int IntGridValue;
        }

        public void Dispose()
        {
            SetCommandsQueue.Dispose();
        }
    }
}