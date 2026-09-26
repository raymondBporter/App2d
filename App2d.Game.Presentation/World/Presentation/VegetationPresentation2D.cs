using App2d.Core.Geometry;
using App2d.Rendering;
using App2d.Rendering.Vegetation;
using App2d.Tiles;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.World.Presentation;

/// <summary>Camera-streamed foliage; only the simulation's observation supplies cut state.</summary>
public sealed class VegetationPresentation2D
{
    private readonly Dictionary<TileChunk2D, Chunk> _chunks = [];
    private readonly List<GrassClipping2D> _clippings = [];
    internal int ClippingCount => _clippings.Count;
    private ImmutableHashSet<GrassCell2D>? _cuts;
    private ImmutableArray<TerrainChunkState2D> _terrain;
    private double _time;

    public void SetTerrain(ImmutableArray<TerrainChunkState2D> terrain)
    {
        if (_terrain == terrain) return;
        _terrain = terrain;
        var sources = terrain.ToDictionary(c => c.Chunk);
        var visible = terrain.Select(c => c.Chunk).ToHashSet();
        foreach (var key in _chunks.Keys.Where(k => !visible.Contains(k)).ToArray()) _chunks.Remove(key);
        foreach (var map in terrain)
        {
            if (_chunks.TryGetValue(map.Chunk, out var old) && ReferenceEquals(old.Source, map)) continue;
            var patches = new List<Patch>();
            var trees = new List<ProceduralTree2D>();
            for (var y = map.Chunk.Y * map.ChunkSize; y < Math.Min(map.Height, (map.Chunk.Y + 1) * map.ChunkSize); y++)
            for (var x = map.Chunk.X * map.ChunkSize; x < Math.Min(map.Width, (map.Chunk.X + 1) * map.ChunkSize); x++)
            {
                if (!VegetationPlacement2D.HasGrass(map, x, y)) continue;
                var cell = new GrassCell2D(x, y);
                var seed = VegetationPlacement2D.Seed(x, y);
                var root = map.Origin + new Vector2(x, y + 1) * map.TileSize;
                var style = Style(map.TilesetIds[map.GetTilesetIndex(x, y)], map.TileSize);
                patches.Add(new(cell, root, map.TileSize,
                    new VegetationPatch2D(root.X, root.X + map.TileSize, root.Y, style, seed)));
            }
            _chunks[map.Chunk] = new(map, patches, trees);
        }
        // Canopies cross chunk boundaries: recheck their clearance when any neighbor changes.
        foreach (var chunk in _chunks.Values)
        {
            chunk.Trees.Clear();
            var map = chunk.Source;
            foreach (var patch in chunk.Patches)
            {
                var (x, y) = patch.Cell;
                var seed = VegetationPlacement2D.Seed(x, y);
                // One candidate per eight columns avoids trees bunching together.
                if (x % 8 == 4 && seed % 3 != 0 &&
                    VegetationPlacement2D.HasGrass(map, x - 1, y) &&
                    VegetationPlacement2D.HasGrass(map, x + 1, y) && HasTreeClearance(map, x, y, sources))
                    chunk.Trees.Add(new(patch.Root + new Vector2(patch.Size * 0.5f, 0f),
                        patch.Size * (4.5f + seed % 100 / 50f), seed));
            }
        }
    }

    private static bool HasTreeClearance(TerrainChunkState2D map, int x, int y,
        Dictionary<TileChunk2D, TerrainChunkState2D> sources)
    {
        for (var dy = 1; dy <= 10; dy++)
        for (var dx = -3; dx <= 3; dx++)
        {
            var tx = x + dx;
            var ty = y + dy;
            if (tx < 0 || tx >= map.Width || ty >= map.Height ||
                !sources.TryGetValue(new(tx / map.ChunkSize, ty / map.ChunkSize), out var source) ||
                source.GetTileKind(tx, ty) != TileKind2D.Empty) return false;
        }
        return true;
    }

    public void ApplyCuts(ImmutableHashSet<GrassCell2D> cuts)
    {
        if (ReferenceEquals(_cuts, cuts)) return;
        if (_cuts is not null)
        {
            foreach (var chunk in _chunks.Values)
            foreach (var patch in chunk.Patches)
                if (cuts.Contains(patch.Cell) && !_cuts.Contains(patch.Cell))
                    Burst(patch);
        }
        _cuts = cuts;
    }

