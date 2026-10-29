using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Json;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace App2d.Tests.Authored;

/// <summary>
/// P0b of the character-system migration (docs/character-system-migration-plan.md): the reference set that later
/// conversion passes compare against. Every case samples the current evaluator, collision placement and contour path for
/// one representative bundle at one instant and facing, and the result is checked against
/// Authored/References/character-migration-references.json with the tolerances that file declares.
///
/// Set APP2D_WRITE_REFERENCES=1 to rewrite the file from the current code. Do that only for an approved, recorded
/// difference (plan section 7): the point of the file is that it does not move when the implementation does.
/// </summary>
public sealed class MigrationReferenceTests
{
    private const string FileName = "character-migration-references.json";
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
    private static readonly ResolvedModel Linkage = ResolvedModel.From(TestModels.Linkage());
    private static readonly (CharacterModel Model, IReadOnlyList<MotionClip> Animations) WheelScene = TestModels.Wheel();
    private static readonly ResolvedModel Wheel = ResolvedModel.From(WheelScene.Model);
    private static readonly Dictionary<string, MotionClip> FixtureClips = new(StringComparer.Ordinal)
    {
        ["linkage-crank"] = TestModels.LinkageCrank(Linkage),
        ["linkage-reach"] = TestModels.LinkageReach(Linkage),
        ["wheel-turn"] = WheelScene.Animations[0],
    };


    /// <summary>Absolute tolerances by observable quantity (plan section 7: per quantity and scale, no global epsilon).</summary>
    private static readonly Dictionary<string, float> Tolerances = new(StringComparer.Ordinal)
    {
        // Evaluated control, socket, prop and region positions in authored units (about 2 m per character). Float
        // evaluation reproduces itself to ~1e-7; 1e-4 leaves room for a different but equivalent operation order.
        ["position"] = 1e-4f,
        // Accumulated frame angles and socket orientations in radians.
        ["angle"] = 1e-4f,
        // Unit axes of 3D socket frames (dimensionless components).
        ["axis"] = 1e-4f,
        // IK and contact residuals in authored units: today they are ~1e-8, so any residual above this is a real change.
        ["residual"] = 1e-5f,
        // Silhouette bounds and centroids in authored units: 48-segment outlines, line widths excluded.
        ["contour"] = 1e-3f,
        // Shoelace area of a part outline in square authored units.
        ["area"] = 1e-3f,
        // Combat positions in authored units at the end of a scenario.
        ["combat"] = 1e-3f,
    };

    private sealed record ReferenceCase(string Id, string Model, string? Entity, string? Clip, float Seconds, int Facing, bool InPlace = true, string? Expression = null, string? Gear = null);

    private sealed record Scenario(string Id, string[] Entities, Vector2[] Positions, int Steps, int[] AttackSteps, float Move = 0);

    private static float Marker(MotionClip clip, string id) => clip.Markers.First(m => m.Id == id).Time;

    private static MotionClip ClipOf(string id) => FixtureClips.TryGetValue(id, out var fixture) ? fixture : Catalog.Animations[id];

    private static ResolvedModel Resolve(string id) => id switch { "linkage" => Linkage, "wheel" => Wheel, _ => Catalog.Resolve(id) };

