using App2d.Contracts.World;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Grids;
using App2d.Core.Tiles;
using App2d.Core.Vegetation;
using App2d.Gameplay.World;
using App2d.Gameplay.World.Vegetation;
using App2d.Presentation.World.Vegetation;
using App2d.Rendering;
using System.Collections.Immutable;

namespace App2d.Presentation.World.Presentation;

/// <summary>Foliage cached by source chunk. The simulation supplies cut state.</summary>
public sealed class VegetationPresentation2D : IDisposable
{
    private const float CutRoughness = 0.6f;
    private const float MinimumTreeHeightInTiles = 4.5f;
    private const float TreeHeightVariationInTiles = 2f;
    private const int TreeColumnSpacing = 8;
    private const int TreeColumnOffset = 4;
    private const int TreeClearanceHalfWidth = 3;
    private const int TreeClearanceHeight = 10;
    private readonly ViewportTerrainSource2D? _terrainSource;
    private readonly Dictionary<TileChunk2D, Chunk> _chunks = [];
    private readonly List<GrassClipping2D> _clippings = [];
    internal int ClippingCount => _clippings.Count;
    private ImmutableHashSet<GrassCell2D>? _cuts;
    private ImmutableArray<TerrainChunkState2D> _terrain;
    private double _time;
    private ImmutableArray<GiantTree2D> _giantTrees = [];
    private GiantTreeVisual2D[] _giantTreeVisuals = [];

    public VegetationPresentation2D() : this(null) { }

    internal VegetationPresentation2D(ViewportTerrainSource2D? terrainSource)
    {
        _terrainSource = terrainSource;
        if (_terrainSource is not null)
            _terrainSource.ChunkChanged += InvalidateChunk;
    }

    private IChunkedTileMap2D? TerrainMap => _terrainSource?.Map ?? (_terrain.IsDefaultOrEmpty ? null : _terrain[0]);

    public void SetGiantTrees(ImmutableArray<GiantTree2D> trees)
    {
        if (_giantTrees == trees)
        {
            return;
        }

        _giantTrees = trees;
        _giantTreeVisuals = [.. trees.Select(tree => new GiantTreeVisual2D(tree))];
    }

    public void SetTerrain(ImmutableArray<TerrainChunkState2D> terrain)
    {
        if (_terrain == terrain)
        {
            return;
        }

        _terrain = terrain;
        var sources = terrain.ToDictionary(c => c.Chunk);
        foreach (var key in _chunks.Keys.Where(k => !sources.ContainsKey(k)).ToArray())
        {
            _chunks.Remove(key);
        }

        foreach (var map in terrain)
        {
            if (_chunks.TryGetValue(map.Chunk, out var old) && ReferenceEquals(old.Source, map))
            {
                continue;
            }

            _chunks[map.Chunk] = new(map) { Patches = BuildPatches(map, map.Chunk) };
        }
        // Remote observations contain only the chunks supplied by the session.
        foreach (var (id, chunk) in _chunks)
            chunk.Trees = BuildTrees(chunk.Source, id, sources);
    }

    private static List<Patch> BuildPatches(IChunkedTileMap2D map, TileChunk2D chunk)
    {
        var patches = new List<Patch>();
        for (var y = chunk.Y * map.ChunkSize; y < Math.Min(map.Height, (chunk.Y + 1) * map.ChunkSize); y++)
        {
            for (var x = chunk.X * map.ChunkSize; x < Math.Min(map.Width, (chunk.X + 1) * map.ChunkSize); x++)
            {
                if (!VegetationPlacement2D.HasGrass(map, x, y))
                {
                    continue;
                }

                var cell = new GrassCell2D(x, y);
                var seed = VegetationPlacement2D.Seed(x, y);
                var root = map.Origin + new Vector2f(x, y + 1) * map.TileSize;
                var style = Style(map.TilesetIds[map.GetTilesetIndex(x, y)], map.TileSize);
                patches.Add(new(cell, root, map.TileSize,
                    new VegetationPatch2D(root.X, root.X + map.TileSize, root.Y, style, seed)));
            }
        }
        return patches;
    }

