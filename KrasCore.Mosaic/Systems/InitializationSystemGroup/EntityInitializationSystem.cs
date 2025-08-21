using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;

namespace KrasCore.Mosaic
{
    [UpdateInGroup(typeof(TilemapInitializationSystemGroup))]
    public partial struct EntityInitializationSystem : ISystem
    {
        private NativeList<EntityCommand> _commandsList;
        private NativeHashMap<Hash128, RuleEngineSystem.IntGridLayer> _intGridLayers;
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteDependencyBeforeRO<RuleEngineSystem.Singleton>();
            var dataSingleton = SystemAPI.GetSingletonRW<RuleEngineSystem.Singleton>().ValueRW;
            _commandsList = dataSingleton.EntityCommands.List;
            _intGridLayers = dataSingleton.IntGridLayers;
            
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
            UploadBatch(ref state, beginBatchIndex, _commandsList.Length - 1, _commandsList[^1].SrcEntity);
            
            dataSingleton.EntityCommands.Clear();
        }
        
        private void UploadBatch(ref SystemState state, int beginIndex, int endIndex, in Entity srcEntity)
        {
            var length = endIndex - beginIndex + 1;
            if (length <= 0) return;
            
            var srcTransform = state.EntityManager.GetComponentData<LocalTransform>(srcEntity);
            
            var instances = new NativeArray<Entity>(length, Allocator.Temp);
            state.EntityManager.Instantiate(srcEntity, instances);
                
            for (var i = 0; i < instances.Length; i++)
            {
                var currentCommand = _commandsList[beginIndex + i];
                var instance = instances[i];
                    
                var position = currentCommand.Position;

                ref var dataLayer = ref _intGridLayers.GetValueAsRef(currentCommand.IntGridHash);
                var rendererData = state.EntityManager.GetComponentData<TilemapRendererData>(dataLayer.IntGridEntity);
                
                state.EntityManager.SetComponentData(instance, new LocalTransform
                {
                    Position = MosaicUtils.ToWorldSpace(position, rendererData) + srcTransform.Position, 
                    Scale = srcTransform.Scale,
                    Rotation = srcTransform.Rotation
                });
                
                dataLayer.SpawnedEntities[position] = instance;
            }
        }
    }
}