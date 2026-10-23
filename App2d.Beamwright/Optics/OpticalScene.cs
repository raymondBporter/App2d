using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.ClosestPoint;
using App2d.Core.Geometry.Operations.Intersection;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Beamwright.Optics;

internal enum Interaction { Absorb, Refract, Mirror, Split, Lens, CurvedMirror, Sensor, Mirage }

internal sealed class OpticalPort(Segment<Vector2> localEdge, Frame2D frame)
{
    public Segment<Vector2> LocalEdge { get; } = localEdge;
    public Vector2 Midpoint => frame.TransformPoint(LocalEdge.Midpoint);
    public bool Joined { get; set; }
}

/// <summary>Material and pose on existing Core geometry. A circle's material decides whether it absorbs or refracts.</summary>
internal sealed class OpticalBody
{
    public required string Id { get; init; }
    public required Interaction Interaction { get; init; }
    public required IGeometryValue2D Geometry { get; init; }
    public Frame2D Frame { get; init; } = Frame2D.Identity;
    public Matrix3x2 Inverse { get; private set; }
    public PartDefinition? Part { get; init; }
    public int Target { get; init; } = -1;
    public string Color { get; init; } = "any";
    public OpticalPort[] Ports { get; init; } = [];

    public void Prepare()
    {
        if (!Matrix3x2.Invert(Frame.Matrix, out var inverse)) throw new InvalidOperationException("Singular optical frame.");
        Inverse = inverse;
    }

    public bool TryHit(Ray2D ray, float limit, out RayHit2D hit, out Vector2 localPoint)
    {
        var origin = Vector2.Transform(ray.Origin, Inverse);
        var direction = Vector2.TransformNormal(ray.Direction, Inverse);
        hit = default;
        localPoint = default;
        // Upstream's box/slab query ignores the exit of a volume the ray is already inside.
        if (Interaction is Interaction.Mirage or Interaction.Absorb && Geometry is AxisAlignedRectangle2D box && box.ContainsPoint(origin)) return false;
        var found = GeometryRaycast2D.TryIntersect(origin, direction, Geometry, limit, out hit);
        if (!found || hit.Distance <= 1e-7f) return false;
        localPoint = hit.Point;
        hit = new(ray.PointAt(hit.Distance), Vector2.Normalize(Inverse.TransposeTransformDirection(hit.Normal)), hit.Distance);
        return true;
    }

    public bool IsJoinedPort(Vector2 point) => Ports.Any(p => p.Joined &&
        Vector2.DistanceSquared(point, ClosestPoint2D.OnSegment(point, p.LocalEdge.Start, p.LocalEdge.End)) < 1e-8f);
}

internal readonly record struct SceneHit(OpticalBody? Body, RayHit2D Hit, Vector2 LocalPoint);

internal sealed class OpticalScene
{
    private readonly List<OpticalBody> _bodies = [];
    private readonly BenchLevel _level;

    public OpticalScene(BeamwrightContent content, BenchLevel level, IReadOnlyList<PartPlacement> placed)
    {
        _level = level;
        for (var i = 0; i < level.Fixed.Length; i++) 
            AddPart(content, level.Fixed[i], "f" + i);
        for (var i = 0; i < placed.Count; i++) 
            AddPart(content, placed[i], "p" + i);
        var ports = _bodies.SelectMany(b => b.Ports).ToArray();
        foreach (var port in ports)
            port.Joined = ports.Any(other => other != port && Vector2.Distance(other.Midpoint, port.Midpoint) < .03f);
        for (var i = 0; i < level.Sources.Length; i++)
        {
            var source = level.Sources[i];
            Add(new()
            {
                Id = "s" + i,
                Interaction = Interaction.Absorb,
                Geometry = Circle2D.Builder.Create(.3f),
                Frame = Frame2D.Identity.WithOrigin(source.Center)
            });
        }
        for (var i = 0; i < level.Targets.Length; i++)
        {
            var target = level.Targets[i];
            Add(new()
            {
                Id = "t" + i,
                Interaction = target.Profile ? Interaction.Sensor : Interaction.Absorb,
                Geometry = target.Profile
                    ? LineSegmentCurve2D.Create(new(-target.Half, 0), new(target.Half, 0))
                    : Circle2D.Builder.Create(target.Radius),
                Frame = Frame2D.FromRotationDegrees(target.Angle + 90, target.Center),
                Target = i,
                Color = target.Color
            });
        }
        foreach (var gate in level.Gates)
        {
            AddBox(new(gate.X + .46f, 0), new(gate.X + .54f, gate.Y + .5f - gate.Gap));
            AddBox(new(gate.X + .46f, gate.Y + .5f + gate.Gap), new(gate.X + .54f, level.H));
        }
        foreach (var wall in level.Walls) AddBox(new(wall[0], wall[1]), new(wall[0] + 1, wall[1] + 1));
        foreach (var zone in level.Mirage)
        {
            AddBox(zone.AirBounds.Min, zone.AirBounds.Max, Interaction.Mirage);
            AddBox(new(zone.X, zone.Y + zone.H - .12f), new(zone.X + zone.W, zone.Y + zone.H));
        }
    }

    private void AddPart(BeamwrightContent content, PartPlacement placement, string id)
    {
        var part = content.Part(placement.Type);
        var frame = Frame2D.FromRotationDegrees(placement.Angle, placement.Center);
        
        var ports = part.Ports.Select(index =>
        {
            var a = part.Pts[index];
            var b = part.Pts[(index + 1) % part.Pts.Length];
            var edge = new Segment<Vector2>(new(a[0], a[1]), new(b[0], b[1]));
            return new OpticalPort(edge, frame);
        }).ToArray();

        Add(new()
        {
            Id = id,
            Geometry = part.Geometry,
            Frame = frame,
            Part = part,
            Ports = ports,
            Interaction = part.Act switch
            {
                "mirror" => Interaction.Mirror,
                "split" => Interaction.Split,
                "lens" => Interaction.Lens,
                "cmirror" => Interaction.CurvedMirror,
                _ => Interaction.Refract
            }
        });
    }

    private void AddBox(Vector2 min, Vector2 max, Interaction interaction = Interaction.Absorb)
    {
        if (min.X >= max.X || min.Y >= max.Y) return;
        Add(new()
        {
            Id = interaction == Interaction.Mirage ? "z" : "w",
            Interaction = interaction,
            Geometry = AxisAlignedRectangle2D.Builder.Create(min, max)
        });
    }

    private void Add(OpticalBody body)
    {
        body.Prepare();
        _bodies.Add(body);
    }

    public SceneHit FirstHit(Ray2D ray)
    {
        if (!Raycast2D.TryRectangle(ray.Origin, ray.Direction, Vector2.Zero, new(_level.W, _level.H), float.PositiveInfinity, out var closest))
            throw new InvalidOperationException("Light left the finite bench without crossing its boundary.");

        OpticalBody? nearest = null;
        var point = Vector2.Zero;
        foreach (var body in _bodies)
        {
            if (body.TryHit(ray, closest.Distance, out var hit, out var local) && (nearest is null || hit.Distance < closest.Distance))
            {
                closest = hit;
                nearest = body;
                point = local;
            }
        }
        return new(nearest, closest, point);
    }
}
