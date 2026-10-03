namespace App2d.Core.Curves;

/// <summary>Stable kind IDs used by authored curve JSON and future curve editors.</summary>
public static class CurveKinds2D
{
    public const string Line = "line";
    public const string Polyline = "polyline";
    public const string Arc = "arc";
    public const string QuadraticBezier = "quadratic-bezier";
    public const string CubicBezier = "cubic-bezier";
    public const string BSpline = "b-spline";

    public static IReadOnlyList<string> All { get; } =
        [Line, Polyline, Arc, QuadraticBezier, CubicBezier, BSpline];
}
