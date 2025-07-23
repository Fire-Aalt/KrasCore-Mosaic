using System.Collections.Generic;
using System.Runtime.InteropServices;
using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using Mesh = UnityEngine.Mesh;

namespace KrasCore.Mosaic
{
	[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
	public partial class TilemapPresentationSystem : SystemBase
	{
		private readonly Dictionary<Hash128, Mesh> _meshMap = new();
		private readonly List<Mesh> _meshesToUpdate = new();
		
		protected override void OnCreate()
		{
			EntityManager.CreateSingleton(new TilemapMeshDataSingleton
			{
				HashesToUpdate = new NativeList<Hash128>(8, Allocator.Persistent),
				UpdatedMeshBoundsMap = new NativeParallelHashMap<Hash128, AABB>(8, Allocator.Persistent)
			});
		}

		protected override void OnDestroy()
		{
			SystemAPI.GetSingleton<TilemapMeshDataSingleton>().Dispose();
			foreach (var kvp in _meshMap)
			{
				Object.Destroy(kvp.Value);
			}
		}
 
		protected override void OnUpdate()
		{
			var uninitializedQuery = SystemAPI.QueryBuilder().WithAll<TilemapData, RuntimeMaterial>().WithNone<MaterialMeshInfo>().Build();
			if (!uninitializedQuery.IsEmpty)
			{
				var entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
				
				var entities = uninitializedQuery.ToEntityArray(Allocator.Temp);
				var tilemapsData = uninitializedQuery.ToComponentDataArray<TilemapData>(Allocator.Temp);
				var runtimeMaterials = uninitializedQuery.ToComponentDataArray<RuntimeMaterial>(Allocator.Temp);
				
				for (int i = 0; i < entities.Length; i++)
				{
					var tilemapData = tilemapsData[i];
					
					if (!_meshMap.TryGetValue(tilemapData.IntGridHash, out var mesh))
					{
						mesh = new Mesh { name = "Mosaic.TilemapMesh" };
						mesh.MarkDynamic();
						_meshMap.Add(tilemapData.IntGridHash, mesh);
					}
					
					var meshId = entitiesGraphicsSystem.RegisterMesh(mesh);
					var materialId = entitiesGraphicsSystem.RegisterMaterial(runtimeMaterials[i].Value);

					var desc = new RenderMeshDescription(
						tilemapData.ShadowCastingMode,
						receiveShadows: tilemapData.ReceiveShadows);
					var materialMeshInfo = new MaterialMeshInfo(materialId, meshId);
					
					RenderMeshUtility.AddComponents(entities[i], EntityManager, desc, materialMeshInfo);
				}
			}
			
			EntityManager.CompleteDependencyBeforeRW<TilemapMeshDataSingleton>();
			var meshDataSingleton = SystemAPI.GetSingleton<TilemapMeshDataSingleton>();
			
			foreach (var intGridHash in meshDataSingleton.HashesToUpdate)
			{
				_meshesToUpdate.Add(_meshMap[intGridHash]);
			}
			
			if (_meshesToUpdate.Count != 0)
			{
				Mesh.ApplyAndDisposeWritableMeshData(meshDataSingleton.MeshDataArray, _meshesToUpdate);
				_meshesToUpdate.Clear();
				
				Dependency = new UpdateBoundsJob
				{
					UpdatedMeshBoundsMap = meshDataSingleton.UpdatedMeshBoundsMap
				}.Schedule(Dependency);
			}
		}
		
		[BurstCompile]
		private partial struct UpdateBoundsJob : IJobEntity
		{
			[ReadOnly]
			public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;
			
			private void Execute(in TilemapData tilemapData, ref RenderBounds renderBounds)
			{
				if (UpdatedMeshBoundsMap.TryGetValue(tilemapData.IntGridHash, out var aabb))
				{
					renderBounds.Value = aabb;
				}
			}
		}
	}
}
