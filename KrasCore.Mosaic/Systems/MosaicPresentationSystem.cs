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
		private readonly List<Mesh> _meshesToUpdate = new();
		private readonly Mesh _dummyMesh = new();
		
		protected override void OnCreate()
		{
			EntityManager.CreateSingleton(new TilemapMeshDataSingleton
			{
				TerrainHashesToUpdate = new NativeList<Hash128>(1, Allocator.Persistent),
				HashesToUpdate = new NativeList<Hash128>(8, Allocator.Persistent),
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
			
			foreach (var terrainHash in meshDataSingleton.TerrainHashesToUpdate)
			{
				var terrainRenderingData = meshSingleton.TilemapTerrainMap[terrainHash];
				var terrainData = tilemapTerrainSingleton.Terrains[terrainHash];
				
				terrainRenderingData.SetTileSize(terrainData.TileSize);
				terrainRenderingData.SetTileBuffer(terrainData.TileBuffer);
				terrainRenderingData.SetIndexBuffer(terrainData.IndexBuffer);
			}
			
			foreach (var intGridHash in meshDataSingleton.HashesToUpdate)
			{
				_meshesToUpdate.Add(meshSingleton.MeshMap[intGridHash]);
			}

			if (_meshesToUpdate.Count != 0)
			{
				// Mesh.ApplyAndDisposeWritableMeshData() expects same size List<Mesh>, so we have to populate it with dummy meshes
				var neededDummies = meshDataSingleton.MeshDataArray.Length - _meshesToUpdate.Count;
				for (int i = 0; i < neededDummies; i++)
				{
					_meshesToUpdate.Add(_dummyMesh);
				}
				
				meshDataSingleton.ApplyAndDisposeWritableMeshData(_meshesToUpdate);
				_meshesToUpdate.Clear();
			}
			
			if (!meshDataSingleton.IsMeshDataArrayCreated || meshSingleton.MeshMap.Count != meshDataSingleton.MeshDataArray.Length)
			{
				meshDataSingleton.AllocateWritableMeshData(meshSingleton.MeshMap.Count);
			}
			
			if (meshDataSingleton.HashesToUpdate.Length != 0)
			{
				new UpdateBoundsJob
				{
					UpdatedMeshBoundsMap = meshDataSingleton.UpdatedMeshBoundsMap
				}.Run();
				meshDataSingleton.UpdatedMeshBoundsMap.Clear();
				meshDataSingleton.HashesToUpdate.Clear();
				meshDataSingleton.TerrainHashesToUpdate.Clear();
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
