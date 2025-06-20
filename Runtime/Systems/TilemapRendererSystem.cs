using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using Object = UnityEngine.Object;

namespace KrasCore.Mosaic
{
	[RequireMatchingQueriesForUpdate]
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial class TilemapRendererSystem : SystemBase
	{
		private readonly Dictionary<Hash128, Mesh> _meshes = new();
		private readonly List<Mesh> _meshesToUpdate = new();
		
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
			foreach (var kvp in _meshes)
			{
				Object.Destroy(kvp.Value);
			}
		}
 
		protected override void OnUpdate()
		{
			EntityManager.CompleteDependencyBeforeRW<TilemapMeshDataSingleton>();
			var meshDataSingleton = SystemAPI.GetSingleton<TilemapMeshDataSingleton>();

			foreach (var intGridHash in meshDataSingleton.IntGridHashesToUpdate)
			{
				if (!_meshes.TryGetValue(intGridHash, out var mesh))
				{
					mesh = new Mesh { name = "Mosaic.TilemapMesh" };
					mesh.MarkDynamic();
					_meshes.Add(intGridHash, mesh);
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
					var meshId = entitiesGraphicsSystem.RegisterMesh(_meshes[tilemapData.IntGridHash]);
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
				Mesh.ApplyAndDisposeWritableMeshData(meshDataSingleton.MeshDataArray, _meshesToUpdate);
				meshDataSingleton.IntGridHashesToUpdate.Clear();
				_meshesToUpdate.Clear();
			}
			
			foreach (var (tilemapDataRO, renderBoundsRW) in SystemAPI.Query<RefRO<TilemapData>, RefRW<RenderBounds>>())
			{
				if (meshDataSingleton.UpdatedMeshBoundsMap.TryGetValue(tilemapDataRO.ValueRO.IntGridHash, out var aabb))
				{
					renderBoundsRW.ValueRW.Value = aabb;
				}
			}
			meshDataSingleton.UpdatedMeshBoundsMap.Clear();
		}
	}
}
