namespace App2d.Core.Shapes;

/// <summary>Stable kind IDs used by authored shape JSON and shape editors.</summary>
public static class ShapeKinds2D
{
    public const string Circle = "circle";
    public const string Ellipse = "ellipse";
    public const string Capsule = "capsule";
    public const string Rectangle = "rectangle";
    public const string Triangle = "triangle";
    public const string ConvexPolygon = "convex-polygon";
    public const string HalfSpace = "half-space";
    public const string Composite = "composite";

    /// <summary>Every kind in a stable order for editors and validation.</summary>
    public static IReadOnlyList<string> All { get; } = [Circle, Ellipse, Capsule, Rectangle, Triangle, ConvexPolygon, HalfSpace, Composite];
}
