using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics;
using System.Numerics;
using System.Text.Json;

namespace App2d.CharacterStudio.Importing;

/// <summary>
/// Converts one imported library clip onto a native model through an explicit point mapping. Motion is transferred as offsets
/// from a reference frame, so the model keeps its own proportions: each control's offset from its parent, and each chain end's
/// offset in its chain's frame, is the source's change from the reference pose, scaled by the ratio of the matching measure.
/// Joints are solved by IK with the model's lengths. The clip is sampled at a fixed rate and has no contacts until they are
/// authored; the library is only read.
/// </summary>
public static class LibraryImport
{
    public const int Rate = 30;
    private const float Epsilon = 1e-5f;

    /// <summary>
    /// A starting mapping: for the Person template on an exported person library, the matching limbs by depth (the export's
    /// left side is the far side, which the template calls right), otherwise controls named like source points.
    /// </summary>
    public static Dictionary<string, List<string>> DefaultMapping(CharacterModel model, PointLibrary library)
    {
        var mapping = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var names = library.PointNames.ToHashSet(StringComparer.Ordinal);
        if (library.Anatomy == "person")
        {
            foreach (var (control, points) in new (string, string[])[]
            {
                ("hips", ["leg_l_0", "leg_r_0"]),
                ("chest", ["arm_l_0", "arm_r_0"]),
                ("head", ["head"]),
                ("right-shoulder", ["arm_l_0"]),
                ("right-elbow", ["arm_l_1"]),
                ("right-hand", ["arm_l_2"]),
                ("left-shoulder", ["arm_r_0"]),
                ("left-elbow", ["arm_r_1"]),
                ("left-hand", ["arm_r_2"]),
                ("right-hip", ["leg_l_0"]),
                ("right-knee", ["leg_l_1"]),
                ("right-foot", ["leg_l_2"]),
                ("left-hip", ["leg_r_0"]),
                ("left-knee", ["leg_r_1"]),
                ("left-foot", ["leg_r_2"]),
            })
            {
                if (model.Controls.Any(c => c.Id == control) && points.All(names.Contains))
                {
                    mapping[control] = [.. points];
                }
            }
        }
        foreach (var control in model.Controls)
        {
            if (!mapping.ContainsKey(control.Id) && names.Contains(control.Id))
            {
                mapping[control.Id] = [control.Id];
            }
        }

        return mapping;
    }

    /// <summary>The library's reference clip: its idle when it has one, otherwise the clip being converted.</summary>
    public static string DefaultRest(PointLibrary library, string clipId) => library.Clips.ContainsKey("idle") ? "idle" : clipId;

    /// <summary>A library point in model-like units: pivot at the origin, Y up, pixels divided by the export's pixels per unit.</summary>
    public static Func<Vector3, Vector3> Units(PointLibrary library)
    {
        var drawing = library.Drawing;
        var pivot = drawing.TryGetProperty("pivot", out var p) ? new Vector2(p[0].GetSingle(), p[1].GetSingle()) : Vector2.Zero;
        var ppu = drawing.TryGetProperty("pixelsPerUnit", out var u) && u.ValueKind == JsonValueKind.Number ? u.GetSingle() : 1;
        return raw => new((raw.X - pivot.X) / ppu, (pivot.Y - raw.Y) / ppu, raw.Z / ppu);
    }