    public void Advance(float dt)
    {
        App2d.Core.ArgGuard.ThrowIfNegativeOrNotFinite(dt);
        _time += dt;
        var wind = Wind();
        for (var i = _clippings.Count - 1; i >= 0; i--)
        {
            var piece = _clippings[i];
            var previousBounds = piece.Shape.WorldBounds(piece.Position, piece.Rotation);
            piece.Advance(dt, wind);
            var bounds = piece.Shape.WorldBounds(piece.Position, piece.Rotation);
            if (piece.IsExpired || HitsGround(previousBounds, bounds)) _clippings.RemoveAt(i);
        }
    }

    private bool HitsGround(Bounds2D previous, Bounds2D current)
    {
        if (current.Bottom >= previous.Bottom || _chunks.Count == 0) return false;
        var map = _chunks.Values.First().Source;
        var firstX = Math.Clamp((int)MathF.Floor((Math.Min(previous.Left, current.Left) - map.Origin.X) / map.TileSize), 0, map.Width - 1);
        var lastX = Math.Clamp((int)MathF.Floor((Math.Max(previous.Right, current.Right) - map.Origin.X) / map.TileSize), 0, map.Width - 1);
        var firstY = Math.Clamp((int)MathF.Floor((current.Bottom - map.Origin.Y) / map.TileSize) - 1, 0, map.Height - 1);
        var lastY = Math.Clamp((int)MathF.Floor((previous.Bottom - map.Origin.Y) / map.TileSize), 0, map.Height - 1);
        for (var y = firstY; y <= lastY; y++)
        for (var x = firstX; x <= lastX; x++)
        {
            if (!_chunks.TryGetValue(new(x / map.ChunkSize, y / map.ChunkSize), out var chunk)) continue;
            var kind = chunk.Source.GetTileKind(x, y);
            var top = map.Origin.Y + (y + 1) * map.TileSize;
            if ((kind.IsCollidable() || kind.IsSpikes()) && current.Bottom <= top && previous.Bottom > top)
                return true;
        }
        return false;
    }

    public void DrawTrees(Renderer2D renderer, Bounds2D visible)
    {
        var wind = Wind();
        foreach (var chunk in _chunks.Values)
        foreach (var tree in chunk.Trees)
            if (visible.Intersects(tree.Bounds)) tree.Render(renderer, wind);
    }

    public void DrawGrass(Renderer2D renderer, Bounds2D visible)
    {
        var wind = Wind();
        foreach (var chunk in _chunks.Values)
        foreach (var patch in chunk.Patches)
        {
            var bounds = new Bounds2D(patch.Root - new Vector2(patch.Size * 0.5f, 0f),
                patch.Root + new Vector2(patch.Size * 1.5f, patch.Size * 1.2f));
            if (bounds.Intersects(visible))
                patch.Visual.Render(renderer, visible.Left, visible.Right, wind,
                    _cuts?.Contains(patch.Cell) == true ? patch.Size * VegetationPlacement2D.CutHeightInTiles : null);
        }
        foreach (var piece in _clippings)
        {
            if (visible.Intersects(piece.Shape.WorldBounds(piece.Position, piece.Rotation)))
                piece.Shape.Render(renderer, piece.Position, piece.Rotation, piece.Opacity);
        }
    }

    private VegetationWind2D Wind() => new(_time, 5f, 1.15f, 0.027f, 0.12f);

    private void Burst(Patch patch)
    {
        var random = new Random(VegetationPlacement2D.Seed(patch.Cell.X, patch.Cell.Y));
        var scale = patch.Size / 32f;
        foreach (var tip in patch.Visual.CreateClippings(patch.Size * VegetationPlacement2D.CutHeightInTiles, Wind()))
        {
            if (_clippings.Count >= 256) break;
            _clippings.Add(new(tip,
                new Vector2((random.NextSingle() - 0.5f) * 65f, 90f + random.NextSingle() * 40f) * scale,
                (random.NextSingle() - 0.5f) * 12f, random.NextSingle() * MathF.Tau, scale));
        }
    }

    private static VegetationStyle2D Style(string tileset, float size)
    {
        var dry = tileset == "dark-cave";
        var reeds = tileset == "mossy-cavern";
        return new(size * 0.55f, size * VegetationPlacement2D.MaximumHeightInTiles,
            size * 0.035f, size * 0.07f, size * 0.105f, size * 0.15f,
            dry ? new(105, 82, 39) : reeds ? new(18, 83, 82) : new(28, 104, 67),
            dry ? new(210, 183, 94) : reeds ? new(83, 190, 145) : new(129, 211, 109),
            dry || reeds ? 0f : 0.045f, new(255, 150, 201));
    }

    private sealed record Patch(GrassCell2D Cell, Vector2 Root, float Size, VegetationPatch2D Visual);
    private sealed record Chunk(TerrainChunkState2D Source, List<Patch> Patches, List<ProceduralTree2D> Trees);
}
