using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;
using System.Text.Json;

namespace App2d.CharacterStudio.CharacterSeeds;

/// <summary>
/// Extracts a rig module from a model (character-system migration, P10b): the subtree beneath an anchor control becomes an
/// animated library part through the P7 instancing mechanism, carrying its controls (the anchor as the module's root at the
/// origin, which is its connection frame), chains, pieces, groups, measures, parameters (the module's dimension inputs) and
/// every clip's tracks on those targets as the module's own clips, each bound as an expression word. A wearer addresses the
/// module's targets as <c>instance/target</c>, so two instances of one module never collide. Everything outside the subtree
/// stays with the wearer and is reported: tracks on other controls, contacts on other chains, root travel (unless the plan
/// keeps it for a lower body), face selections, slot timelines and sockets. A piece or measure that reaches one control
/// outside the subtree may name a stand-in point for it (a static point at that control's rest offset); any other straddling
/// reference is refused by name. Conversion is transactional on a scratch tree with a report and verification against the
/// source (plan rules 3, 5, 6, 7): every converted control at every clip instant lands where the source put it relative to
/// the anchor, every piece's silhouette matches, and a rerun leaves an existing or edited output alone.
/// </summary>
public static class ModuleConversion
{
    public sealed record Result(string PartId, string Source, string? Output, string Status, IReadOnlyList<string> Report, IReadOnlyList<string> Differences)
    {
        public bool Converted => Output is not null && Differences.Count == 0;
    }

    /// <summary>What to extract: the anchor control, what to leave out, the stand-ins, the clips and their words.</summary>
    public sealed record Plan(string PartId, string Name, string Anchor)
    {
        public string Usage { get; init; } = "module";
        public string Fitting { get; init; } = PartFittings.Stretch;
        /// <summary>Controls whose subtrees stay with the wearer although they hang beneath the anchor.</summary>
        public List<string> Exclude { get; init; } = [];
        /// <summary>An outside control a carried piece or measure reaches → the id of a static stand-in point inside the module at its rest offset.</summary>
        public Dictionary<string, string> StandIns { get; init; } = [];
        /// <summary>Pieces left with the wearer although they touch module controls.</summary>
        public List<string> Leave { get; init; } = [];
        /// <summary>The clips to carry (null: every clip of the model).</summary>
        public List<string>? Clips { get; init; }
        /// <summary>Clip id → expression word; a clip not listed binds as its id without the model's prefix.</summary>
        public Dictionary<string, string> Words { get; init; } = [];
        public string? Default { get; init; }
        /// <summary>Whether the module carries the clips' root travel (a lower body that owns the gait).</summary>
        public bool KeepTravel { get; init; }
        /// <summary>Named points the module publishes beyond the origin, each riding on one of its controls.</summary>
        public List<(string Id, string Control, float X, float Y)> Points { get; init; } = [];
    }

