using App2d.Core.Validation;
using App2d.Contracts.World;
using App2d.Core.Geometry;
using System.Collections.Immutable;

namespace App2d.Gameplay.World;

public sealed partial class SideScrollerLevel2D
{
    private ImmutableHashSet<GrassCell2D> _cutGrass = [];

    /// <summary>Called with an active sword hitbox after physics. Grass adds no physics bodies.</summary>
    public void CutGrass(Rect2D strike)
    {
        if (!strike.IsFinite || strike.Size.X < 0f || strike.Size.Y < 0f)
            ArgGuard.ThrowOutOfRange(strike, "Grass cutting bounds must be finite and ordered.");
        var streamer = RequireEnvironment().Streamer;
        var firstX = Math.Clamp((int)MathF.Floor((strike.Min.X - TileMap.Origin.X) / _tileSize), 0, TileMap.Width - 1);
        var lastX = Math.Clamp((int)MathF.Floor((strike.Max.X - TileMap.Origin.X) / _tileSize), 0, TileMap.Width - 1);
        var firstY = Math.Clamp((int)MathF.Floor((strike.Min.Y - TileMap.Origin.Y) / _tileSize -
            1f - VegetationPlacement2D.MaximumHeightInTiles), 0, TileMap.Height - 1);
        var lastY = Math.Clamp((int)MathF.Floor((strike.Max.Y - TileMap.Origin.Y) / _tileSize), 0, TileMap.Height - 1);
        for (var y = firstY; y <= lastY; y++)
            for (var x = firstX; x <= lastX; x++)
            {
                var cell = new GrassCell2D(x, y);
                var grass = VegetationPlacement2D.GrassBounds(TileMap, cell);
                if (streamer.IsChunkActive(new(x / TileMap.ChunkSize, y / TileMap.ChunkSize)) &&
                    !_cutGrass.Contains(cell) && VegetationPlacement2D.HasGrass(TileMap, x, y) &&
                    strike.Left < grass.Right && strike.Right > grass.Left &&
                    strike.Bottom < grass.Top && strike.Top > grass.Bottom)
                    _cutGrass = _cutGrass.Add(cell);
            }
    }

    private void ForgetUnloadedGrass(SideScrollerChunkStreamer2D streamer)
    {
        // Iterate the old immutable snapshot while replacing only the live set.
        // No timers, per-blade state, or growing history of visited terrain.
        foreach (var cell in _cutGrass)
            if (!streamer.IsChunkActive(new(cell.X / TileMap.ChunkSize, cell.Y / TileMap.ChunkSize)))
                _cutGrass = _cutGrass.Remove(cell);
    }
}
