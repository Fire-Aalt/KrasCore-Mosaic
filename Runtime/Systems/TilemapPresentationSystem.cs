using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

namespace KrasCore.Mosaic
{
	[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
	public partial class TilemapPresentationSystem : SystemBase
	{
		private readonly Dictionary<Hash128, UnityEngine.Mesh> _meshMap = new();
		private readonly List<UnityEngine.Mesh> _meshesToUpdate = new();
		
		protected override void OnCreate()
		{
			EntityManager.CreateSingleton(new TilemapMeshDataSingleton
			{
				IntGridHashesToUpdate = new NativeList<Hash128>(8, Allocator.Persistent),
				UpdatedMeshBoundsMap = new NativeParallelHashMap<Hash128, AABB>(8, Allocator.Persistent)
			});
		}

		protected override void OnDestroy()
		{
			SystemAPI.GetSingleton<TilemapMeshDataSingleton>().Dispose();
			foreach (var kvp in _meshMap)
			{
				UnityEngine.Object.Destroy(kvp.Value);
			}
		}
 
		protected override void OnUpdate()
		{
			EntityManager.CompleteDependencyBeforeRW<TilemapMeshDataSingleton>();
			var meshDataSingleton = SystemAPI.GetSingleton<TilemapMeshDataSingleton>();

			foreach (var intGridHash in meshDataSingleton.IntGridHashesToUpdate)
			{
				if (!_meshMap.TryGetValue(intGridHash, out var mesh))
				{
					mesh = new UnityEngine.Mesh { name = "Mosaic.TilemapMesh" };
					mesh.MarkDynamic();
					_meshMap.Add(intGridHash, mesh);
				}
				_meshesToUpdate.Add(mesh);
			}
			
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
					var meshId = entitiesGraphicsSystem.RegisterMesh(_meshMap[tilemapData.IntGridHash]);
					var materialId = entitiesGraphicsSystem.RegisterMaterial(runtimeMaterials[i].Value);

					var desc = new RenderMeshDescription(
						tilemapData.ShadowCastingMode,
						receiveShadows: tilemapData.ReceiveShadows);
					var materialMeshInfo = new MaterialMeshInfo(materialId, meshId);
					
					RenderMeshUtility.AddComponents(entities[i], EntityManager, desc, materialMeshInfo);
				}
			}
			
			if (meshDataSingleton.IsDirty)
			{
				UnityEngine.Mesh.ApplyAndDisposeWritableMeshData(meshDataSingleton.MeshDataArray, _meshesToUpdate);
				meshDataSingleton.IntGridHashesToUpdate.Clear();
				_meshesToUpdate.Clear();
			}

			if (!meshDataSingleton.UpdatedMeshBoundsMap.IsEmpty)
			{
				Dependency = new UpdateBoundsJob
				{
					UpdatedMeshBoundsMap = meshDataSingleton.UpdatedMeshBoundsMap
				}.Schedule(Dependency);
				Dependency = new ClearJob
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

		[BurstCompile]
		private struct ClearJob : IJob
		{
			public NativeParallelHashMap<Hash128, AABB> UpdatedMeshBoundsMap;
			
			public void Execute()
			{
				UpdatedMeshBoundsMap.Clear();
			}
		}
	}
}
