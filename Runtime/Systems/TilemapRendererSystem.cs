using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace KrasCore.Mosaic
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial class TilemapRendererSystem : SystemBase
	{
		private readonly Dictionary<int, Mesh> _meshes = new();
		
		private VertexAttributeDescriptor[] _layout;
		
		protected override void OnCreate()
		{
			_layout = new[]
			{
				new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
				new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
			};
		}

		protected override void OnDestroy()
		{
			foreach (var mesh in _meshes)
			{
				Object.Destroy(mesh.Value);
			}
		}
 
		protected override void OnUpdate()
		{
			var dataSingleton = SystemAPI.GetSingleton<TilemapDataSingleton>();
			var rendererSingleton = SystemAPI.GetSingleton<TilemapRendererSingleton>();
			rendererSingleton.JobHandle.Complete();

			foreach (var kvp in dataSingleton.IntGridLayers)
			{
				var intGridHash = kvp.Key;
				var dataLayer = kvp.Value;
				var rendererLayer = rendererSingleton.IntGridLayers[intGridHash];

				var tilemapData = dataLayer.TilemapData;
				
				if (!_meshes.TryGetValue(intGridHash, out var mesh))
				{
					mesh = new Mesh { name = "Mosaic.TilemapMesh" };
					_meshes.Add(intGridHash, mesh);
				}
				
				if (rendererLayer.IsDirty.Value)
				{
					mesh.SetVertexBufferParams(rendererLayer.Vertices.Length, _layout);
					mesh.SetIndexBufferParams(rendererLayer.Triangles.Length, IndexFormat.UInt32);

					mesh.SetVertexBufferData(rendererLayer.Vertices.AsArray(), 0, 0, rendererLayer.Vertices.Length);
					mesh.SetIndexBufferData(rendererLayer.Triangles.AsArray(), 0, 0, rendererLayer.Triangles.Length);

					mesh.subMeshCount = 1;
					mesh.SetSubMesh(0, new SubMeshDescriptor(0, rendererLayer.Triangles.Length, MeshTopology.Triangles),
						MeshUpdateFlags.DontRecalculateBounds);
					
					mesh.RecalculateBounds();
				}
				
				Graphics.RenderMesh(new RenderParams(tilemapData.Material)
				{
					worldBounds = new Bounds(Vector3.zero, Vector3.one * 999999),
					receiveShadows = tilemapData.ReceiveShadows,
					shadowCastingMode = tilemapData.ShadowCastingMode,
				}, mesh, 0, dataLayer.TilemapTransform.ToMatrix());
			}
		}
	}
}
