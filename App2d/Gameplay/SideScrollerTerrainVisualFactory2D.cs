using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Rendering;
using App2d.Engine.Tiles;

namespace App2d.Gameplay;

internal sealed class SideScrollerTerrainVisualFactory2D
{
    private readonly Scene2D _scene;
    private readonly IChunkedTileMap2D _tileMap;
    private readonly SideScrollerTerrainTilesetResolver2D _tilesets;
    private readonly float _tileSize;

    public SideScrollerTerrainVisualFactory2D(
        Scene2D scene,
        IChunkedTileMap2D tileMap,
        SideScrollerTerrainTilesetResolver2D tilesets)
    {
        _scene = ArgGuard.RequireNotNull(scene);
        _tileMap = ArgGuard.RequireNotNull(tileMap);
        _tilesets = ArgGuard.RequireNotNull(tilesets);
        _tileSize = tileMap.TileSize;
    }

    public List<WorldObject2D> CreateSolidFill(Bounds2D bounds)
    {
        var visuals = new List<WorldObject2D>();
        var startX = WorldToTileX(bounds.Min.X);
        var endX = WorldToTileX(bounds.Max.X);
        var startY = WorldToTileY(bounds.Min.Y);
        var endY = WorldToTileY(bounds.Max.Y);
        var activeRuns = new Dictionary<FillRun, int>();

        for (var y = startY; y < endY; y++)
        {
            var rowRuns = GetFillRuns(startX, endX, y);
            foreach (var active in activeRuns.ToArray())
            {
                if (rowRuns.Contains(active.Key))
                    continue;

                AddFillVisual(visuals, active.Key, active.Value, y);
                activeRuns.Remove(active.Key);
            }

            foreach (var run in rowRuns)
                activeRuns.TryAdd(run, y);
        }

        foreach (var active in activeRuns)
            AddFillVisual(visuals, active.Key, active.Value, endY);

        return visuals;
    }

    private HashSet<FillRun> GetFillRuns(int startX, int endX, int y)
    {
        var runs = new HashSet<FillRun>();
        var runStartX = startX;
        while (runStartX < endX)
        {
            var tileset = _tilesets.GetTileset(runStartX, y);
            var runEndX = runStartX + 1;
            while (runEndX < endX &&
                   ReferenceEquals(tileset, _tilesets.GetTileset(runEndX, y)))
            {
                runEndX++;
            }

            runs.Add(new FillRun(runStartX, runEndX, tileset));
            runStartX = runEndX;
        }

        return runs;
    }

    private void AddFillVisual(
        List<WorldObject2D> visuals,
        FillRun run,
        int startY,
        int endY)
    {
        var runBounds = new Bounds2D(
            new Vector2(TileToWorldX(run.StartX), TileToWorldY(startY)),
            new Vector2(TileToWorldX(run.EndX), TileToWorldY(endY)));
        AddVisual(visuals, run.Tileset.CreateSolidFill(runBounds));
    }

    public List<WorldObject2D> CreateSurfaceVisuals(TileChunk2D chunk)
    {
        var visuals = new List<WorldObject2D>();
        var startX = chunk.X * _tileMap.ChunkSize;
        var startY = chunk.Y * _tileMap.ChunkSize;
        var endX = Math.Min(startX + _tileMap.ChunkSize, _tileMap.Width);
        var endY = Math.Min(startY + _tileMap.ChunkSize, _tileMap.Height);

        for (var y = startY; y < endY; y++)
        {
            for (var x = startX; x < endX; x++)
            {
                if (_tileMap.GetTileKind(x, y) == TileKind2D.OneWay)
                {
                    var oneWayBounds = GetTileBounds(x, y);
                    AddVisual(
                        visuals,
                        _tilesets.GetTileset(x, y).CreateOneWay(
                            oneWayBounds,
                            GetOneWayPart(x, y)));
                    continue;
                }

                var surfaces = _tileMap.GetExposedSurfaces(x, y);
                if (surfaces == TileSurface2D.None)
                    continue;

                var tileset = _tilesets.GetTileset(x, y);
                var tileBounds = GetTileBounds(x, y);
                if (surfaces.HasFlag(TileSurface2D.Top))
                    AddVisual(visuals, tileset.CreateSurface(tileBounds, TileSurface2D.Top));
                if (surfaces.HasFlag(TileSurface2D.Right))
                    AddVisual(visuals, tileset.CreateSurface(tileBounds, TileSurface2D.Right));
                if (surfaces.HasFlag(TileSurface2D.Bottom))
                    AddVisual(visuals, tileset.CreateSurface(tileBounds, TileSurface2D.Bottom));
                if (surfaces.HasFlag(TileSurface2D.Left))
                    AddVisual(visuals, tileset.CreateSurface(tileBounds, TileSurface2D.Left));
            }
        }

        for (var y = startY; y < endY; y++)
        {
            for (var x = startX; x < endX; x++)
            {
                var corners = _tileMap.GetCorners(x, y);
                if (corners == TileCorner2D.None)
                    continue;

                AddCornerVisuals(visuals, x, y, GetTileBounds(x, y), corners);
            }
        }

        return visuals;
    }

