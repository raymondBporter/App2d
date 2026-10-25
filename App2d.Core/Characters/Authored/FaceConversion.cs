using App2d.Core.Characters.Geometry;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Explicit conversion of a model's face into a reusable animated face part (character-system migration, P7b; plan rules 3, 5
/// and 6). The controls beneath one attachment control become the part's rig, re-expressed in that control's rest frame; the
/// pieces hanging on them become the part's pieces; the model's clips become the part's clips, with a legacy affine control's
/// scale tracks turned into explicit artwork-fit tracks on the pieces it frames (baseline 7.3: eye squash is an artwork fit,
/// never rig scale); the model's groups become the part's groups; the template's roles become expression bindings. The
/// conversion runs on a scratch output tree with a report, verifies every clip against the source model at sampled instants
/// (control positions 1e-4, piece silhouettes 1e-3), never writes under the source, and a rerun keeps an output edited after
/// an earlier run. Moving the output into the authored tree is a separate, deliberate step.
/// </summary>
public static class FaceConversion
{
    /// <summary>One conversion's outcome.</summary>
    public sealed record Result(string PartId, string Source, string? Output, string Status, IReadOnlyList<string> Report, IReadOnlyList<string> Differences)
    {
        public bool Converted => Output is not null && Differences.Count == 0;
    }

    /// <summary>What to build: the part's ID and name, the blink layer (mask group and clip) and its clock, and the expression words per clip.</summary>
    public sealed record Plan(string PartId, string Name)
    {
        public string Usage { get; init; } = "face";
        public string Fitting { get; init; } = PartFittings.Layout;
        /// <summary>The group the blink layer owns and the clip it loops by default on the actor's clock; null for no blink layer.</summary>
        public (string Group, string Clip)? Blink { get; init; }
        /// <summary>Expression word to clip ID (and an optional held time for a pose); the template's roles by default.</summary>
        public Dictionary<string, (string Clip, float? Time)> Expressions { get; init; } = [];
        /// <summary>Words that play on the blink layer rather than the base.</summary>
        public HashSet<string> OnBlinkLayer { get; init; } = new(StringComparer.Ordinal);
        /// <summary>Groups the part declares beyond the model's own (a blink mask over the eye controls and pupils).</summary>
        public Dictionary<string, string[]> Groups { get; init; } = [];
        public string? Default { get; init; }
    }

