using System.Collections.Generic;
using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
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
			EntityManager.CreateSingleton(new TilemapMeshDataSingleton
			{
				TilemapHashesToUpdate = new NativeList<Hash128>(8, Allocator.Persistent),
				TerrainHashesToUpdate = new NativeList<Hash128>(1, Allocator.Persistent),
				UpdatedMeshBoundsMap = new NativeParallelHashMap<Hash128, AABB>(8, Allocator.Persistent)
			});
			EntityManager.CreateSingleton(new TilemapRenderingSingleton
			{
				MeshMap = new Dictionary<Hash128, Mesh>(4),
				TilemapTerrainMap = new Dictionary<Hash128, TilemapTerrainRenderingData>(1)
			});
		}

		protected override void OnDestroy()
		{
			SystemAPI.GetSingleton<TilemapMeshDataSingleton>().Dispose();
			SystemAPI.ManagedAPI.GetSingleton<TilemapRenderingSingleton>().Dispose();
		}
 
		protected override void OnUpdate()
		{
			EntityManager.CompleteDependencyBeforeRW<TilemapMeshDataSingleton>();
			EntityManager.CompleteDependencyBeforeRW<TilemapTerrainMeshDataSingleton>();
			var meshSingleton = SystemAPI.ManagedAPI.GetSingleton<TilemapRenderingSingleton>();
			var tilemapTerrainSingleton = SystemAPI.GetSingleton<TilemapTerrainMeshDataSingleton>();
			ref var meshDataSingleton = ref SystemAPI.GetSingletonRW<TilemapMeshDataSingleton>().ValueRW;
			
			foreach (var intGridHash in meshDataSingleton.TilemapHashesToUpdate)
			{
				_tilemapMeshesToUpdate.Add(meshSingleton.MeshMap[intGridHash]);
			}
			
			foreach (var terrainHash in meshDataSingleton.TerrainHashesToUpdate)
			{
				var terrainRenderingData = meshSingleton.TilemapTerrainMap[terrainHash];
				var terrainData = tilemapTerrainSingleton.Terrains[terrainHash];
				
				terrainRenderingData.SetTileSize(terrainData.TileSize);
				terrainRenderingData.SetTileBuffer(terrainData.TileBuffer);
				terrainRenderingData.SetIndexBuffer(terrainData.IndexBuffer);
				
				_terrainMeshesToUpdate.Add(meshSingleton.MeshMap[terrainHash]);
			}
			
			UpdateMeshes(ref meshDataSingleton.TerrainMeshDataArray, _terrainMeshesToUpdate, meshSingleton.MeshMap.Count); // FIX
			UpdateMeshes(ref meshDataSingleton.TilemapMeshDataArray, _terrainMeshesToUpdate, meshSingleton.MeshMap.Count);
			
			if (_tilemapMeshesToUpdate.Count != 0 || _terrainMeshesToUpdate.Count != 0)
			{
				new UpdateBoundsJob
				{
					UpdatedMeshBoundsMap = meshDataSingleton.UpdatedMeshBoundsMap
				}.Run();
				
				meshDataSingleton.TilemapHashesToUpdate.Clear();
				meshDataSingleton.TerrainHashesToUpdate.Clear();
				meshDataSingleton.UpdatedMeshBoundsMap.Clear();
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
			public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;
			
			private void Execute(in TilemapRendererInitData rendererData, ref RenderBounds renderBounds)
			{
				if (UpdatedMeshBoundsMap.TryGetValue(rendererData.MeshHash, out var aabb))
				{
					renderBounds.Value = aabb;
				}
			}
		}
	}
}