    private void AddCornerVisuals(
        List<WorldObject2D> visuals,
        int tileX,
        int tileY,
        Bounds2D tileBounds,
        TileCorner2D corners)
    {
        var tileset = _tilesets.GetTileset(tileX, tileY);
        if (corners.HasFlag(TileCorner2D.OuterTopRight))
            AddVisual(visuals, tileset.CreateCorner(tileBounds, TileCorner2D.OuterTopRight));
        if (corners.HasFlag(TileCorner2D.OuterBottomRight))
            AddVisual(visuals, tileset.CreateCorner(tileBounds, TileCorner2D.OuterBottomRight));
        if (corners.HasFlag(TileCorner2D.OuterBottomLeft))
            AddVisual(visuals, tileset.CreateCorner(tileBounds, TileCorner2D.OuterBottomLeft));
        if (corners.HasFlag(TileCorner2D.OuterTopLeft))
            AddVisual(visuals, tileset.CreateCorner(tileBounds, TileCorner2D.OuterTopLeft));

        if (corners.HasFlag(TileCorner2D.InnerTopRight))
            AddVisual(visuals, tileset.CreateCorner(tileBounds, TileCorner2D.InnerTopRight));
        if (corners.HasFlag(TileCorner2D.InnerBottomRight))
            AddVisual(visuals, tileset.CreateCorner(tileBounds, TileCorner2D.InnerBottomRight));
        if (corners.HasFlag(TileCorner2D.InnerBottomLeft))
            AddVisual(visuals, tileset.CreateCorner(tileBounds, TileCorner2D.InnerBottomLeft));
        if (corners.HasFlag(TileCorner2D.InnerTopLeft))
            AddVisual(visuals, tileset.CreateCorner(tileBounds, TileCorner2D.InnerTopLeft));
    }

    private void AddVisual(List<WorldObject2D> visuals, WorldObject2D visual)
    {
        _scene.Add(visual);
        visuals.Add(visual);
    }

    private OneWayTilePart2D GetOneWayPart(int x, int y)
    {
        var hasLeft = x > 0 &&
            _tilesets.UsesSameTileset(x, y, x - 1, y) &&
            _tileMap.GetTileKind(x - 1, y) == TileKind2D.OneWay;
        var hasRight = x < _tileMap.Width - 1 &&
            _tilesets.UsesSameTileset(x, y, x + 1, y) &&
            _tileMap.GetTileKind(x + 1, y) == TileKind2D.OneWay;
        return (hasLeft, hasRight) switch
        {
            (false, false) => OneWayTilePart2D.Standalone,
            (false, true) => OneWayTilePart2D.Left,
            (true, false) => OneWayTilePart2D.Right,
            _ => OneWayTilePart2D.Middle
        };
    }

    private Bounds2D GetTileBounds(int x, int y)
    {
        var min = _tileMap.Origin + new Vector2(x, y) * _tileSize;
        return new Bounds2D(min, min + new Vector2(_tileSize));
    }

    private int WorldToTileX(float worldX) =>
        (int)MathF.Round((worldX - _tileMap.Origin.X) / _tileSize);

    private int WorldToTileY(float worldY) =>
        (int)MathF.Round((worldY - _tileMap.Origin.Y) / _tileSize);

    private float TileToWorldX(int tileX) =>
        _tileMap.Origin.X + tileX * _tileSize;

    private float TileToWorldY(int tileY) =>
        _tileMap.Origin.Y + tileY * _tileSize;

    private readonly record struct FillRun(
        int StartX,
        int EndX,
        SideScrollerTerrainTileset2D Tileset);
}
