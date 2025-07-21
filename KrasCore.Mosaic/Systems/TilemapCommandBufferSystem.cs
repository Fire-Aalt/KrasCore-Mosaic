using BovineLabs.Core.Collections;
using BovineLabs.Core.Extensions;
using KrasCore.Mosaic.Data;
using KrasCore.NZCore;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

// ReSharper disable Unity.Entities.MustBeSurroundedWithRefRwRo

namespace KrasCore.Mosaic
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct TilemapCommandBufferSystem : ISystem
    {
        private NativeList<JobHandle> _jobHandles;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TilemapCommandBufferSingleton>();
            state.RequireForUpdate<TilemapDataSingleton>();
            
            state.EntityManager.CreateSingleton(new TilemapCommandBufferSingleton(8, Allocator.Persistent));
            state.EntityManager.CreateSingleton(new TilemapDataSingleton
            {
                IntGridLayers = new NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer>(8, Allocator.Persistent),
                EntityCommands = new ParallelToListMapper<EntityCommand>(256, Allocator.Persistent)
            });

            _jobHandles = new NativeList<JobHandle>(8, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            SystemAPI.GetSingleton<TilemapCommandBufferSingleton>().Dispose();
            SystemAPI.GetSingleton<TilemapDataSingleton>().Dispose();
            _jobHandles.Dispose();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteDependencyBeforeRW<TilemapCommandBufferSingleton>();
            state.EntityManager.CompleteDependencyBeforeRW<TilemapDataSingleton>();
            
            ref var tcb = ref SystemAPI.GetSingletonRW<TilemapCommandBufferSingleton>().ValueRW;
            ref var dataSingleton = ref SystemAPI.GetSingletonRW<TilemapDataSingleton>().ValueRW;
            
            // Clear last frame data
            dataSingleton.EntityCommands.Clear();

            foreach (var (tilemapDataRO, rulesBuffer, refreshPositionsBuffer, entityBuffer) in SystemAPI.Query<RefRO<TilemapData>,
                         DynamicBuffer<RuleBlobReferenceElement>, DynamicBuffer<RefreshPositionElement>, DynamicBuffer<WeightedEntityElement>>())
            {
                var intGridHash = tilemapDataRO.ValueRO.IntGridHash;
                var dataLayer = dataSingleton.IntGridLayers[intGridHash];
                
                // Clear last frame data
                dataLayer.RuleCommands.Clear();
                dataLayer.SpriteCommands.Clear();
                dataLayer.PositionToRemove.Clear();
                
                var commandsLayer = tcb.Layers[intGridHash];
                
                if (TryClearAll(ref commandsLayer, ref dataLayer, refreshPositionsBuffer)
                    || commandsLayer.SetCommands.Length == 0)
                {
                    continue;
                }

                // ProcessCommandsJob
                var jobDependency = new ProcessCommandsJob
                {
                    IntGrid = dataLayer.IntGrid,
                    ChangedPositions = dataLayer.ChangedPositions,
                    SetCommands = commandsLayer.SetCommands,
                    DualGrid = dataLayer.DualGrid
                }.Schedule(state.Dependency);
                
                // Find and filter refresh positions
                jobDependency = new FindRefreshPositionsJob
                {
                    RefreshOffsets = refreshPositionsBuffer.AsNativeArray().Reinterpret<int2>(),
                    ChangedPositions = dataLayer.ChangedPositions,
                    PositionsToRefresh = dataLayer.PositionsToRefresh,
                    PositionsToRefreshList = dataLayer.PositionsToRefreshList
                }.Schedule(jobDependency);
                
                // Apply rules
                jobDependency = new ProcessRulesJob
                {
                    IntGrid = dataLayer.IntGrid,
                    RuleGrid = dataLayer.RuleGrid,
                    PositionsToRefresh = dataLayer.PositionsToRefreshList.AsDeferredJobArray(),
                    EntityBuffer = entityBuffer,
                    RulesBuffer = rulesBuffer,
                    EntityCommands = dataSingleton.EntityCommands.AsThreadWriter(),
                    RuleCommands = dataLayer.RuleCommands.AsThreadWriter(),
                    SpriteCommands = dataLayer.SpriteCommands.AsThreadWriter(),
                    PositionsToRemove = dataLayer.PositionToRemove.AsThreadWriter(),
                    IntGridHash = intGridHash,
                    Seed = tcb.GlobalSeed.Value
                }.Schedule(dataLayer.PositionsToRefreshList, 16, jobDependency);
                
                var handle1 = dataLayer.SpriteCommands.CopyParallelToListSingle(jobDependency);
                var handle2 = dataLayer.PositionToRemove.CopyParallelToListSingle(jobDependency);
                var handle3 = dataLayer.RuleCommands.CopyParallelToListSingle(jobDependency);

                var finalHandle = JobHandle.CombineDependencies(handle1, handle2, handle3);

                finalHandle = new UpdateRuleGridJob
                {
                    RuleGrid = dataLayer.RuleGrid,
                    RuleCommands = dataLayer.RuleCommands.List.AsDeferredJobArray(),
                    PositionsToRemove = dataLayer.PositionToRemove.List.AsDeferredJobArray()
                }.Schedule(finalHandle);
                _jobHandles.Add(finalHandle);
            }

            if (_jobHandles.Length == 0) return;
            var combinedDependency = JobHandle.CombineDependencies(_jobHandles.AsArray());
            
            state.Dependency = dataSingleton.EntityCommands.CopyParallelToListSingle(combinedDependency);
            _jobHandles.Clear();
        }

        private bool TryClearAll(ref TilemapCommandBufferSingleton.IntGridLayer bufferLayer, ref TilemapDataSingleton.IntGridLayer layer,
            in DynamicBuffer<RefreshPositionElement> refreshPositionsBuffer)
        {
            if (!bufferLayer.ClearCommand.Value) return false;
            bufferLayer.ClearCommand.Value = false;

            foreach (var kvp in layer.IntGrid)
            {
                foreach (var refreshOffset in refreshPositionsBuffer)
                {
                    var pos = kvp.Key + refreshOffset.Value;
                    layer.PositionToRemove.List.Add(new RemoveCommand { Position = pos });
                }
            }
            layer.IntGrid.Clear();
            layer.RuleGrid.Clear();
            return true;
        }

        [BurstCompile]
        private struct UpdateRuleGridJob : IJob
        {
            public NativeParallelHashMap<int2, int> RuleGrid;
            
            [ReadOnly]
            public NativeArray<RemoveCommand> PositionsToRemove;
            [ReadOnly]
            public NativeArray<RuleCommand> RuleCommands;
            
            public void Execute()
            {
                foreach (var positionToRemove in PositionsToRemove)
                {
                    RuleGrid.Remove(positionToRemove.Position);
                }
            
                foreach (var command in RuleCommands)
                {
                    RuleGrid[command.Position] = command.AppliedRuleHash;
                }
            }
        }

        [BurstCompile]
        private struct ProcessCommandsJob : IJob
        {
            public ParallelList<SetCommand> SetCommands;
            public NativeParallelHashMap<int2, IntGridValue> IntGrid;
            public NativeHashSet<int2> ChangedPositions;
            
            public bool DualGrid;
            
            public void Execute()
            {
                foreach (var command in SetCommands)
                {
                    SetPosition(command.Position, command.IntGridValue);
                    
                    if (DualGrid)
                    {
                        SetPosition(command.Position + new int2(1, 0), command.IntGridValue);
                        SetPosition(command.Position + new int2(0, 1), command.IntGridValue);
                        SetPosition(command.Position + new int2(1, 1), command.IntGridValue);
                    }
                }
                SetCommands.Clear();
            }

            private void SetPosition(int2 position, IntGridValue value)
            {
                ChangedPositions.Add(position);
                IntGrid[position] = value;
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
            public NativeParallelHashMap<int2, IntGridValue> IntGrid;
            [ReadOnly]
            public NativeParallelHashMap<int2, int> RuleGrid;
            [ReadOnly]
            public NativeArray<int2> PositionsToRefresh;
            
            [ReadOnly]
            [NativeDisableContainerSafetyRestriction]
            public DynamicBuffer<RuleBlobReferenceElement> RulesBuffer;
            [ReadOnly]
            [NativeDisableContainerSafetyRestriction]
            public DynamicBuffer<WeightedEntityElement> EntityBuffer;

            [NativeDisableContainerSafetyRestriction]
            public ParallelList<EntityCommand>.ThreadWriter EntityCommands;
            
            public ParallelList<RuleCommand>.ThreadWriter RuleCommands;
            public ParallelList<SpriteCommand>.ThreadWriter SpriteCommands;
            public ParallelList<RemoveCommand>.ThreadWriter PositionsToRemove;

            public Hash128 IntGridHash;
            public uint Seed;
            
            public void Execute(int index)
            {
                var posToRefresh = PositionsToRefresh[index];
                RuleCommands.Begin();
                EntityCommands.Begin();
                SpriteCommands.Begin();
                PositionsToRemove.Begin();

                for (var ruleIndex = 0; ruleIndex < RulesBuffer.Length; ruleIndex++)
                {
                    var ruleElement = RulesBuffer[ruleIndex];
                    if (!ruleElement.Enabled) continue;
                    ref var rule = ref ruleElement.Value.Value;
                    
                    var random = new Random(MosaicUtils.Hash(Seed, posToRefresh));
                    if (random.NextFloat() * 100f > rule.Chance) continue;

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

                    var ruleExists = RuleGrid.ContainsKey(posToRefresh);
                    var currentRuleHash = ruleExists
                        ? RuleGrid[posToRefresh]
                        : -1;
                    
                    var newRuleHash = Hash(ruleIndex, appliedMirror, appliedRotation);
                    if (currentRuleHash == newRuleHash) return;

                    if (ruleExists) QueueRemovePos(posToRefresh);
                    
                    RuleCommands.Write(new RuleCommand
                    {
                        Position = posToRefresh,
                        AppliedRuleHash = newRuleHash
                    });
                    
                    if (rule.TryGetEntity(ref random, EntityBuffer, out var newEntity))
                    {
                        EntityCommands.Write(new EntityCommand
                        {
                            SrcEntity = newEntity,
                            Position = posToRefresh,
                            IntGridHash = IntGridHash
                        });
                    }
                    if (rule.TryGetSpriteMesh(ref random, out var newSprite))
                    {
                        var resultFlip = new bool2();
                        var resultRotation = 0;
                        if (rule.ResultTransform.HasFlagBurst(ResultTransform.MirrorX))
                        {
                            resultFlip.x = random.NextBool();
                        }
                        if (rule.ResultTransform.HasFlagBurst(ResultTransform.MirrorY))
                        {
                            resultFlip.y = random.NextBool();
                        }
                        if (rule.ResultTransform.HasFlagBurst(ResultTransform.Rotated))
                        {
                            resultRotation = random.NextInt(0, 4);
                        }
                        
                        newSprite.Flip = appliedMirror ^ resultFlip;
                        newSprite.Rotation = appliedRotation + resultRotation;
                        if (newSprite.Rotation > 3)
                            newSprite.Rotation -= 4;
                        
                        SpriteCommands.Write(new SpriteCommand
                        {
                            SpriteMesh = newSprite,
                            Position = posToRefresh
                        });
                    }
                    return;
                }

                if (RuleGrid.ContainsKey(posToRefresh))
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
                    passedCheck = CanPlace(cell.IntGridValue, value);

                    if (!passedCheck)
                        break;
                }

                return passedCheck;
            }

            private void QueueRemovePos(in int2 posToRefresh)
            {
                PositionsToRemove.Write(new RemoveCommand
                {
                    Position = posToRefresh
                });
            }

            private static int Hash(int ruleIndex, in bool2 mirror, int rotation)
            {
                var mirrorHash = (mirror.x ? 1 : 0) | ((mirror.y ? 1 : 0) << 1);
                var hash = ruleIndex;
                hash = (hash * 431) + mirrorHash;
                hash = (hash * 701) + rotation;
                return hash;
            }
            
            private bool CanPlace(short ruleIntGridValue, short valueIntGridValue)
            {
                if (ruleIntGridValue == -RuleGridConsts.AnyIntGridValue) 
                    return false;
                if (ruleIntGridValue < 0 && -ruleIntGridValue == valueIntGridValue) 
                    return false;
                if (ruleIntGridValue != RuleGridConsts.AnyIntGridValue &&
                    (ruleIntGridValue > 0 && ruleIntGridValue != valueIntGridValue)) 
                    return false;
                return true;
            }
        }
    }
}