    /// <summary>Converts the module beneath the plan's anchor of <paramref name="modelId"/> under <paramref name="root"/> into <c>&lt;output&gt;/parts/&lt;id&gt;.json</c> with a report. Nothing under <paramref name="root"/> is written.</summary>
    public static Result Convert(string root, string modelId, Plan plan, string output, float positionTolerance = 1e-4f, float silhouetteTolerance = 1e-3f)
    {
        var catalog = AuthoredCatalog.Load(root);
        var model = catalog.Models.GetValueOrDefault(modelId) ?? throw new InvalidDataException($"No model '{modelId}' under '{root}'.");
        var clips = catalog.Animations.Values.Where(c => c.Model == modelId && (plan.Clips is null || plan.Clips.Contains(c.Id))).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var source = catalog.PathOf(modelId);
        Directory.CreateDirectory(Path.Combine(output, "parts"));
        var target = Path.Combine(output, "parts", plan.PartId + ".json");
        Result result;
        try
        {
            var (asset, report, _) = Build(model, clips, plan);
            var differences = Verify(catalog.Resolve(modelId), clips, plan, asset, positionTolerance, silhouetteTolerance);
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
                        Part.FromJson(existing);
                        result = new(plan.PartId, source, target, "kept: the output was edited after an earlier run (delete it to reconvert)", lines, []);
                        goto write;
                    }
                    catch (Exception ex) when (ex is InvalidDataException or JsonException)
                    {
                        lines.Add($"replacing an unreadable earlier output: {ex.Message}");
                    }
                }
                asset.Save(target);
                result = new(plan.PartId, source, target, $"converted: model '{modelId}' module beneath '{plan.Anchor}' -> part '{plan.PartId}' with {asset.Controls.Count} control(s) and {asset.Clips.Count} clip(s)", lines, []);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException)
        {
            result = new(plan.PartId, source, null, "failed", [], [ex.Message]);
        }
    write:
        File.WriteAllText(Path.Combine(output, $"module-conversion-{plan.PartId}.md"), Report(root, modelId, plan, result));
        return result;
    }

    private static string Report(string root, string modelId, Plan plan, Result result)
    {
        var lines = new List<string>
        {
            $"# Module conversion: {modelId} beneath '{plan.Anchor}' -> part '{plan.PartId}'",
            "",
            $"Source root: {root}",
            $"Status: {result.Status}",
            "",
            "## Report",
            "",
        };
        lines.AddRange(result.Report.Select(r => "- " + r));
        lines.Add("");
        lines.Add("## Differences");
        lines.Add("");
        lines.AddRange(result.Differences.Count == 0 ? ["- none"] : result.Differences.Select(d => "- " + d));
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// The module beneath the plan's anchor, in memory, with the report lines and the target mapping (source target →
    /// the module target a wearer addresses as <c>instance/target</c>). Nothing is written.
    /// </summary>
    public static (Part Asset, IReadOnlyList<string> Report, IReadOnlyDictionary<string, string> Mapping) Build(CharacterModel model, IReadOnlyList<MotionClip> clips, Plan plan)
    {
        var report = new List<string>();
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var anchor = controls.GetValueOrDefault(plan.Anchor) ?? throw new InvalidDataException($"Model '{model.Id}' has no control '{plan.Anchor}'.");
        bool Beneath(string id, string ancestor)
        {
            for (var current = controls[id].Parent; current is not null; current = controls[current].Parent)
            {
                if (current == ancestor)
                {
                    return true;
                }
            }

            return false;
        }
        foreach (var excluded in plan.Exclude)
        {
            if (!controls.ContainsKey(excluded) || !Beneath(excluded, plan.Anchor))
            {
                throw new InvalidDataException($"Plan excludes '{excluded}', which does not hang beneath '{plan.Anchor}'.");
            }
        }
        var converted = model.Controls.Where(c => c.Id == plan.Anchor || Beneath(c.Id, plan.Anchor) && !plan.Exclude.Any(e => c.Id == e || Beneath(c.Id, e))).ToList();
        if (converted.Count < 2)
        {
            throw new InvalidDataException($"Model '{model.Id}': nothing hangs beneath '{plan.Anchor}' after the exclusions.");
        }

        var resolved = ResolvedModel.Rig(model);
        var origin = resolved.Rest[plan.Anchor];
        if (resolved.RestTransforms[plan.Anchor].M11 != 1 || resolved.RestTransforms[plan.Anchor].M12 != 0)
        {
            throw new InvalidDataException($"Model '{model.Id}': '{plan.Anchor}' is not axis-aligned at rest; the conversion re-expresses positions by translation only.");
        }

        var asset = new Part
        {
            Id = plan.PartId,
            Name = plan.Name,
            Usage = plan.Usage,
            Payload = PartPayloads.Animated,
            Fitting = plan.Fitting,
            Ink = model.Ink,
            LineWidth = model.LineWidth,
            Connection = plan.Anchor,
            Materials = model.Materials.ToDictionary(m => m.Key, m => m.Value, StringComparer.Ordinal),
        };
        var ids = converted.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var c in converted)
        {
            ModelControl part;
            if (c.Id == plan.Anchor)
            {
                // The anchor is the module's root at the origin: its placement is the wearer's socket, its rotation and children its own.
                part = c with { Parent = null, Node = RigNodeKinds.Point, Link = null, Rest = new(0, 0, 0), RestAngle = 0 };
                report.Add($"control '{c.Id}': the connection, a point root at the origin");
            }
            else if (c.Node == RigNodeKinds.Bone)
            {
                part = c with { };
                report.Add($"control '{c.Id}': bone node kept (parent-local setup)");
            }
            else
            {
                part = c with { Rest = new(c.Rest.X - origin.X, c.Rest.Y - origin.Y, c.Rest.Z - origin.Z) };
                report.Add($"control '{c.Id}': point control at ({part.Rest.X:0.####}, {part.Rest.Y:0.####})");
            }

            mapping[c.Id] = c.Id;
            asset.Controls.Add(part);
        }
        // Stand-ins: static points for the outside controls a carried piece or measure reaches.
        var standIns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (outside, standIn) in plan.StandIns)
        {
            if (!controls.ContainsKey(outside) || ids.Contains(outside))
            {
                throw new InvalidDataException($"Plan stand-in '{standIn}' names '{outside}', which is not a control outside the module.");
            }

            if (ids.Contains(standIn) || !standIns.Add(standIn))
            {
                throw new InvalidDataException($"Plan stand-in '{standIn}' collides with a module control.");
            }

            var rest = resolved.Rest[outside];
            asset.Controls.Add(new() { Id = standIn, Rest = new(rest.X - origin.X, rest.Y - origin.Y, rest.Z - origin.Z) });
            report.Add($"stand-in '{standIn}': a static point at the rest offset of the wearer's '{outside}' ({rest.X - origin.X:0.####}, {rest.Y - origin.Y:0.####}); it does not follow that control");
        }
        string Map(string id) => plan.StandIns.TryGetValue(id, out var standIn) ? standIn : id;
        bool Inside(string id) => ids.Contains(id) || standIns.Contains(id);
        // Measures along module controls (through stand-ins) are the module's; a control scaled by a dropped measure is refused.
        foreach (var measure in model.Measures)
        {
            var path = measure.Path.ConvertAll(Map);
            if (path.All(Inside))
            {
                asset.Measures.Add(new() { Id = measure.Id, Path = path });
                report.Add($"measure '{measure.Id}' kept along {string.Join(" > ", path)}");
            }
            else
            {
                report.Add($"measure '{measure.Id}' left with the wearer (its path reaches {string.Join(", ", path.Where(p => !Inside(p)))})");
            }
        }
        var measures = asset.Measures.Select(m => m.Id).Append(CharacterModel.Unit).ToHashSet(StringComparer.Ordinal);
        foreach (var c in asset.Controls.Where(c => !measures.Contains(c.Scale)))
        {
            throw new InvalidDataException($"Control '{c.Id}' scales by measure '{c.Scale}', whose path reaches outside the module; add a stand-in for it or convert elsewhere.");
        }
        foreach (var constraint in model.Constraints)
        {
            if (constraint is not ModelChain chain || !ids.Contains(chain.Root))
            {
                continue;
            }

            if (!ids.Contains(chain.Joint) || !ids.Contains(chain.End))
            {
                throw new InvalidDataException($"Chain '{chain.Id}' is rooted inside the module but reaches outside it; exclude the root or convert elsewhere.");
            }

            if (chain.Frame != CharacterModel.Locomotion && !ids.Contains(chain.Frame))
            {
                throw new InvalidDataException($"Chain '{chain.Id}' is framed on '{chain.Frame}', outside the module (report, do not approximate).");
            }

            if (!measures.Contains(chain.Scale))
            {
                throw new InvalidDataException($"Chain '{chain.Id}' scales by measure '{chain.Scale}', which the module does not carry.");
            }

            asset.Constraints.Add(chain with { });
            mapping[chain.Id] = chain.Id;
            report.Add($"chain '{chain.Id}' kept ({chain.Frame} frame, scale {chain.Scale})");
        }
        var chains = asset.Constraints.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        // Pieces: everything hanging on module controls (through stand-ins); a piece the plan leaves or that touches nothing stays.
        foreach (var piece in model.Parts)
        {
            var refs = new[] { piece.A, piece.B, piece.Frame }.Concat(piece.Bindings?.SelectMany(b => new[] { b.Control, b.Toward }) ?? []).OfType<string>().ToArray();
            if (plan.Leave.Contains(piece.Id))
            {
                report.Add($"piece '{piece.Id}' left with the wearer by the plan");
                continue;
            }

            if (refs.All(r => !ids.Contains(r)))
            {
                continue;
            }

            var mapped = refs.Select(Map).ToArray();
            if (mapped.Any(r => !Inside(r)))
            {
                throw new InvalidDataException($"Piece '{piece.Id}' references both module controls and others ({string.Join(", ", mapped.Where(r => !Inside(r)))}); leave it with the wearer, add a stand-in or split it before converting.");
            }

            var carried = piece with
            {
                A = Map(piece.A),
                B = piece.B is null ? null : Map(piece.B),
                Frame = piece.Frame is null ? null : Map(piece.Frame),
                Bindings = piece.Bindings?.Select(b => b with { Control = Map(b.Control), Toward = b.Toward is null ? null : Map(b.Toward) }).ToList(),
            };
            asset.Geometry.Add(carried);
            mapping[piece.Id] = piece.Id;
            report.Add($"piece '{piece.Id}' -> module piece{(mapped.Any(standIns.Contains) ? " (on a stand-in)" : "")}");
        }
        var pieces = asset.Geometry.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var group in model.Groups)
        {
            if (group.Targets.All(t => ids.Contains(t) || chains.Contains(t) || pieces.Contains(t)))
            {
                asset.Groups.Add(new() { Id = group.Id, Targets = [.. group.Targets] });
                report.Add($"group '{group.Id}' kept ({group.Targets.Count} targets)");
            }
        }
        // Parameters whose bindings land inside are the module's inputs (bindings on the anchor's own placement belong to the wearer).
        foreach (var parameter in model.Parameters)
        {
            var kept = new List<ParameterBinding>();
            var dropped = new List<string>();
            foreach (var binding in parameter.Bindings)
            {
                var target = binding.Target;
                var inside = target.Control is { } control ? ids.Contains(control) && !(control == plan.Anchor && target.Property.StartsWith("local.", StringComparison.Ordinal))
                    : target.Part is { } part && pieces.Contains(part);
                var reference = binding.Reference is null || (binding.Reference.Control is { } rc ? ids.Contains(rc) : binding.Reference.Part is { } rp && pieces.Contains(rp));
                if (inside && reference)
                {
                    kept.Add(binding with { Target = binding.Target with { }, Reference = binding.Reference is null ? null : binding.Reference with { } });
                }
                else
                {
                    dropped.Add(target.ToString());
                }
            }

            if (kept.Count == 0)
            {
                report.Add($"parameter '{parameter.Id}' left with the wearer (no binding lands inside the module)");
                continue;
            }

            asset.Parameters.Add(parameter with { Bindings = kept });
            report.Add($"input '{parameter.Id}': {kept.Count} binding(s) kept{(dropped.Count > 0 ? $", {dropped.Count} left with the wearer ({string.Join(", ", dropped)})" : "")}");
        }
        var inputs = asset.Parameters.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        if (inputs.Count > 0)
        {
            foreach (var preset in model.Presets)
            {
                var values = preset.Values.Where(v => inputs.Contains(v.Key)).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
                if (values.Count > 0)
                {
                    asset.Presets.Add(new() { Id = preset.Id, Name = preset.Name, Values = values });
                }
            }
            report.Add($"presets kept for the inputs: {string.Join(", ", asset.Presets.Select(p => p.Id))}");
        }
        foreach (var (id, control, x, y) in plan.Points)
        {
            if (!ids.Contains(control))
            {
                throw new InvalidDataException($"Plan point '{id}' rides on '{control}', which the module does not carry.");
            }

            asset.Locations.Add(new() { Id = id, Control = control, X = x, Y = y });
            report.Add($"point '{id}' on '{control}' at ({x:0.###}, {y:0.###})");
        }
        // The clips: each source clip's tracks on module targets, contacts on module chains, travel only for a lower body.
        foreach (var clip in clips)
        {
            var copy = MotionClip.FromJson(clip.ToJson());
            copy.Model = plan.PartId;
            copy.StructureRevision = 1;
            copy.Source = null;
            copy.Reference = clip.Reference.Where(r => measures.Contains(r.Key)).ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);
            var tracks = new List<ClipTrack>();
            var outside = new List<string>();
            foreach (var track in copy.Tracks)
            {
                var carried = track.Kind switch
                {
                    MotionClip.OrientKind => false,
                    MotionClip.TargetKind => chains.Contains(track.Target),
                    MotionClip.FitKind or MotionClip.ShowKind or MotionClip.TintKind or MotionClip.AlphaKind => pieces.Contains(track.Target),
                    _ => ids.Contains(track.Target),
                };
                if (!carried)
                {
                    outside.Add($"{track.Kind} {track.Target}");
                    continue;
                }

                if (track.Scale is { } scale && !measures.Contains(scale))
                {
                    throw new InvalidDataException($"Clip '{clip.Id}' {track.Kind} track '{track.Target}' scales by measure '{scale}', which the module does not carry (report, do not approximate).");
                }

                tracks.Add(track);
            }
            copy.Tracks = tracks;
            var contacts = copy.Contacts.Where(c => chains.Contains(c.Chain)).ToList();
            var droppedContacts = copy.Contacts.Count - contacts.Count;
            copy.Contacts = contacts;
            var notes = new List<string>();
            if (outside.Count > 0)
            {
                notes.Add($"{outside.Count} track(s) left with the wearer");
            }

            if (droppedContacts > 0)
            {
                notes.Add($"{droppedContacts} contact(s) left with the wearer");
            }

            if (!plan.KeepTravel)
            {
                if (copy.Travel.Keys.Count > 0)
                {
                    notes.Add("root travel left with the wearer");
                }

                copy.Travel = new();
            }
            else if (copy.Travel.Keys.Count > 0)
            {
                if (!measures.Contains(copy.Travel.Scale))
                {
                    throw new InvalidDataException($"Clip '{clip.Id}' travels by measure '{copy.Travel.Scale}', which the module does not carry (report, do not approximate).");
                }

                notes.Add("root travel carried (the module may own the wearer's motion)");
            }

            if (copy.Plays.Count > 0)
            {
                copy.Plays = [];
                notes.Add("face selections left with the wearer");
            }

            if (copy.Attachments.Count > 0 || copy.Colors.Count > 0 || copy.DrawOrder.Count > 0)
            {
                copy.Attachments = [];
                copy.Colors = [];
                copy.DrawOrder = [];
                notes.Add("slot timelines left with the wearer");
            }

            if (tracks.Count == 0 && contacts.Count == 0)
            {
                report.Add($"clip '{clip.Id}' skipped: it keys nothing inside the module");
                continue;
            }

            asset.Clips.Add(copy);
            report.Add($"clip '{clip.Id}' -> module clip ({tracks.Count} track(s), {contacts.Count} contact(s), {clip.Duration:0.##} s{(clip.Loop ? ", loops" : "")}{(notes.Count > 0 ? "; " + string.Join(", ", notes) : "")})");
        }
        asset.Layers.Add(new() { Id = "motion" });
        foreach (var clip in asset.Clips)
        {
            var word = plan.Words.GetValueOrDefault(clip.Id) ?? (clip.Id.StartsWith(model.Id + "-", StringComparison.Ordinal) ? clip.Id[(model.Id.Length + 1)..] : clip.Id);
            if (asset.Expressions.TryGetValue(word, out var assetExpression))
            {
                throw new InvalidDataException($"Clips '{assetExpression.Clip}' and '{clip.Id}' both bind the word '{word}'; name one in the plan.");
            }

            asset.Expressions[word] = new() { Clip = clip.Id };
            report.Add($"word '{word}' plays '{clip.Id}'");
        }
        asset.Expression = plan.Default;
        asset.Exposed = [.. asset.Controls.Where(c => !standIns.Contains(c.Id)).Select(c => c.Id).Concat(asset.Geometry.Select(p => p.Id)).Distinct(StringComparer.Ordinal)];
        report.Add($"exposed: every control and piece ({asset.Exposed.Count}); targets a wearer keys as <instance>/<target>: {string.Join(", ", mapping.Keys)}");
        asset.Validate();
        return (asset, report, mapping);
    }

    /// <summary>A minimal wearer carrying the module on a socket at the anchor's rest, for verification and previews.</summary>
    public static CharacterModel Stand(ResolvedModel source, Plan plan, Part asset, string instance = "module")
    {
        var stand = new CharacterModel { Id = plan.PartId + "-stand", Name = plan.Name + " stand" };
        stand.Controls.Add(new() { Id = "mount", Rest = PuppetPoint.From(source.Rest[plan.Anchor]) });
        stand.Parts.Add(new() { Id = "mount-mark", A = "mount", Geometry = Core.Shapes.Ellipse2D.Builder.Create(new(.01f, .01f)), Material = new() { Fill = "#000000" }, Hidden = true });
        stand.Sockets.Add(new() { Id = "module", Control = "mount", Frame = CharacterModel.Locomotion, Usage = asset.Usage });
        stand.Attachments.Add(new() { Id = instance, Asset = asset.Id, Socket = "module" });
        stand.Validate();
        return stand;
    }

    /// <summary>
    /// Every carried clip at five instants on the source model (in place) against the module on its stand: each converted
    /// control's position relative to the anchor in the anchor's parent frame, and each piece's silhouette (translated to
    /// the anchor; pieces on stand-ins only at rest, since a stand-in does not follow the wearer's control).
    /// </summary>
    public static List<string> Verify(ResolvedModel source, IReadOnlyList<MotionClip> clips, Plan plan, Part asset, float positionTolerance, float silhouetteTolerance)
    {
        var differences = new List<string>();
        var probe = Part.FromJson(asset.ToJson());
        probe.Expressions.Clear();
        foreach (var layer in probe.Layers.Skip(1))
        {
            layer.Clip = null;
        }

        foreach (var clip in asset.Clips)
        {
            probe.Expressions["verify-" + clip.Id] = new() { Clip = clip.Id };
        }
        probe.Expression = null;
        probe.Validate();
        var stand = ResolvedModel.From(Stand(source, plan, probe), null, id => id == probe.Id ? probe : null);
        var parent = source.Controls[plan.Anchor].Parent;
        var standIns = plan.StandIns.Values.ToHashSet(StringComparer.Ordinal);
        foreach (var clip in asset.Clips)
        {
            var original = clips.First(c => c.Id == clip.Id);
            foreach (var time in new[] { 0f, clip.Duration * .25f, clip.Duration * .5f, clip.Duration * .75f, clip.Duration })
            {
                var expected = PoseEvaluator.Sample(source, original, time, repeat: true, new() { InPlace = true });
                var evaluated = PoseEvaluator.Sample(stand, null, time, input: new() { InPlace = true, Plays = [new("module", "verify-" + clip.Id, time)] });
                var instance = evaluated.Attachments["module"];
                var turn = parent is null ? 0 : expected.Angles[parent] - source.Nodes[parent].RestAngle;
                var anchorWorld = expected.Points[plan.Anchor];
                var moduleAnchor = instance.World(plan.Anchor);
                foreach (var c in asset.Controls)
                {
                    if (standIns.Contains(c.Id))
                    {
                        continue;
                    }

                    var relative = Rotation2D.ApplyXY(expected.Points[c.Id] - anchorWorld, -turn);
                    var converted = instance.World(c.Id) - moduleAnchor;
                    var distance = Vector3.Distance(relative, converted);
                    if (distance > positionTolerance)
                    {
                        differences.Add($"clip '{clip.Id}' at {time:0.###} s: control '{c.Id}' expected {relative} from the anchor, converted {converted} ({distance:0.######} apart)");
                    }
                }
                if (MathF.Abs(turn) > 1e-6f)
                {
                    continue;
                }

                foreach (var piece in asset.Geometry)
                {
                    var onStandIn = new[] { piece.A, piece.B, piece.Frame }.OfType<string>().Any(standIns.Contains);
                    if (onStandIn && time > 0)
                    {
                        continue;
                    }

                    var (Min, Max, Centroid, Area) = Silhouette(PartContours2D.Contour(source.Parts.First(p => p.Id == piece.Id), source, expected), anchorWorld);
                    var after = Silhouette(PartContours2D.Contour(instance.Pose.Fitted(piece), instance.World, instance.Frame, instance.Rest, instance.RestFrame), moduleAnchor);
                    var gap = MathF.Max(Vector3.Distance(Min, after.Min), MathF.Max(Vector3.Distance(Max, after.Max), MathF.Max(Vector3.Distance(Centroid, after.Centroid), MathF.Abs(Area - after.Area))));
                    if (gap > silhouetteTolerance)
                    {
                        differences.Add($"clip '{clip.Id}' at {time:0.###} s: piece '{piece.Id}' silhouette moved by {gap:0.######}");
                    }
                }
            }
        }
        return differences;
    }

    private static (Vector3 Min, Vector3 Max, Vector3 Centroid, float Area) Silhouette(List<Vector3> contour, Vector3 anchor)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        var sum = Vector3.Zero;
        double area = 0;
        for (var i = 0; i < contour.Count; i++)
        {
            var p = contour[i] - anchor;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            sum += p;
            var q = contour[(i + 1) % contour.Count] - anchor;
            area += (double)p.X * q.Y - (double)q.X * p.Y;
        }

        return (min, max, sum / contour.Count, (float)Math.Abs(area / 2));
    }
}
