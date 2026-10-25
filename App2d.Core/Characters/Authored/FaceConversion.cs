using App2d.Core.Characters.Geometry;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

            asset.Parts.Add(piece with { });
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
            if (copy.Plays.Count > 0 || copy.Attachments.Count > 0 || copy.Colors.Count > 0 || copy.DrawOrder.Count > 0 || copy.Contacts.Count > 0 || copy.Travel.Keys.Count > 0)
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
            stand.Parts.Add(piece with { });
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

    // ---- The procedural face payload -> a face part instance (P7c) ------------------------------------------------

    /// <summary>One file of a payload conversion.</summary>
    public sealed record FileResult(string Kind, string Id, string Status, IReadOnlyList<string> Report);

    /// <summary>A payload conversion's outcome: every file written or kept, the verification and the report path.</summary>
    public sealed record PayloadResult(IReadOnlyList<FileResult> Files, IReadOnlyList<string> Differences, string ReportPath)
    {
        public bool Converted => Differences.Count == 0 && Files.All(f => !f.Status.StartsWith("failed", StringComparison.Ordinal));
    }

    /// <summary>
    /// Converts a model's procedural face payload (the P2b <c>face</c> on one part, with its clips' face tracks, its variants'
    /// face overrides and its looks' face words) into an instance of an animated face part on a socket, on a scratch tree with
    /// a report (plan rules 3, 5, 6). The model loses the payload and gains the instance (offset = the payload's offset, fit =
    /// the part's authored scale, layer just in front of the part) with the part's fit bindings copied onto the instance's fit;
    /// every clip's face track on that part becomes a <c>plays</c> track on the instance with the same words and times; every
    /// variant's face override becomes an expression override on the instance (legacy files convert to version 6 on the way);
    /// every look's face word becomes the look's expression for the instance. The asset must already exist under
    /// <c>parts/</c>. Verification: the converted model resolves with the instance and the same rest, every converted clip
    /// validates and selects at sampled instants exactly the word the face track held, and every variant resolves with the
    /// expression its override named. Nothing under <paramref name="root"/> is written; a rerun reports unchanged or kept outputs.
    /// </summary>
    public static PayloadResult ConvertPayload(string root, string modelId, string partId, string instanceId, string assetId, string socketId, string output)
    {
        var files = new List<FileResult>();
        var differences = new List<string>();
        var modelPath = Path.Combine(root, "models", modelId + ".json");
        var raw = JsonNode.Parse(File.ReadAllText(modelPath)) as JsonObject ?? throw new InvalidDataException($"'{modelPath}': empty file.");
        var modelReport = new List<string>();
        // The payload as the file carried it: a loose word with faceX (versions 5-9) or the typed object (10).
        string? word = null;
        var offset = 0f;
        foreach (var part in (Member(raw, "parts") as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var id = Member(part, "id")?.ToString();
            var faceNode = Take(part, "face");
            var faceX = Take(part, "faceX");
            if (faceNode is null && faceX is null)
            {
                continue;
            }

            if (id != partId)
            {
                throw new InvalidDataException($"Model '{modelId}' part '{id}' also carries a face; this conversion covers the face on '{partId}' only.");
            }

            if (faceNode is JsonObject payload)
            {
                word = Member(payload, "expression")?.ToString() ?? "relaxed";
                offset = Member(payload, "offsetX") is JsonValue o && o.TryGetValue(out float x) ? x : 0;
            }
            else
            {
                word = faceNode?.ToString() ?? "none";
                offset = faceX is JsonValue o && o.TryGetValue(out float x) ? x : 0;
            }
            modelReport.Add($"part '{partId}': face '{word}' at offset {offset:0.###} removed from the part");
        }
        if (word is null)
        {
            throw new InvalidDataException($"Model '{modelId}' part '{partId}' carries no face payload to convert.");
        }

        // Looks: a part's face word becomes the look's expression for the instance (set once the instance exists); a look offset has no equivalent and is reported.
        var lookWords = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var look in (Member(raw, "looks") as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            if (Member(look, "parts") is not JsonObject parts || Member(parts, partId) is not JsonObject change)
            {
                continue;
            }

            var lookFace = Take(change, "face");
            var lookOffset = Take(change, "faceX");
            var lookId = Member(look, "id")?.ToString() ?? "?";
            if (lookFace is not null)
            {
                lookWords[lookId] = lookFace.ToString();
                modelReport.Add($"look '{lookId}': face '{lookFace}' -> expressions.{instanceId}");
            }

            if (lookOffset is not null)
            {
                modelReport.Add($"look '{lookId}': faceX dropped (no instance equivalent in a look)");
            }

            if (change.Count == 0)
            {
                parts.Remove(partId);
            }
        }
        // The source files no longer read through the catalog (the readers refuse the retired face fields), so the model,
        // its clips and its variants are scanned raw; the part library loads as usual.
        var model = CharacterModel.FromJson(raw.ToJsonString());
        var before = ResolvedModel.From(CharacterModel.FromJson(raw.ToJsonString()));
        var catalog = AuthoredCatalog.Load(root);
        var asset = catalog.Parts.GetValueOrDefault(assetId) ?? throw new InvalidDataException($"No part asset '{assetId}' under '{root}/parts'; bake or convert it first.");
        static IEnumerable<(string Id, string Path, JsonObject Raw)> Scan(string directory, string owner, string ownerKey)
        {
            if (!Directory.Exists(directory))
            {
                yield break;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
            {
                if (JsonNode.Parse(File.ReadAllText(path)) is JsonObject node && Member(node, ownerKey)?.ToString() == owner && Member(node, "id")?.ToString() is { } id)
                {
                    yield return (id, path, node);
                }
            }
        }
        var face = model.Parts.FirstOrDefault(p => p.Id == partId) ?? throw new InvalidDataException($"Model '{modelId}' has no part '{partId}'.");
        var socket = model.Sockets.FirstOrDefault(s => s.Id == socketId) ?? throw new InvalidDataException($"Model '{modelId}' has no socket '{socketId}'.");
        Func<string, PartAsset?> partOf = id => catalog.Parts.GetValueOrDefault(id);
        ModelAuthoring.AddAttachment(model, assetId, socketId, partOf, instanceId);
        var instance = model.Attachments.First(a => a.Id == instanceId);
        instance.X = offset;
        instance.FitX = face.ScaleX;
        instance.FitY = face.ScaleY;
        instance.Layer = face.Depth - .002f;
        instance.Expression = asset.Binds(word) ? word : null;
        foreach (var (lookId, lookWord) in lookWords)
        {
            model.Looks.First(l => l.Id == lookId).Expressions[instanceId] = lookWord;
        }

        modelReport.Add($"attachment '{instanceId}': part '{assetId}' on socket '{socketId}' (control '{socket.Control}'), x {offset:0.###}, fit ({face.ScaleX:0.###}, {face.ScaleY:0.###}), layer {instance.Layer:0.###}{(instance.Expression is null ? "" : $", default expression '{instance.Expression}'")}");
        if (!asset.Binds(word))
        {
            differences.Add($"model: part '{assetId}' binds no expression '{word}' for the part's default");
        }

        foreach (var parameter in model.Parameters)
        {
            foreach (var binding in parameter.Bindings.Where(b => b.Target.Part == partId && b.Target.Property is RigProperties.ScaleX or RigProperties.ScaleY).ToArray())
            {
                var fit = binding.Target.Property == RigProperties.ScaleX ? RigProperties.FitX : RigProperties.FitY;
                parameter.Bindings.Add(binding with { Target = PropertyRef.OfAttachment(instanceId, fit), Reference = binding.Reference is null ? null : binding.Reference with { } });
                modelReport.Add($"parameter '{parameter.Id}': the {binding.Kind} binding on part '{partId}' {binding.Target.Property} copied onto attachment '{instanceId}' {fit}");
            }
        }
        model.Validate();
        var resolved = ResolvedModel.From(model, null, partOf);
        foreach (var control in before.Order)
        {
            var distance = Vector3.Distance(before.Rest[control.Id], resolved.Rest[control.Id]);
            if (distance > 1e-6f)
            {
                differences.Add($"model: control '{control.Id}' rest moved by {distance:0.######}");
            }
        }
        files.Add(Write("model", modelId, Path.Combine(output, "models", modelId + ".json"), model.ToJson(), json => CharacterModel.FromJson(json), modelReport));

        // Clips: the face track on the part becomes the instance's selection channel, word for word.
        var faceInstances = new Dictionary<string, string>(StringComparer.Ordinal) { [partId] = instanceId };
        foreach (var (clipId, path, clipRaw) in Scan(Path.Combine(root, "animations"), modelId, "model"))
        {
            if (Take(clipRaw, "faces") is not JsonArray faces || faces.Count == 0)
            {
                continue;
            }

            var report = new List<string>();
            var plays = new JsonArray();
            var expected = new List<(float Time, string Word)>();
            foreach (var track in faces.OfType<JsonObject>())
            {
                var part = Member(track, "part")?.ToString();
                if (part != partId)
                {
                    throw new InvalidDataException($"Clip '{clipId}' animates a face on '{part}', not '{partId}'.");
                }

                var keys = new JsonArray();
                foreach (var key in (Member(track, "keys") as JsonArray)?.OfType<JsonObject>() ?? [])
                {
                    var time = Member(key, "time") is JsonValue t && t.TryGetValue(out float at) ? at : 0;
                    var expression = Member(key, "expression")?.ToString() ?? "relaxed";
                    keys.Add(new JsonObject { ["time"] = time, ["expression"] = expression });
                    expected.Add((time, expression));
                    if (!asset.Binds(expression))
                    {
                        differences.Add($"clip '{clipId}': part '{assetId}' binds no expression '{expression}'");
                    }
                }
                plays.Add(new JsonObject { ["instance"] = instanceId, ["keys"] = keys });
                report.Add($"face track on '{partId}' ({keys.Count} keys) -> plays on '{instanceId}'");
            }
            clipRaw["plays"] = plays;
            var clip = MotionClip.FromJson(clipRaw.ToJsonString());
            try
            {
                clip.Validate(resolved);
            }
            catch (InvalidDataException ex)
            {
                differences.Add($"clip '{clipId}': {ex.Message}");
            }
            foreach (var (time, expression) in expected)
            {
                var shown = PoseEvaluator.Sample(resolved, clip, time).Expressions.GetValueOrDefault(instanceId);
                if (shown != expression)
                {
                    differences.Add($"clip '{clipId}' at {time:0.###} s: the face track held '{expression}', the instance shows '{shown}'");
                }
            }
            files.Add(Write("clip", clipId, Path.Combine(output, "animations", clipId + ".json"), clip.ToJson(), json => MotionClip.FromJson(json), report));
        }

        // Variants: a face override on the part becomes an expression override on the instance; legacy files convert on the way.
        foreach (var (variantId, path, _) in Scan(Path.Combine(root, "variants"), modelId, "base"))
        {
            var json = File.ReadAllText(path);
            var report = new List<string>();
            ModelVariant variant;
            if (LegacyVariantReader.LegacyVersion(json) is { } legacy)
            {
                var (converted, lines) = LegacyVariantReader.Convert(json, null, faceInstances);
                report.AddRange(lines);
                variant = ModelVariant.FromJson(converted);
            }
            else
            {
                variant = ModelVariant.FromJson(json);
                var changed = false;
                foreach (var change in variant.Overrides.ToArray())
                {
                    if (change.Target.Part != partId || change.Target.Property is not ("face" or "faceX"))
                    {
                        continue;
                    }

                    variant.Overrides.Remove(change);
                    if (change.Target.Property == "face" && change.Selection is { } selection)
                    {
                        variant.Overrides.Add(PropertyOverride.Of(PropertyRef.OfAttachment(instanceId, RigProperties.Expression), selection));
                        report.Add($"part '{partId}' face '{selection}' -> attachment '{instanceId}' expression");
                    }
                    else
                    {
                        report.Add($"part '{partId}' {change.Target.Property} override dropped ({change.Describe()}; no instance equivalent)");
                    }

                    changed = true;
                }
                if (!changed)
                {
                    files.Add(new("variant", variantId, "untouched: no face override", []));
                    continue;
                }
            }
            try
            {
                var resolvedVariant = ResolvedModel.From(model, variant, partOf);
                var expression = variant.Override(PropertyRef.OfAttachment(instanceId, RigProperties.Expression))?.Selection;
                var shown = PoseEvaluator.Rest(resolvedVariant).Expressions.GetValueOrDefault(instanceId);
                if (expression is not null && shown != expression)
                {
                    differences.Add($"variant '{variantId}': the face override said '{expression}', the instance shows '{shown}'");
                }
            }
            catch (InvalidDataException ex)
            {
                differences.Add($"variant '{variantId}': {ex.Message}");
            }
            files.Add(Write("variant", variantId, Path.Combine(output, "variants", variantId + ".json"), variant.ToJson(), j => ModelVariant.FromJson(j), report));
        }
        var reportPath = Path.Combine(output, "face-payload-conversion-report.md");
        Directory.CreateDirectory(output);
        var text = new StringBuilder();
        text.AppendLine($"# Face payload conversion report: '{modelId}' part '{partId}' -> attachment '{instanceId}' of '{assetId}' on '{socketId}'");
        text.AppendLine();
        text.AppendLine($"Source: `{root}` (unchanged). Date: {DateTime.Now:yyyy-MM-dd HH:mm}.");
        text.AppendLine();
        text.AppendLine("| Kind | Id | Status |");
        text.AppendLine("| --- | --- | --- |");
        foreach (var file in files)
        {
            text.AppendLine($"| {file.Kind} | {file.Id} | {file.Status} |");
        }
        foreach (var file in files.Where(f => f.Report.Count > 0))
        {
            text.AppendLine();
            text.AppendLine($"## {file.Kind} {file.Id}");
            text.AppendLine();
            foreach (var line in file.Report)
            {
                text.AppendLine("- " + line);
            }
        }
        foreach (var difference in differences)
        {
            text.AppendLine("- DIFFERENCE: " + difference);
        }
        File.WriteAllText(reportPath, text.ToString());
        return new(files, differences, reportPath);
    }

    private static FileResult Write(string kind, string id, string target, string fresh, Func<string, object> parse, List<string> report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target))
        {
            var existing = File.ReadAllText(target);
            if (existing == fresh)
            {
                return new(kind, id, "unchanged: the output already holds this conversion", report);
            }

            try
            {
                parse(existing);
                return new(kind, id, "kept: the output was edited after an earlier run (delete it to reconvert)", report);
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            {
                report.Add($"replacing an unreadable earlier output: {ex.Message}");
            }
        }
        AuthoredAsset.Write(target, fresh);
        return new(kind, id, $"converted -> `{target}`", report);
    }

    private static JsonNode? Member(JsonObject node, string name) => node.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static JsonNode? Take(JsonObject node, string name)
    {
        var key = node.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Key;
        if (key is null)
        {
            return null;
        }

        var value = node[key];
        node.Remove(key);
        return value;
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
