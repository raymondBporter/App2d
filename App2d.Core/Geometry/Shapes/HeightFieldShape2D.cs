using App2d.Core.Geometry.HeightFields;
using App2d.Core.Geometry.Operations.HeightFields;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Shapes;

/// <summary>
/// A finite heightfield solid with a polyline top, vertical end caps and a flat bottom.
/// Spline/function sources are sampled explicitly by Builder.FromHeightField; every shape query then
/// uses that same immutable snapshot. Collision pieces come from a slope scan, not polygon decomposition.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly record struct HeightFieldShape2D : IShape2D, IHeightField2D
{
    private readonly Vector2[]? _surface;
    private static readonly ConditionalWeakTable<Vector2[], Dictionary<float, Prepared>> PreparedShapes = [];

    /// <summary>Snapshots source data without validating, consistent with other shape constructors. Prefer Builder.</summary>
    public HeightFieldShape2D(IEnumerable<Vector2> surface, float yMin)
    {
        _surface = surface?.ToArray();
        YMin = yMin;
    }

    public string Kind => ShapeKinds2D.HeightField;
    public ReadOnlySpan<Vector2> Surface { get => _surface; init => _surface = value.ToArray(); }
    public float YMin { get; init; }
    public float XMin => Profile.XMin;
    public float XMax => Profile.XMax;
    public float YMax => Profile.YMax;
    public float Area => Polygon.Area;
    public float Diameter => Polygon.Diameter;
    public PolylineHeightField2D Profile => Data.Profile;

    /// <summary>Convex runs closed down to YMin. Adjacent pieces share only their vertical boundary.</summary>
    public CompositeShape2D ConvexPieces => Data.Pieces;
    internal SimplePolygon2D Polygon => Data.Polygon;
    public float HeightAt(float x) => Profile.HeightAt(x);
    public float SlopeAt(float x) => Profile.SlopeAt(x);
    public bool ContainsPoint(Vector2 point) => Profile.ContainsPoint(point);
    public void Validate() => _ = Data;

    private Prepared Data
    {
        get
        {
            ArgGuard.ThrowIfNull(_surface, "surface");
            var entries = PreparedShapes.GetValue(_surface, static _ => []);
            lock (entries)
            {
                if (!entries.TryGetValue(YMin, out var data))
                {
                    ArgGuard.Finite(YMin);
                    var profile = new PolylineHeightField2D(_surface, YMin);
                    var polygon = SimplePolygon2D.Builder.BelowPolyline(_surface, YMin);
                    data = new(profile, polygon, profile.ToConvexPieces());
                    entries[YMin] = data;
                }
                return data;
            }
        }
    }

    private sealed record Prepared(PolylineHeightField2D Profile, SimplePolygon2D Polygon, CompositeShape2D Pieces);

    public static class Builder
    {
        /// <summary>Preserves the authored polyline exactly, with no curve sampling or general polygon splitting.</summary>
        public static HeightFieldShape2D Create(IEnumerable<Vector2> surface, float yMin) => new HeightFieldShape2D(surface, yMin).Validated();

        /// <summary>Freezes a finite heightfield into a shape. Polyline sources keep their original vertices.</summary>
        public static HeightFieldShape2D FromHeightField(IHeightField2D field,
            float maximumSegmentLength = 12f, float maximumDeviation = 0.05f)
        {
            ArgGuard.ThrowIfNull(field);
            if (!float.IsFinite(field.YMin))
                throw new InvalidOperationException("A heightfield shape needs a finite bottom. Use ToClippedShape with an explicit cutoff.");
            ArgGuard.Positive(maximumSegmentLength);
            ArgGuard.Positive(maximumDeviation);
            var surface = field switch
            {
                HeightFieldShape2D shape => shape.Surface.ToArray(),
                PolylineHeightField2D polyline => polyline.Curve.Points.ToArray(),
                _ => field.ToPolyline(maximumSegmentLength, maximumDeviation).Points.ToArray()
            };
            return Create(surface, field.YMin);
        }
    }
}
