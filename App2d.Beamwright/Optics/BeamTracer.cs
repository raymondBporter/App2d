using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Beamwright.Optics;

public sealed class LightSegment(Vector2 start, Vector2 end, string color, double power, double distance, double length, int rayIndex)
{
    public Vector2 Start { get; } = start;
    public Vector2 End { get; } = end;
    public string Color { get; } = color;
    public double Power { get; } = power;
    public double Distance { get; } = distance;
    public double EndDistance { get; } = distance + length;
    public int RayIndex { get; } = rayIndex;
    public string Event { get; internal set; } = "";
}

public sealed class SensorProfile(int bins)
{
    public double[] Bins { get; } = new double[bins];
    public double Accepted { get; internal set; }
    public double AnglePower { get; internal set; }
}

public sealed class TraceResult
{
    public List<LightSegment> Segments { get; } = [];
    public required double[] Lumens { get; init; }
    public required double[] Quality { get; init; }
    public required bool[] Lit { get; init; }
    public required SensorProfile?[] Profiles { get; init; }
    public Dictionary<string, double> Hits { get; } = [];
    public bool Truncated { get; internal set; }
    public bool Win => Lit.Length > 0 && Lit.All(lit => lit);
}

/// <summary>The original optical rules on Core raycasts, filled shapes, and affine poses.</summary>
public sealed class BeamTracer(BeamwrightContent content)
{
    public static readonly string[] Bands = ["R", "O", "Y", "G", "B", "V"];
    private const float MirageStep = .08f;
    private readonly record struct Beam(Ray2D Ray, double Power, string Color, int Depth, int Index, int Steps, double Distance);

