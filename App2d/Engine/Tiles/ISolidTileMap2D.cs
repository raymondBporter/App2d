using System.Numerics;

namespace App2d.Engine.Tiles;

public interface ISolidTileMap2D
{
    int Width { get; }
    int Height { get; }
    float TileSize { get; }
    Vector2 Origin { get; }

    bool IsSolid(int x, int y);
}
