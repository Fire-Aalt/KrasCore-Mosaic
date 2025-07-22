using System.Runtime.CompilerServices;
using BovineLabs.Core.Extensions;
using KrasCore.Mosaic.Data;
using KrasCore.NZCore;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;
using Random = Unity.Mathematics.Random;

namespace KrasCore.Mosaic
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct TilemapCommandBufferSystem : ISystem
    {
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
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            SystemAPI.GetSingleton<TilemapCommandBufferSingleton>().Dispose();
            SystemAPI.GetSingleton<TilemapDataSingleton>().Dispose();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        { 
            var tcb = SystemAPI.GetSingletonRW<TilemapCommandBufferSingleton>().ValueRW; 
            var dataSingleton = SystemAPI.GetSingletonRW<TilemapDataSingleton>().ValueRW;
            
            // Clear last frame data
            dataSingleton.EntityCommands.Clear();
            
            var query = SystemAPI.QueryBuilder()
                .WithAll<TilemapData, RuleBlobReferenceElement, RefreshPositionElement, WeightedEntityElement>()
                .Build();

            var tilemapDataLookup = SystemAPI.GetComponentLookup<TilemapData>(true);
            
            var intGridEntities = query.ToEntityListAsync(state.WorldUpdateAllocator,
                state.Dependency, out var dependency);
            
            state.Dependency = new ClearJob
            {
                IntGridEntities = intGridEntities.AsDeferredJobArray(),
                TilemapDataLookup = tilemapDataLookup,
                IntGridLayers = dataSingleton.IntGridLayers,
                TcbLayers = tcb.Layers,
            }.Schedule(intGridEntities, 1, dependency);
            
            state.Dependency = new ProcessCommandsJob
            {
                IntGridEntities = intGridEntities.AsDeferredJobArray(),
                TilemapDataLookup = tilemapDataLookup,
                RefreshOffsetsBufferLookup = SystemAPI.GetBufferLookup<RefreshPositionElement>(true),
                IntGridLayers = dataSingleton.IntGridLayers,
                TcbLayers = tcb.Layers,
            }.Schedule(intGridEntities, 1, state.Dependency);
            
            state.Dependency = new ProcessRulesJob
            {
                IntGridEntities = intGridEntities.AsDeferredJobArray(),
                TilemapData = tilemapDataLookup,
                RulesBufferLookup = SystemAPI.GetBufferLookup<RuleBlobReferenceElement>(true),
                EntitiesBufferLookup = SystemAPI.GetBufferLookup<WeightedEntityElement>(true),
                IntGridLayers = dataSingleton.IntGridLayers,
                EntityCommands = dataSingleton.EntityCommands.AsThreadWriter(),
                Seed = tcb.GlobalSeed.Value
            }.Schedule(intGridEntities, 1, state.Dependency);
            
            state.Dependency = dataSingleton.EntityCommands.CopyParallelToListSingle(state.Dependency);
        }
        
        [BurstCompile]
        private struct ClearJob : IJobParallelForDefer
        {
            public NativeArray<Entity> IntGridEntities;
            [ReadOnly]
            public ComponentLookup<TilemapData> TilemapDataLookup;
            
            [NativeDisableContainerSafetyRestriction]
            public NativeHashMap<Hash128, TilemapCommandBufferSingleton.IntGridLayer> TcbLayers;
            [NativeDisableParallelForRestriction]
            public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;

            private Entity _intGridEntity;
            
            public void Execute(int index)
            {
                _intGridEntity = IntGridEntities[index];
                
                var intGridHash = TilemapDataLookup[_intGridEntity].IntGridHash;
                
                ref var dataLayer = ref IntGridLayers.GetOrAddRef(intGridHash);
                ref var commandsLayer = ref TcbLayers.GetOrAddRef(intGridHash);

                dataLayer.PositionsToRefresh.Clear();
                dataLayer.RefreshedPositions.Clear();
                
                if (TryClearAll(ref commandsLayer, ref dataLayer)
                    || commandsLayer.SetCommands.Length == 0)
                {
                    dataLayer.Skip = true;
                }
            }
            
            private bool TryClearAll(ref TilemapCommandBufferSingleton.IntGridLayer bufferLayer, ref TilemapDataSingleton.IntGridLayer dataLayer)
            {
                if (!bufferLayer.ClearCommand.Value) return false;

                bufferLayer.SetCommands.Clear();
                bufferLayer.ClearCommand.Value = false;
                
                dataLayer.IntGrid.Clear();
                dataLayer.RuleGrid.Clear();
                dataLayer.RenderedSprites.Clear();
                return true;
            }
        }
        
        
        [BurstCompile]
        private struct ProcessCommandsJob : IJobParallelForDefer
        {
            public NativeArray<Entity> IntGridEntities;
            [ReadOnly]
            public ComponentLookup<TilemapData> TilemapDataLookup;
            [ReadOnly]
            public BufferLookup<RefreshPositionElement> RefreshOffsetsBufferLookup;
            
            [NativeDisableContainerSafetyRestriction]
            public NativeHashMap<Hash128, TilemapCommandBufferSingleton.IntGridLayer> TcbLayers;
            [NativeDisableParallelForRestriction]
            public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;
            
            public void Execute(int index)
            {
                var intGridEntity = IntGridEntities[index];
                
                var intGridHash = TilemapDataLookup[intGridEntity].IntGridHash;
                ref var dataLayer = ref IntGridLayers.GetOrAddRef(intGridHash);
                
                RefreshOffsetsBufferLookup.TryGetBuffer(intGridEntity, out var refreshPositionsBuffer);
                ref var commandsLayer = ref TcbLayers.GetOrAddRef(intGridHash);
                
                // Set int grid values and get changedPositions
                foreach (var command in commandsLayer.SetCommands)
                {
                    SetPosition(ref dataLayer, command.Position, command.IntGridValue);
                    
                    if (dataLayer.DualGrid)
                    {
                        SetPosition(ref dataLayer, command.Position + new int2(1, 0), command.IntGridValue);
                        SetPosition(ref dataLayer, command.Position + new int2(0, 1), command.IntGridValue);
                        SetPosition(ref dataLayer, command.Position + new int2(1, 1), command.IntGridValue);
                    }
                }
                commandsLayer.SetCommands.Clear();
                
                // Convert changed positions set into positions to refresh
                foreach (var changedPosition in dataLayer.ChangedPositions)
                {
                    foreach (var refreshOffset in refreshPositionsBuffer)
                    {
                        var pos = changedPosition + refreshOffset.Value;
                        dataLayer.PositionsToRefresh.Add(pos);
                    }
                }
                dataLayer.ChangedPositions.Clear();
            }

            private void SetPosition(ref TilemapDataSingleton.IntGridLayer layer, int2 position, IntGridValue value)
            {
                layer.ChangedPositions.Add(position);
                layer.IntGrid[position] = value;
            }
        }

        [BurstCompile]
        private struct ProcessRulesJob : IJobParallelForDefer
        {
            public NativeArray<Entity> IntGridEntities;
            [ReadOnly]
            public ComponentLookup<TilemapData> TilemapData;
            [ReadOnly]
            public BufferLookup<RuleBlobReferenceElement> RulesBufferLookup;
            [ReadOnly]
            public BufferLookup<WeightedEntityElement> EntitiesBufferLookup;
            
            [NativeDisableParallelForRestriction]
            public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;
            public ParallelList<EntityCommand>.ThreadWriter EntityCommands;
            
            public uint Seed;

            private Hash128 _intGridHash;
            [ReadOnly]
            private DynamicBuffer<RuleBlobReferenceElement> _rulesBuffer;
            [ReadOnly]
            private DynamicBuffer<WeightedEntityElement> _entityBuffer;

            private UnsafeHashMap<int2, IntGridValue>.ReadOnly _intGridMap;
            
            public void Execute(int index)
            {
                var intGridEntity = IntGridEntities[index];
                
                _intGridHash = TilemapData[intGridEntity].IntGridHash;
                ref var dataLayer = ref IntGridLayers.GetOrAddRef(_intGridHash);

                _intGridMap = dataLayer.IntGrid.AsReadOnly();
                
                RulesBufferLookup.TryGetBuffer(intGridEntity, out _rulesBuffer);
                EntitiesBufferLookup.TryGetBuffer(intGridEntity, out _entityBuffer);

                EntityCommands.Begin();
                foreach (var posToRefresh in dataLayer.PositionsToRefresh)
                {
                    var ruleHashExists = dataLayer.RuleGrid.TryGetValue(posToRefresh, out var ruleHash);
                    
                    var positionStillValid = RefreshPosition(ref dataLayer, posToRefresh, ruleHashExists, ruleHash);

                    if (ruleHashExists && !positionStillValid)
                    {
                        dataLayer.RenderedSprites.Remove(posToRefresh);
                        dataLayer.RuleGrid.Remove(posToRefresh);
                        dataLayer.RefreshedPositions.Add(posToRefresh);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool RefreshPosition(ref TilemapDataSingleton.IntGridLayer dataLayer, int2 posToRefresh,
                bool ruleHashExists, int ruleHash)
            {
                for (var ruleIndex = 0; ruleIndex < _rulesBuffer.Length; ruleIndex++)
                {
                    var ruleElement = _rulesBuffer[ruleIndex];
                    if (!ruleElement.Enabled)
                        continue;
                    
                    ref var rule = ref ruleElement.Value.Value;
                        
                    var random = new Random(MosaicUtils.Hash(Seed, posToRefresh));
                    if (random.NextFloat() * 100f > rule.Chance)
                        continue;

                    if (!ExecuteRules(ref rule, posToRefresh, out var appliedRotation, out var appliedMirror))
                        continue;

                    var currentRuleHash = ruleHashExists ? ruleHash : 0;
                    var newRuleHash = MosaicUtils.Hash(ruleIndex, appliedMirror, appliedRotation);
                        
                    if (currentRuleHash == newRuleHash)
                        return true;
                    
                    dataLayer.RefreshedPositions.Add(posToRefresh);
                    dataLayer.RuleGrid[posToRefresh] = newRuleHash;
                        
                    TryAddEntity(ref rule, ref random, posToRefresh);
                    TryAddSpriteMesh(ref dataLayer, ref rule, ref random, posToRefresh, appliedMirror, appliedRotation);
                    
                    return true;
                }
                return false;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ExecuteRules(ref RuleBlob rule, int2 posToRefresh, out int appliedRotation, out bool2 appliedMirror)
            {
                appliedRotation = 0;
                appliedMirror = new bool2(false, false);
                if (ExecuteRule(ref rule, posToRefresh, 0))
                    return true;
                
                if (rule.RuleTransform == RuleTransform.None)
                    return false;

                if (rule.RuleTransform.IsMirroredX())
                {
                    appliedMirror = new bool2(true, false);
                    if (ExecuteRule(ref rule, posToRefresh, 1)) 
                        return true;
                }

                if (rule.RuleTransform.IsMirroredY())
                {
                    appliedMirror = new bool2(false, true);
                    if (ExecuteRule(ref rule, posToRefresh, 2)) 
                        return true;
                }

                if (rule.RuleTransform == RuleTransform.MirrorXY)
                {
                    appliedMirror = new bool2(true, true);
                    if (ExecuteRule(ref rule, posToRefresh, 3)) 
                        return true;
                }

                if (rule.RuleTransform == RuleTransform.Rotated)
                {
                    for (appliedRotation = 1; appliedRotation < 4; appliedRotation++)
                    {
                        if (ExecuteRule(ref rule, posToRefresh, appliedRotation)) 
                            return true;
                    }
                }
                return false;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void TryAddEntity(ref RuleBlob rule, ref Random random, int2 posToRefresh)
            {
                if (rule.TryGetEntity(ref random, _entityBuffer, out var newEntity))
                {
                    EntityCommands.Write(new EntityCommand
                    {
                        SrcEntity = newEntity,
                        Position = posToRefresh,
                        IntGridHash = _intGridHash
                    });
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void TryAddSpriteMesh(ref TilemapDataSingleton.IntGridLayer dataLayer, ref RuleBlob rule, ref Random random, int2 posToRefresh,
                bool2 appliedMirror, int appliedRotation)
            {
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
                            
                    dataLayer.RenderedSprites[posToRefresh] = newSprite;
                }
                else
                {
                    dataLayer.RenderedSprites.Remove(posToRefresh);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ExecuteRule(ref RuleBlob rule, in int2 posToRefresh, int patternOffset)
            {
                var offset = patternOffset * rule.CellsToCheckCount;
                var passedCheck = true;
                
                for (int i = 0; i < rule.CellsToCheckCount; i++)
                {
                    var cell = rule.Cells[offset + i];

                    var posToCheck = posToRefresh + cell.Offset;
                            
                    _intGridMap.TryGetValue(posToCheck, out var value);
                    passedCheck = CanPlace(cell.IntGridValue, value);

                    if (!passedCheck)
                        break;
                }

                return passedCheck;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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