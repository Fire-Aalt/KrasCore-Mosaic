using System;
using Game;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
    
    public partial struct TilemapCommandBufferSystem : ISystem
    {
        private NativeHashMap<int, NativeHashMap<int2, int>> _intGrid;
        
        private NativeHashMap<int, NativeHashSet<int2>> _changedPositions;
        private NativeHashMap<int, NativeHashSet<int2>> _positionsToRefresh;
            
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateSingleton(new TilemapCommandBufferSingleton
            {
                Tcb = new TilemapCommandBuffer(64, Allocator.Persistent)
            });
            _intGrid = new NativeHashMap<int, NativeHashMap<int2, int>>(64, Allocator.Persistent);
            _changedPositions = new NativeHashMap<int, NativeHashSet<int2>>(64, Allocator.Persistent);
            _positionsToRefresh = new NativeHashMap<int, NativeHashSet<int2>>(64, Allocator.Persistent);
            
            state.RequireForUpdate<TilemapCommandBufferSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<TilemapCommandBufferSingleton>();
            var prefabDatabase = SystemAPI.GetSingleton<PrefabDatabase>();
            var tcb = singleton.Tcb;

            // Set NativeHashMap
            while (tcb.SetCommandsQueue.TryDequeue(out var command))
            {
                _intGrid[command.Position] = command.IntGridValue;
                _changedPositions.Add(command.Position);
            }

            // Calculate refresh positions
            foreach (var changedPos in _changedPositions)
            {
                for (int x = -RuleGroup.Rule.MatrixSizeHalf; x < RuleGroup.Rule.MatrixSizeHalf + 1; x++)
                {
                    for (int y = -RuleGroup.Rule.MatrixSizeHalf; y < RuleGroup.Rule.MatrixSizeHalf + 1; y++)
                    {
                        _positionsToRefresh.Add(changedPos + new int2(x, y));
                    }
                }
            }
            _changedPositions.Clear();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Apply rules
            foreach (var (rulesBuffer, entityBuffer) in SystemAPI.Query<DynamicBuffer<RuleBlobReferenceElement>, DynamicBuffer<WeightedEntityElement>>())
            {
                foreach (var posToRefresh in _positionsToRefresh)
                {
                    foreach (var ruleElement in rulesBuffer)
                    {
                        if (!ruleElement.Enabled) continue;

                        ref var rule = ref ruleElement.Value.Value;
                        var passed = true;

                        for (int i = 0; i < rule.Cells.Length; i++)
                        {
                            var cell = rule.Cells[i];

                            var posToCheck = posToRefresh + cell.Offset;
                            
                            _intGrid.TryGetValue(posToCheck, out var value);
                            passed = CanPlace(cell, value);

                            if (!passed)
                                break;
                        }
                        

                        if (passed)
                        {
                            var srcEntity = entityBuffer[rule.WeightedEntities[0].EntityBufferIndex].Value;
                            var entity = ecb.Instantiate(srcEntity);
                            var srcTransform = SystemAPI.GetComponent<LocalTransform>(srcEntity);
                            ecb.SetComponent(entity, new LocalTransform
                            {
                                Position = new float3(posToRefresh.x, 0f, posToRefresh.y) + srcTransform.Position, 
                                Scale = srcTransform.Scale,
                                Rotation = srcTransform.Rotation
                            });
                            break;
                        }
                    }
                }
            }
            _positionsToRefresh.Clear();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
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
            _intGrid.Dispose();
            _changedPositions.Dispose();
            _positionsToRefresh.Dispose();
        }
    }

    public struct TilemapCommandBuffer : IDisposable
    {
        public NativeQueue<SetCommand> SetCommandsQueue;

        public TilemapCommandBuffer(int capacity, Allocator allocator)
        {
            SetCommandsQueue = new NativeQueue<SetCommand>(allocator);
        }
        
        public void Set(UnityObjectRef<IntGrid> intGrid, int2 position, int intGridValue)
        {
            SetCommandsQueue.Enqueue(new SetCommand { IntGridRef = intGrid, Position = position, IntGridValue = intGridValue });
        }
            
        public struct SetCommand
        {
            public UnityObjectRef<IntGrid> IntGridRef;
            public int2 Position;
            public int IntGridValue;
        }

        public void Dispose()
        {
            SetCommandsQueue.Dispose();
        }
    }
}