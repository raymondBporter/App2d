using App2d.Core.Mathematics;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// P8c: converts an entity's legacy collision fields into explicit attached colliders at entity version 3. Hit-window
/// inline geometry and the guard become attack/guard colliders with exactly the ids resolution already gave them (the
/// placement is the same function, so nothing moves); the hurt layout's regions become hurt colliders: each region's rest
/// bounding box inflated by its pad, as a box bound to the region's anchor control (its first control whose parent lies
/// outside the region) in the screen frame, so the rest-pose region is reproduced exactly and from then on the box follows
/// its anchor instead of being re-derived from every control. Runs on a scratch output tree with a report; rerunnable.
/// </summary>
public static class ColliderConversion
{
    public sealed record Result(string EntityId, string Source, string? Output, string Status, IReadOnlyList<string> Report, IReadOnlyList<string> Differences)
    {
        public bool Converted => Output is not null && Differences.Count == 0;
    }

    /// <summary>Converts a copy of <paramref name="source"/> in memory. An entity with nothing legacy left is returned as an equal copy with a report saying so.</summary>
    public static (EntityAsset Entity, IReadOnlyList<string> Report) Convert(EntityAsset source, ResolvedModel model)
    {
        var entity = EntityAsset.FromJson(source.ToJson());
        var report = new List<string>();
        var changed = false;
        var windowIds = entity.Actions.SelectMany(a => a.Hits).Where(h => h.Collider is null).Select(h => h.Id).ToLookup(id => id, StringComparer.Ordinal);
        var blank = new HitWindow();
        foreach (var action in entity.Actions)
        {
            foreach (var hit in action.Hits)
            {
                if (hit.Collider is not null)
                {
                    continue;
                }

                var id = windowIds[hit.Id].Count() == 1 ? hit.Id : $"{action.Id}-{hit.Id}";
                if (entity.Colliders.Any(c => c.Id == id))
                {
                    throw new InvalidDataException($"Entity '{entity.Id}' action '{action.Id}' hit '{hit.Id}': its collider would be '{id}', which the entity already declares.");
                }

                entity.Colliders.Add(new()
                {
                    Id = id,
                    Use = ColliderUses.Attack,
                    Shape = hit.Shape,
                    Socket = hit.Socket,
                    Prop = hit.Prop,
                    Point = hit.Point,
                    Along = hit.Along,
                    OffsetX = hit.OffsetX,
                    OffsetY = hit.OffsetY,
                });
                report.Add($"action '{action.Id}' window '{hit.Id}' -> attack collider '{id}' ({Describe(hit)}); the window keeps its interval, damage and sound");
                hit.Collider = id;
                hit.Socket = blank.Socket;
                hit.Prop = blank.Prop;
                hit.Point = blank.Point;
                hit.Along = blank.Along;
                hit.OffsetX = blank.OffsetX;
                hit.OffsetY = blank.OffsetY;
                hit.Shape = blank.Shape;
                changed = true;
            }
        }
        if (entity.Guard is { Collider: null } guard)
        {
            if (entity.Colliders.Any(c => c.Id == ColliderUses.Guard))
            {
                throw new InvalidDataException($"Entity '{entity.Id}' guard: its collider would be '{ColliderUses.Guard}', which the entity already declares.");
            }

            entity.Colliders.Add(new() { Id = ColliderUses.Guard, Use = ColliderUses.Guard, Shape = guard.Shape, Prop = guard.Prop });
            report.Add($"guard on prop '{guard.Prop}' -> guard collider '{ColliderUses.Guard}' (prop {guard.Prop} {PropAsset.TipPoint}, as the legacy guard window was anchored)");
            var fresh = new GuardDef();
            guard.Collider = ColliderUses.Guard;
            guard.Prop = fresh.Prop;
            guard.Shape = fresh.Shape;
            changed = true;
        }
        if (entity.Hurt.Layout is { } layoutId)
        {
            var layout = model.Base.HurtLayouts.FirstOrDefault(l => l.Id == layoutId)
                ?? throw new InvalidDataException($"Entity '{entity.Id}' hurt.layout: '{model.Base.Id}' has no hurt layout '{layoutId}'.");
            var rest = PoseEvaluator.Rest(model);
            foreach (var region in layout.Regions)
            {
                var change = entity.Hurt.Regions.GetValueOrDefault(region.Id);
                if (change?.Disabled == true)
                {
                    report.Add($"hurt region '{region.Id}' is disabled on this entity: not converted");
                    continue;
                }

                if (entity.Colliders.Any(c => c.Id == region.Id))
                {
                    throw new InvalidDataException($"Entity '{entity.Id}' hurt region '{region.Id}': a collider of that id already exists.");
                }

                var pad = change?.Pad ?? region.Pad;
                var inRegion = region.Controls.ToHashSet(StringComparer.Ordinal);
                var anchor = region.Controls.First(c => model.Controls[c].Parent is not { } parent || !inRegion.Contains(parent));
                var anchorRest = rest.Points[anchor].XY;
                var bounds = Rect2D.FromPoints(region.Controls.Select(c => rest.Points[c].XY)).InflatedBy(pad);
                var definition = new ColliderDef { Id = region.Id, Use = ColliderUses.Hurt, Control = anchor, Frame = CharacterModel.Locomotion };
                string shape;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    definition.Shape = Rectangle2D.Builder.FromSize(new Vector2(MathF.Round(bounds.Width, 5), MathF.Round(bounds.Height, 5)));
                    var offset = bounds.Center - anchorRest;
                    definition.OffsetX = MathF.Round(offset.X, 5);
                    definition.OffsetY = MathF.Round(offset.Y, 5);
                    shape = $"box {bounds.Width:0.###} x {bounds.Height:0.###} at offset ({definition.OffsetX:0.###}, {definition.OffsetY:0.###})";
                }
                else
                {
                    definition.Shape = Capsule2D.Builder.Create(bounds.Min - anchorRest, bounds.Max - anchorRest, EntityCollision.DegenerateHurtRadius);
                    shape = $"hairline capsule from ({bounds.Min.X - anchorRest.X:0.###}, {bounds.Min.Y - anchorRest.Y:0.###}) to ({bounds.Max.X - anchorRest.X:0.###}, {bounds.Max.Y - anchorRest.Y:0.###})";
                }
                entity.Colliders.Add(definition);
                report.Add($"hurt region '{region.Id}' ({string.Join(", ", region.Controls)}; pad {pad:0.###}{(change?.Pad is not null ? " from this entity's override" : "")}) -> hurt collider '{region.Id}' bound to '{anchor}' in the locomotion frame: {shape}; the rest-pose region is reproduced exactly and the box now follows '{anchor}' instead of every control");
                changed = true;
            }
            entity.Hurt = new();
        }
        if (!changed)
        {
            report.Add("nothing to convert: the entity already declares its colliders");
            return (entity, report);
        }

        entity.Version = Math.Max(entity.Version, EntityAsset.ColliderVersion);
        entity.Validate();
        return (entity, report);
    }