    private static List<ProceduralTree2D> BuildTrees(IChunkedTileMap2D map, TileChunk2D chunk,
        Dictionary<TileChunk2D, TerrainChunkState2D>? sources = null)
    {
        var trees = new List<ProceduralTree2D>();
        var startX = chunk.X * map.ChunkSize;
        var endX = Math.Min(map.Width, startX + map.ChunkSize);
        // One candidate per eight columns avoids trees bunching together.
        var firstCandidate = startX + (TreeColumnOffset + TreeColumnSpacing - startX % TreeColumnSpacing) % TreeColumnSpacing;
        for (var y = chunk.Y * map.ChunkSize; y < Math.Min(map.Height, (chunk.Y + 1) * map.ChunkSize); y++)
        {
            for (var x = firstCandidate; x < endX; x += TreeColumnSpacing)
            {
                var seed = VegetationPlacement2D.Seed(x, y);
                if (seed % 3 != 0 && VegetationPlacement2D.HasGrass(map, x, y) &&
                    GrowsTrees(map.TilesetIds[map.GetTilesetIndex(x, y)]) &&
                    VegetationPlacement2D.HasGrass(map, x - 1, y) &&
                    VegetationPlacement2D.HasGrass(map, x + 1, y) && HasTreeClearance(map, x, y, sources))
                {
                    var root = map.Origin + new Vector2f(x + 0.5f, y + 1) * map.TileSize;
                    trees.Add(new(root, map.TileSize * (MinimumTreeHeightInTiles + seed % 100 / 50f), seed));
                }
            }
        }
        return trees;
    }

    private static bool HasTreeClearance(IChunkedTileMap2D map, int x, int y,
        Dictionary<TileChunk2D, TerrainChunkState2D>? sources)
    {
        for (var dy = 1; dy <= TreeClearanceHeight; dy++)
        {
            for (var dx = -TreeClearanceHalfWidth; dx <= TreeClearanceHalfWidth; dx++)
            {
                var tx = x + dx;
                var ty = y + dy;
                if (tx < 0 || tx >= map.Width || ty >= map.Height)
                    return false;

                var source = map;
                if (sources is not null)
                {
                    if (!sources.TryGetValue(new(tx / map.ChunkSize, ty / map.ChunkSize), out var snapshot))
                        return false;
                    source = snapshot;
                }
                if (source.GetTileKind(tx, ty) != TileKind2D.Empty)
                    return false;
            }
        }

        return true;
    }

    public void ApplyCuts(ImmutableHashSet<GrassCell2D> cuts)
    {
        if (ReferenceEquals(_cuts, cuts))
        {
            return;
        }

        if (_cuts is not null && TerrainMap is { } map)
        {
            foreach (var cell in cuts)
            {
                if (_cuts.Contains(cell) || !_chunks.TryGetValue(new(cell.X / map.ChunkSize, cell.Y / map.ChunkSize), out var chunk) ||
                    chunk.Patches is not { } patches)
                    continue;

                foreach (var patch in patches)
                {
                    if (patch.Cell == cell)
                    {
                        Burst(patch);
                        break;
                    }
                }
            }
        }
        _cuts = cuts;
    }

    public void Advance(float dt)
    {
        Core.Validation.Ensure.NonNegative(dt);
        _time += dt;
        var wind = Wind();
        for (var i = _clippings.Count - 1; i >= 0; i--)
        {
            var piece = _clippings[i];
            var previousBounds = piece.Shape.WorldBounds(piece.Position, piece.Rotation);
            piece.Advance(dt, wind);
            var bounds = piece.Shape.WorldBounds(piece.Position, piece.Rotation);
            if (piece.IsExpired || HitsGround(previousBounds, bounds))
            {
                _clippings.RemoveAt(i);
            }
        }
    }

