using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry;

/// <summary>
/// A JSON-friendly pair of coordinates for authored definitions such as curves and shapes.
/// Runtime geometry uses <see cref="Vector2"/>; convert with <see cref="Vector"/> and <see cref="From"/>.
/// </summary>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
public readonly record struct Point2D(float X, float Y)
{
    /// <summary>The same coordinates as a runtime vector.</summary>
    [JsonIgnore] public Vector2 Vector => new(X, Y);

    /// <summary>Copies a runtime vector into definition data.</summary>
    /// <param name="point">The vector to copy.</param>
    /// <returns>A point with the same coordinates.</returns>
    public static Point2D From(Vector2 point) => new(point.X, point.Y);
}
