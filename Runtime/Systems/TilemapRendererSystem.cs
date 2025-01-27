using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace KrasCore.Mosaic
{
	public class TilemapMeshesSingleton : IComponentData
	{
		public readonly Dictionary<Hash128, Mesh> Meshes = new();
		public readonly List<Mesh> MeshesToUpdate = new();
	}
	
	[RequireMatchingQueriesForUpdate]
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial class TilemapRendererSystem : SystemBase
	{
		protected override void OnCreate()
		{
			EntityManager.CreateSingleton(new TilemapMeshesSingleton());
		}

		protected override void OnDestroy()
		{
			var singleton = SystemAPI.ManagedAPI.GetSingleton<TilemapMeshesSingleton>();
			foreach (var kvp in singleton.Meshes)
			{
				Object.Destroy(kvp.Value);
			}
		}
 
		protected override void OnUpdate()
		{
			EntityManager.CompleteDependencyBeforeRW<TilemapMeshDataSingleton>();
			
			var meshDataSingleton = SystemAPI.GetSingleton<TilemapMeshDataSingleton>();
			var meshesSingleton = SystemAPI.ManagedAPI.GetSingleton<TilemapMeshesSingleton>();

			if (meshDataSingleton.IsDirty)
				Mesh.ApplyAndDisposeWritableMeshData(meshDataSingleton.MeshDataArray, meshesSingleton.MeshesToUpdate);
					
			foreach (var (tilemapDataRO, localToWorldRO, runtimeMaterialRO) in SystemAPI.Query<RefRO<TilemapData>, RefRO<LocalToWorld>, RefRO<RuntimeMaterial>>())
			{
				var tilemapData = tilemapDataRO.ValueRO;
				var mesh = meshesSingleton.Meshes[tilemapData.IntGridHash];
				
				Graphics.RenderMesh(new RenderParams(runtimeMaterialRO.ValueRO.Value)
				{
					worldBounds = new Bounds(Vector3.zero, Vector3.one * 999999),
					receiveShadows = tilemapData.ReceiveShadows,
					shadowCastingMode = tilemapData.ShadowCastingMode,
				}, mesh, 0, localToWorldRO.ValueRO.Value);
			}
			
			meshDataSingleton.IntGridHashesToUpdate.Clear();
			meshesSingleton.MeshesToUpdate.Clear();
		}
	}
}
