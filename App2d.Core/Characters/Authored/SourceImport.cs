using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace App2d.Core.Characters;

/// <summary>
/// Where a converted asset came from. The source file is never changed; this record is enough to repeat the conversion.
/// Kind <c>puppet</c>: File is the .puppet.json path and Motion the motion's name. Kind <c>library</c>: File is the imported
/// library's id, Motion its clip, Rest the library clip whose first frame stands for the model's rest pose, and Points
/// which source points each control follows (several are averaged).
/// </summary>
public sealed record AssetSource
{
    public const string Puppet = "puppet", Library = "library";
    public string Kind { get; set; } = "";
    public string File { get; set; } = "";
    public string? Motion { get; set; }
    public string? Rest { get; set; }
    public Dictionary<string, List<string>>? Points { get; set; }

    public void Validate(string owner)
    {
        EntityVocabulary.Require(Kind, [Puppet, Library], owner + " source kind");
        if (string.IsNullOrWhiteSpace(File)) throw new InvalidDataException($"{owner} source: a file is required.");
        if (Points is not null && Points.Any(p => p.Value is null || p.Value.Count == 0 || p.Value.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException($"{owner} source: every mapped control needs at least one source point.");
    }
}

/// <summary>
/// Explicit, one-way conversion of a prototype <c>.puppet.json</c> into a base model and one clip per motion. IDs become
/// lowercase asset IDs; chains whose ends ever touch the ground are keyed in the locomotion frame and share one reach measure,
/// the others are keyed from their root. The puppet file is only read.
/// </summary>
public static partial class PuppetImport
{
    [GeneratedRegex("[^a-z0-9]+")] private static partial Regex NotId();

    public static string Slug(string text, string fallback = "asset")
    {
        var slug = NotId().Replace(text.ToLowerInvariant(), "-").Trim('-');
        slug = slug.Length == 0 ? fallback : slug[..Math.Min(slug.Length, 60)];
        return char.IsAsciiLetterOrDigit(slug[0]) ? slug : fallback;
    }

    public sealed record Result(CharacterModel Model, IReadOnlyList<MotionClip> Clips);

    /// <param name="taken">IDs already in use; new model and clip IDs avoid them.</param>
    public static Result Convert(PuppetDefinition puppet, string modelId, string modelName, string sourcePath, IEnumerable<string> taken)
    {
        puppet.Validate();
        var used = new HashSet<string>(taken, StringComparer.Ordinal);
        if (!used.Add(modelId)) throw new InvalidDataException($"The id '{modelId}' is already used.");

        // Control IDs are model-local, so only clashes inside this model matter.
        var ids = new Dictionary<string, string>(StringComparer.Ordinal); var localIds = new HashSet<string>(StringComparer.Ordinal) { CharacterModel.Locomotion, CharacterModel.Unit };
        string Unique(string basis, HashSet<string> set) { var id = basis; for (var n = 2; !set.Add(id); n++) id = $"{basis}-{n}"; return id; }
        foreach (var control in puppet.Controls) ids[control.Id] = Unique(Slug(control.Id, "control"), localIds);
        var parents = puppet.Bones.ToDictionary(b => ids[b.To], b => ids[b.From], StringComparer.Ordinal);
        var grounded = puppet.Motions.SelectMany(m => m.Contacts).Select(c => ids[c.End]).ToHashSet(StringComparer.Ordinal);
        var chainIds = new HashSet<string>(StringComparer.Ordinal);
        string ChainId(string end) => Unique(end.EndsWith("-hand", StringComparison.Ordinal) ? end[..^5] + "-arm" : end.EndsWith("-foot", StringComparison.Ordinal) ? end[..^5] + "-leg" : end + "-chain", chainIds);

        var chains = puppet.Chains.Select(c => new ModelChain { Id = ChainId(ids[c.End]), Root = ids[c.Root], Joint = ids[c.Joint], End = ids[c.End], Bend = c.Bend }).ToList();
        var measures = chains.Select(c => new ModelMeasure { Id = c.Id, Path = [c.Root, c.Joint, c.End] }).ToList();
        // Contacts need their chain's scale to equal the travel scale, so every grounded chain shares the first one's reach.
        var reach = chains.FirstOrDefault(c => grounded.Contains(c.End))?.Id;
        foreach (var chain in chains)
        {
            var onGround = grounded.Contains(chain.End);
            chain.Frame = onGround ? CharacterModel.Locomotion : chain.Root;
            chain.Scale = onGround ? reach! : chain.Id;
        }
        var partIds = new HashSet<string>(StringComparer.Ordinal);
        var model = new CharacterModel
        {
            Id = modelId, Name = modelName, Ink = puppet.Ink, LineWidth = puppet.LineWidth,
            Controls = [.. puppet.Controls.Select(c => new ModelControl { Id = ids[c.Id], Parent = parents.GetValueOrDefault(ids[c.Id]), Rest = c.Rest })],
            Chains = chains, Measures = measures,
            Parts = [.. puppet.Parts.Select(p => p with { Id = Unique(Slug(p.Id, "part"), partIds), A = ids[p.A], B = p.B is null ? null : ids[p.B] })],
            Source = new() { Kind = AssetSource.Puppet, File = sourcePath },
        };
        model.Validate();

        var converted = new PuppetDefinition
        {
            Name = puppet.Name, Ink = puppet.Ink, LineWidth = puppet.LineWidth,
            Motions = [.. puppet.Motions.Select(m => m with
            {
                Keys = [.. m.Keys.Select(k => k with { Points = k.Points.ToDictionary(p => ids[p.Key], p => p.Value, StringComparer.Ordinal) })],
                Contacts = [.. m.Contacts.Select(c => c with { End = ids[c.End] })],
            })],
        };
        var clips = new List<MotionClip>();
        foreach (var motion in converted.Motions)
        {
            var id = Unique(Slug($"{modelId}-{motion.Name}", modelId + "-motion"), used);
            var clip = PuppetMotionConverter.Convert(converted, motion, model, id, motion.Name, reach ?? CharacterModel.Unit);
            clip.Source = new() { Kind = AssetSource.Puppet, File = sourcePath, Motion = motion.Name };
            clips.Add(clip);
        }
        return new(model, clips);
    }
}

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
                ("hips", ["leg_l_0", "leg_r_0"]), ("chest", ["arm_l_0", "arm_r_0"]), ("head", ["head"]),
                ("right-shoulder", ["arm_l_0"]), ("right-elbow", ["arm_l_1"]), ("right-hand", ["arm_l_2"]),
                ("left-shoulder", ["arm_r_0"]), ("left-elbow", ["arm_r_1"]), ("left-hand", ["arm_r_2"]),
                ("right-hip", ["leg_l_0"]), ("right-knee", ["leg_l_1"]), ("right-foot", ["leg_l_2"]),
                ("left-hip", ["leg_r_0"]), ("left-knee", ["leg_r_1"]), ("left-foot", ["leg_r_2"]),
            })
                if (model.Controls.Any(c => c.Id == control) && points.All(names.Contains)) mapping[control] = [.. points];
        }
        foreach (var control in model.Controls)
            if (!mapping.ContainsKey(control.Id) && names.Contains(control.Id)) mapping[control.Id] = [control.Id];
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
        private readonly PointClip _clip;
        private readonly Vector3[] _raw;
        private readonly Func<Vector3, Vector3> _units;
        private readonly Dictionary<string, int[]> _indices;
        public Sampler(PointLibrary library, string clipId, IReadOnlyDictionary<string, List<string>> points)
        {
            _clip = library.Clips.TryGetValue(clipId, out var clip) ? clip : throw new KeyNotFoundException($"Library '{library.Id}' has no clip '{clipId}'.");
            _raw = new Vector3[library.PointNames.Count]; _units = Units(library);
            var index = library.PointNames.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i, StringComparer.Ordinal);
            _indices = points.ToDictionary(p => p.Key, p => p.Value.Select(n => index.TryGetValue(n, out var i) ? i : throw new InvalidDataException($"Library '{library.Id}' has no point '{n}' (mapped to '{p.Key}').")).ToArray(), StringComparer.Ordinal);
        }
        public PointClip Clip => _clip;
        public IReadOnlyDictionary<string, Vector3> At(double seconds)
        {
            _clip.Sample(seconds, _raw, holdEnd: true);
            return _indices.ToDictionary(p => p.Key, p => _units(p.Value.Aggregate(Vector3.Zero, (sum, i) => sum + _raw[i]) / p.Value.Length), StringComparer.Ordinal);
        }
    }

    public static MotionClip Convert(PointLibrary library, string clipId, CharacterModel model, IReadOnlyDictionary<string, List<string>> points, string id, string name, string? restClip = null)
    {
        var resolved = ResolvedModel.From(model);
        foreach (var control in points.Keys)
            if (!resolved.Controls.ContainsKey(control)) throw new InvalidDataException($"The mapping names '{control}', which model '{model.Id}' does not have.");
        restClip ??= DefaultRest(library, clipId);
        var source = new Sampler(library, clipId, points);
        var reference = new Sampler(library, restClip, points).At(0);

        var (ratios, overall) = Ratios(resolved, reference);
        float Ratio(string scale) => ratios.TryGetValue(scale, out var ratio) ? ratio : overall;

        var solved = model.Chains.SelectMany(c => new[] { c.Joint, c.End }).ToHashSet(StringComparer.Ordinal);
        var duration = (float)source.Clip.Duration;
        var clip = new MotionClip
        {
            Id = id, Name = name, Model = model.Id, StructureRevision = model.StructureRevision, Duration = duration, Loop = source.Clip.Loop,
            Reference = resolved.Measures.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            Source = new() { Kind = AssetSource.Library, File = library.Id, Motion = clipId, Rest = restClip, Points = points.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.Ordinal) },
        };
        // The clip's reference measures are the model's own, so a model-scaled offset is already in reference units.
        Vector3? Offset(IReadOnlyDictionary<string, Vector3> now, string target, string? frame, string scale)
        {
            if (!now.ContainsKey(target) || frame is not null && !now.ContainsKey(frame)) return null;
            var current = now[target] - (frame is null ? Vector3.Zero : now[frame]);
            var rest = reference[target] - (frame is null ? Vector3.Zero : reference[frame]);
            return (current - rest) * Ratio(scale);
        }
        var tracks = new List<ClipTrack>();
        foreach (var control in resolved.Order.Where(c => !solved.Contains(c.Id)))
            tracks.Add(new() { Kind = MotionClip.TranslateKind, Target = control.Id, Keys = [] });
        foreach (var chain in model.Chains) tracks.Add(new() { Kind = MotionClip.TargetKind, Target = chain.Id, Keys = [] });
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
                if (delta is { } d) track.Keys.Add(new() { Time = time, X = d.X, Y = d.Y, Z = d.Z });
            }
        }
        clip.Tracks = [.. tracks.Where(t => t.Keys.Any(k => MathF.Abs(k.X) > Epsilon || MathF.Abs(k.Y) > Epsilon || MathF.Abs(k.Z) > Epsilon))];
        clip.Validate(resolved); return clip;
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
            var length = measure.Path.Zip(measure.Path.Skip(1)).Sum(p => Vector2.Distance(Flat(reference[p.First]), Flat(reference[p.Second])));
            if (length > .001f) ratios[measure.Id] = model.Measures[measure.Id] / length;
        }
        var overall = ratios.Count > 0 ? ratios.Values.Average() : Height(model.Rest.Values) / MathF.Max(.001f, Height(reference.Values));
        return (ratios, overall);
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Y);
    private static float Height(IEnumerable<Vector3> points) { var ys = points.Select(p => p.Y).ToArray(); return ys.Length == 0 ? 1 : ys.Max() - ys.Min(); }
}

