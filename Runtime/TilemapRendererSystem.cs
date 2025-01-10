using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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

			foreach (var dataLayer in dataSingleton.IntGridLayers)
			{
				var intGridHash = dataLayer.Key;
				var rendererLayer = rendererSingleton.IntGridLayers[intGridHash];

				var tilemapData = dataLayer.Value.TilemapData;
				
				if (!_meshes.TryGetValue(intGridHash, out var mesh))
				{
					mesh = new Mesh { name = "Mosaic.TilemapMesh" };
					_meshes.Add(intGridHash, mesh);
				}
				
				// Specify the layout of each vertex. This should match the Vertex struct
				mesh.SetVertexBufferParams(rendererLayer.Vertices.Length, _layout);
				// To allow for more than ≈16k agents we need to use a 32 bit format for the mesh
				mesh.SetIndexBufferParams(rendererLayer.Triangles.Length, IndexFormat.UInt32);

				// Set the vertex and index data
				mesh.SetVertexBufferData(rendererLayer.Vertices.AsArray(), 0, 0, rendererLayer.Vertices.Length);
				mesh.SetIndexBufferData(rendererLayer.Triangles.AsArray(), 0, 0, rendererLayer.Triangles.Length);

				mesh.subMeshCount = 1;
				mesh.SetSubMesh(0, new SubMeshDescriptor(0, rendererLayer.Triangles.Length, MeshTopology.Triangles),
					MeshUpdateFlags.DontRecalculateBounds);
				// SetSubMesh doesn't seem to update the bounds properly for some reason, so we do it manually instead
				mesh.RecalculateBounds();
				
				Graphics.RenderMesh(new RenderParams(tilemapData.Material)
				{
					worldBounds = new Bounds(Vector3.zero, Vector3.one * 999999),
				}, mesh, 0, Matrix4x4.identity);
			}
		}
	}
}
