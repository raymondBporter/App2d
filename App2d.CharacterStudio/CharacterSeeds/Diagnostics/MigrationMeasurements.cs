using App2d.Core.Assets;
using App2d.Core.Characters.Authored;
using System.Diagnostics;
using System.Text;

namespace App2d.CharacterSeeds.Diagnostics;

/// <summary>
/// The character-system migration's closing measurements (P11c, plan section 7): catalog load and rig compilation time,
/// per-sample and per-actor-step allocation, the registered collider count and the broad-phase candidates per attack query
/// of the guard-thrust arena, written as a report beside the numbers the plan records.
/// </summary>
internal static class MigrationMeasurements
{
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var report = new StringBuilder();
        report.AppendLine("# Character-system migration measurements (P11c)");
        report.AppendLine();
        report.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}. Authored root: `{AssetPaths.Current.AuthoredCharacters}`.");
        report.AppendLine();

        var stopwatch = Stopwatch.StartNew();
        var catalog = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        var load = stopwatch.Elapsed.TotalMilliseconds;
        report.AppendLine($"- Catalog load (every model, variant, clip, part, prop, entity, template and profile parsed, resolved and compiled): {load:0} ms, {catalog.Models.Count} models, {catalog.Variants.Count} variants, {catalog.Animations.Count} clips, {catalog.Entities.Count} entities, {catalog.Errors.Count} errors.");

        var person = catalog.Models["person"];
        var tall = catalog.Variants["tall-thin"];
        double Median(Func<double> measure, int runs)
        {
            var samples = new List<double>();
            for (var i = 0; i < runs; i++)
            {
                samples.Add(measure());
            }

            samples.Sort();
            return samples[samples.Count / 2];
        }
        double Time(Action action)
        {
            var watch = Stopwatch.StartNew();
            action();
            return watch.Elapsed.TotalMilliseconds;
        }
        ResolvedModel.From(person, tall, catalog.Parts.GetValueOrDefault);
        var rig = Median(() => Time(() => ResolvedModel.Rig(person)), 50);
        var resolve = Median(() => Time(() => ResolvedModel.From(person, null, catalog.Parts.GetValueOrDefault)), 50);
        var variant = Median(() => Time(() => ResolvedModel.From(person, tall, catalog.Parts.GetValueOrDefault)), 50);
        report.AppendLine($"- Rig compilation, median of 50 warm runs: `ResolvedModel.Rig(person)` {rig:0.00} ms; `ResolvedModel.From(person)` with the part library {resolve:0.00} ms; `tall-thin` (parameters, bindings and overrides applied) {variant:0.00} ms.");

        var resolved = catalog.Resolve("person");
        var walk = catalog.Animations["person-walk"];
        PoseEvaluator.Sample(resolved, walk, .3, true);
        var before = GC.GetAllocatedBytesForCurrentThread();
        const int samples = 200;
        for (var i = 0; i < samples; i++)
        {
            PoseEvaluator.Sample(resolved, walk, i * walk.Duration / samples, true);
        }
        var perSample = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)samples;
        var sampleMs = Median(() => Time(() => PoseEvaluator.Sample(resolved, walk, .3, true)), 50);
        report.AppendLine($"- Pose sampling of the Person walk (17 controls, 4 bone constraints, 2 contacts, the face instance): {sampleMs:0.000} ms and {perSample:0} bytes allocated per sample (a fresh evaluated pose with its dictionaries; the keyframe sampler itself allocates nothing, `KeyframeSamplingTests`).");

        var arena = new AuthoredArena([catalog.Entities["player"], catalog.Entities["spear-guard"]]);
        arena.Teleport(0, new(0, 0));
        arena.Teleport(1, new(2.05f, 0));
        var registered = arena.Collision.Colliders.Count;
        var candidates = new List<int>();
        for (var i = 0; i < 60; i++)
        {
            arena.Step(default);
        }
        before = GC.GetAllocatedBytesForCurrentThread();
        const int steps = 300;
        for (var i = 0; i < steps; i++)
        {
            arena.Step(default);
            if (arena.Actors.Any(a => a.Attacks.Any()))
            {
                candidates.Add(arena.LastQueryCandidates);
            }
        }
        var perActorStep = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)(steps * arena.Actors.Count);
        report.AppendLine($"- Guard-thrust arena (player and spear guard): {registered} colliders registered once for the pair ({string.Join(" + ", arena.Actors.Select(a => $"{a.Entity.Id} {a.Colliders.All.Count}"))}), still {arena.Collision.Colliders.Count} after 360 steps (nothing re-registers); broad-phase candidates per step with an active attack query: min {(candidates.Count == 0 ? 0 : candidates.Min())}, max {(candidates.Count == 0 ? 0 : candidates.Max())}; {arena.Hits.Count} hit(s) at tick(s) {string.Join(", ", arena.Hits.Select(h => h.Tick))}; {perActorStep:0} bytes allocated per actor step (animator, pose, collider sync and the queries).");
        report.AppendLine();
        report.AppendLine("P8 recorded 11 registered colliders for the pair and 9 broad-phase candidates per active attack query (P8c); the plan row compares against those.");
        File.WriteAllText(Path.Combine(directory, "migration-measurements.md"), report.ToString());
        Console.Write(report.ToString());
    }
}
