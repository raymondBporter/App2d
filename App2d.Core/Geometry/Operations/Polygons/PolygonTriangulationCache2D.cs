using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Conversion;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Operations.Polygons;

/// <summary>Reuses rest-topology triangulation until a polygon's XY coordinates or tolerance change.</summary>
/// <remarks>Keys are weak references. Edit a perimeter between calls, never concurrently with a call.</remarks>
public static class PolygonTriangulationCache2D
{
    private static readonly ConditionalWeakTable<object, Entry> Entries = [];

    public static TriangleMesh2D Get(IReadOnlyList<Vector2> perimeter, double tolerance = 0) =>
        Get(perimeter, static point => point, tolerance);

    public static TriangleMesh2D GetXY(IReadOnlyList<Vector3> perimeter, double tolerance = 0) =>
        Get(perimeter, static point => new Vector2(point.X, point.Y), tolerance);

    /// <summary>Projects arbitrary authored vertices or vertex-buffer entries into XY before comparing or triangulating.</summary>
    public static TriangleMesh2D Get<TPoint>(IReadOnlyList<TPoint> perimeter, Func<TPoint, Vector2> xy, double tolerance = 0)
    {
        ArgGuard.ThrowIfNull(perimeter);
        ArgGuard.ThrowIfNull(xy);
        ArgGuard.NonNegative(tolerance);
        var entry = Entries.GetValue(perimeter, static _ => new());
        lock (entry)
        {
            if (entry.Mesh is { } cached && tolerance == entry.Tolerance && Matches(perimeter, cached.Vertices, xy))
                return cached;

            var mesh = PolygonTriangulation2D.Triangulate(perimeter.Select(xy), tolerance);
            entry.Mesh = mesh;
            entry.Tolerance = tolerance;
            return mesh;
        }
    }

    public static CompositeShape2D GetConvexPieces(IReadOnlyList<Vector2> perimeter, double tolerance = 0)
    {
        var mesh = Get(perimeter, tolerance);
        var entry = Entries.GetValue(perimeter, static _ => new());
        lock (entry)
        {
            if (!ReferenceEquals(entry.PiecesMesh, mesh))
            {
                entry.Pieces = mesh.ToCompositeShape();
                entry.PiecesMesh = mesh;
            }
            return entry.Pieces;
        }
    }

    /// <summary>Installs a triangulation constructed with a known topology, such as monotone terrain strips.</summary>
    internal static void StorePrepared(IReadOnlyList<Vector2> perimeter, TriangleMesh2D mesh, double tolerance)
    {
        StateGuard.ThrowIfNot(Matches(perimeter, mesh.Vertices, static point => point));
        var entry = Entries.GetValue(perimeter, static _ => new());
        lock (entry)
        {
            entry.Mesh = mesh;
            entry.Tolerance = tolerance;
            entry.PiecesMesh = null;
        }
    }

    private static bool Matches<TPoint>(IReadOnlyList<TPoint> perimeter, ReadOnlySpan<Vector2> snapshot, Func<TPoint, Vector2> xy)
    {
        if (perimeter.Count != snapshot.Length) return false;
        for (var i = 0; i < snapshot.Length; i++)
            if (xy(perimeter[i]) != snapshot[i]) return false;
        return true;
    }

    private sealed class Entry
    {
        public TriangleMesh2D? Mesh;
        public TriangleMesh2D? PiecesMesh;
        public CompositeShape2D Pieces;
        public double Tolerance;
    }
}