    /// <summary>Mapped source positions for every control the mapping names, at one time.</summary>
    public sealed class Sampler
    {
        private readonly Vector3[] _raw;
        private readonly Func<Vector3, Vector3> _units;
        private readonly Dictionary<string, int[]> _indices;
        public Sampler(PointLibrary library, string clipId, IReadOnlyDictionary<string, List<string>> points)
        {
            Clip = library.Clips.TryGetValue(clipId, out var clip) ? clip : throw new KeyNotFoundException($"Library '{library.Id}' has no clip '{clipId}'.");
            _raw = new Vector3[library.PointNames.Count];
            _units = Units(library);
            var index = library.PointNames.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i, StringComparer.Ordinal);
            _indices = points.ToDictionary(p => p.Key, p => p.Value.Select(n => index.TryGetValue(n, out var i) ? i : throw new InvalidDataException($"Library '{library.Id}' has no point '{n}' (mapped to '{p.Key}').")).ToArray(), StringComparer.Ordinal);
        }
        public PointClip Clip { get; }
        public IReadOnlyDictionary<string, Vector3> At(double seconds)
        {
            Clip.Sample(seconds, _raw, holdEnd: true);
            return _indices.ToDictionary(p => p.Key, p => _units(p.Value.Aggregate(Vector3.Zero, (sum, i) => sum + _raw[i]) / p.Value.Length), StringComparer.Ordinal);
        }
    }

    public static MotionClip Convert(PointLibrary library, string clipId, CharacterModel model, IReadOnlyDictionary<string, List<string>> points, string id, string name, string? restClip = null)
    {
        var resolved = ResolvedModel.Rig(model);
        foreach (var control in points.Keys)
        {
            if (!resolved.Controls.ContainsKey(control))
            {
                throw new InvalidDataException($"The mapping names '{control}', which model '{model.Id}' does not have.");
            }
        }

        restClip ??= DefaultRest(library, clipId);
        var source = new Sampler(library, clipId, points);
        var reference = new Sampler(library, restClip, points).At(0);

        var (ratios, overall) = Ratios(resolved, reference);
        float Ratio(string scale) => ratios.TryGetValue(scale, out var ratio) ? ratio : overall;

        var solved = model.IkChains.SelectMany(c => new[] { c.Joint, c.End }).ToHashSet(StringComparer.Ordinal);
        foreach (var chain in model.IkChains)
        {
            // A bone constraint turns its root; the solved bone beneath a pivot (P11a) sits at its parent's origin and gets no channel.
            if (resolved.Controls[chain.Root].Parent is { } parent && Vector2.Distance(new(resolved.Rest[chain.Root].X, resolved.Rest[chain.Root].Y), new(resolved.Rest[parent].X, resolved.Rest[parent].Y)) < 1e-6f)
            {
                solved.Add(chain.Root);
            }
        }
        var duration = (float)source.Clip.Duration;
        var clip = new MotionClip
        {
            Id = id,
            Name = name,
            Model = model.Id,
            StructureRevision = model.StructureRevision,
            Duration = duration,
            Loop = source.Clip.Loop,
            Reference = resolved.Measures.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            Source = new() { Kind = AssetSource.Library, File = library.Id, Motion = clipId, Rest = restClip, Points = points.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.Ordinal) },
        };
        // The clip's reference measures are the model's own, so a model-scaled offset is already in reference units.
        Vector3? Offset(IReadOnlyDictionary<string, Vector3> now, string target, string? frame, string scale)
        {
            if (!now.ContainsKey(target) || frame is not null && !now.ContainsKey(frame))
            {
                return null;
            }

            var current = now[target] - (frame is null ? Vector3.Zero : now[frame]);
            var rest = reference[target] - (frame is null ? Vector3.Zero : reference[frame]);
            return (current - rest) * Ratio(scale);
        }
        var tracks = new List<ClipTrack>();
        foreach (var control in resolved.Order.Where(c => !solved.Contains(c.Id)))
        {
            tracks.Add(new()
            {
                Kind = MotionClip.TranslateKind,
                Target = control.Id,
                Keys = []
            });
        }

        foreach (var chain in model.IkChains)
        {
            tracks.Add(new()
            {
                Kind = MotionClip.TargetKind,
                Target = chain.Id,
                Keys = []
            });
        }

        var count = (int)MathF.Floor(duration * Rate + 1e-4f);
        var times = Enumerable.Range(0, count + 1).Select(i => MathF.Min(duration, i / (float)Rate)).Append(duration).Distinct().ToArray();
        foreach (var time in times)
        {
            var now = source.At(time);
            foreach (var track in tracks)
            {
                Vector3? delta;
                if (track.Kind == MotionClip.TargetKind)
                {
                    var chain = resolved.Chains[track.Target];
                    delta = Offset(now, chain.End, chain.Frame == CharacterModel.Locomotion ? null : chain.Frame, chain.Scale);
                }
                else
                {
                    var control = resolved.Controls[track.Target];
                    delta = Offset(now, control.Id, control.Parent, control.Scale);
                }
                if (delta is { } d)
                {
                    track.Keys.Add(new()
                    {
                        Time = time,
                        Value = new(d.X, d.Y, d.Z)
                    });
                }
            }
        }
        clip.Tracks = [.. tracks.Where(t => t.Keys.Any(k => MathF.Abs(k.Value.X) > Epsilon || MathF.Abs(k.Value.Y) > Epsilon || MathF.Abs(k.Value.Z) > Epsilon))];
        clip.Validate(resolved);
        return clip;
    }

    /// <summary>
    /// Model length over source length for each measure whose path is mapped, along the source's reference pose, and one
    /// overall ratio (their mean, or the ratio of heights) for everything else.
    /// </summary>
    public static (IReadOnlyDictionary<string, float> Measures, float Overall) Ratios(ResolvedModel model, IReadOnlyDictionary<string, Vector3> reference)
    {
        var ratios = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var measure in model.Base.Measures.Where(m => m.Path.All(reference.ContainsKey)))
        {
            var length = measure.Path.Zip(measure.Path.Skip(1)).Sum(p => Vector2.Distance(reference[p.First].XY, reference[p.Second].XY));
            if (length > .001f)
            {
                ratios[measure.Id] = model.Measures[measure.Id] / length;
            }
        }
        var overall = ratios.Count > 0 ? ratios.Values.Average() : Height(model.Rest.Values) / MathF.Max(.001f, Height(reference.Values));
        return (ratios, overall);
    }

    private static float Height(IEnumerable<Vector3> points)
    {
        var ys = points.Select(p => p.Y).ToArray();
        return ys.Length == 0 ? 1 : ys.Max() - ys.Min();
    }
}
