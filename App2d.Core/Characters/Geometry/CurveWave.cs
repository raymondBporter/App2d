using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Geometry;

/// <summary>
/// The typed wave modifier of a linked curve part: sine bumps along the curve normal, in units of the endpoint span, carried
/// along the curve by an optional phase control. Only a curve part owns one (P2a of the character-system migration), so the
/// shared part type carries no wave fields; the modifier owns its limits, its control references and its evaluation, which
/// <see cref="PartCurves2D"/> applies while sampling the curve.
/// </summary>
public sealed record CurveWave : IPartPayload
{
    public static readonly Limit AmplitudeLimit = new(-1, 1);
    public static readonly Limit CyclesLimit = new(.1f, 16);

    /// <summary>Bump height as a fraction of the endpoint span. Negative bends to the other side.</summary>
    public float Amplitude { get; set; }
    /// <summary>Bumps along the curve.</summary>
    public float Cycles { get; set; } = 1;
    /// <summary>Moving this control along the endpoint span moves the wave by the same distance.</summary>
    public string? PhaseControl { get; set; }

    /// <summary>A visible wave for a modifier the author has just added.</summary>
    public static CurveWave Starter => new() { Amplitude = .05f, Cycles = 3 };

    /// <summary>A zero-height wave leaves the curve where it was.</summary>
    [JsonIgnore] public bool Deforms => Amplitude != 0;

    /// <summary>The samples a deformed curve needs so every bump is drawn.</summary>
    [JsonIgnore] public int Samples => Math.Max(33, (int)MathF.Ceiling(Cycles * 16) + 1);

    [JsonIgnore] public string Name => "wave";
    [JsonIgnore] public string Requirement => "a linked curve";

    /// <summary>A wave deforms a curve sampled between its endpoints; shapes and images carry none.</summary>
    public bool Fits(IGeometryValue2D? geometry) => geometry is ICurve2D;

    /// <summary>Whether deleting this control would leave the modifier dangling; the owning authoring commands refuse that.</summary>
    public bool References(string controlId) => PhaseControl == controlId;

    /// <summary>The modifier's own limits and control references, checked with the part that owns it.</summary>
    public void Validate(Func<string, bool> isControl, string field)
    {
        AmplitudeLimit.Check(Amplitude, field + ".amplitude");
        CyclesLimit.Check(Cycles, field + ".cycles");
        if (PhaseControl is not null && !isControl(PhaseControl))
        {
            throw new InvalidDataException("Unknown wave phase control: " + PhaseControl);
        }
    }

    /// <summary>The wave's travel in cycles: the phase control's position along the curve's local Y, which spans the endpoints.</summary>
    public float Phase(Frame2D coordinates, Func<string, Vector3> world) =>
        PhaseControl is { } control && coordinates.TryToLocal(world(control).XY, out var local) ? local.Y * Cycles : 0;

    /// <summary>Deforms a curve in its own coordinates. Span scales the amplitude when the source already lies in world units.</summary>
    public ICurve2D Apply(ICurve2D source, float phase, float span = 1)
    {
        var amplitude = Amplitude * span;
        var cycles = Cycles;
        return new NormalOffsetCurve2D(source, t => amplitude * .5f * (1 + MathF.Sin(MathF.Tau * (cycles * t - phase))));
    }
}
