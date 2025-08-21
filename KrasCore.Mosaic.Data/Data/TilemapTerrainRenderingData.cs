using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace KrasCore.Mosaic.Data
{
    public class TilemapTerrainRenderingData : IDisposable
    {
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");
        private static readonly int TileBufferId = Shader.PropertyToID("_TerrainTileBuffer");
        private static readonly int IndexBufferId = Shader.PropertyToID("_TerrainIndexBuffer");
        
        public Material Material;
        
        private GraphicsBuffer _tileBuffer;
        private GraphicsBuffer _indexBuffer;

        public void SetTileSize(float2 tileSize)
        {
            Material.SetVector(TileSizeId, new Vector4(tileSize.x, tileSize.y));
        }
        
        public void SetTileBuffer(UnsafeList<GpuTerrainTile> buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }
            
            if (_tileBuffer == null || _tileBuffer.count < buffer.Length)
            {
                _tileBuffer?.Dispose();
                _tileBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, buffer.Length, UnsafeUtility.SizeOf<GpuTerrainTile>());
                Material.SetBuffer(TileBufferId, _tileBuffer);
            }
            
            _tileBuffer.SetData(buffer.AsNativeArray());
        }
        
        public void SetIndexBuffer(UnsafeList<GpuTerrainIndex> buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }
            
            if (_indexBuffer == null || _indexBuffer.count < buffer.Length)
            {
                _indexBuffer?.Dispose();
                _indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, buffer.Length, UnsafeUtility.SizeOf<GpuTerrainIndex>());
                Material.SetBuffer(IndexBufferId, _indexBuffer);
            }
            
            _indexBuffer.SetData(buffer.AsNativeArray());
        }
        
        public void Dispose()
        {
            _tileBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }
    }
}