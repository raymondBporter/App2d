using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.HeightFields;
using App2d.Core.Geometry.Operations.Curves;
using App2d.Core.Geometry.Operations.HeightFields;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Validation;
using App2d.Rendering;

namespace App2d.Trials;

/// <summary>A heightfield source sampled once into the shared visible/collidable terrain boundary.</summary>
public sealed class TrialsTerrain2D
{
    private static readonly Color4b SurfaceColor = new(117, 193, 150);
    private static readonly Lazy<CourseData> DefaultCourse = new(() => CreateCourse(TrialsCourse2D.Default.CreateCurve(), TrialsCourse2D.Default.YMin));
    private readonly CourseData _data;

    public TrialsTerrain2D(PhysicsWorld2D world) : this(world, DefaultCourse.Value) { }

    public TrialsTerrain2D(PhysicsWorld2D world, TrialsCourse2D course)
        : this(world, CreateCourse(course)) { }

    /// <summary>Accepts a B-spline, polyline, or other strictly left-to-right curve. The region below it is solid.</summary>
    public TrialsTerrain2D(PhysicsWorld2D world, ICurve2D curve, float bottom = -1800f)
        : this(world, CreateCourse(curve, bottom)) { }

    /// <summary>Accepts analytic functions as well as polyline/spline sources. Trials requires a finite bottom.</summary>
    public TrialsTerrain2D(PhysicsWorld2D world, IHeightField2D heightField)
        : this(world, CreateCourse(heightField)) { }

    private TrialsTerrain2D(PhysicsWorld2D world, CourseData data)
    {
        _data = data;
        Ground = new WorldObject2D(data.Solid, new SolidColorShader(new Color4b(18, 23, 28)));
        var body = world.AddBody(Ground, BodyMotionType2D.Static);
        body.Restitution = 0f;
        body.Friction = 1.4f;
        body.CollisionLayer = TrialsBike2D.GroundCollisionLayer;
        body.CollisionMask = TrialsBike2D.BikeCollisionLayer;
    }

    /// <summary>The original source. HeightAt on this terrain instead queries the baked collision profile.</summary>
    public IHeightField2D HeightField => _data.HeightField;
    public ICurve2D Curve => _data.Curve;
    public PolylineCurve2D Surface => Shape.Profile.Curve;
    public HeightFieldShape2D Shape => _data.Solid;
    public WorldObject2D Ground { get; }

    /// <summary>Height of the actual collision polyline, clamped to the course's end heights.</summary>
    public float HeightAt(float x)
    {
        ArgGuard.Finite(x);
        var profile = Shape.Profile;
        return profile.HeightAt(Math.Clamp(x, profile.XMin, profile.XMax));
    }

    public void Render(Renderer2D renderer, Rect2D visible)
    {
        renderer.Draw(Ground);
        renderer.DrawWorldPolyline(Surface.Points, SurfaceColor, 3f);
        var points = Surface.Points;
        var minimum = Math.Max(points[0].X, visible.Min.X);
        var maximum = Math.Min(points[^1].X, visible.Max.X);
        for (var x = MathF.Ceiling(minimum / 100f) * 100f; x <= maximum; x += 100f)
        {
            var height = HeightAt(x);
            renderer.DrawWorldSegment(new(x, height), new(x, height - 8f), SurfaceColor);
        }
    }

    private static CourseData CreateCourse(TrialsCourse2D course)
    {
        ArgGuard.ThrowIfNull(course);
        return ReferenceEquals(course, TrialsCourse2D.Default)
            ? DefaultCourse.Value : CreateCourse(course.CreateCurve(), course.YMin);
    }

    private static CourseData CreateCourse(ICurve2D curve, float bottom)
    {
        ArgGuard.ThrowIfNull(curve);
        IHeightField2D heightField = curve switch
        {
            BSpline2D spline => new BSplineHeightField2D(spline, bottom),
            PolylineCurve2D polyline => new PolylineHeightField2D(polyline, bottom),
            _ => new PolylineHeightField2D(Curve2D.ToPolyline(curve), bottom)
        };
        return CreateCourse(heightField) with { Curve = curve };
    }

    private static CourseData CreateCourse(IHeightField2D heightField)
    {
        ArgGuard.ThrowIfNull(heightField);
        ArgGuard.ThrowIf(!float.IsFinite(heightField.YMin), "Trials needs a finite YMin for its polygon collider.", nameof(heightField));
        var shape = heightField.ToShape(maximumSegmentLength: 12f, maximumDeviation: 0.05f);
        ICurve2D curve = heightField switch
        {
            BSplineHeightField2D spline => spline.Curve,
            PolylineHeightField2D polyline => polyline.Curve,
            _ => shape.Profile.Curve
        };
        return new(heightField, curve, shape);
    }

    private sealed record CourseData(IHeightField2D HeightField, ICurve2D Curve, HeightFieldShape2D Solid);
}
