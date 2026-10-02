using App2d.Core.Grids;

namespace App2d.Tiles;

/// <summary>Adapts tile maps to the storage-independent Core grid math.</summary>
public static class TileMapGridExtensions2D
{
    extension<TMap>(TMap map) where TMap : ISolidTileMap2D
    {
        public GridSize2D GridSize => new(map.Width, map.Height);
        public GridGeometry2D GridGeometry => new(map.TileSize, map.Origin);
    }

    extension<TMap>(TMap map) where TMap : IChunkedTileMap2D
    {
        public GridSize2D ChunkGridSize => map.GridSize.DivideRoundUp(map.ChunkSize, map.ChunkSize);
        public GridGeometry2D ChunkGridGeometry => new(map.TileSize * map.ChunkSize, map.Origin);
    }
}
