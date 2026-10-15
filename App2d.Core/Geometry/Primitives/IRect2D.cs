using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// An axis-aligned rectangle in its own coordinate space, with Y increasing upward.
/// Min and Max are component-wise ordered and never NaN. Infinite bounds are allowed.
/// Implement these two properties to receive the shared <see cref="Rect2DExtensions"/>; no shape or bounds cache is required.
/// </summary>
public interface IRect2D
{
    /// <summary>The lower-left corner.</summary>
    Vector2 Min { get; }

    /// <summary>The upper-right corner.</summary>
    Vector2 Max { get; }
}