    private static string Describe(HitWindow hit) => hit.Prop is not null ? $"prop {hit.Prop} {hit.Point}, along {hit.Along:0.###}"
        : hit.Socket is not null ? $"socket {hit.Socket}, along {hit.Along:0.###}"
        : $"actor offset ({hit.OffsetX:0.###}, {hit.OffsetY:0.###})";

    /// <summary>
    /// Converts <c>&lt;root&gt;/entities/&lt;id&gt;.json</c> into <c>&lt;output&gt;/entities/&lt;id&gt;.json</c> with a
    /// report, verifying that every attack and guard placement is unchanged at the windows' instants in both facings and that
    /// the hurt colliders reproduce the legacy regions at rest; the animated hurt differences are measured and reported as
    /// the evidence for that intentional change. Nothing under <paramref name="root"/> is written; an output that already
    /// holds this conversion is left as it is, and one edited since is kept.
    /// </summary>
    public static Result ConvertFile(string root, string entityId, string output)
    {
        var catalog = AuthoredCatalog.Load(root);
        var compiled = catalog.Entities.GetValueOrDefault(entityId) ?? throw new InvalidDataException($"No compiled entity '{entityId}' under '{root}'.");
        var source = catalog.PathOf(entityId);
        var (entity, report) = Convert(compiled.Asset, compiled.Model);
        var lines = new List<string>(report);
        var differences = new List<string>();
        var converted = ResolvedEntity.Compile(entity, catalog.Resolve, catalog.Animations.GetValueOrDefault, catalog.Props.GetValueOrDefault);
        Verify(compiled, converted, lines, differences);
        var target = Path.Combine(output, "entities", entityId + ".json");
        string status;
        if (differences.Count > 0)
        {
            status = "not written: verification failed";
            target = null!;
        }
        else
        {
            status = Write(target, entity.ToJson(), lines);
        }

        var result = new Result(entityId, source, differences.Count > 0 ? null : target, status, lines, differences);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, $"collider-conversion-{entityId}.md"), Report(root, result));
        return result;
    }

    internal static string Write(string target, string fresh, List<string> report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target))
        {
            var existing = File.ReadAllText(target);
            if (existing == fresh)
            {
                return "unchanged: the output already holds this conversion";
            }

            try
            {
                EntityAsset.FromJson(existing);
                return "kept: the output was edited after an earlier run (delete it to reconvert)";
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            {
                report.Add($"replacing an unreadable earlier output: {ex.Message}");
            }
        }
        File.WriteAllText(target, fresh);
        return "written";
    }

    /// <summary>Attack and guard placements must be identical at every window instant in both facings; hurt must match at rest, and its animated drift is measured.</summary>
    private static void Verify(ResolvedEntity before, ResolvedEntity after, List<string> report, List<string> differences)
    {
        const float tolerance = 1e-4f;
        if (before.Colliders.Select(c => c.Id).Except(after.Colliders.Select(c => c.Id)).Any())
        {
            differences.Add($"collider ids changed: {string.Join(", ", before.Colliders.Select(c => c.Id))} -> {string.Join(", ", after.Colliders.Select(c => c.Id))}");
        }

        static bool Same(EntityRegion a, EntityRegion b, float tolerance) =>
            Vector2.Distance(a.Bounds.Min, b.Bounds.Min) <= tolerance && Vector2.Distance(a.Bounds.Max, b.Bounds.Max) <= tolerance
            && Vector2.Distance(a.Pose.Translation, b.Pose.Translation) <= tolerance && Vector2.Distance(a.Pose.XAxis, b.Pose.XAxis) <= tolerance && a.Pose.IsMirrored == b.Pose.IsMirrored;
        var instants = new List<(MotionClip Clip, float Seconds, string What)>();
        foreach (var action in before.Actions.Values)
        {
            foreach (var hit in action.Hits)
            {
                instants.Add((action.Clip, hit.Start, $"{action.Id} {hit.Window.Id} start"));
                instants.Add((action.Clip, (hit.Start + hit.Finish) / 2, $"{action.Id} {hit.Window.Id} middle"));
                instants.Add((action.Clip, Math.Max(hit.Start, hit.Finish - 1e-3f), $"{action.Id} {hit.Window.Id} end"));
            }
        }
        foreach (var role in before.Roles.Values.Where(r => r.Clip.Loop).Take(3))
        {
            foreach (var fraction in new[] { 0f, .25f, .5f, .75f })
            {
                instants.Add((role.Clip, fraction * role.Clip.Duration, $"{role.Role} at {fraction:0.##}"));
            }
        }
        var checks = 0;
        foreach (var facing in new[] { 1, -1 })
        {
            foreach (var (clip, seconds, what) in instants)
            {
                var pose = new ActorPose(PoseEvaluator.Sample(before.Model, clip, seconds, repeat: clip.Loop, new() { InPlace = true }), new(2, 0), facing);
                foreach (var action in before.Actions.Values)
                {
                    foreach (var hit in action.Hits)
                    {
                        var legacy = EntityCollision.Attack(before, pose, hit);
                        var fresh = EntityCollision.Attack(after, pose, after.Actions[action.Id].Hits.First(h => h.Window.Id == hit.Window.Id));
                        checks++;
                        if (!Same(legacy, fresh, tolerance))
                        {
                            differences.Add($"attack '{hit.Window.Id}' of '{action.Id}' moved at {what}, facing {facing}: {legacy.Bounds} -> {fresh.Bounds}");
                        }
                    }
                }
                if (before.GuardCollider is { } guard)
                {
                    checks++;
                    if (!Same(guard.Region(pose), after.GuardCollider!.Region(pose), tolerance))
                    {
                        differences.Add($"guard moved at {what}, facing {facing}");
                    }
                }
            }
        }
        report.Add($"verified {checks} attack/guard placements unchanged at the windows' instants and three locomotion clips in both facings");
        if (before.Hurt.Count > 0)
        {
            var restPose = new ActorPose(PoseEvaluator.Rest(before.Model), Vector2.Zero, 1);
            var legacyRest = EntityCollision.Hurt(before, restPose);
            var freshRest = EntityCollision.Hurt(after, restPose);
            foreach (var region in legacyRest)
            {
                var fresh = freshRest.FirstOrDefault(r => r.Id == region.Id);
                if (fresh is null || Vector2.Distance(region.Bounds.Min, fresh.Bounds.Min) > tolerance || Vector2.Distance(region.Bounds.Max, fresh.Bounds.Max) > tolerance)
                {
                    differences.Add($"hurt '{region.Id}' at rest: {region.Bounds} -> {fresh?.Bounds.ToString() ?? "missing"}");
                }
            }
            // The intentional difference, measured: how far each converted box drifts from the legacy pose-derived box over the sampled instants.
            var drift = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var (clip, seconds, _) in instants)
            {
                var pose = new ActorPose(PoseEvaluator.Sample(before.Model, clip, seconds, repeat: clip.Loop, new() { InPlace = true }), Vector2.Zero, 1);
                var legacy = EntityCollision.Hurt(before, pose);
                var fresh = EntityCollision.Hurt(after, pose);
                foreach (var region in legacy)
                {
                    var converted = fresh.First(r => r.Id == region.Id);
                    var moved = MathF.Max(Vector2.Distance(region.Bounds.Min, converted.Bounds.Min), Vector2.Distance(region.Bounds.Max, converted.Bounds.Max));
                    drift[region.Id] = MathF.Max(drift.GetValueOrDefault(region.Id), moved);
                }
            }
            report.Add("hurt at rest reproduced exactly; largest animated drift of each box's corner from the legacy pose-derived box over the sampled instants: "
                + string.Join(", ", drift.Select(d => $"{d.Key} {d.Value:0.###}")));
        }
    }

    private static string Report(string root, Result result)
    {
        var text = new StringBuilder();
        text.AppendLine($"# Collider conversion report: entity '{result.EntityId}'");
        text.AppendLine();
        text.AppendLine($"Source: `{result.Source}` under `{root}` (unchanged). Output: `{result.Output ?? "none"}`. Status: {result.Status}. Date: {DateTime.Now:yyyy-MM-dd HH:mm}.");
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
