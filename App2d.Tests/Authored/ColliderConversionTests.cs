using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P8c: the explicit, rerunnable conversion of an entity's legacy collision fields into attached colliders, and the shipped converted set.</summary>
public sealed class ColliderConversionTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    private static ResolvedEntity Compile(EntityAsset asset) =>
        ResolvedEntity.Compile(asset, Catalog.Resolve, Catalog.Animations.GetValueOrDefault, Catalog.Props.GetValueOrDefault);

    [Fact]
    public void ConvertingTheSeedSpearGuardReproducesTheShippedFileExceptForItsAuthoredTrigger()
    {
        // P11b: the seed declares its colliders; the conversion is exercised on its version-2 form.
        var model = Catalog.Resolve(StarterContent.SpearGuardEntity().Model);
        var seed = LegacyEntities.Strip(StarterContent.SpearGuardEntity(), model.Base, keepBehaviors: true);
        var (converted, report) = ColliderConversion.Convert(seed, model);
        Assert.Equal(EntityAsset.CurrentVersion, converted.Version);
        Assert.Null(converted.Hurt.Layout);
        Assert.Equal(["spear-tip", "body", "head", "legs"], converted.Colliders.Select(c => c.Id));
        Assert.Equal("spear-tip", converted.Actions[0].Hits[0].Collider);
        Assert.Null(converted.Actions[0].Hits[0].Prop);
        Assert.Contains(report, line => line.StartsWith("hurt region 'legs'", StringComparison.Ordinal) && line.Contains("bound to 'left-hip'"));
        // The shipped file is this conversion plus the reach trigger that was authored on it afterwards.
        var shipped = Catalog.Entities["spear-guard"].Asset;
        var trigger = Assert.Single(shipped.Colliders.Where(c => c.Use == ColliderUses.Trigger));
        Assert.Equal(("reach", ColliderBindings.Prop, "spear"), (trigger.Id, trigger.Binding, trigger.Prop));
        var withoutTrigger = EntityAsset.FromJson(shipped.ToJson());
        withoutTrigger.Colliders.RemoveAll(c => c.Use == ColliderUses.Trigger);
        Assert.Equal(converted.ToJson(), withoutTrigger.ToJson());
        // Rerunning on the converted entity changes nothing and says so.
        var (again, note) = ColliderConversion.Convert(converted, model);
        Assert.Equal(converted.ToJson(), again.ToJson());
        Assert.Equal("nothing to convert: the entity already declares its colliders", Assert.Single(note));
    }

    [Fact]
    public void TheConvertedSetKeepsEveryAttackPlacementAndReproducesTheLegacyHurtAtRest()
    {
        foreach (var (id, seed) in new[] { ("player", StarterContent.PlayerEntity()), ("spear-guard", StarterContent.SpearGuardEntity()) })
        {
            var shipped = Catalog.Entities[id];
            var stripped = LegacyEntities.Strip(seed, shipped.Model.Base);
            if (id == "player")
            {
                stripped.Hurt.Regions["legs"] = new() { Pad = .05f }; // the player's tighter legs box, as the P8c file carried it
            }
            var legacy = Compile(stripped);
            Assert.True(shipped.HurtRegistered);
            Assert.False(legacy.HurtRegistered);
            foreach (var facing in new[] { 1, -1 })
            {
                var action = legacy.Actions["attack"];
                foreach (var t in new[] { action.Hits[0].Start, action.Hits[0].Finish - 1e-3f })
                {
                    var pose = new ActorPose(PoseEvaluator.Sample(legacy.Model, action.Clip, t), new(1, 0), facing);
                    var before = EntityCollision.Attack(legacy, pose, action.Hits[0]);
                    var after = EntityCollision.Attack(shipped, pose, shipped.Actions["attack"].Hits[0]);
                    Assert.Equal(before.Pose, after.Pose);
                    Assert.Equal(before.Bounds, after.Bounds);
                }
                var rest = new ActorPose(PoseEvaluator.Rest(legacy.Model), new(1, 0), facing);
                var legacyHurt = EntityCollision.Hurt(legacy, rest);
                var fresh = EntityCollision.Hurt(shipped, rest);
                Assert.Equal(legacyHurt.Select(r => r.Id), fresh.Select(r => r.Id));
                for (var i = 0; i < legacyHurt.Count; i++)
                {
                    Assert.True(Vector2.Distance(legacyHurt[i].Bounds.Min, fresh[i].Bounds.Min) < 1e-4f && Vector2.Distance(legacyHurt[i].Bounds.Max, fresh[i].Bounds.Max) < 1e-4f,
                        $"{id} {legacyHurt[i].Id} at rest facing {facing}: {legacyHurt[i].Bounds} -> {fresh[i].Bounds}");
                }
                // The intentional difference: a converted box follows its anchor control and no longer deforms with the pose.
                var walk = new ActorPose(PoseEvaluator.Sample(legacy.Model, legacy.Clip("walk"), .3, true), new(1, 0), facing);
                var legs = EntityCollision.Hurt(legacy, walk).Single(r => r.Id == "legs");
                var converted = EntityCollision.Hurt(shipped, walk).Single(r => r.Id == "legs");
                Assert.True(Vector2.Distance(converted.Bounds.Size, fresh.Single(r => r.Id == "legs").Bounds.Size) < 1e-5f, "the converted box keeps its rest size while walking");
                Assert.True(Vector2.Distance(legs.Bounds.Min, converted.Bounds.Min) > 1e-3f || Vector2.Distance(legs.Bounds.Max, converted.Bounds.Max) > 1e-3f, "the legacy box deforms with the stride, the converted one does not");
            }
        }
        // The shield defender's guard is the converted guard collider at the shell's tip, placed as the legacy guard window was.
        var defender = Catalog.Entities["shield-defender"];
        var seedLike = EntityAsset.FromJson(defender.Asset.ToJson());
        seedLike.Version = EntityAsset.LegacyVersion;
        seedLike.Behaviors = null;
        seedLike.Health = 8;
        seedLike.Mass = 1.5f;
        seedLike.Colliders.Clear();
        seedLike.Guard = new() { Prop = "scavenged-shell", Shape = Rectangle2D.Builder.FromSize(.9f, 1.4f) };
        seedLike.Hurt = new() { Layout = "standard" };
        var bash = seedLike.Actions.Single().Hits.Single();
        bash.Collider = null;
        bash.Prop = "scavenged-shell";
        bash.Shape = Rectangle2D.Builder.FromSize(.9f, 1.4f);
        bash.Damage = 2;
        bash.Sound = "hit";
        var legacyDefender = Compile(seedLike);
        var idle = new ActorPose(PoseEvaluator.Sample(legacyDefender.Model, legacyDefender.Clip("idle"), .5, true), Vector2.Zero, -1);
        Assert.Equal(legacyDefender.GuardCollider!.Region(idle).Pose, defender.GuardCollider!.Region(idle).Pose);
    }

    [Fact]
    public void ConvertFileWritesAScratchTreeWithAReportAndIsRerunnable()
    {
        var output = Path.Combine(Path.GetTempPath(), "app2d-collider-conversion", Guid.NewGuid().ToString("N"));
        // P11a converted the shipped maul brute; the conversion is exercised on its version-2 form in a scratch tree, twice.
        var source = LegacyEntities.ScratchTree(Catalog, "maul-brute");
        try
        {
            var first = ColliderConversion.ConvertFile(source, "maul-brute", output);
            Assert.True(first.Converted, string.Join("; ", first.Differences));
            Assert.Equal("written", first.Status);
            Assert.True(File.Exists(first.Output));
            Assert.True(File.Exists(Path.Combine(output, "collider-conversion-maul-brute.md")));
            Assert.Contains(first.Report, line => line.Contains("window 'hammer-head' -> attack collider 'hammer-head'"));
            Assert.Contains(first.Report, line => line.StartsWith("verified", StringComparison.Ordinal));
            Assert.Contains(first.Report, line => line.StartsWith("hurt at rest reproduced exactly", StringComparison.Ordinal));
            var converted = EntityAsset.FromJson(File.ReadAllText(first.Output!));
            Assert.Equal(EntityAsset.ColliderVersion, converted.Version); // colliders alone need version 3; behaviors (P9a) are a separate conversion
            Assert.Equal(["hammer-head", "body", "head", "legs"], converted.Colliders.Select(c => c.Id));
            var compiled = Compile(converted);
            Assert.True(compiled.HurtRegistered);
            var second = ColliderConversion.ConvertFile(source, "maul-brute", output);
            Assert.StartsWith("unchanged", second.Status);
            File.WriteAllText(first.Output!, File.ReadAllText(first.Output!).Replace("\"name\": \"Maul brute\"", "\"name\": \"Maul brute (edited)\""));
            var third = ColliderConversion.ConvertFile(source, "maul-brute", output);
            Assert.StartsWith("kept", third.Status);
            Assert.Contains("(edited)", File.ReadAllText(first.Output!));
            // The source tree was never written.
            Assert.Equal(EntityAsset.LegacyVersion, EntityAsset.FromJson(File.ReadAllText(Path.Combine(source, "entities", "maul-brute.json"))).Version);
        }
        finally
        {
            foreach (var directory in new[] { output, source })
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }

    [Fact]
    public void TheSessionConvertsAsOneUndoStepAndEditsCollidersWithValidation()
    {
        // A scratch copy of the authored tree with the maul brute in its version-2 form, so the session's save never touches the shipped files.
        var root = LegacyEntities.ScratchTree(Catalog, "maul-brute");
        try
        {
            var workspace = App2d.Core.Characters.Editing.AuthoringWorkspace.Open(root);
            Assert.Empty(workspace.LoadErrors);
            var session = new App2d.Core.Characters.Editing.EditorSession(workspace);
            session.Open("maul-brute");
        var document = session.EntityDocument!;
        Assert.Equal(EntityAsset.LegacyVersion, document.Asset.Version);
        var strike = session.Entity!.Actions["attack"];
        var pose = new ActorPose(PoseEvaluator.Sample(session.Entity.Model, strike.Clip, strike.Hits[0].Start), Vector2.Zero, 1);
        var before = EntityCollision.Attack(session.Entity, pose, strike.Hits[0]);
        Assert.True(session.ConvertColliders(), session.Message);
        Assert.Equal(EntityAsset.ColliderVersion, document.Asset.Version);
        Assert.Equal(["hammer-head", "body", "head", "legs"], document.Asset.Colliders.Select(c => c.Id));
        var after = EntityCollision.Attack(session.Entity!, pose, session.Entity!.Actions["attack"].Hits[0]);
        Assert.Equal(before.Pose, after.Pose);
        Assert.True(session.Entity.HurtRegistered);
        Assert.True(session.AddCollider(new() { Id = "reach", Use = ColliderUses.Trigger, Prop = "hammer", Shape = Circle2D.Builder.Create(.4f) }), session.Message);
        Assert.Contains(session.Entity!.Colliders, c => c.Id == "reach" && c.Use == ColliderUses.Trigger);
        Assert.False(session.AddCollider(new() { Id = "reach", Use = ColliderUses.Hurt, Control = Humanoid.Head }));
        Assert.Contains("already has a collider 'reach'", session.Message);
        Assert.False(session.AddCollider(new() { Id = "ghost", Use = ColliderUses.Hurt, Control = "tail" }));
        Assert.Contains("has no control 'tail'", session.Message);
        Assert.False(session.RemoveCollider("hammer-head"));
        Assert.Contains("re-point the window first", session.Message);
        Assert.True(session.RemoveCollider("reach"));
        session.Undo();
        Assert.Contains(document.Asset.Colliders, c => c.Id == "reach");
        session.Undo();
        session.Undo();
        Assert.Equal(EntityAsset.LegacyVersion, document.Asset.Version);
        Assert.Empty(document.Asset.Colliders);
        Assert.Equal("standard", document.Asset.Hurt.Layout);
        Assert.True(session.ConvertColliders());
        Assert.True(session.Save(document), session.Message);
        var reopened = App2d.Core.Characters.Editing.AuthoringWorkspace.Open(session.Assets.Root);
        Assert.Equal(EntityAsset.ColliderVersion, reopened.Entity("maul-brute")!.Asset.Version);
        Assert.True(reopened.CompileEntity("maul-brute")!.HurtRegistered);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
