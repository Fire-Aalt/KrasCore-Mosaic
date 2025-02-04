using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
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
				IntGridHashesToUpdate = new NativeList<Hash128>(8, Allocator.Persistent)
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
					_meshes.Add(intGridHash, mesh);
				}
				_meshesToUpdate.Add(mesh);
			}

			if (meshDataSingleton.IsDirty)
			{
				Mesh.ApplyAndDisposeWritableMeshData(meshDataSingleton.MeshDataArray, _meshesToUpdate);
				meshDataSingleton.IntGridHashesToUpdate.Clear();
				_meshesToUpdate.Clear();
			}
			
			foreach (var (tilemapDataRO, localToWorldRO, runtimeMaterialRO) in SystemAPI.Query<RefRO<TilemapData>, RefRO<LocalToWorld>, RefRO<RuntimeMaterial>>())
			{
				var tilemapData = tilemapDataRO.ValueRO;
				
				if (_meshes.TryGetValue(tilemapData.IntGridHash, out var mesh))
				{
					Graphics.RenderMesh(new RenderParams(runtimeMaterialRO.ValueRO.Value)
					{
						worldBounds = new Bounds(Vector3.zero, Vector3.one * 999999),
						receiveShadows = tilemapData.ReceiveShadows,
						shadowCastingMode = tilemapData.ShadowCastingMode,
					}, mesh, 0, localToWorldRO.ValueRO.Value);
				}
			}
		}
	}
}