    /// <summary>The representative bundles from plan section 5, P0, each at the instants that exercise its named cases.</summary>
    private static IEnumerable<ReferenceCase> Cases()
    {
        // Person family: shared clips on the base and two proportion variants, contacts active, both facings on one instant.
        foreach (var model in new[] { "person", "tall-thin", "short-broad" })
        {
            yield return new($"{model}/rest", model, null, null, 0, 1);
            foreach (var seconds in new[] { 0f, .3f, .9f })
            {
                yield return new($"{model}/person-walk@{seconds}", model, null, "person-walk", seconds, 1);
            }

            yield return new($"{model}/person-walk@0.3/left", model, null, "person-walk", .3f, -1);
            yield return new($"{model}/person-run@0.2", model, null, "person-run", .2f, 1);
            yield return new($"{model}/person-jump@apex", model, null, "person-jump", Marker(Catalog.Animations["person-jump"], "apex"), 1);
        }

        // Hero: tilted/twisted sword at the strike, the sheathing transition and the gun shot.
        var sideCut = Catalog.Animations["player-sword-side-cut"];
        var sheathe = Catalog.Animations["player-sword-sheathe"];
        foreach (var facing in new[] { 1, -1 })
        {
            yield return new($"hero/player-sword-side-cut@strike/{(facing > 0 ? "right" : "left")}", "person", "hero", sideCut.Id, Marker(sideCut, "strike"), facing, Gear: "sword");
        }

        yield return new("hero/player-sword-side-cut@swoosh", "person", "hero", sideCut.Id, Marker(sideCut, "swoosh"), 1, Gear: "sword");
        yield return new("hero/player-sword-forehand@strike", "person", "hero", "player-sword-forehand", Marker(Catalog.Animations["player-sword-forehand"], "strike"), 1, Gear: "sword");
        yield return new("hero/player-sword-backhand@strike", "person", "hero", "player-sword-backhand", Marker(Catalog.Animations["player-sword-backhand"], "strike"), 1, Gear: "sword");
        foreach (var seconds in new[] { 0f, .31f, sheathe.Duration })
        {
            yield return new($"hero/player-sword-sheathe@{seconds}", "person", "hero", sheathe.Id, seconds, 1, Gear: "sword");
        }

        yield return new("hero/player-gun-shot@fire", "person", "hero", "player-gun-shot", Marker(Catalog.Animations["player-gun-shot"], "fire"), 1, Gear: "gun");
        yield return new("hero/player-gun-shot@fire/left", "person", "hero", "player-gun-shot", Marker(Catalog.Animations["player-gun-shot"], "fire"), -1, Gear: "gun");
        yield return new("hero/player-climb@0.4", "person", "hero", "player-climb", .4f, 1, Gear: "sword");
        yield return new("hero/player-climb-on@0.15", "person", "hero", "player-climb-on", .15f, 1, Gear: "sword");
        yield return new("hero/player-idle@1.0/hurt", "person", "hero", "player-idle", 1, 1, Expression: "hurt");

        // Cinder gunner and Maul brute: variant builds with their wardrobe, weapons and hit windows.
        yield return new("cinder-gunner/person-pistol-shot@fire", "cinder", "cinder-gunner", "person-pistol-shot", Marker(Catalog.Animations["person-pistol-shot"], "fire"), 1);
        yield return new("cinder-gunner/person-idle@0.5", "cinder", "cinder-gunner", "person-idle", .5f, 1);
        yield return new("maul-brute/person-hammer-slam@strike", "brute", "maul-brute", "person-hammer-slam", Marker(Catalog.Animations["person-hammer-slam"], "strike"), 1);
        yield return new("maul-brute/person-hammer-slam@strike/left", "brute", "maul-brute", "person-hammer-slam", Marker(Catalog.Animations["person-hammer-slam"], "strike"), -1);
        yield return new("maul-brute/person-heavy-walk@0.5", "brute", "maul-brute", "person-heavy-walk", .5f, 1);
        yield return new("spear-guard/person-thrust@strike", "tall-thin", "spear-guard", "person-thrust", Marker(Catalog.Animations["person-thrust"], "strike"), 1);

        // Quadruped and triceratops gaits, the baby rig's charge with its hit window.
        foreach (var seconds in new[] { 0f, .4f, .8f })
        {
            yield return new($"quadruped/quadruped-walk@{seconds}", "quadruped", null, "quadruped-walk", seconds, 1);
        }

        yield return new("quadruped/quadruped-run@0.3", "quadruped", null, "quadruped-run", .3f, 1);
        yield return new("quadruped/quadruped-run@0.3/left", "quadruped", null, "quadruped-run", .3f, -1);
        yield return new("triceratops/triceratops-walk@0.4", "triceratops", null, "triceratops-walk", .4f, 1);
        yield return new("triceratops/triceratops-rush@0.2", "triceratops", null, "triceratops-rush", .2f, 1);
        var charge = Catalog.Animations["baby-triceratops-charge"];
        yield return new("baby-triceratops/charge@rush", "baby-triceratops-rig", "baby-triceratops", charge.Id, Marker(charge, "rush"), 1);
        yield return new("baby-triceratops/charge@brake", "baby-triceratops-rig", "baby-triceratops", charge.Id, Marker(charge, "brake"), 1);

        // Pterosaur: point-bound wing polygons through hover, fold and dive, plus the swoop's beak window.
        yield return new("pterosaur/pterosaur-hover@0.5", "pterosaur", null, "pterosaur-hover", .5f, 1);
        yield return new("pterosaur/pterosaur-fold@end", "pterosaur", null, "pterosaur-fold", Catalog.Animations["pterosaur-fold"].Duration, 1);
        yield return new("pterosaur/pterosaur-dive@0.3", "pterosaur", null, "pterosaur-dive", .3f, 1);
        yield return new("pterosaur/pterosaur-dive@0.3/left", "pterosaur", null, "pterosaur-dive", .3f, -1);
        var swoop = Catalog.Animations["pterosaur-swoop"];
        yield return new("flying-pterosaur/swoop@rush", "pterosaur", "flying-pterosaur", swoop.Id, Marker(swoop, "rush"), 1);

        // Stalker: three-legged point rig with a socket off the body.
        yield return new("stalker-pest/stalker-lunge@strike", "stalker", "stalker-pest", "stalker-lunge", Marker(Catalog.Animations["stalker-lunge"], "strike"), 1);
        yield return new("stalker/stalker-walk@0.5", "stalker", null, "stalker-walk", .5f, 1);

        // Mechanical linkage: reached, unreachable and zero-length IK, through a full crank turn.
        yield return new("linkage/rest", "linkage", null, null, 0, 1);
        foreach (var seconds in new[] { 0f, .125f, .25f, .375f, .5f, .625f, .75f, .875f })
        {
            yield return new($"linkage/linkage-crank@{seconds}", "linkage", null, "linkage-crank", seconds, 1);
        }

        yield return new("linkage/linkage-crank@0.25/left", "linkage", null, "linkage-crank", .25f, -1);
        yield return new("linkage/linkage-reach@0", "linkage", null, "linkage-reach", 0, 1);
        yield return new("linkage/linkage-reach@0.5", "linkage", null, "linkage-reach", .5f, 1);
        yield return new("linkage/linkage-reach@1", "linkage", null, "linkage-reach", 1, 1);

        // Native explicit-bone wheel: inherited reflection, nonuniform scale and shear, animated scale/shear, slot timelines.
        yield return new("wheel/rest", "wheel", null, null, 0, 1);
        foreach (var seconds in new[] { .25f, .5f, 1f })
        {
            yield return new($"wheel/wheel-turn@{seconds}", "wheel", null, "wheel-turn", seconds, 1);
        }

        yield return new("wheel/wheel-turn@0.5/left", "wheel", null, "wheel-turn", .5f, -1);
    }

