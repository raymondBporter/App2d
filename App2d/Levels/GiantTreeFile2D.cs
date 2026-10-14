using App2d.Core.Geometry;
using App2d.Core.Tiles;
using App2d.Core.Validation;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Levels;

/// <summary>Editable giant trees alongside level.db. All file dimensions and positions are in tiles.</summary>
public static class GiantTreeFile2D
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static ImmutableArray<GiantTree2D> Load(string path, IChunkedTileMap2D map)
    {
        ArgGuard.ThrowIfNull(map);
        if (!File.Exists(path)) return [];
        try
        {
            var file = JsonSerializer.Deserialize<TreeFile>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException("Tree document is null.");
            if (file.Version != 1 || file.Trees is null) throw new InvalidDataException("Expected tree format version 1.");
            var length = WorldRange(file.BranchLengthTiles, map.TileSize, "branchLengthTiles");
            var gap = WorldRange(file.BranchGapTiles, map.TileSize, "branchGapTiles");
            var foliage = WorldRange(file.FoliageRadiusTiles, map.TileSize, "foliageRadiusTiles");
            var trees = ImmutableArray.CreateBuilder<GiantTree2D>(file.Trees.Length);
            foreach (var entry in file.Trees)
            {
                if (entry is null) throw new InvalidDataException("A tree entry cannot be null.");
                ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(entry.XTiles, 0f, map.Width - 1f, "xTiles");
                var ground = entry.RootYTiles ?? TileGroundHeights2D.AtColumn(map, (int)entry.XTiles);
                ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(ground, 0f, map.Height, "rootYTiles");
                var root = map.Origin + new Vector2(entry.XTiles, ground) * map.TileSize;
                trees.Add(new(root, new()
                {
                    Height = entry.HeightTiles * map.TileSize,
                    TrunkWidth = entry.TrunkWidthTiles * map.TileSize,
                    BranchLength = length,
                    BranchGap = gap,
                    BranchThickness = file.BranchThicknessTiles * map.TileSize,
                    FoliageRadius = foliage
                }, entry.Seed));
            }
            return trees.MoveToImmutable();
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidDataException)
        {
            throw new InvalidDataException($"Invalid giant trees in '{path}': {e.Message}", e);
        }
    }

    private static Interval<float> WorldRange(float[]? values, float tileSize, string name)
    {
        if (values is not { Length: 2 }) throw new InvalidDataException($"{name} must be [minimum, maximum].");
        var range = new Interval<float>(values[0] * tileSize, values[1] * tileSize);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(range.Min, name);
        ArgGuard.ThrowIfNotFiniteOrLessThan(range.Max, range.Min, name);
        return range;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json.")]
    private sealed record TreeFile(int Version, TreeEntry[] Trees)
    {
        public float[] BranchLengthTiles { get; init; } = [4, 12];
        public float[] BranchGapTiles { get; init; } = [2, 3];
        public float BranchThicknessTiles { get; init; } = 0.75f;
        public float[] FoliageRadiusTiles { get; init; } = [1.75f, 3.5f];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json.")]
    private sealed record TreeEntry(
        [property: JsonRequired] float XTiles,
        [property: JsonRequired] float HeightTiles,
        [property: JsonRequired] float TrunkWidthTiles,
        [property: JsonRequired] int Seed,
        float? RootYTiles);
}