    public TraceResult Trace(BenchLevel level, IReadOnlyList<PartPlacement> placed, int maxSegments = 6000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSegments);
        var scene = new OpticalScene(content, level, placed);
        var benchBounds = new Rect2D(Vector2.Zero, new(level.W, level.H));
        var result = new TraceResult
        {
            Lumens = new double[level.Targets.Length],
            Quality = new double[level.Targets.Length],
            Lit = new bool[level.Targets.Length],
            Profiles = [.. level.Targets.Select(t => t.Profile ? new SensorProfile(t.Bins) : null)]
        };
        var stack = new Stack<Beam>();
        foreach (var source in level.Sources)
        {
            var sourceFrame = Frame2D.FromRotationDegrees(source.Angle, source.Center);
            var direction = sourceFrame.XAxis;
            var spacing = source.Width is { } width ? width / Math.Max(1, source.Rays - 1) : .08f;
            for (var i = 0; i < source.Rays; i++)
            {
                var offset = (i - (source.Rays - 1) / 2f) * spacing;
                var origin = sourceFrame.TransformPoint(new(.32f, offset));
                stack.Push(new(new(origin, direction), 1d / source.Rays, source.Color, 0, i, 0, 0));
            }
        }
        while (stack.Count > 0 && result.Segments.Count < maxSegments)
        {
            var beam = stack.Pop();
            var ray = beam.Ray;
            var hit = scene.FirstHit(ray);
            var zone = level.Mirage.FirstOrDefault(z => z.AirBounds.ContainsInterior(ray.Origin));
            if (zone is not null && hit.Hit.Distance > MirageStep && beam.Steps < 500)
            {
                var point = ray.PointAt(MirageStep);
                result.Segments.Add(new(ray.Origin, point, beam.Color, beam.Power, beam.Distance, MirageStep, beam.Index));
                var bent = ray.Direction - new Vector2(0, zone.K * MirageStep);
                stack.Push(beam with { Ray = new(point, bent), Steps = beam.Steps + 1, Distance = beam.Distance + MirageStep });
                continue;
            }
            var segment = new LightSegment(ray.Origin, hit.Hit.Point, beam.Color, beam.Power, beam.Distance, hit.Hit.Distance, beam.Index);
            result.Segments.Add(segment);
            var body = hit.Body;
            if (body is null) { segment.Event = "edge"; continue; }
            if (beam.Depth > 80 || beam.Power < .004) { segment.Event = "fade"; continue; }
            if (body.Id is not "w" and not "z") result.Hits[body.Id] = result.Hits.GetValueOrDefault(body.Id) + beam.Power;

            void Next(Vector2 direction, double power, string? color = null)
            {
                var normalized = Vector2.Normalize(direction);
                var origin = hit.Hit.Point + normalized * .00001f;
                // An interaction exactly on the bench edge must not launch an out-of-bounds ray.
                if (!benchBounds.ContainsInterior(origin)) return;
                stack.Push(new(new(origin, normalized), power, color ?? beam.Color, beam.Depth + 1, beam.Index, beam.Steps, segment.EndDistance));
            }

            if (body.Interaction == Interaction.Sensor)
            {
                var target = level.Targets[body.Target];
                var profile = result.Profiles[body.Target]!;
                // The sensor faces along -local Y; the segment itself runs along local X.
                var angle = Math.Acos(Math.Clamp(Vector2.Dot(ray.Direction, -body.Frame.YAxis), -1d, 1d)) * 180d / Math.PI;
                if (body.Color == "any" || body.Color == beam.Color)
                {
                    result.Lumens[body.Target] += beam.Power * 100;
                    if (angle <= target.Tolerance)
                    {
                        var bin = Math.Clamp((int)Math.Floor((hit.LocalPoint.X + target.Half) / (2 * target.Half) * profile.Bins.Length), 0, profile.Bins.Length - 1);
                        profile.Bins[bin] += beam.Power * 100;
                        profile.Accepted += beam.Power * 100;
                    }
                    profile.AnglePower += angle * beam.Power * 100;
                }
                segment.Event = "target";
                continue;
            }
            if (body.Interaction == Interaction.Absorb)
            {
                var counts = body.Target >= 0 && (body.Color == "any" || body.Color == beam.Color);
                if (counts) result.Lumens[body.Target] += beam.Power * 100;
                segment.Event = counts ? "target" : "wall";
                continue;
            }
            if (body.Interaction == Interaction.Mirage)
            {
                Next(ray.Direction, beam.Power);
                continue;
            }

            var part = body.Part!;
            var normal = body.Frame.YAxis;
            var dot = Vector2.Dot(ray.Direction, normal);
            if (body.Interaction is Interaction.Mirror or Interaction.Split)
            {
                segment.Event = body.Interaction == Interaction.Mirror ? "mirror" : "split";
                var reflected = Vector2.Reflect(ray.Direction, normal);
                if (body.Interaction == Interaction.Mirror)
                {
                    Next(reflected, beam.Power);
                }
                else
                {
                    Next(reflected, beam.Power / 2);
                    Next(ray.Direction, beam.Power / 2);
                }
                continue;
            }
            if (body.Interaction is Interaction.Lens or Interaction.CurvedMirror)
            {
                var isMirror = body.Interaction == Interaction.CurvedMirror;
                if (isMirror && dot >= 0) { segment.Event = "wall"; continue; }
                segment.Event = isMirror ? "cmirror" : "lens";

                var axis = isMirror ? normal : normal * (dot >= 0 ? 1 : -1);

                var direction = isMirror ? Vector2.Reflect(ray.Direction, normal) : ray.Direction;

                var lensFrame = new Frame2D(Vector2.Zero, body.Frame.XAxis, axis);

                // Optical poses have orthonormal axes, so the transpose projects onto tangent/axis.
                var localDirection = lensFrame.Matrix.TransposeTransformDirection(direction);

                var slope = localDirection.X / Math.Max(localDirection.Y, .05f) - hit.LocalPoint.X / part.FocalLength;

                Next(lensFrame.TransformDirection(new(slope, 1)), beam.Power);
                continue;
            }
            if (body.IsJoinedPort(hit.LocalPoint)) { Next(ray.Direction, beam.Power); continue; }
            normal = hit.Hit.Normal;
            if (part.Mirror && Vector2.Dot(normal, body.Frame.XAxis) > 0)
            {
                segment.Event = "retro";
                Next(Vector2.Reflect(ray.Direction, normal), beam.Power);
                continue;
            }
            var entering = Vector2.Dot(ray.Direction, normal) < 0;
            var incidentNormal = entering ? normal : -normal;
            var partial = entering ? 0 : part.Partial;
            segment.Event = entering ? part.Tab is not null && beam.Color == "W" ? "disperse" : "glassIn" : "glassOut";

            void Emit(string color, double power)
            {
                var index = part.Tab?.GetValueOrDefault(color) ?? part.RefractiveIndex;
                var transmitted = Vector2.TryRefract(ray.Direction, incidentNormal, entering ? 1 : index, entering ? index : 1, out var direction);
                var reflected = Vector2.Reflect(ray.Direction, incidentNormal);
                if (!transmitted)
                {
                    segment.Event = part.Ports.Length > 0 ? "fiberTir" : "tir";
                    Next(reflected, power, color);
                }
                else if (partial > 0)
                {
                    Next(direction, power * (1 - partial), color);
                    Next(reflected, power * partial, color);
                }
                else
                {
                    Next(direction, power, color);
                }
            }
            if (part.Tab is not null && entering && beam.Color == "W")
                foreach (var band in Bands) Emit(band, beam.Power / Bands.Length);
            else Emit(beam.Color, beam.Power);
        }
        result.Truncated = stack.Count > 0;
        for (var i = 0; i < level.Targets.Length; i++)
        {
            var target = level.Targets[i];
            var quality = result.Lumens[i] / target.Need;
            if (result.Profiles[i] is { } profile)
                quality = Math.Min(profile.Accepted / target.Need, profile.Bins.Min() / target.BinNeed);
            result.Quality[i] = Math.Min(1, quality);
            result.Lit[i] = result.Quality[i] >= 1 - 1e-6;
        }
        return result;
    }
}
