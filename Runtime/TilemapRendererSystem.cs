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
	public partial class TilemapRendererSystem : SystemBase
	{
		private Dictionary<int, Mesh> _meshes = new();

		/// <summary>Material for rendering</summary>
		public Material material;
		
		protected override void OnDestroy()
		{
			foreach (var mesh in _meshes)
			{
				Object.Destroy(mesh.Value);
			}
		}
 
		protected override void OnUpdate()
		{
			foreach (var tilemapDataRO in SystemAPI.Query<RefRO<TilemapData>>())
			{
				var vertexCount = agentCount * 4;
				var indexCount = agentCount * 6;
				var vertices = CollectionHelper.CreateNativeArray<Vertex>(vertexCount, WorldUpdateAllocator);
				var tris = CollectionHelper.CreateNativeArray<int>(indexCount, WorldUpdateAllocator);
				Dependency = new JobGenerateMesh
				{
					verts = vertices,
					tris = tris,
					renderingOffset = renderingOffset
				}.Schedule(entityQuery, Dependency);

				// Specify the layout of each vertex. This should match the Vertex struct
				var layout = new[]
				{
					new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
					new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
					new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
				};
				mesh.SetVertexBufferParams(vertexCount, layout);
				// To allow for more than ≈16k agents we need to use a 32 bit format for the mesh
				mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

				// Wait for the JobGenerateMesh job to complete before we try to use the mesh data
				Dependency.Complete();

				// Set the vertex and index data
				mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length);
				mesh.SetIndexBufferData(tris, 0, 0, tris.Length);

				mesh.subMeshCount = 1;
				mesh.SetSubMesh(0, new SubMeshDescriptor(0, tris.Length, MeshTopology.Triangles),
					MeshUpdateFlags.DontRecalculateBounds);
				// SetSubMesh doesn't seem to update the bounds properly for some reason, so we do it manually instead
				mesh.RecalculateBounds();
			}
			
		}

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		public struct Vertex
		{
			public float3 position;
			public Color32 color;
			public float2 uv;
		}

		/// <summary>
		/// Generates a simple mesh for rendering the agents.
		/// Each agent is a quad rotated and positioned to align with the agent.
		/// </summary>
		[BurstCompile(FloatMode = FloatMode.Fast)]
		public partial struct JobGenerateMesh : IJobEntity
		{
			[WriteOnly] public NativeArray<Vertex> verts;
			[WriteOnly] public NativeArray<int> tris;

			public Vector3 renderingOffset;

			public void Execute(in LocalTransform transform, in LightweightAgentData agentData,
				in AgentCylinderShape shape, [EntityIndexInQuery] int entityIndex)
			{
				// Create a square with the "forward" direction along the agent's velocity
				float3 forward = transform.Forward() * shape.radius;
				if (math.all(forward == 0)) forward = new float3(0, 0, shape.radius);
				float3 right = math.cross(new float3(0, 1, 0), forward);
				float3 orig = transform.Position + (float3)renderingOffset;

				int vc = 4 * entityIndex;
				int tc = 2 * 3 * entityIndex;

				Color32 color = agentData.color;
				verts[vc + 0] = new Vertex
				{
					position = (orig + forward - right),
					uv = new float2(0, 1),
					color = color,
				};

				verts[vc + 1] = new Vertex
				{
					position = (orig + forward + right),
					uv = new float2(1, 1),
					color = color,
				};

				verts[vc + 2] = new Vertex
				{
					position = (orig - forward + right),
					uv = new float2(1, 0),
					color = color,
				};

				verts[vc + 3] = new Vertex
				{
					position = (orig - forward - right),
					uv = new float2(0, 0),
					color = color,
				};

				tris[tc + 0] = (vc + 0);
				tris[tc + 1] = (vc + 1);
				tris[tc + 2] = (vc + 2);

				tris[tc + 3] = (vc + 0);
				tris[tc + 4] = (vc + 2);
				tris[tc + 5] = (vc + 3);
			}
		}
	}
}
}