    /// <summary>Graphics-free arena fights: who hits whom, on which tick, for how much, and where everyone ends up.</summary>
    private static IEnumerable<Scenario> Scenarios()
    {
        yield return new("guard-thrust/right", ["player", "spear-guard"], [new(0, 0), new(2.05f, 0)], 360, []);
        yield return new("guard-thrust/left", ["player", "spear-guard"], [new(0, 0), new(-2.05f, 0)], 360, []);
        yield return new("hero-combo", ["hero", "stalker-pest"], [new(0, 0), new(1.2f, 0)], 240, [1, 25, 50, 75]);
        yield return new("brute-slam", ["maul-brute", "player"], [new(0, 0), new(1.7f, 0)], 240, [1]);
        yield return new("club-slam", ["club-caveman", "player"], [new(0, 0), new(1.5f, 0)], 240, [1]);
        yield return new("gunner-bolt", ["cinder-gunner", "player"], [new(0, 0), new(4, 0)], 240, [1]);
        yield return new("baby-charge", ["baby-triceratops", "player"], [new(0, 0), new(3.5f, 0)], 360, [1]);
        yield return new("player-walk-thrust", ["player", "spear-guard"], [new(-3, 0), new(2.05f, 0)], 300, [150], Move: 1);
    }

    [Fact]
    public void ReferencesMatchTheCurrentEvaluatorCollisionAndCombat()
    {
        var path = ReferencePath();
        var fresh = new Document
        {
            Note = "P0b reference set for the character-system migration. Generated by MigrationReferenceTests with APP2D_WRITE_REFERENCES=1; compared with the tolerances below. Record an approved difference in docs/character-migration-baseline.md before regenerating.",
            Tolerances = Tolerances,
            Cases = Cases().ToDictionary(c => c.Id, Capture, StringComparer.Ordinal),
            Combat = Scenarios().ToDictionary(s => s.Id, Fight, StringComparer.Ordinal),
        };
        if (Environment.GetEnvironmentVariable("APP2D_WRITE_REFERENCES") is { Length: > 0 })
        {
            WriteReferences(path, JsonSerializer.Serialize(fresh, JsonProfiles.SparseCamelCase));
            return;
        }

        Assert.True(File.Exists(path), $"No reference file at {path}; run once with APP2D_WRITE_REFERENCES=1.");
        using var expected = JsonDocument.Parse(File.ReadAllText(path));
        using var actual = JsonDocument.Parse(JsonSerializer.Serialize(fresh, JsonProfiles.SparseCamelCase));
        var differences = new List<string>();
        Compare(expected.RootElement.GetProperty("cases"), actual.RootElement.GetProperty("cases"), "cases", differences);
        Compare(expected.RootElement.GetProperty("combat"), actual.RootElement.GetProperty("combat"), "combat", differences);
        if (differences.Count > 0 && Environment.GetEnvironmentVariable("APP2D_SPLICE_REFERENCES") is { Length: > 0 })
        {
            // Splice: only the values the comparison listed take their fresh value; everything else keeps its recorded text, so
            // an approved difference lands in the file without last-digit float noise elsewhere (record it in the baseline first).
            var changed = differences.Select(d => d[..d.IndexOf(": ", StringComparison.Ordinal)]).ToHashSet(StringComparer.Ordinal);
            var merged = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            foreach (var section in new[] { "cases", "combat" })
            {
                merged[section] = Splice(merged[section], actual.RootElement.GetProperty(section), section, changed);
            }

            WriteReferences(path, merged.ToJsonString(JsonProfiles.Indented));
            return;
        }

        Assert.True(differences.Count == 0, $"{differences.Count} reference difference(s):{Environment.NewLine}{string.Join(Environment.NewLine, differences.Take(Environment.GetEnvironmentVariable("APP2D_REFERENCE_DIFFERENCES") is { Length: > 0 } ? int.MaxValue : 40))}");
    }

