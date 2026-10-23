using System.Numerics;

namespace App2d.Tests.Geometry;

/// <summary>Absolute per-component tolerances for vector assertions.</summary>
internal static class VectorComparers
{
    public static readonly IEqualityComparer<Vector2> Near = new Tolerance(1e-5f);
    public static readonly IEqualityComparer<Vector2> Loose = new Tolerance(1e-3f);

    private sealed class Tolerance(float tolerance) : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 x, Vector2 y) => MathF.Abs(x.X - y.X) <= tolerance && MathF.Abs(x.Y - y.Y) <= tolerance;
        public int GetHashCode(Vector2 obj) => 0;
    }
}
