using App2d.Core.Characters.Geometry;
using System.Numerics;
using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Converts one model bundle onto the canonical rig (character-system migration, P11a): every point-solved constraint onto
/// the bone solver (<see cref="RigConversion.ToBoneConstraint"/>), every legacy affine node whose setup is a pure translation
/// into a point control with its scale tracks turned into artwork-fit tracks on the pieces it frames (baseline 7.3), and the
/// file to the current version. Transactional on a scratch output tree with a report (plan rules 3, 5, 6, 7): the converted
/// model and clips are verified against the source at several instants of every clip on the base and on every variant of
/// the model (control positions, chain residuals and reached flags, contacts, socket frames, nested instances and part
/// silhouettes within the stated tolerances; the frame angles of the solved bones are expected to move and are reported, not
/// counted), nothing is written when a difference remains, a rerun reports an output already holding the conversion as
/// "unchanged" and an output edited afterwards as "kept", and the source tree is never written.
/// </summary>
public static class ModelConversion
{
    public sealed record FileResult(string Kind, string Id, string Status);

    public sealed record Result(string ModelId, string Source, string? Output, string Status, IReadOnlyList<FileResult> Files, IReadOnlyList<string> Report, IReadOnlyList<string> Differences)
    {
        public bool Converted => Output is not null && Differences.Count == 0;
    }

    /// <summary>How to name the solved bone inserted beneath a pivot (null: <c>&lt;root&gt;-bone</c>).</summary>
    public delegate string? BoneNaming(ModelChain chain);

    /// <summary>Converts <paramref name="modelId"/> under <paramref name="root"/> into <c>&lt;output&gt;/models</c> and <c>&lt;output&gt;/animations</c> with <c>model-conversion-&lt;id&gt;.md</c>. Nothing under <paramref name="root"/> is written.</summary>
    public static Result Convert(string root, string modelId, string output, BoneNaming? naming = null, float positionTolerance = 1e-4f, float residualTolerance = 1e-5f, float silhouetteTolerance = 1e-3f)
    {
        var catalog = AuthoredCatalog.Load(root);
        var source = catalog.Models.GetValueOrDefault(modelId) ?? throw new InvalidDataException($"No model '{modelId}' under '{root}'.");
        var sourcePath = catalog.PathOf(modelId);
        var sourceVersion = FileVersion(File.ReadAllText(sourcePath));
        var clips = catalog.Animations.Values.Where(c => c.Model == modelId).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var variants = catalog.Variants.Values.Where(v => v.Base == modelId).OrderBy(v => v.Id, StringComparer.Ordinal).ToList();
        Func<string, PartAsset?> partOf = catalog.Parts.GetValueOrDefault;
        var report = new List<string> { $"model '{modelId}' (file at version {sourceVersion?.ToString() ?? "?"}, read at {source.Version}), {clips.Count} clip(s), {variants.Count} variant(s)" };
        var files = new List<FileResult>();
        Result result;
        try
        {
            var (model, converted, lines) = Build(source, clips, naming, partOf);
            report.AddRange(lines);
            var differences = Verify(source, clips, variants, model, converted, partOf, positionTolerance, residualTolerance, silhouetteTolerance, report);
            if (differences.Count > 0)
            {
                result = new(modelId, sourcePath, null, "not written: verification failed", files, report, differences);
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(output, "models"));
                Directory.CreateDirectory(Path.Combine(output, "animations"));
                var statuses = new List<string> { Write(Path.Combine(output, "models", model.Id + ".json"), model.ToJson(), "model", model.Id, files, json => CharacterModel.FromJson(json)) };
                for (var i = 0; i < clips.Count; i++)
                {
                    if (converted[i].ToJson() == clips[i].ToJson() && clips[i].Version == MotionClip.CurrentVersion)
                    {
                        files.Add(new("clip", clips[i].Id, "unchanged (not written: the source clip needs no conversion)"));
                        continue;
                    }

                    statuses.Add(Write(Path.Combine(output, "animations", converted[i].Id + ".json"), converted[i].ToJson(), "clip", converted[i].Id, files, json => MotionClip.FromJson(json)));
                }

                var status = statuses.All(s => s == "unchanged") ? "unchanged: the output already holds this conversion"
                    : statuses.Any(s => s == "written") ? $"converted: model '{modelId}' at version {model.Version} with {statuses.Count(s => s == "written") - (statuses[0] == "written" ? 1 : 0)} clip(s) rewritten"
                    : "kept: the output was edited after an earlier run (delete it to reconvert)";
                result = new(modelId, sourcePath, output, status, files, report, []);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException)
        {
            result = new(modelId, sourcePath, null, "failed", files, report, [ex.Message]);
        }

        Directory.CreateDirectory(output);
        var text = $"# Model conversion: {modelId}\n\nSource: `{sourcePath}` (never written).\n\nStatus: {result.Status}\n\n" +
            string.Join("\n", result.Report.Select(r => "- " + r)) + "\n" +
            (result.Files.Count > 0 ? "\n## Files\n\n" + string.Join("\n", result.Files.Select(f => $"- {f.Kind} `{f.Id}`: {f.Status}")) + "\n" : "") +
            (result.Differences.Count > 0 ? "\n## Differences\n\n" + string.Join("\n", result.Differences.Select(d => "- " + d)) + "\n" : "");
        File.WriteAllText(Path.Combine(output, $"model-conversion-{modelId}.md"), text);
        return result;
    }