    [Fact]
    public void TheLinkageExercisesReachedUnreachableAndZeroLengthTargets()
    {
        var crank = FixtureClips["linkage-crank"];
        var reached = Enumerable.Range(0, 8).Select(i => PoseEvaluator.Sample(Linkage, crank, i / 8.0, true).Chains.Single().Reached).ToList();
        Assert.Contains(true, reached);
        Assert.Contains(false, reached);
        Assert.True(reached[0], "the pin at rest is within the rod's reach");
        Assert.False(reached[4], "the pin on the far side of the hub is beyond full reach");
        // P4b: reached is residual-based. At 1/8 and 7/8 turn the target sits 5.6e-3 past full reach; the solver used to clamp
        // and still report reached (baseline 7.2), now it reports the gap with the same residual.
        Assert.False(reached[1], "a target 5.6e-3 past full reach is not reached");
        Assert.False(reached[7], "a target 5.6e-3 past full reach is not reached");
        Assert.Equal(.0055427f, PoseEvaluator.Sample(Linkage, crank, 1 / 8.0, true).Chains.Single().Residual, 5); // the rigid solve's gap (P11c; the point adapter reported .0055562)
        var pin = PoseEvaluator.Sample(Linkage, crank, .5, true);
        TestModels.Near(new(-.4f, 1, 0), pin.World("crank-pin"), 1e-4f, "half a turn carries the pin to the far side");
        // A degenerate target folds the rod fully onto its root (P11c: the rigid solve; the point adapter kept a 1e-3 minimum fold and reported it as a gap).
        var zero = PoseEvaluator.Sample(Linkage, FixtureClips["linkage-reach"], 0);
        TestModels.Near(zero.World("piston"), zero.World("rod-end"), 1e-5f, "a zero-length target folds the rod onto its root");
        Assert.Equal(0, zero.Chains.Single().Residual, 4);
        Assert.True(zero.Chains.Single().Reached);
    }

