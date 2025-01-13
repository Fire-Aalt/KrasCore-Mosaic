using KrasCore.Mosaic;
using KrasCore.NZCore;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[assembly: RegisterGenericJobType(typeof(ParallelList<RuleCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<EntityCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<SpriteCommand>.UnsafeParallelListToArraySingleThreaded))]
[assembly: RegisterGenericJobType(typeof(ParallelList<PositionToRemove>.UnsafeParallelListToArraySingleThreaded))]

namespace KrasCore.Mosaic
{
    public struct PositionToRemove
    {
        public int2 Position;
    }

    public struct SpriteCommand
    {
        public SpriteMesh SpriteMesh;
        public int2 Position;
    }
    
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct TilemapCommandBufferSystem : ISystem
    {
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
                IntGridLayers = new NativeHashMap<int, TilemapDataSingleton.IntGridLayer>(8, Allocator.Persistent),
                EntityCommands = new ParallelToListMapper<EntityCommand>(256, Allocator.Persistent)
            });

            _jobHandles = new NativeList<JobHandle>(8, Allocator.Persistent);
            state.RequireForUpdate<TilemapCommandBufferSingleton>();
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
            var tcb = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>().Tcb;
            ref var singleton = ref SystemAPI.GetSingletonRW<TilemapDataSingleton>().ValueRW;
            singleton.JobHandle = default;
            var intGridLayers = singleton.IntGridLayers;
            
            // Clear last frame data
            singleton.EntityCommands.Clear();
            
            foreach (var (tilemapDataRO, transformRO, rulesBuffer, refreshPositionsBuffer, entityBuffer) in SystemAPI.Query<RefRO<TilemapData>, RefRO<LocalTransform>, DynamicBuffer<RuleBlobReferenceElement>, DynamicBuffer<RefreshPositionElement>, DynamicBuffer<WeightedEntityElement>>())
            {
                var intGridHash = tilemapDataRO.ValueRO.IntGridReference.GetHashCode();

                if (!intGridLayers.ContainsKey(intGridHash))
                {
                    intGridLayers.Add(intGridHash, new TilemapDataSingleton.IntGridLayer(tilemapDataRO.ValueRO, 64, Allocator.Persistent));
                }
                var layer = intGridLayers[intGridHash];
                layer.TilemapTransform = transformRO.ValueRO;
                layer.TilemapData = tilemapDataRO.ValueRO;
                intGridLayers[intGridHash] = layer;

                // Clear last frame data
                layer.RuleCommands.Clear();
                layer.SpriteCommands.Clear();
                layer.PositionToRemove.Clear();
                
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
                jobDependency = new ProcessRulesJob
                {
                    IntGrid = layer.IntGrid,
                    RuleGrid = layer.RuleGrid,
                    PositionsToRefresh = layer.PositionsToRefreshList.AsDeferredJobArray(),
                    EntityBuffer = entityBuffer,
                    RulesBuffer = rulesBuffer,
                    EntityCommands = singleton.EntityCommands.AsThreadWriter(),
                    RuleCommands = layer.RuleCommands.AsThreadWriter(),
                    SpriteCommands = layer.SpriteCommands.AsThreadWriter(),
                    PositionsToRemove = layer.PositionToRemove.AsThreadWriter(),
                    IntGridHash = intGridHash
                }.Schedule(layer.PositionsToRefreshList, 16, jobDependency);
                
                var handle1 = layer.SpriteCommands.CopyParallelToList(jobDependency);
                var handle2 = layer.PositionToRemove.CopyParallelToList(jobDependency);
                var handle3 = layer.RuleCommands.CopyParallelToList(jobDependency);

                var finalHandle = JobHandle.CombineDependencies(handle1, handle2, handle3);

                finalHandle = new UpdateRuleGridJob
                {
                    RuleGrid = layer.RuleGrid,
                    RuleCommands = layer.RuleCommands.List.AsDeferredJobArray(),
                    PositionsToRemove = layer.PositionToRemove.List.AsDeferredJobArray()
                }.Schedule(finalHandle);
                _jobHandles.Add(finalHandle);
            }

            if (_jobHandles.Length == 0) return;
            var combinedDependency = JobHandle.CombineDependencies(_jobHandles.AsArray());
            
            state.Dependency = singleton.EntityCommands.CopyParallelToList(combinedDependency);
            
            singleton.JobHandle = state.Dependency;
            _jobHandles.Clear();
        }

        [BurstCompile]
        private struct UpdateRuleGridJob : IJob
        {
            public NativeParallelHashMap<int2, int> RuleGrid;
            
            [ReadOnly]
            public NativeArray<PositionToRemove> PositionsToRemove;
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
            public ParallelList<PositionToRemove>.ThreadWriter PositionsToRemove;

            public int IntGridHash;
            
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
                    
                    // TODO: add a check if the newSrcEntity == oldSrcEntity to remove redundant memcpy
                    if (rule.TryGetEntity(EntityBuffer, out var newEntity))
                    {
                        EntityCommands.Write(new EntityCommand
                        {
                            SrcEntity = newEntity,
                            Position = posToRefresh,
                            IntGridHash = IntGridHash
                        });
                    }
                    if (rule.TryGetSpriteMesh(out var newSprite))
                    {
                        newSprite.Flip = appliedMirror;
                        newSprite.Rotation = appliedRotation;
                        
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
                    passedCheck = CanPlace(cell, value);

                    if (!passedCheck)
                        break;
                }

                return passedCheck;
            }

            private void QueueRemovePos(in int2 posToRefresh)
            {
                PositionsToRemove.Write(new PositionToRemove
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
    }
}