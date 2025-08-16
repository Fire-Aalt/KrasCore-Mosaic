using System.Collections.Generic;
using KrasCore.Mosaic.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
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
				HashesToUpdate = new NativeList<Hash128>(8, Allocator.Persistent),
				UpdatedMeshBoundsMap = new NativeParallelHashMap<Hash128, AABB>(8, Allocator.Persistent)
			});
			EntityManager.CreateSingleton(new TilemapMeshSingleton
			{
				MeshMap = new Dictionary<Hash128, Mesh>(8)
			});
		}

		protected override void OnDestroy()
		{
			SystemAPI.GetSingleton<TilemapMeshDataSingleton>().Dispose();
			SystemAPI.ManagedAPI.GetSingleton<TilemapMeshSingleton>().Dispose();
		}
 
		protected override void OnUpdate()
		{
			EntityManager.CompleteDependencyBeforeRW<TilemapMeshDataSingleton>();
			ref var meshDataSingleton = ref SystemAPI.GetSingletonRW<TilemapMeshDataSingleton>().ValueRW;
			var meshSingleton = SystemAPI.ManagedAPI.GetSingleton<TilemapMeshSingleton>();
			
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