    private static void WriteReferences(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Scalar arrays (points, bounds, axes) on one line each: the file stays diffable without a line per component.
        var text = Regex.Replace(json, @"\[\s+((?:-?[\d.Ee+-]+|true|false)(?:,\s+(?:-?[\d.Ee+-]+|true|false))*)\s+\]",
            m => "[" + Regex.Replace(m.Groups[1].Value, @",\s+", ", ") + "]");
        File.WriteAllText(path, text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
    }

    /// <summary>The recorded node with the fresh values at the listed paths (new members added, missing ones removed, resized arrays taken whole).</summary>
    private static JsonNode? Splice(JsonNode? recorded, JsonElement fresh, string path, HashSet<string> changed)
    {
        if (changed.Contains(path) || recorded is null)
        {
            return JsonNode.Parse(fresh.GetRawText());
        }

        switch (fresh.ValueKind)
        {
            case JsonValueKind.Object when recorded is JsonObject recordedObject:
                var result = new JsonObject();
                foreach (var property in fresh.EnumerateObject())
                {
                    result[property.Name] = recordedObject.TryGetPropertyValue(property.Name, out var existing)
                        ? Splice(existing, property.Value, $"{path}/{property.Name}", changed)
                        : JsonNode.Parse(property.Value.GetRawText());
                }

                return result;
            case JsonValueKind.Array when recorded is JsonArray recordedArray:
                var items = fresh.EnumerateArray().ToList();
                if (items.Count != recordedArray.Count)
                {
                    return JsonNode.Parse(fresh.GetRawText());
                }

                var array = new JsonArray();
                for (var i = 0; i < items.Count; i++)
                {
                    array.Add(Splice(recordedArray[i]?.DeepClone(), items[i], $"{path}[{i}]", changed));
                }

                return array;
            default:
                return recorded.DeepClone();
        }
    }

    private static string ReferencePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var tests = Path.Combine(directory.FullName, "App2d.Tests");
            if (File.Exists(Path.Combine(tests, "App2d.Tests.csproj")))
            {
                return Path.Combine(tests, "Authored", "References", FileName);
            }
        }