    private bool HitsGround(Rect2f previous, Rect2f current)
    {
        if (current.Bottom >= previous.Bottom || TerrainMap is not { } map)
        {
            return false;
        }

        var firstX = Math.Clamp((int)MathF.Floor((Math.Min(previous.Left, current.Left) - map.Origin.X) / map.TileSize), 0, map.Width - 1);
        var lastX = Math.Clamp((int)MathF.Floor((Math.Max(previous.Right, current.Right) - map.Origin.X) / map.TileSize), 0, map.Width - 1);
        var firstY = Math.Clamp((int)MathF.Floor((current.Bottom - map.Origin.Y) / map.TileSize) - 1, 0, map.Height - 1);
        var lastY = Math.Clamp((int)MathF.Floor((previous.Bottom - map.Origin.Y) / map.TileSize), 0, map.Height - 1);
        for (var y = firstY; y <= lastY; y++)
        {
            for (var x = firstX; x <= lastX; x++)
            {
                var source = map;
                if (_terrainSource is null)
                {
                    if (!_chunks.TryGetValue(new(x / map.ChunkSize, y / map.ChunkSize), out var chunk))
                        continue;
                    source = chunk.Source;
                }

                var kind = source.GetTileKind(x, y);
                var top = map.Origin.Y + (y + 1) * map.TileSize;
                if ((kind.IsCollidable() || kind.IsSpikes()) && current.Bottom <= top && previous.Bottom > top)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void DrawTrees(Renderer2D renderer, Rect2f visible)
    {
        var wind = Wind();
        foreach (var tree in _giantTreeVisuals)
        {
            tree.Render(renderer, visible, wind);
        }

        var sway = new Vector2f(ProceduralTree2D.MaximumSway(wind), 0);
        var cullBounds = new Rect2f(visible.Min - sway, visible.Max + sway);
        if (_terrainSource is not null)
        {
            foreach (var cell in QueryTreeChunks(cullBounds))
                DrawTreeChunk(renderer, cullBounds, wind, GetTrees(new(cell.X, cell.Y)));
            return;
        }

        foreach (var chunk in _chunks.Values)
            DrawTreeChunk(renderer, cullBounds, wind, chunk.Trees!);
    }

    private static void DrawTreeChunk(Renderer2D renderer, Rect2f visible, VegetationWind2D wind, List<ProceduralTree2D> trees)
    {
        foreach (var tree in trees)
        {
            if (visible.Intersects(tree.Bounds))
                tree.Render(renderer, wind);
        }
    }

    internal GridCellRange2D QueryTreeChunks(Rect2f visible)
    {
        var size = _terrainSource!.Map.TileSize;
        var reach = ProceduralTree2D.EstimateBounds(Vector2f.Zero, size * (MinimumTreeHeightInTiles + TreeHeightVariationInTiles));
        return QueryRoots(visible, reach, new(size * 0.5f, size));
    }

    internal List<ProceduralTree2D> GetTrees(TileChunk2D id)
    {
        var chunk = GetLocalChunk(id);
        return chunk.Trees ??= BuildTrees(chunk.Source, id);
    }

    /// <summary>Back grass and flowers draw before characters; front tufts and clippings after them.</summary>
    public void DrawGrass(Renderer2D renderer, Rect2f visible, VegetationLayer2D layer)
    {
        var wind = Wind();
        if (_terrainSource is { } terrain)
        {
            var size = terrain.Map.TileSize;
            foreach (var cell in QueryRoots(visible, PatchBounds(Vector2f.Zero, size), new(0, size)))
            {
                var id = new TileChunk2D(cell.X, cell.Y);
                var chunk = GetLocalChunk(id);
                DrawGrassChunk(renderer, visible, wind, layer, chunk.Patches ??= BuildPatches(chunk.Source, id));
            }
        }
        else
        {
            foreach (var chunk in _chunks.Values)
                DrawGrassChunk(renderer, visible, wind, layer, chunk.Patches!);
        }

        if (layer != VegetationLayer2D.Front)
        {
            return;
        }

        foreach (var piece in _clippings)
        {
            if (visible.Intersects(piece.Shape.WorldBounds(piece.Position, piece.Rotation)))
            {
                piece.Shape.Render(renderer, piece.Position, piece.Rotation, piece.Opacity);
            }
        }
    }

    private void DrawGrassChunk(Renderer2D renderer, Rect2f visible, VegetationWind2D wind, VegetationLayer2D layer, List<Patch> patches)
    {
        foreach (var patch in patches)
        {
            if (PatchBounds(patch.Root, patch.Size).Intersects(visible))
                patch.Visual.Render(renderer, visible.Left, visible.Right, wind, layer,
                    _cuts?.Contains(patch.Cell) == true ? patch.Size * VegetationPlacement2D.CutHeightInTiles : null,
                    CutRoughness);
        }
    }

    private static Rect2f PatchBounds(Vector2f root, float size) =>
        new(root - new Vector2f(size * 0.5f, 0f), root + new Vector2f(size * 1.5f, size * 1.2f));

    // Invert the overlap to find the cells that could own those roots.
    private GridCellRange2D QueryRoots(Rect2f visible, Rect2f reach, Vector2f rootOffset) =>
        _terrainSource!.QueryChunks(new(visible.Min - reach.Max - rootOffset, visible.Max - reach.Min - rootOffset));

    private Chunk GetLocalChunk(TileChunk2D id)
    {
        if (!_chunks.TryGetValue(id, out var chunk))
        {
            chunk = new(_terrainSource!.Map);
            _chunks.Add(id, chunk);
        }
        return chunk;
    }

    private void InvalidateChunk(TileChunk2D changed)
    {
        _chunks.Remove(changed);
        var size = _terrainSource!.Map.ChunkSize;
        var side = (TreeClearanceHalfWidth - 1) / size + 1;
        var below = (TreeClearanceHeight - 1) / size + 1;
        // A root can depend on tiles beside it and above it, even across chunk edges.
        for (var y = Math.Max(0, changed.Y - below); y <= changed.Y; y++)
        {
            for (var x = Math.Max(0, changed.X - side); x <= changed.X + side; x++)
            {
                if (_chunks.TryGetValue(new(x, y), out var neighbor))
                    neighbor.Trees = null;
            }
        }
    }

    public void Dispose()
    {
        if (_terrainSource is not null)
            _terrainSource.ChunkChanged -= InvalidateChunk;
        _chunks.Clear();
        _clippings.Clear();
    }

    private VegetationWind2D Wind() => new(_time, 5f, 1.15f, 0.027f, 0.12f);

    private void Burst(Patch patch)
    {
        var random = new Random(VegetationPlacement2D.Seed(patch.Cell.X, patch.Cell.Y));
        var scale = patch.Size / 32f;
        foreach (var tip in patch.Visual.CreateClippings(patch.Size * VegetationPlacement2D.CutHeightInTiles, Wind(), CutRoughness))
        {
            if (_clippings.Count >= 256)
            {
                break;
            }

            _clippings.Add(new(tip,
                new Vector2f(random.NextFloat(-0.5f, 0.5f) * 65f, random.NextFloat(90f, 130f)) * scale,
                random.NextFloat(-0.5f, 0.5f) * 12f, random.NextFloat(MathF.Tau), scale));
        }
    }

    /// <summary>The leafy procedural trees would contradict a dead wasteland.</summary>
    private static bool GrowsTrees(string tileset) => !tileset.StartsWith("ink-wasteland-", StringComparison.Ordinal);

    private static VegetationStyle2D Style(string tileset, float size)
    {
        // Wasteland grass is sparse and dead, and concrete barely grows any; prehistoric grass runs tall.
        var dry = tileset is "dark-cave" or "ink-wasteland-ground" or "ink-wasteland-wall";
        var reeds = tileset == "mossy-cavern";
        var lush = tileset.StartsWith("ink-prehistoric-", StringComparison.Ordinal);
        var coverage = tileset switch
        {
            "ink-wasteland-wall" => 0.12f,
            "ink-wasteland-ground" => 0.35f,
            _ when dry => 0.45f,
            _ when lush => 0.75f,
            _ => 0.6f
        };
        return new(size * 0.25f, size * 0.53f, size * (lush ? 1.1f : 0.9f), size * 0.16f, size * 0.39f,
            dry ? new(150, 118, 52) : reeds ? new(36, 128, 104) : new(79, 184, 47),
            dry ? new(206, 176, 92) : reeds ? new(82, 184, 148) : new(108, 203, 66),
            reeds || lush ? 0.4f : 0.2f, 0.22f, dry || reeds ? 0f : 0.08f,
            Coverage: coverage, PatchWidth: size * 6f, SprigChance: 0.35f);
    }

    private sealed record Patch(GrassCell2D Cell, Vector2f Root, float Size, VegetationPatch2D Visual);
    private sealed class Chunk(IChunkedTileMap2D source)
    {
        public IChunkedTileMap2D Source { get; } = source;
        public List<Patch>? Patches { get; set; }
        public List<ProceduralTree2D>? Trees { get; set; }
    }
}
