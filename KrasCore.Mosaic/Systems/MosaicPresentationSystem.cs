using System.Collections.Generic;
using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Hash128 = Unity.Entities.Hash128;
using Mesh = UnityEngine.Mesh;

namespace KrasCore.Mosaic
{
	[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
	public partial class MosaicPresentationSystem : SystemBase
	{
		private readonly List<Mesh> _tilemapMeshesToUpdate = new();
		private readonly List<Mesh> _terrainMeshesToUpdate = new();
		private readonly Mesh _dummyMesh = new();
		
		protected override void OnCreate()
		{
			EntityManager.CreateSingleton(new TilemapRenderingSingleton
			{
				MeshMap = new Dictionary<Hash128, Mesh>(4),
				TerrainMap = new Dictionary<Hash128, TilemapTerrainRenderingData>(1)
			});
		}

		protected override void OnDestroy()
		{
			SystemAPI.ManagedAPI.GetSingleton<TilemapRenderingSingleton>().Dispose();
		}
 
		protected override void OnUpdate()
		{
			EntityManager.CompleteDependencyBeforeRW<TilemapMeshDataSystem.Singleton>();
			EntityManager.CompleteDependencyBeforeRW<TerrainMeshDataSystem.Singleton>();
			var meshSingleton = SystemAPI.ManagedAPI.GetSingleton<TilemapRenderingSingleton>();
			ref var tilemapSingleton = ref SystemAPI.GetSingletonRW<TilemapMeshDataSystem.Singleton>().ValueRW;
			ref var terrainSingleton = ref SystemAPI.GetSingletonRW<TerrainMeshDataSystem.Singleton>().ValueRW;
			
			foreach (var intGridHash in tilemapSingleton.HashesToUpdate)
			{
				_tilemapMeshesToUpdate.Add(meshSingleton.MeshMap[intGridHash]);
			}
			
			foreach (var terrainHash in terrainSingleton.HashesToUpdate)
			{
				var terrainRenderingData = meshSingleton.TerrainMap[terrainHash];
				var terrainData = terrainSingleton.Terrains[terrainHash];
				
				terrainRenderingData.SetTileSize(terrainData.TileSize);
				terrainRenderingData.SetTileBuffer(terrainData.TileBuffer);
				terrainRenderingData.SetIndexBuffer(terrainData.IndexBuffer);
				
				_terrainMeshesToUpdate.Add(meshSingleton.MeshMap[terrainHash]);
			}
			
			UpdateMeshes(ref tilemapSingleton.MeshDataArray, _tilemapMeshesToUpdate, tilemapSingleton.RenderingEntities.Length);
			UpdateMeshes(ref terrainSingleton.MeshDataArray, _terrainMeshesToUpdate, terrainSingleton.RenderingEntities.Length);
			
			if (_tilemapMeshesToUpdate.Count != 0 || _terrainMeshesToUpdate.Count != 0)
			{
				new UpdateBoundsJob
				{
					TilemapUpdatedMeshBoundsMap = tilemapSingleton.UpdatedMeshBoundsMap,
					TerrainUpdatedMeshBoundsMap = terrainSingleton.UpdatedMeshBoundsMap
				}.Run();
				
				tilemapSingleton.HashesToUpdate.Clear();
				tilemapSingleton.UpdatedMeshBoundsMap.Clear();
				terrainSingleton.HashesToUpdate.Clear();
				terrainSingleton.UpdatedMeshBoundsMap.Clear();
				_tilemapMeshesToUpdate.Clear();
				_terrainMeshesToUpdate.Clear();
			}
		}

		private void UpdateMeshes(ref MeshDataArrayWrapper meshDataArray, List<Mesh> meshes, int neededMeshesCount)
		{
			if (meshes.Count != 0)
			{
				// Mesh.ApplyAndDisposeWritableMeshData() expects same size List<Mesh>, so we have to populate it with dummy meshes
				var neededDummies = meshDataArray.Array.Length - meshes.Count;
				for (int i = 0; i < neededDummies; i++)
				{
					meshes.Add(_dummyMesh);
				}
				meshDataArray.ApplyAndDisposeWritableMeshData(meshes);
			}
			
			if (!meshDataArray.IsCreated || neededMeshesCount != meshDataArray.Array.Length)
			{
				meshDataArray.AllocateWritableMeshData(neededMeshesCount);
			}
		}

		[BurstCompile]
		private partial struct UpdateBoundsJob : IJobEntity
		{
			[ReadOnly]
			public NativeParallelHashMap<Hash128, AABB> TilemapUpdatedMeshBoundsMap;
			[ReadOnly]
			public NativeParallelHashMap<Hash128, AABB> TerrainUpdatedMeshBoundsMap;
			
			private void Execute(in TilemapRendererInitData rendererData, ref RenderBounds renderBounds)
			{
				if (TilemapUpdatedMeshBoundsMap.TryGetValue(rendererData.MeshHash, out var aabb) 
				    || TerrainUpdatedMeshBoundsMap.TryGetValue(rendererData.MeshHash, out aabb))
				{
					renderBounds.Value = aabb;
				}
			}
		}
	}
}
