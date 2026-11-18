namespace App2d.Tests.Geometry;

/// <summary>Absolute per-component tolerances for vector assertions.</summary>
internal static class VectorComparers
{
    public static readonly IEqualityComparer<Vector2f> Near = new Tolerance(1e-5f);
    public static readonly IEqualityComparer<Vector2f> Loose = new Tolerance(1e-3f);

    private sealed class Tolerance(float tolerance) : IEqualityComparer<Vector2f>
    {
        public bool Equals(Vector2f x, Vector2f y) => MathF.Abs(x.X - y.X) <= tolerance && MathF.Abs(x.Y - y.Y) <= tolerance;
        public int GetHashCode(Vector2f obj) => 0;
    }
}