    /// <summary>Converts a copy of <paramref name="source"/> and its clips in memory; the source objects are not changed.</summary>
    public static (CharacterModel Model, IReadOnlyList<MotionClip> Clips, IReadOnlyList<string> Report) Build(CharacterModel source, IReadOnlyList<MotionClip> clips, BoneNaming? naming = null, Func<string, PartAsset?>? partOf = null)
    {
        var model = CharacterModel.FromJson(source.ToJson());
        var converted = clips.Select(c => MotionClip.FromJson(c.ToJson())).ToList();
        var report = new List<string>();
        var changed = false;
        // Legacy affine nodes: a pure translation is a point control; anything else is unsupported and reported by name.
        var nodes = RigGraph.Compile(model);
        foreach (var control in model.Controls.Where(c => c.Node == RigNodeKinds.Affine).ToArray())
        {
            var affine = control.Transform!;
            if (affine.ScaleX != 1 || affine.ScaleY != 1 || affine.Rotation != 0 || affine.ShearX != 0 || affine.ShearY != 0)
            {
                throw new InvalidDataException($"Control '{control.Id}' is a legacy affine node with scale ({affine.ScaleX:0.###}, {affine.ScaleY:0.###}), rotation {affine.Rotation:0.###} and shear ({affine.ShearX:0.###}, {affine.ShearY:0.###}); inherited nonuniform scale or shear is not representable in the similarity rig (unsupported, report; path: {model.Id} > controls > {control.Id} > transform).");
            }

            var framed = model.Parts.Where(p => p.Frame == control.Id).Select(p => p.Id).ToList();
            changed = true;
            control.Node = RigNodeKinds.Point;
            control.Rest = PuppetPoint.From(nodes[control.Id].Rest);
            control.RestAngle = 0;
            control.Transform = null;
            report.Add($"control '{control.Id}': translation-only affine node -> point control at ({control.Rest.X:0.####}, {control.Rest.Y:0.####}, {control.Rest.Z:0.####}){(framed.Count > 0 ? $", framing {string.Join(", ", framed.Select(f => "'" + f + "'"))}" : "")}");
            foreach (var clip in converted)
            {
                foreach (var track in clip.Tracks.Where(t => t.Target == control.Id && t.Kind is MotionClip.ScaleKind or MotionClip.ShearKind).ToArray())
                {
                    if (track.Kind == MotionClip.ShearKind)
                    {
                        throw new InvalidDataException($"Clip '{clip.Id}' shears '{control.Id}'; shear has no artwork-fit equivalent (unsupported, report).");
                    }

                    clip.Tracks.Remove(track);
                    foreach (var piece in framed)
                    {
                        clip.Tracks.Add(new() { Kind = MotionClip.FitKind, Target = piece, Keys = [.. track.Keys.Select(k => k with { })], SetupBeforeFirst = track.SetupBeforeFirst });
                    }

                    report.Add($"clip '{clip.Id}': scale track on '{control.Id}' -> fit track(s) on {string.Join(", ", framed.Select(f => "'" + f + "'"))} (the same multipliers as declared artwork fit)");
                }
            }
        }

        foreach (var chain in model.IkChains.Where(c => c.Solver == ModelChain.PointSolver).ToArray())
        {
            var conversion = RigConversion.ToBoneConstraint(model, chain.Id, converted, naming?.Invoke(chain));
            report.AddRange(conversion.Report.Select(line => $"constraint '{chain.Id}': {line}"));
            changed = true;
        }

        model.Version = CharacterModel.CurrentVersion;
        if (changed)
        {
            // The rig's structure changed: the model's revision moves on and every clip of the bundle follows it.
            model.StructureRevision++;
        }

        foreach (var clip in converted)
        {
            clip.Version = MotionClip.CurrentVersion;
            clip.StructureRevision = model.StructureRevision;
        }

        if (!changed)
        {
            report.Add("nothing to convert: every constraint is on the bone solver and no control is an affine node");
        }

        model.Validate();
        var resolved = model.Attachments.Count > 0 && partOf is not null ? ResolvedModel.From(model, null, partOf) : ResolvedModel.Rig(model);
        foreach (var clip in converted)
        {
            clip.Validate(resolved, exactRevision: false);
        }

        return (model, converted, report);
    }

