using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The declared meaning of every animated channel (plan section 3.5). Each existing timeline kind maps to one semantics
/// here, so a consumer reads the declaration instead of inferring units from a track name. Continuous channels go through the
/// shared keyframe sampler and curve rules; discrete channels hold their value until the next key.
/// </summary>
public static class ChannelSemantics
{
    public const string Angle = "angle";
    public const string NormalizedParameter = "normalized-parameter";
    public const string LocalTranslation = "local-translation";
    public const string IkTarget = "ik-target";
    public const string UniformPoseScale = "uniform-pose-scale";
    public const string ArtworkFit = "artwork-fit";
    public const string MaterialValue = "material-value";
    public const string AttachmentSelection = "attachment-selection";
    public const string EnabledState = "enabled-state";

    /// <summary>One declared channel meaning: its units, whether keys interpolate or hold, and what carries it today.</summary>
    public sealed record Declaration(string Semantics, string Units, bool Held, string Carrier);

    /// <summary>Every semantics the plan names, with the timeline that carries it; two are declared ahead of their first channel.</summary>
    public static readonly IReadOnlyList<Declaration> All =
    [
        new(LocalTranslation, ParameterUnits.Length, false, "translate tracks (x, y in the control's scale; z is a layer offset)"),
        new(Angle, ParameterUnits.Radians, false, "rotate tracks (accumulated, unwrapped) and socket orient tracks (twist, tilt, turn)"),
        new(IkTarget, ParameterUnits.Length, false, "target tracks in the chain's frame and scale"),
        new(UniformPoseScale, ParameterUnits.Factor, false, "scale tracks on canonical nodes (one multiplier, length stays a dimension)"),
        new(ArtworkFit, ParameterUnits.Factor, false, "fit tracks on drawing parts (explicit leaf fit multipliers, nothing inherits them)"),
        new(MaterialValue, ParameterUnits.Factor, false, "geometry tint/alpha tracks and slot color timelines"),
        new(AttachmentSelection, ParameterUnits.Factor, true, "play keys on animated part instances, slot attachment keys and draw-order keys (held)"),
        new(NormalizedParameter, ParameterUnits.Normalized, false, "bend tracks: 0..1 mapped through the bone node's declared joint limits (P4b)"),
        new(EnabledState, ParameterUnits.Factor, true, "show tracks on drawing parts (P7a, held); an action's activation windows enable its attack colliders over half-open intervals, crossed-in-one-step included (P8b)"),
    ];

    /// <summary>The semantics of a control, chain, socket or part track by its kind.</summary>
    public static string OfTrack(string kind) => kind switch
    {
        MotionClip.TranslateKind => LocalTranslation,
        MotionClip.RotateKind or MotionClip.OrientKind => Angle,
        MotionClip.TargetKind => IkTarget,
        MotionClip.BendKind => NormalizedParameter,
        MotionClip.ScaleKind => UniformPoseScale,
        MotionClip.FitKind => ArtworkFit,
        MotionClip.ShowKind => EnabledState,
        MotionClip.TintKind or MotionClip.AlphaKind => MaterialValue,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown track kind."),
    };

    public static Declaration Of(string semantics) => All.First(d => d.Semantics == semantics);
}
