using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class EntityRuntimeTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
    private const float Dt = 1 / 120f;

    private static ResolvedEntity Compile(EntityAsset asset) =>
        ResolvedEntity.Compile(asset, Catalog.Resolve, Catalog.Animations.GetValueOrDefault, Catalog.Props.GetValueOrDefault);

    private static string Error(EntityAsset asset) => Assert.Throws<InvalidDataException>(() => Compile(asset)).Message;

    [Fact]
    public void CapabilitiesComeFromTheControllerAndTheEntitysOwnActions()
    {
        var guard = Catalog.Entities["spear-guard"]; var player = Catalog.Entities["player"];
        // Both use the same Person base and the same shared library, which contains a jump clip.
        Assert.Equal("person", guard.Model.Base.Id); Assert.Equal("person", player.Model.Base.Id);
        Assert.False(new EntityAnimator(guard).TryStart("jump"));
        Assert.True(new EntityAnimator(player).TryStart("jump"));
        Assert.False(new EntityAnimator(player).TryStart("spin"));

        var jumpingGuard = StarterContent.SpearGuardEntity();
        jumpingGuard.Actions.Add(new() { Id = "jump", Role = "jump", Events = [new() { Id = "launch", At = new() { Marker = "launch" } }] });
        Assert.Contains("'walker' controller does not support it", Assert.Throws<InvalidDataException>(jumpingGuard.Validate).Message);
    }

    [Fact]
    public void ControllerRequirementsReplaceTheUniversalActionList()
    {
        var post = StarterContent.SpearGuardEntity(); post.Id = "post"; post.Controller.Kind = EntityControllers.Stationary; post.Actions.Clear();
        post.MotionSet = "deliberate"; post.Roles = [];
        Assert.Empty(Compile(post).Actions); // a stationary object needs neither walk nor attack

        var stalker = StarterContent.StalkerEntity(); stalker.Roles["walk"] = "stalker-lunge";
        Assert.Contains("locomotion clip 'stalker-lunge' must loop", Error(stalker));
        var set = Catalog.Models["stalker"].MotionSets[0]; var model = Catalog.Models["stalker"];
        var noWalk = StarterContent.StalkerEntity(); noWalk.MotionSet = "bare";
        var original = model.MotionSets;
        try
        {
            model.MotionSets = [.. original, new() { Id = "bare", Name = "Bare", Roles = new() { ["idle"] = "stalker-idle" } }];
            Assert.Contains("needs role 'walk', which is unassigned", Error(noWalk));
        }
        finally { model.MotionSets = original; }
        Assert.Equal("stalker-walk", set.Roles["walk"]);
    }

    [Fact]
    public void EntityOverridesWinOverTheSelectedSetAndReportTheirSource()
    {
        var player = Catalog.Entities["player"];
        Assert.Equal("set:standard", player.Roles["walk"].Source);
        var custom = StarterContent.PlayerEntity(); custom.Roles["idle"] = "person-walk";
        var compiled = Compile(custom);
        Assert.Equal(("person-walk", "entity"), (compiled.Roles["idle"].Clip.Id, compiled.Roles["idle"].Source));
        Assert.Equal("clip", compiled.Actions["attack"].Source);
        Assert.Equal("role:jump", compiled.Actions["jump"].Source);
    }

    [Fact]
    public void MissingReferencesAndMarkersAreErrorsNamingTheField()
    {
        var entity = StarterContent.SpearGuardEntity(); entity.Actions[0].Hits[0].Finish = new() { Marker = "follow-through" };
        Assert.Contains("action 'attack' hit 'spear-tip' finish: animation 'person-thrust' has no marker 'follow-through'", Error(entity));
        entity = StarterContent.SpearGuardEntity(); entity.Equipment[0].Socket = "left-ear";
        Assert.Contains("'person' has no socket 'left-ear'", Error(entity));
        entity = StarterContent.SpearGuardEntity(); entity.MotionSet = "hulking";
        Assert.Contains("has no motion set 'hulking'", Error(entity));
        entity = StarterContent.SpearGuardEntity(); entity.Hurt.Regions["tail"] = new() { Disabled = true };
        Assert.Contains("hurt.regions.tail", Error(entity));
        entity = StarterContent.StalkerEntity(); entity.Actions[0].Clip = "person-thrust";
        Assert.Contains("is for model 'person', not 'stalker'", Error(entity));
        var player = StarterContent.PlayerEntity(); player.Actions[1].Events.Clear();
        Assert.Contains("a jump needs a 'launch' event", Error(player));
    }

    [Fact]
    public void HurtLayoutOverridesDisableAndPadIndividualRegions()
    {
        var entity = StarterContent.SpearGuardEntity(); entity.Hurt.Regions["head"] = new() { Disabled = true }; entity.Hurt.Regions["legs"] = new() { Pad = .3f };
        var compiled = Compile(entity);
        Assert.Equal(new[] { "body", "legs" }, compiled.Hurt.Select(r => r.Id));
        Assert.Equal(.3f, compiled.Hurt.Single(r => r.Id == "legs").Pad);
    }

    private static List<AnimationEvent> Attack(EntityAnimator animator, int facing, Vector2 position, Func<EntityAnimator, bool>? until = null)
    {
        var events = new List<AnimationEvent>();
        Assert.True(animator.TryStart("attack"));
        while (!animator.ActionComplete && until?.Invoke(animator) != true) animator.Step(Dt, position, facing, "idle", 0, false, events);
        return events;
    }

    [Theory, InlineData(1), InlineData(-1)]
    public void SpearGripAndTipAgreeWithCollisionInBothFacings(int facing)
    {
        var guard = Catalog.Entities["spear-guard"]; var animator = new EntityAnimator(guard); var position = new Vector2(3, 0);
        var sawAnticipation = false; var sawActive = false; var sawRecovery = false;
        Assert.True(animator.TryStart("attack"));
        var hit = guard.Actions["attack"].Hits[0];
        while (!animator.ActionComplete)
        {
            animator.Step(Dt, position, facing, "idle", 0, false, []);
            var pose = animator.Pose; var spear = guard.Equipment[0];
            var frame = pose.Socket(spear.Socket);
            // The grip lands on the hand socket, and the tip is out along the facing direction.
            TestModels.Near(pose.World("right-hand"), ActorPose.PropPoint(frame, spear.Prop, spear.Prop.Grip), 1e-5f);
            var tip = ActorPose.PropPoint(frame, spear.Prop, spear.Prop.Tip);
            Assert.True((tip.X - pose.World("right-hand").X) * facing > 1, "the spear points the way the guard faces");
            var active = animator.ActiveHits().ToList();
            if (animator.ActionTime < hit.Start) { sawAnticipation = true; Assert.Empty(active); }
            else if (animator.ActionTime < hit.Finish)
            {
                sawActive = true; var region = EntityCollision.Attack(guard, pose, Assert.Single(active));
                var xs = region.Points.Select(p => p.X); var ys = region.Points.Select(p => p.Y);
                Assert.InRange(tip.X, xs.Min(), xs.Max()); Assert.InRange(tip.Y, ys.Min(), ys.Max());
                Assert.True((tip.X - position.X) * facing > 1.8f, "the strike reaches forward");
            }
            else if (animator.PreviousActionTime >= hit.Finish) { sawRecovery = true; Assert.Empty(active); }
        }
        Assert.True(sawAnticipation && sawActive && sawRecovery);
    }

    [Fact]
    public void FacingMirrorsTheWholePoseAboutThePosition()
    {
        var guard = Catalog.Entities["spear-guard"]; var right = new EntityAnimator(guard); var left = new EntityAnimator(guard); var position = new Vector2(-2, .5f);
        right.TryStart("attack"); left.TryStart("attack");
        for (var i = 0; i < 50; i++) { right.Step(Dt, position, 1, "idle", 0, false, []); left.Step(Dt, position, -1, "idle", 0, false, []); }
        foreach (var control in guard.Model.Controls.Keys)
        {
            var r = right.Pose.World(control); var l = left.Pose.World(control);
            TestModels.Near(new(2 * position.X - r.X, r.Y, r.Z), l, 1e-5f, control);
        }
        var hurtRight = EntityCollision.Hurt(guard, right.Pose); var hurtLeft = EntityCollision.Hurt(guard, left.Pose);
        for (var i = 0; i < hurtRight.Count; i++)
            Assert.Equal(hurtRight[i].Points.Select(p => p.X).Max() - position.X, position.X - hurtLeft[i].Points.Select(p => p.X).Min(), 4);
    }

    [Fact]
    public void MarkersAndEventsDispatchOnceIncludingSkippedOnes()
    {
        var guard = Catalog.Entities["spear-guard"];
        var small = Attack(new EntityAnimator(guard), 1, Vector2.Zero);
        Assert.Equal(new[] { "windup", "strike", "swing", "recover" }, small.Select(e => e.Id));
        Assert.All(small, e => Assert.Equal(1, e.ActionSequence));

        // One huge step crosses every marker at once and still dispatches each exactly once, in time order per kind.
        var animator = new EntityAnimator(guard); var events = new List<AnimationEvent>();
        animator.TryStart("attack"); animator.Step(5, Vector2.Zero, 1, "idle", 0, false, events);
        Assert.Equal(new[] { "windup", "strike", "recover", "swing" }, events.Select(e => e.Id));
        Assert.Single(animator.ActiveHits()); // a window opened and closed within the step is still tested once
        events.Clear(); animator.Step(Dt, Vector2.Zero, 1, "idle", 0, false, events);
        Assert.Empty(events); Assert.Empty(animator.ActiveHits());
    }

    [Fact]
    public void LoopingMarkersFireEveryCycleAndNeverWithoutAdvancing()
    {
        var stalker = Catalog.Entities["stalker-pest"]; var animator = new EntityAnimator(stalker); var events = new List<AnimationEvent>();
        var stride = PoseEvaluator.CycleTravel(stalker.Model, stalker.Clip("walk")!).X;
        animator.Step(Dt, Vector2.Zero, 1, "walk", stride * 2.25f, false, events); // crosses two full cycles and a quarter in one step
        Assert.Equal(new[] { "step", "step-middle", "step", "step-middle", "step" }, events.Select(e => e.Id));
        Assert.Equal(2.25, animator.RoleTime, 5);
        events.Clear();
        for (var i = 0; i < 10; i++) animator.Step(Dt, Vector2.Zero, 1, "walk", 0, false, events);
        Assert.Empty(events);
        Assert.Equal(2.25, animator.RoleTime, 5); // gait phase follows ground distance, not the clock
    }

    [Fact]
    public void PlantedFeetHoldTheirWorldPositionWhileTheControllerMoves()
    {
        foreach (var id in new[] { "player", "spear-guard", "stalker-pest" })
        {
            var entity = Catalog.Entities[id]; var animator = new EntityAnimator(entity);
            var clip = entity.Clip("walk")!; var speed = PoseEvaluator.CycleTravel(entity.Model, clip).X / clip.Duration * 1.3f;
            var position = Vector2.Zero; var held = new Dictionary<string, Vector3>(); var holds = 0;
            for (var step = 0; step < 600; step++)
            {
                position.X += speed * Dt;
                animator.Step(Dt, position, 1, "walk", speed * Dt, false, []);
                foreach (var chain in entity.Model.Chains.Values)
                {
                    if (!animator.Anchors.TryGetValue(chain.Id, out var anchor)) { held.Remove(chain.Id); continue; }
                    var foot = animator.Pose.World(chain.End);
                    Assert.True(animator.Pose.Local.Chains.Single(c => c.Chain == chain.Id).Reached, $"{id} {chain.Id} reaches its anchor");
                    TestModels.Near(anchor, foot, 1e-4f, $"{id} {chain.Id} stays on its anchor");
                    if (held.TryGetValue(chain.Id, out var previous)) { Assert.Equal(previous, anchor); holds++; }
                    held[chain.Id] = anchor;
                }
            }
            Assert.True(holds > 200, $"{id} held contacts across steps");
        }
    }

    [Fact]
    public void ContactsResetOnStartsStopsFacingAndInterruptions()
    {
        var player = Catalog.Entities["player"]; var animator = new EntityAnimator(player);
        animator.Step(Dt, Vector2.Zero, 1, "idle", 0, false, []);
        Assert.Equal(2, animator.Anchors.Count);
        var idleAnchor = animator.Anchors["left-leg"];
        animator.Step(Dt, new(.5f, 0), -1, "idle", 0, false, []); // turning around re-captures instead of dragging a foot across
        Assert.NotEqual(idleAnchor, animator.Anchors["left-leg"]);
        Assert.True(animator.TryStart("attack"));
        animator.Step(Dt, new(.5f, 0), -1, "idle", 0, false, []);
        var attackAnchor = animator.Anchors["left-leg"];
        Assert.True(animator.TryStart("attack")); // an interruption by a fresh attack starts with fresh contacts
        Assert.Empty(animator.Anchors);
        animator.Step(Dt, new(.9f, 0), -1, "idle", 0, false, []);
        Assert.NotEqual(attackAnchor, animator.Anchors["left-leg"]);
        animator.EndAction(); Assert.Empty(animator.Anchors);
        animator.Step(Dt, new(.9f, 0), -1, "walk", .01f, false, []);
        animator.Reset(); Assert.Empty(animator.Anchors); Assert.Equal("idle", animator.Role);
    }

    [Fact]
    public void FaceExpressionNeverAffectsLocomotion()
    {
        var player = Catalog.Entities["player"]; var plain = new EntityAnimator(player); var hurt = new EntityAnimator(player);
        var position = Vector2.Zero;
        for (var step = 0; step < 240; step++)
        {
            position.X += 1.8f * Dt;
            plain.Step(Dt, position, 1, "walk", 1.8f * Dt, false, []);
            hurt.Step(Dt, position, 1, "walk", 1.8f * Dt, false, [], expression: "hurt");
            foreach (var control in player.Model.Controls.Keys) Assert.Equal(plain.Pose.World(control), hurt.Pose.World(control));
        }
        Assert.Equal("hurt", hurt.Pose.Local.Expressions["head"]);
        Assert.Equal("relaxed", plain.Pose.Local.Expressions["head"]);
    }

    [Fact]
    public void HitLedgerDamagesEachTargetOncePerAttack()
    {
        var ledger = new HitLedger();
        Assert.True(ledger.TryHit(1, "spear-tip", 7));
        Assert.False(ledger.TryHit(1, "spear-tip", 7));
        Assert.True(ledger.TryHit(1, "spear-tip", 8));
        Assert.True(ledger.TryHit(2, "spear-tip", 7)); // the next attack may hit again
    }

    [Fact]
    public void CaptureAndRestoreResumeIdentically()
    {
        var player = Catalog.Entities["player"]; var a = new EntityAnimator(player); var b = new EntityAnimator(player);
        var position = Vector2.Zero;
        for (var i = 0; i < 90; i++) { position.X += .015f; a.Step(Dt, position, 1, "walk", .015f, false, []); }
        b.Restore(a.Capture(), position);
        for (var i = 0; i < 90; i++)
        {
            position.X += .015f;
            var ea = new List<AnimationEvent>(); var eb = new List<AnimationEvent>();
            if (i == 30) { a.TryStart("attack"); b.TryStart("attack"); }
            a.Step(Dt, position, 1, "walk", .015f, false, ea); b.Step(Dt, position, 1, "walk", .015f, false, eb);
            Assert.Equal(ea, eb);
            foreach (var control in player.Model.Controls.Keys) Assert.Equal(a.Pose.World(control), b.Pose.World(control));
        }
    }

    [Fact]
    public void InPlaceSamplingDropsTravelButKeepsTheBody()
    {
        var model = Catalog.Resolve("person"); var walk = Catalog.Animations["person-walk"];
        var authored = PoseEvaluator.Sample(model, walk, .3, true);
        var inPlace = PoseEvaluator.Sample(model, walk, .3, true, new() { InPlace = true });
        Assert.Equal(Vector3.Zero, inPlace.Locomotion);
        foreach (var control in model.Controls.Keys)
            TestModels.Near(authored.World(control) - authored.Locomotion, inPlace.World(control), 1e-4f, control);
    }
}