        throw new DirectoryNotFoundException("App2d.Tests project not found above the test output.");
    }

    private static CaseRecord Capture(ReferenceCase c)
    {
        var model = Resolve(c.Model);
        var entity = c.Entity is null ? null : Catalog.Entities[c.Entity];
        if (entity is not null)
        {
            model = entity.Model;
        }

        var clip = c.Clip is null ? null : ClipOf(c.Clip);
        var pose = PoseEvaluator.Sample(model, clip, c.Seconds, repeat: true, new(c.Expression) { InPlace = c.InPlace });
        var actor = new ActorPose(pose, Vector2.Zero, c.Facing);
        var record = new CaseRecord
        {
            Model = model.Id,
            Entity = c.Entity,
            Clip = c.Clip,
            Seconds = c.Seconds,
            Facing = c.Facing,
            Points = model.Order.ToDictionary(k => k.Id, k => V(actor.World(k.Id)), StringComparer.Ordinal),
            Angles = model.Order.ToDictionary(k => k.Id, k => pose.Angles[k.Id], StringComparer.Ordinal),
            Chains = [.. pose.Chains.Select(ch => new ChainRecord(ch.Chain, ch.Residual, ch.Reached))],
            Contacts = [.. pose.Contacts.Select(ct => new ContactRecord(ct.Chain, V(actor.Place(ct.Target)), ct.Residual))],
            Expressions = pose.Expressions.Count == 0 ? null : new(pose.Expressions),
            // Full bone matrices for the wheel fixture: that is where the inherited reflection and scale live.
            Bones = model.Base.Id == "wheel" ? model.Order.ToDictionary(k => k.Id, k => M(pose.Bones[k.Id]), StringComparer.Ordinal) : null,
            Slots = pose.Slots.Count == 0 ? null : [.. pose.Slots.Select(s => new SlotRecord(s.Slot.Id, s.Part?.Id, [s.Color.X, s.Color.Y, s.Color.Z, s.Color.W]))],
            Sockets = model.Base.Sockets.ToDictionary(s => s.Id, s => Frame(actor.Socket(s)), StringComparer.Ordinal),
            Parts = [.. model.Parts.Where(p => !p.Hidden).Select(p => Silhouette(p, pose, model, actor))],
        };
        if (entity is null)
        {
            return record;
        }

        var placements = entity.Equipment.Select(e => (e.Prop, e.Socket)).ToList();
        if (c.Gear is { } gear && clip is not null)
        {
            placements = [.. PersonLoadout.Dressed(clip, c.Seconds, gear == "gun" ? PersonGear.Gun : PersonGear.Sword, entity)
                .Where(d => Catalog.Parts.ContainsKey(d.Prop) && entity.Sockets.ContainsKey(d.Socket))
                .Select(d => (Catalog.Parts[d.Prop], entity.Sockets[d.Socket]))];
        }

        record.Props = placements.ToDictionary(p => $"{p.Prop.Id}@{p.Socket.Id}", p => PropPoints(p.Prop, actor.Socket(p.Socket)), StringComparer.Ordinal);
        record.Movement = Bounds(EntityCollision.Movement(entity, Vector2.Zero, c.Facing).Bounds);
        record.Hurt = [.. EntityCollision.Hurt(entity, actor).Select(h => new RegionRecord(h.Id, Bounds(h.Bounds), V2(h.Pose.Translation), V2(h.Pose.XAxis), h.Pose.IsMirrored))];
        record.Attacks = [.. entity.Actions.Values.Where(a => a.Clip.Id == c.Clip).SelectMany(a => a.Hits.Select(h =>
        {
            var region = EntityCollision.Attack(entity, actor, h);
            return new AttackRecord(a.Id, h.Window.Id, h.Start, h.Finish, h.Start <= c.Seconds && c.Seconds < h.Finish, Bounds(region.Bounds), V2(region.Pose.Translation), V2(region.Pose.XAxis), region.Pose.IsMirrored);
        }))];
        if (entity.Equipment.Any(e => e.Prop.Point(EquipmentPoints.Muzzle) is not null))
        {
            var (point, axis) = EntityCollision.Muzzle(entity, actor);
            record.Muzzle = new(V(point), V2(axis));
        }

        return record;
    }

    private static SilhouetteRecord Silhouette(PartGeometry part, EvaluatedPose pose, ResolvedModel model, ActorPose actor)
    {
        var contour = PartContours2D.Contour(part, model, pose)
            .ConvertAll(actor.Place);
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

        return new(part.Id, part.Depth, contour.Count, V(min), V(max), V(sum / contour.Count), (float)Math.Abs(area / 2), V(contour[0]));
    }

    private static CombatRecord Fight(Scenario s)
    {
        var arena = new AuthoredArena(s.Entities.Select(id => Catalog.Entities[id]));
        for (var i = 0; i < s.Positions.Length; i++)
        {
            arena.Teleport(i, s.Positions[i]);
        }

        for (var step = 0; step < s.Steps; step++)
        {
            arena.Step(new(Move: s.Move, Attack: s.AttackSteps.Contains(step)));
        }

        return new(
            [.. arena.Hits.Select(h => new HitRecord(h.Tick, h.Attacker, h.Target, h.Window, h.Damage))],
            [.. arena.Events.Select(e => new EventRecord(e.Tick, e.Actor, e.Event.Kind, e.Event.Id, e.Event.Sound, e.Event.Action, e.Event.ActionSequence))],
            [.. arena.Actors.Select(a => new ActorRecord(a.Entity.Id, a.Health, a.Alive, V2(a.Position), a.Facing, a.Animator.Role, a.Animator.Action, a.Animator.ActionSequence))],
            arena.Bolts.Count);
    }

    private static void Compare(JsonElement expected, JsonElement actual, string path, List<string> differences)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            differences.Add($"{path}: {expected.ValueKind} became {actual.ValueKind}");
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedNames = expected.EnumerateObject().Select(p => p.Name).ToList();
                var actualNames = actual.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                foreach (var missing in expectedNames.Where(n => !actualNames.Contains(n)))
                {
                    differences.Add($"{path}/{missing}: missing");
                }

                foreach (var added in actualNames.Where(n => !expectedNames.Contains(n)))
                {
                    differences.Add($"{path}/{added}: new");
                }

                foreach (var name in expectedNames.Where(actualNames.Contains))
                {
                    Compare(expected.GetProperty(name), actual.GetProperty(name), $"{path}/{name}", differences);
                }

                break;
            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();
                if (expectedItems.Count != actualItems.Count)
                {
                    differences.Add($"{path}: {expectedItems.Count} items became {actualItems.Count}");
                    break;
                }

                for (var i = 0; i < expectedItems.Count; i++)
                {
                    Compare(expectedItems[i], actualItems[i], $"{path}[{i}]", differences);
                }

                break;
            case JsonValueKind.Number:
                if (expected.TryGetInt64(out var expectedInt) && actual.TryGetInt64(out var actualInt))
                {
                    if (expectedInt != actualInt)
                    {
                        differences.Add($"{path}: {expectedInt} became {actualInt}");
                    }

                    break;
                }

                var tolerance = ToleranceFor(path);
                var delta = Math.Abs(expected.GetDouble() - actual.GetDouble());
                if (delta > tolerance)
                {
                    differences.Add($"{path}: {expected.GetDouble():R} became {actual.GetDouble():R} (delta {delta:G3} > {tolerance:G3})");
                }

                break;
            default:
                if (expected.GetRawText() != actual.GetRawText())
                {
                    differences.Add($"{path}: {expected.GetRawText()} became {actual.GetRawText()}");
                }

                break;
        }
    }

    private static float ToleranceFor(string path)
    {
        var leaf = path[(path.LastIndexOf('/') + 1)..];
        var parent = path[..path.LastIndexOf('/')];
        return leaf.StartsWith("residual", StringComparison.Ordinal) ? Tolerances["residual"]
            : leaf.StartsWith("area", StringComparison.Ordinal) ? Tolerances["area"]
            : parent.Contains("/parts[", StringComparison.Ordinal) ? Tolerances["contour"]
            : path.StartsWith("combat", StringComparison.Ordinal) ? Tolerances["combat"]
            : parent.EndsWith("/angles", StringComparison.Ordinal) || leaf.StartsWith("start", StringComparison.Ordinal) || leaf.StartsWith("finish", StringComparison.Ordinal) || leaf.StartsWith("seconds", StringComparison.Ordinal) || leaf.StartsWith("depth", StringComparison.Ordinal) ? Tolerances["angle"]
            : leaf.EndsWith("Axis", StringComparison.Ordinal) || leaf.StartsWith("axis", StringComparison.Ordinal) ? Tolerances["axis"]
            : Tolerances["position"];
    }

    private static float[] V(Vector3 v) => [v.X, v.Y, v.Z];

    private static float[] V2(Vector2 v) => [v.X, v.Y];

    private static float[] M(Matrix3x2 m) => [m.M11, m.M12, m.M21, m.M22, m.M31, m.M32];

    private static float[][] Bounds(Rect2D r) => [V2(r.Min), V2(r.Max)];

    private static FrameRecord Frame(Frame3D f) => new(V(f.Origin), V(f.XAxis), V(f.YAxis), V(f.ZAxis));

    private static Dictionary<string, float[]> PropPoints(Part prop, Frame3D frame)
    {
        var points = new Dictionary<string, float[]>(StringComparer.Ordinal);
        foreach (var name in EquipmentPoints.All)
        {
            if (prop.Point(name) is { } local)
            {
                points[name] = V(PartPlacement3D.Point(frame, prop, local));
            }
        }

        return points;
    }

    private sealed class Document
    {
        public string Note { get; set; } = "";
        public Dictionary<string, float> Tolerances { get; set; } = [];
        public Dictionary<string, CaseRecord> Cases { get; set; } = [];
        public Dictionary<string, CombatRecord> Combat { get; set; } = [];
    }

    private sealed class CaseRecord
    {
        public string Model { get; set; } = "";
        public string? Entity { get; set; }
        public string? Clip { get; set; }
        public float Seconds { get; set; }
        public int Facing { get; set; }
        public Dictionary<string, float[]> Points { get; set; } = [];
        public Dictionary<string, float> Angles { get; set; } = [];
        public List<ChainRecord> Chains { get; set; } = [];
        public List<ContactRecord> Contacts { get; set; } = [];
        public Dictionary<string, string>? Expressions { get; set; }
        public Dictionary<string, float[]>? Bones { get; set; }
        public List<SlotRecord>? Slots { get; set; }
        public Dictionary<string, FrameRecord> Sockets { get; set; } = [];
        public Dictionary<string, Dictionary<string, float[]>>? Props { get; set; }
        public MuzzleRecord? Muzzle { get; set; }
        public float[][]? Movement { get; set; }
        public List<RegionRecord>? Hurt { get; set; }
        public List<AttackRecord>? Attacks { get; set; }
        public List<SilhouetteRecord> Parts { get; set; } = [];
    }

    private sealed record ChainRecord(string Chain, float Residual, bool Reached);

    private sealed record SlotRecord(string Slot, string? Part, float[] Color);

    private sealed record ContactRecord(string Chain, float[] Target, float Residual);

    private sealed record FrameRecord(float[] Origin, float[] XAxis, float[] YAxis, float[] ZAxis);

    private sealed record MuzzleRecord(float[] Point, float[] Axis);

    private sealed record RegionRecord(string Id, float[][] Bounds, float[] Translation, float[] XAxis, bool Mirrored);

    private sealed record AttackRecord(string Action, string Window, float Start, float Finish, bool Active, float[][] Bounds, float[] Translation, float[] XAxis, bool Mirrored);

    private sealed record SilhouetteRecord(string Id, float Depth, int Count, float[] Min, float[] Max, float[] Centroid, float Area, float[] First);

    private sealed record CombatRecord(List<HitRecord> Hits, List<EventRecord> Events, List<ActorRecord> Actors, int Bolts);

    private sealed record HitRecord(long Tick, int Attacker, int Target, string Window, int Damage);

    private sealed record EventRecord(long Tick, int Actor, string Kind, string Id, string? Sound, string? Action, int ActionSequence);

    private sealed record ActorRecord(string Entity, int Health, bool Alive, float[] Position, int Facing, string Role, string? Action, int ActionSequence);
}
