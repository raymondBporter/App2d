namespace App2d.Core.Grids;

/// <summary>Integer coordinates in a grid. Negative coordinates are valid; no storage or world position is implied.</summary>
public readonly record struct GridCell2D(int X, int Y);
