using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct TilemapEntityInitializationSystem : ISystem
    {
        private NativeList<EntityCommand> _commandsList;
        private NativeHashMap<int, IntGridLayer> _intGridLayers;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TilemapDataSingleton>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            _commandsList = singleton.EntityCommands.List;
            _intGridLayers = singleton.IntGridLayers;
            
            if (_commandsList.Length == 0) return;
            _commandsList.Sort(new DeferredCommandComparer());
            
            var beginBatchIndex = 0;
            for (int i = 0; i < _commandsList.Length - 1; i++)
            {
                var currentCommand = _commandsList[i];
                var nextCommand = _commandsList[i + 1];
                if (currentCommand.SrcEntity == nextCommand.SrcEntity) continue;
                
                UploadBatch(ref state, beginBatchIndex, i, currentCommand.SrcEntity);
                beginBatchIndex = i + 1;
            }
            UploadBatch(ref state, beginBatchIndex, _commandsList.Length, _commandsList[^1].SrcEntity);
        }
        
        private void UploadBatch(ref SystemState state, int beginIndex, int endIndex, in Entity srcEntity)
        {
            var length = endIndex - beginIndex;
            if (length <= 0) return;
            
            var srcTransform = state.EntityManager.GetComponentData<LocalTransform>(srcEntity);
            
            var instances = new NativeArray<Entity>(length, Allocator.Temp);
            state.EntityManager.Instantiate(srcEntity, instances);
                
            for (var i = 0; i < instances.Length; i++)
            {
                var currentCommand = _commandsList[beginIndex + i];
                var instance = instances[i];
                    
                var position = currentCommand.Position;

                var layer = _intGridLayers[currentCommand.IntGridHash];
                
                state.EntityManager.SetComponentData(instance, new LocalTransform
                {
                    Position = MosaicUtils.ApplySwizzle(position, layer.TilemapData.Swizzle) * layer.TilemapData.GridData.CellSize + srcTransform.Position, 
                    Scale = srcTransform.Scale,
                    Rotation = srcTransform.Rotation
                });
                
                layer.SpawnedEntities.Add(position, instance);
            }
        }
    }
}