    /// <summary>Samples every clip on the source and the converted model at several instants, on the base and every variant, and lists what moved beyond tolerance.</summary>
    public static List<string> Verify(CharacterModel source, IReadOnlyList<MotionClip> clips, IReadOnlyList<ModelVariant> variants, CharacterModel model, IReadOnlyList<MotionClip> converted, Func<string, PartAsset?> partOf,
        float positionTolerance, float residualTolerance, float silhouetteTolerance, List<string>? report = null)
    {
        var differences = new List<string>();
        var moved = new SortedSet<string>(StringComparer.Ordinal);
        var largest = 0f;
        var residualShift = 0f;
        // The point adapter clamped a target at full reach to (l1 + l2)(1 - 1e-5), which bent the joint sideways by up to a
        // few thousandths while the end stayed put; the exact solver lays the segments straight. A joint that moves by that
        // much while its end does not is the adapter's inset, counted and reported rather than treated as a difference.
        const float insetTolerance = 2.5e-3f;
        var insets = 0;
        var insetLargest = 0f;
        foreach (var variant in variants.Prepend<ModelVariant?>(null))
        {
            var who = variant is null ? source.Id : variant.Id;
            ResolvedModel was, now;
            try
            {
                was = ResolvedModel.From(source, variant, partOf);
                now = ResolvedModel.From(model, variant, partOf);
            }
            catch (InvalidDataException ex)
            {
                differences.Add($"{who}: does not resolve on the converted model: {ex.Message}");
                continue;
            }

            foreach (var control in was.Order)
            {
                if (Vector3.Distance(was.Rest[control.Id], now.Rest[control.Id]) > positionTolerance)
                {
                    differences.Add($"{who}: rest of '{control.Id}' moved from {was.Rest[control.Id]} to {now.Rest[control.Id]}");
                }
            }

            foreach (var (id, measure) in was.Measures)
            {
                if (MathF.Abs(measure - now.Measures[id]) > positionTolerance)
                {
                    differences.Add($"{who}: measure '{id}' changed from {measure:0.#####} to {now.Measures[id]:0.#####}");
                }
            }

            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var instants = new SortedSet<float> { 0, clip.Duration * .25f, clip.Duration * .5f, clip.Duration * .75f, clip.Duration };
                foreach (var marker in clip.Markers)
                {
                    instants.Add(marker.Time);
                }

                foreach (var seconds in instants)
                {
                    var a = PoseEvaluator.Sample(was, clip, seconds);
                    var b = PoseEvaluator.Sample(now, converted[i], seconds);
                    var where = $"{who} / clip '{clip.Id}' at {seconds:0.###} s";
                    var insetJoints = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var chain in was.Base.IkChains)
                    {
                        var x = a.Chains.FirstOrDefault(c => c.Chain == chain.Id);
                        var y = b.Chains.FirstOrDefault(c => c.Chain == chain.Id);
                        if (x is { Residual: > 0 } && y is not null && Vector3.Distance(a.Points[chain.End], b.Points[chain.End]) <= positionTolerance
                            && Vector3.Distance(a.Points[chain.Joint], b.Points[chain.Joint]) is > 0 and <= insetTolerance)
                        {
                            insetJoints.Add(chain.Joint);
                        }
                    }

                    foreach (var (id, point) in a.Points)
                    {
                        var distance = Vector3.Distance(point, b.Points[id]);
                        if (insetJoints.Contains(id) && distance > positionTolerance)
                        {
                            insets++;
                            insetLargest = MathF.Max(insetLargest, distance);
                        }
                        else
                        {
                            largest = MathF.Max(largest, distance);
                            if (distance > positionTolerance)
                            {
                                differences.Add($"{where}: '{id}' moved from {point} to {b.Points[id]} ({distance:0.######})");
                            }
                        }

                        if (MathF.Abs(a.Angles[id] - b.Angles[id]) > 1e-4f)
                        {
                            moved.Add(id);
                        }
                    }

                    for (var k = 0; k < a.Chains.Count; k++)
                    {
                        var x = a.Chains[k];
                        var y = b.Chains.FirstOrDefault(c => c.Chain == x.Chain);
                        if (y is null)
                        {
                            differences.Add($"{where}: constraint '{x.Chain}' was not solved");
                            continue;
                        }

                        residualShift = MathF.Max(residualShift, MathF.Abs(x.Residual - y.Residual));
                        if (MathF.Abs(x.Residual - y.Residual) > residualTolerance || x.Reached != y.Reached)
                        {
                            differences.Add($"{where}: constraint '{x.Chain}' residual {x.Residual:0.######} reached {x.Reached} -> residual {y.Residual:0.######} reached {y.Reached}");
                        }
                    }

                    if (a.Contacts.Count != b.Contacts.Count)
                    {
                        differences.Add($"{where}: {a.Contacts.Count} contact(s) became {b.Contacts.Count}");
                    }
                    else
                    {
                        for (var k = 0; k < a.Contacts.Count; k++)
                        {
                            if (a.Contacts[k].Chain != b.Contacts[k].Chain || Vector3.Distance(a.Contacts[k].Target, b.Contacts[k].Target) > positionTolerance || MathF.Abs(a.Contacts[k].Residual - b.Contacts[k].Residual) > residualTolerance)
                            {
                                differences.Add($"{where}: contact '{a.Contacts[k].Chain}' changed");
                            }
                        }
                    }

                    var actorA = new ActorPose(a, Vector2.Zero, 1);
                    var actorB = new ActorPose(b, Vector2.Zero, 1);
                    foreach (var socket in was.Base.Sockets)
                    {
                        var fa = actorA.Socket(socket);
                        var fb = actorB.Socket(socket);
                        if (Vector3.Distance(fa.Origin, fb.Origin) > positionTolerance || Vector3.Distance(fa.XAxis, fb.XAxis) > positionTolerance || Vector3.Distance(fa.YAxis, fb.YAxis) > positionTolerance || Vector3.Distance(fa.ZAxis, fb.ZAxis) > positionTolerance)
                        {
                            // A socket pointing toward an inset joint turns by the joint's lateral inset over the segment (a few thousandths of a radian).
                            if ((socket.Toward is { } toward && insetJoints.Contains(toward) || insetJoints.Contains(socket.Control))
                                && Vector3.Distance(fa.Origin, fb.Origin) <= positionTolerance && Vector3.Distance(fa.XAxis, fb.XAxis) <= 4 * insetTolerance && Vector3.Distance(fa.YAxis, fb.YAxis) <= 4 * insetTolerance && Vector3.Distance(fa.ZAxis, fb.ZAxis) <= 4 * insetTolerance)
                            {
                                insets++;
                                continue;
                            }

                            differences.Add($"{where}: socket '{socket.Id}' frame moved (origin {fa.Origin} -> {fb.Origin}, x {fa.XAxis} -> {fb.XAxis})");
                        }
                    }

                    foreach (var (id, word) in a.Expressions)
                    {
                        if (!b.Expressions.TryGetValue(id, out var other) || other != word)
                        {
                            differences.Add($"{where}: expression of '{id}' '{word}' became '{(b.Expressions.TryGetValue(id, out var w) ? w : "(none)")}'");
                        }
                    }

                    foreach (var (id, instance) in a.Attachments)
                    {
                        if (!b.Attachments.TryGetValue(id, out var other))
                        {
                            differences.Add($"{where}: attachment '{id}' was not evaluated");
                            continue;
                        }

                        foreach (var (control, point) in instance.Pose.Points)
                        {
                            if (Vector3.Distance(instance.World(control), other.World(control)) > positionTolerance)
                            {
                                differences.Add($"{where}: attachment '{id}' control '{control}' moved from {instance.World(control)} to {other.World(control)}");
                            }
                        }
                    }

                    var partsA = was.Parts.Where(p => !p.Hidden).ToList();
                    var partsB = now.Parts.Where(p => !p.Hidden).ToList();
                    for (var k = 0; k < partsA.Count; k++)
                    {
                        var pa = Silhouette(PartContours2D.Contour(partsA[k], was, a));
                        var pb = Silhouette(PartContours2D.Contour(partsB[k], now, b));
                        if (Vector3.Distance(pa.Min, pb.Min) > silhouetteTolerance || Vector3.Distance(pa.Max, pb.Max) > silhouetteTolerance || Vector3.Distance(pa.Centroid, pb.Centroid) > silhouetteTolerance || MathF.Abs(pa.Area - pb.Area) > silhouetteTolerance)
                        {
                            var part = partsA[k];
                            if (insetJoints.Overlaps(Attached(part))
                                && Vector3.Distance(pa.Min, pb.Min) <= insetTolerance && Vector3.Distance(pa.Max, pb.Max) <= insetTolerance && Vector3.Distance(pa.Centroid, pb.Centroid) <= insetTolerance && MathF.Abs(pa.Area - pb.Area) <= insetTolerance)
                            {
                                insets++;
                                continue;
                            }

                            differences.Add($"{where}: silhouette of '{part.Id}' moved (centroid {pa.Centroid} -> {pb.Centroid}, area {pa.Area:0.####} -> {pb.Area:0.####})");
                        }
                    }
                }
            }
        }

        report?.Add($"verified {clips.Count} clip(s) on the base and {variants.Count} variant(s): largest position movement {largest:0.#######}, largest residual shift {residualShift:0.#######}{(insets > 0 ? $"; the point adapter's full-reach inset accounted for {insets} joint/silhouette sample(s) moving by up to {insetLargest:0.######} with every end in place (intentional: the exact solver lays a full-reach limb straight)" : "")}; frame angles moved on {(moved.Count == 0 ? "no control" : string.Join(", ", moved.Select(m => "'" + m + "'")))} (the solved bones now point along their segments: the intentional difference of plan rule 7)");
        return differences;
    }

    /// <summary>Every control a part hangs on: its endpoints, bone frame and bound points.</summary>
    private static IEnumerable<string> Attached(PuppetPart part)
    {
        yield return part.A;
        if (part.B is { } b)
        {
            yield return b;
        }

        if (part.Frame is { } frame)
        {
            yield return frame;
        }

        foreach (var binding in part.Bindings ?? [])
        {
            yield return binding.Control;
            if (binding.Toward is { } toward)
            {
                yield return toward;
            }
        }
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

        return contour.Count == 0 ? (Vector3.Zero, Vector3.Zero, Vector3.Zero, 0) : (min, max, sum / contour.Count, (float)Math.Abs(area / 2));
    }

    private static string Write(string path, string json, string kind, string id, List<FileResult> files, Action<string> parse)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path);
            if (existing == json)
            {
                files.Add(new(kind, id, "unchanged"));
                return "unchanged";
            }

            try
            {
                parse(existing);
                files.Add(new(kind, id, "kept (edited after an earlier run; not overwritten)"));
                return "kept";
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            {
                files.Add(new(kind, id, $"replaced an unreadable earlier output: {ex.Message}"));
            }
        }

        AuthoredAsset.Write(path, json);
        files.Add(new(kind, id, "written"));
        return "written";
    }

    private static int? FileVersion(string json)
    {
        try
        {
            return JsonDocument.Parse(json).RootElement.TryGetProperty("version", out var value) && value.TryGetInt32(out var version) ? version : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