    /// <summary>Converts the face beneath <paramref name="control"/> of <paramref name="modelId"/> under <paramref name="root"/> into <c>&lt;output&gt;/parts/&lt;id&gt;.json</c> with a report. Nothing under <paramref name="root"/> is written.</summary>
    public static Result Convert(string root, string modelId, string control, Plan plan, string output, float positionTolerance = 1e-4f, float silhouetteTolerance = 1e-3f)
    {
        var catalog = AuthoredCatalog.Load(root);
        var model = catalog.Models.GetValueOrDefault(modelId) ?? throw new InvalidDataException($"No model '{modelId}' under '{root}'.");
        var clips = catalog.Animations.Values.Where(c => c.Model == modelId).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var source = catalog.PathOf(modelId);
        Directory.CreateDirectory(Path.Combine(output, "parts"));
        var target = Path.Combine(output, "parts", plan.PartId + ".json");
        Result result;
        try
        {
            var (asset, report) = Build(model, clips, control, plan);
            var differences = Verify(catalog.Resolve(modelId), clips, control, asset, positionTolerance, silhouetteTolerance);
            var lines = new List<string>(report);
            if (differences.Count > 0)
            {
                result = new(plan.PartId, source, null, "not written: verification failed", lines, differences);
            }
            else
            {
                var fresh = asset.ToJson();
                if (File.Exists(target))
                {
                    var existing = File.ReadAllText(target);
                    if (existing == fresh)
                    {
                        result = new(plan.PartId, source, target, "unchanged: the output already holds this conversion", lines, []);
                        goto write;
                    }

                    try
                    {
                        PartAsset.FromJson(existing);
                        result = new(plan.PartId, source, target, "kept: the output was edited after an earlier run (delete it to reconvert)", lines, []);
                        goto write;
                    }
                    catch (Exception ex) when (ex is InvalidDataException or JsonException)
                    {
                        lines.Add($"replacing an unreadable earlier output: {ex.Message}");
                    }
                }
                asset.Save(target);
                result = new(plan.PartId, source, target, $"converted: model '{modelId}' face under '{control}' -> part '{plan.PartId}' with {asset.Clips.Count} clip(s)", lines, []);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException)
        {
            result = new(plan.PartId, source, null, "failed", [], [ex.Message]);
        }
    write:
        File.WriteAllText(Path.Combine(output, "face-conversion-report.md"), Report(root, modelId, control, result));
        return result;
    }

    /// <summary>The part the model's face converts to, in memory, with the conversion report lines. Nothing is written.</summary>
    public static (PartAsset Asset, IReadOnlyList<string> Report) Build(CharacterModel model, IReadOnlyList<MotionClip> clips, string control, Plan plan)
    {
        var report = new List<string>();
        var anchor = model.Controls.FirstOrDefault(c => c.Id == control) ?? throw new InvalidDataException($"Model '{model.Id}' has no control '{control}'.");
        var controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        bool Beneath(ModelControl c)
        {
            for (var current = c.Parent; current is not null; current = controls[current].Parent)
            {
                if (current == control)
                {
                    return true;
                }
            }

            return false;
        }
        var converted = model.Controls.Where(Beneath).ToList();
        if (converted.Count == 0)
        {
            throw new InvalidDataException($"Model '{model.Id}': nothing hangs beneath '{control}'.");
        }

        var resolved = ResolvedModel.From(model);
        var origin = resolved.Rest[control];
        if (resolved.RestTransforms[control].M11 != 1 || resolved.RestTransforms[control].M12 != 0)
        {
            throw new InvalidDataException($"Model '{model.Id}': '{control}' is not axis-aligned at rest; the conversion re-expresses positions by translation only.");
        }

        var asset = new PartAsset
        {
            Id = plan.PartId,
            Name = plan.Name,
            Usage = plan.Usage,
            Payload = PartPayloads.Animated,
            Fitting = plan.Fitting,
            Ink = model.Ink,
            LineWidth = model.LineWidth,
            Materials = model.Materials.ToDictionary(m => m.Key, m => m.Value, StringComparer.Ordinal),
        };
        var ids = converted.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        // The rig: each converted control keeps its kind and parent-local meaning; the ones hanging from the anchor become
        // roots at the anchor's rest, the affine eyes become point controls (their transform was a pure translation).
        var affineFrames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in converted)
        {
            var part = c with { Parent = c.Parent == control ? null : c.Parent };
            switch (c.Node)
            {
                case RigNodeKinds.Affine:
                {
                    var t = c.Transform!;
                    if (t.ScaleX != 1 || t.ScaleY != 1 || t.ShearX != 0 || t.ShearY != 0 || t.Rotation != 0)
                    {
                        throw new InvalidDataException($"Control '{c.Id}' is a legacy affine node with setup scale, shear or rotation; only a translation setup converts to a point control (report, do not approximate).");
                    }

                    part = part with { Node = RigNodeKinds.Point, Transform = null, Rest = new(c.Rest.X - origin.X, c.Rest.Y - origin.Y, c.Rest.Z - origin.Z) };
                    affineFrames[c.Id] = c.Id;
                    report.Add($"control '{c.Id}': legacy affine node (translation only) -> point control at ({part.Rest.X:0.####}, {part.Rest.Y:0.####})");
                    break;
                }
                case RigNodeKinds.Bone:
                    report.Add($"control '{c.Id}': bone node kept{(c.Parent == control ? " as a rig root (its local setup is the anchor-relative one)" : "")}");
                    break;
                default:
                    part = part with { Rest = new(c.Rest.X - origin.X, c.Rest.Y - origin.Y, c.Rest.Z - origin.Z) };
                    report.Add($"control '{c.Id}': point control at ({part.Rest.X:0.####}, {part.Rest.Y:0.####}){(c.Parent == control ? " (rig root)" : "")}");
                    break;
            }
            asset.Controls.Add(part);
        }
        foreach (var constraint in model.Constraints)
        {
            if (constraint is ModelChain chain && ids.Contains(chain.Root))
            {
                asset.Constraints.Add(chain with { });
                report.Add($"constraint '{chain.Id}' kept");
            }
        }
        // The pieces: everything hanging on a converted control; a piece touching the anchor itself stays with the wearer.
        foreach (var piece in model.Parts)
        {
            var refs = new[] { piece.A, piece.B, piece.Frame }.Concat(piece.Bindings?.SelectMany(b => new[] { b.Control, b.Toward }) ?? []).OfType<string>().ToArray();
            if (refs.All(r => !ids.Contains(r)))
            {
                report.Add($"piece '{piece.Id}' stays with the model (it touches no converted control)");
                continue;
            }

            if (refs.Any(r => !ids.Contains(r)))
            {
                throw new InvalidDataException($"Piece '{piece.Id}' references both converted controls and others ({string.Join(", ", refs.Where(r => !ids.Contains(r)))}); split it before converting.");
            }

            asset.Parts.Add(piece with { Face = null });
            report.Add($"piece '{piece.Id}' -> part piece{(piece.Wave is not null ? " (wave kept)" : "")}");
        }
        foreach (var group in model.Groups.Where(g => g.Targets.All(ids.Contains)))
        {
            asset.Groups.Add(new() { Id = group.Id, Targets = [.. group.Targets] });
            report.Add($"group '{group.Id}' kept ({group.Targets.Count} targets)");
        }
        foreach (var (id, targets) in plan.Groups)
        {
            var unknown = targets.Where(t => !ids.Contains(t) && asset.Parts.All(p => p.Id != t)).ToArray();
            if (unknown.Length > 0)
            {
                throw new InvalidDataException($"Plan group '{id}' names {string.Join(", ", unknown)}, which the face does not convert.");
            }

            asset.Groups.Add(new() { Id = id, Targets = [.. targets] });
            report.Add($"group '{id}' declared by the plan ({targets.Length} targets)");
        }
        // The clips: tracks on converted controls copied; a scale track on a former affine control becomes a fit track on
        // every piece that control frames (the same multipliers, now declared artwork fit).
        foreach (var clip in clips)
        {
            var copy = MotionClip.FromJson(clip.ToJson());
            copy.Model = plan.PartId;
            copy.StructureRevision = 1;
            copy.Reference = [];
            copy.Source = null;
            var tracks = new List<ClipTrack>();
            foreach (var track in copy.Tracks)
            {
                if (track.Kind == MotionClip.OrientKind || !ids.Contains(track.Target) && !(track.Kind == MotionClip.TargetKind && asset.Constraints.Any(c => c.Id == track.Target)))
                {
                    throw new InvalidDataException($"Clip '{clip.Id}' {track.Kind} track '{track.Target}' targets something outside the face; the conversion covers tracks on the converted controls only.");
                }

                if (track.Kind == MotionClip.ScaleKind && affineFrames.ContainsKey(track.Target))
                {
                    var framed = asset.Parts.Where(p => p.Frame == track.Target).ToArray();
                    if (framed.Length == 0)
                    {
                        throw new InvalidDataException($"Clip '{clip.Id}' scales '{track.Target}' but no piece is framed by it; nothing would carry the squash.");
                    }

                    foreach (var piece in framed)
                    {
                        tracks.Add(new() { Kind = MotionClip.FitKind, Target = piece.Id, Keys = [.. track.Keys.Select(k => k with { })], SetupBeforeFirst = track.SetupBeforeFirst });
                    }

                    report.Add($"clip '{clip.Id}': scale track on '{track.Target}' -> fit track(s) on {string.Join(", ", framed.Select(p => "'" + p.Id + "'"))}");
                    continue;
                }

                if (track.Kind == MotionClip.ShearKind)
                {
                    throw new InvalidDataException($"Clip '{clip.Id}' shears '{track.Target}'; shear has no artwork-fit equivalent (report, do not approximate).");
                }

                tracks.Add(track);
            }
            copy.Tracks = tracks;
            if (copy.Faces.Count > 0 || copy.Plays.Count > 0 || copy.Attachments.Count > 0 || copy.Colors.Count > 0 || copy.DrawOrder.Count > 0 || copy.Contacts.Count > 0 || copy.Travel.Keys.Count > 0)
            {
                throw new InvalidDataException($"Clip '{clip.Id}' carries faces, plays, slot timelines, contacts or travel; a face part's clip animates its rig alone.");
            }

            asset.Clips.Add(copy);
            report.Add($"clip '{clip.Id}' -> part clip ({tracks.Count} tracks, {clip.Duration:0.##} s{(clip.Loop ? ", loops" : "")})");
        }
        asset.Layers.Add(new() { Id = "expression" });
        if (plan.Blink is { } blink)
        {
            asset.Layers.Add(new() { Id = "blink", Mask = blink.Group, Clock = PartClocks.Local, Clip = blink.Clip });
            report.Add($"layer 'blink' over group '{blink.Group}' loops '{blink.Clip}' on the actor's clock");
        }

        foreach (var (word, (clipId, time)) in plan.Expressions)
        {
            asset.Expressions[word] = new() { Clip = clipId, Time = time, Layer = plan.OnBlinkLayer.Contains(word) ? "blink" : null };
            report.Add($"expression '{word}' -> {(time is { } t ? $"holds '{clipId}' at {t:0.##} s" : $"plays '{clipId}'")}{(plan.OnBlinkLayer.Contains(word) ? " on the blink layer" : "")}");
        }

        asset.Expression = plan.Default;
        asset.Exposed = [.. asset.Controls.Select(c => c.Id).Concat(asset.Parts.Select(p => p.Id))];
        report.Add($"exposed: every control and piece ({asset.Exposed.Count})");
        asset.Validate();
        return (asset, report);
    }

    /// <summary>A minimal wearer carrying the converted part on a socket at the anchor's rest, for verification and previews.</summary>
    public static CharacterModel Stand(ResolvedModel source, string control, PartAsset asset, string instance = "face")
    {
        var stand = new CharacterModel { Id = "face-stand", Name = "Face stand" };
        stand.Controls.Add(new() { Id = control, Rest = PuppetPoint.From(source.Rest[control]) });
        foreach (var piece in source.Base.Parts.Where(p => p.A == control && (p.B is null || p.B == control) && (p.Frame is null || p.Frame == control)))
        {
            stand.Parts.Add(piece with { Face = null });
        }

        stand.Sockets.Add(new() { Id = "face", Control = control, Frame = CharacterModel.Locomotion });
        stand.Attachments.Add(new() { Id = instance, Asset = asset.Id, Socket = "face" });
        stand.Validate();
        return stand;
    }

    /// <summary>Every clip at sampled instants on the source model against the part on its stand: control positions and piece silhouettes.</summary>
    public static List<string> Verify(ResolvedModel source, IReadOnlyList<MotionClip> clips, string control, PartAsset asset, float positionTolerance, float silhouetteTolerance)
    {
        var differences = new List<string>();
        // A probe copy of the asset: every clip bound as its own word on the base layer, the other layers' default clips silenced,
        // so one clip at a time is compared against the source model's clip.
        var probe = PartAsset.FromJson(asset.ToJson());
        probe.Expressions.Clear();
        foreach (var layer in probe.Layers.Skip(1))
        {
            layer.Clip = null;
        }

        foreach (var clip in clips)
        {
            probe.Expressions["verify-" + clip.Id] = new() { Clip = clip.Id };
        }
        probe.Expression = null;
        probe.Validate();
        var stand = ResolvedModel.From(Stand(source, control, probe), null, id => id == probe.Id ? probe : null);
        foreach (var clip in clips)
        {
            foreach (var time in new[] { 0f, clip.Duration * .25f, clip.Duration * .5f, clip.Duration * .75f, clip.Duration })
            {
                // The part repeats a looping clip; the source is sampled the same way, so the end of a cycle wraps on both sides.
                var expected = PoseEvaluator.Sample(source, clip, time, repeat: true);
                var evaluated = PoseEvaluator.Sample(stand, null, time, input: new() { Plays = [new("face", "verify-" + clip.Id, time)] });
                var instance = evaluated.Attachments["face"];
                foreach (var c in asset.Controls)
                {
                    var distance = Vector3.Distance(expected.Points[c.Id], instance.World(c.Id));
                    if (distance > positionTolerance)
                    {
                        differences.Add($"clip '{clip.Id}' at {time:0.###} s: control '{c.Id}' expected {expected.Points[c.Id]}, converted {instance.World(c.Id)} ({distance:0.######} apart)");
                    }
                }
                foreach (var piece in asset.Parts)
                {
                    var before = Silhouette(PartContours2D.Contour(source.Parts.First(p => p.Id == piece.Id), source, expected));
                    var after = Silhouette(PartContours2D.Contour(instance.Pose.Fitted(piece), instance.World, instance.Frame, instance.Rest, instance.RestFrame));
                    var gap = MathF.Max(Vector3.Distance(before.Min, after.Min), MathF.Max(Vector3.Distance(before.Max, after.Max), MathF.Max(Vector3.Distance(before.Centroid, after.Centroid), MathF.Abs(before.Area - after.Area))));
                    if (gap > silhouetteTolerance)
                    {
                        differences.Add($"clip '{clip.Id}' at {time:0.###} s: piece '{piece.Id}' silhouette moved by {gap:0.######}");
                    }
                }
            }
        }
        return differences;
    }

    private static (Vector3 Min, Vector3 Max, Vector3 Centroid, float Area) Silhouette(List<Vector3> contour)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        var sum = Vector3.Zero;
        double area = 0;
        for (var i = 0; i < contour.Count; i++)
        {
            var p = contour[i];
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            sum += p;
            var q = contour[(i + 1) % contour.Count];
            area += (double)p.X * q.Y - (double)q.X * p.Y;
        }

        return (min, max, sum / contour.Count, (float)Math.Abs(area / 2));
    }

    private static string Report(string root, string modelId, string control, Result result)
    {
        var text = new StringBuilder();
        text.AppendLine($"# Face conversion report: '{modelId}' beneath '{control}' -> part '{result.PartId}'");
        text.AppendLine();
        text.AppendLine($"Source: `{root}` (unchanged). Date: {DateTime.Now:yyyy-MM-dd HH:mm}.");
        text.AppendLine();
        text.AppendLine($"Status: {result.Status}{(result.Output is null ? "" : $" (`{result.Output}`)")}");
        text.AppendLine();
        foreach (var line in result.Report)
        {
            text.AppendLine("- " + line);
        }

        foreach (var difference in result.Differences)
        {
            text.AppendLine("- DIFFERENCE: " + difference);
        }

        return text.ToString();
    }
}
