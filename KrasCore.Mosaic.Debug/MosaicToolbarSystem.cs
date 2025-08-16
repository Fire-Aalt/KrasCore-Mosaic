using BovineLabs.Anchor.Toolbar;
using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using Random = Unity.Mathematics.Random;

#if BL_QUILL
using BovineLabs.Quill;
#endif

namespace KrasCore.Mosaic.Debug
{
    [UpdateInGroup(typeof(ToolbarSystemGroup))]
    public partial struct MosaicToolbarSystem : ISystem, ISystemStartStop
    {
        private ToolbarHelper<MosaicToolbarView, MosaicToolbarViewModel, MosaicToolbarViewModel.Data> _toolbar;
        
        private NativeList<MosaicToolbarViewModel.Data.IntGridName> _intGridsBuffer;
        
        public void OnCreate(ref SystemState state)
        {
            _toolbar = new ToolbarHelper<MosaicToolbarView, MosaicToolbarViewModel, MosaicToolbarViewModel.Data>(ref state, "Mosaic");
            
            _intGridsBuffer = new NativeList<MosaicToolbarViewModel.Data.IntGridName>(Allocator.Persistent);
            
            state.RequireForUpdate<TilemapDataSingleton>();
        }
        
        public void OnDestroy(ref SystemState state)
        {
            _intGridsBuffer.Dispose();
        }
        
        public void OnStartRunning(ref SystemState state)
        {
            _toolbar.Load();
        }

        public void OnStopRunning(ref SystemState state)
        {
            _toolbar.Unload();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var data = ref _toolbar.Binding;
            if (_toolbar.IsVisible())
            {
                _intGridsBuffer.Clear();
                foreach (var tilemapDataRO in SystemAPI.Query<RefRO<TilemapData>>())
                {
                    _intGridsBuffer.Add(new MosaicToolbarViewModel.Data.IntGridName
                    {
                        IntGridHash = tilemapDataRO.ValueRO.IntGridHash,
                        Name = tilemapDataRO.ValueRO.DebugName,
                    });
                }
                _intGridsBuffer.Sort();

                data.IntGrids = _intGridsBuffer;
            }
            
            var singleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
            
#if BL_QUILL
            var drawer = SystemAPI.GetSingleton<DrawSystem.Singleton>().CreateDrawer();
            if (!drawer.IsEnabled || data.IntGridValues.Value.Length == 0)
            {
                return;
            }
            
            state.Dependency = new DrawIntGridJob
            {
                Drawer = drawer,
                TilemapRendererDataLookup = SystemAPI.GetComponentLookup<TilemapRendererData>(true),
                TilemapTerrainLayerLookup = SystemAPI.GetComponentLookup<TilemapTerrainLayer>(true),
                SelectedIndices = data.IntGridValues.Value.ToArray(state.WorldUpdateAllocator),
                IntGridsBuffer = _intGridsBuffer.AsArray(),
                IntGridLayers = singleton.IntGridLayers,
            }.ScheduleParallel(data.IntGridValues.Value.Length, 1, state.Dependency);
#endif
        }

#if BL_QUILL
        [BurstCompile]
        private struct DrawIntGridJob : IJobFor
        {
            public Drawer Drawer;

            [ReadOnly]
            public ComponentLookup<TilemapRendererData> TilemapRendererDataLookup;
            [ReadOnly]
            public ComponentLookup<TilemapTerrainLayer> TilemapTerrainLayerLookup;
            
            [ReadOnly]
            public NativeArray<int> SelectedIndices;
            [ReadOnly]
            public NativeArray<MosaicToolbarViewModel.Data.IntGridName> IntGridsBuffer;
            
            [ReadOnly]
            public NativeHashMap<Hash128, TilemapDataSingleton.IntGridLayer> IntGridLayers;
            
            public void Execute(int index)
            {
                var key = IntGridsBuffer[SelectedIndices[index]].IntGridHash;
                var layer = IntGridLayers[key];

                TilemapRendererData rendererData;
                if (layer.IsTerrainLayer)
                {
                    var terrainEntity = TilemapTerrainLayerLookup[layer.IntGridEntity].TerrainEntity;
                    rendererData = TilemapRendererDataLookup[terrainEntity];
                }
                else
                {
                    rendererData = TilemapRendererDataLookup[layer.IntGridEntity];
                }

                var rnd = new Random((uint)key.GetHashCode());
                var rgb = rnd.NextFloat3();
                var color = new Color(rgb.x, rgb.y, rgb.z, 1f);
                    
                foreach (var kvPair in layer.IntGrid)
                {
                    var pos = kvPair.Key;
                    var value = kvPair.Value.value;

                    var str = new FixedString32Bytes();
                    str.Append(value);
                    
                    var cellCenter = MosaicUtils.ToWorldSpace((float2)pos + 0.5f, rendererData);
                    Drawer.SolidRectangleXZ(cellCenter, MosaicUtils.ApplySwizzle(rendererData.CellSize, rendererData.Swizzle).xy, color);
                    Drawer.Text32(cellCenter, str, Color.black, size: 32f);
                }
            }
        }
    }
#endif
}