/// <summary>The imported point libraries listed in the characters catalog, loaded on first use and kept read-only.</summary>
public sealed class SourceLibraries
{
    public sealed record Entry(string Id, string Label, string Anatomy, string Path, int ClipCount);
    private readonly Dictionary<string, PointLibrary> _loaded = new(StringComparer.Ordinal);

    private SourceLibraries(string root, IReadOnlyList<Entry> entries) { Root = root; Entries = entries; }
    public string Root { get; }
    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>Reads <c>catalog.json</c> under the characters root. A missing catalog means no sources, not an error.</summary>
    public static SourceLibraries Open(string charactersRoot)
    {
        var path = System.IO.Path.Combine(charactersRoot, "catalog.json");
        if (!File.Exists(path)) return new(charactersRoot, []);
        using var catalog = JsonDocument.Parse(File.ReadAllText(path));
        var entries = catalog.RootElement.GetProperty("libraries").EnumerateArray().Select(e => new Entry(
            e.GetProperty("id").GetString()!, e.GetProperty("label").GetString()!, e.GetProperty("anatomy").GetString()!,
            e.GetProperty("path").GetString()!, e.TryGetProperty("clipCount", out var count) ? count.GetInt32() : 0)).ToList();
        return new(charactersRoot, entries);
    }

    public bool TryGet(string id, [NotNullWhen(true)] out PointLibrary? library)
    {
        if (_loaded.TryGetValue(id, out library)) return true;
        var entry = Entries.FirstOrDefault(e => e.Id == id);
        if (entry is null) return false;
        _loaded[id] = library = PointLibrary.Load(System.IO.Path.Combine(Root, entry.Path));
        return true;
    }

    public PointLibrary Get(string id) => TryGet(id, out var library) ? library : throw new KeyNotFoundException($"No imported library '{id}'.");
}
