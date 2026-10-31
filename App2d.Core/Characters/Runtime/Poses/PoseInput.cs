using App2d.Core.Animation;
using App2d.Core.Characters.Geometry;
using App2d.Core.Constraints;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Timing;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>A gameplay request that one animated part instance play an expression (P7a), optionally at a clip time gameplay owns.</summary>
public readonly record struct PartPlay(string Instance, string Expression, double? Seconds = null);

/// <summary>
/// Gameplay inputs to evaluation. An expression here is a semantic request every animated part instance whose asset binds
/// it answers; it wins over the clip's selection channel and the instance default.
/// <see cref="InPlace"/> drops clip travel: the locomotion frame stays at the origin because the controller owns actor
/// movement. <see cref="Contact"/> may replace an active contact's target (chain, authored target in the locomotion
/// frame) with a held one, such as a world anchor captured at touchdown.
/// </summary>
public readonly record struct PoseInput(string? Expression = null)
{
    public bool InPlace { get; init; }
    public Func<string, Vector3, Vector3>? Contact { get; init; }
    /// <summary>A masked override: its clip owns every channel whose target is in the mask. The base clip keeps the rest.</summary>
    public PoseLayer? Overlay { get; init; }
    /// <summary>Further masked layers over <see cref="Overlay"/>, each over the ones before it.</summary>
    public IReadOnlyList<PoseLayer>? Overlays { get; init; }
    /// <summary>Reverse authored horizontal offsets and rotation, without turning the actor's rest pose.</summary>
    public bool ReverseHorizontalMotion { get; init; }
    public string? Skin { get; init; }
    /// <summary>The actor's own continuous clock in seconds, driving part layers with a local clock (a blink that never restarts with the walk).</summary>
    public double Clock { get; init; }
    /// <summary>Explicit per-instance requests, each winning over <see cref="Expression"/> for its instance.</summary>
    public IReadOnlyList<PartPlay>? Plays { get; init; }

    /// <summary>Every layer in order, bottom first, that has any weight.</summary>
    internal IEnumerable<PoseLayer> Layers()
    {
        if (Overlay is { Weight: > 0 } first)
        {
            yield return first;
        }

        foreach (var layer in Overlays ?? [])
        {
            if (layer.Weight > 0)
            {
                yield return layer;
            }
        }
    }
}

/// <summary>
/// One override clip over the base, such as a gun shot's arms on any legs. <see cref="Targets"/> names the controls, chains
/// and animated part instances the overlay owns, whole: a masked channel without an overlay track rests rather than falling
/// back to the base. Its travel and contacts are ignored; the base owns locomotion. <see cref="Weight"/> fades the layer:
/// masked channels blend from the base's value toward the overlay's before the hierarchy and IK are solved, so a chain is
/// never blended as two solved poses. A bend choice and the overlay's face win from half weight; a base contact on a masked
/// chain eases out with it. <see cref="Namespace"/> reads the clip's tracks for one part instance (<c>instance/target</c>)
/// as that part's own targets: how a wearer's clip reaches into a nested rig.
/// </summary>
public sealed record PoseLayer(MotionClip Clip, double Seconds, IReadOnlySet<string> Targets, float Weight = 1)
{
    public string? Namespace { get; init; }

    /// <summary>The targets a clip keys inside one instance, stripped of the namespace: the mask of the wearer-driven layer on that part.</summary>
    public static HashSet<string> KeyedTargets(MotionClip clip, string instance)
    {
        var prefix = instance + Part.Namespace;
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var track in clip.Tracks)
        {
            if (track.Target.StartsWith(prefix, StringComparison.Ordinal))
            {
                targets.Add(track.Target[prefix.Length..]);
            }
        }

        return targets;
    }
}
