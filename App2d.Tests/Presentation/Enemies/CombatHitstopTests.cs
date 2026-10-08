using App2d.Contracts.Combat;
using App2d.Contracts.Enemies;
using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Simulation;
using App2d.Contracts.World;
using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Rendering.Textures;
using App2d.Levels;
using App2d.Presentation.Audio;
using App2d.Presentation.Persons;
using App2d.Presentation.World.Presentation;
using System.Numerics;

namespace App2d.Tests.Presentation.Enemies;

public sealed class CombatHitstopTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));
    private static readonly PersonMoves Moves = PersonMoves.From(Catalog);
    private static CombatDamage2D Hit(bool kill = false) => new(new(2), CombatFaction2D.Enemy, Vector2.Zero, kill)
    { Contact = new CombatContact2D(new(77), 4, Vector2.Zero, Vector2.UnitX, CombatImpactKind2D.Sword) { AttackerId = new(1) } };
    private static PersonFrame Swing(double seconds) => new(Moves.Swing(null).Clip.Id, Moves.Swing(null).Clip, seconds, false, null, PersonGear.Sword);

    [Fact]
    public void PlayerHoldsOnlyItsConfirmedSwordContactAndRejoinsLiveTime()
    {
        var hold = new PlayerContactHold2D(CombatHitstop2D.Curve);
        var history = new PersonFrameHistory2D();
        var start = Swing(.058); var later = Swing(.108);
        history.Record(1, start); history.Record(1.05, later);
        hold.Present(Hit(), new(99), 1);
        Assert.Equal(later, hold.Sample(history, 1.05, later));
        hold.Present(Hit() with { Contact = null }, new(1), 1);
        Assert.Equal(later, hold.Sample(history, 1.05, later));
        hold.Present(Hit(), new(1), 1);
        Assert.Equal(start, hold.Sample(history, 1, start));
        Assert.Equal(start, hold.Sample(history, 1.05, later));
        Assert.Equal(later, hold.Sample(history, 1 + CombatHitstop2D.Curve.Duration, later));
        hold.Present(Hit(), new(1), 2); hold.Reset();
        Assert.Equal(later, hold.Sample(history, 2.01, later));
    }

    [Fact]
    public void LiveLegsContinueAndNewAttacksAndReactionsInterruptTheHold()
    {
        var hold = new PlayerContactHold2D(CombatHitstop2D.Curve);
        var history = new PersonFrameHistory2D(); var start = Swing(.058);
        history.Record(0, start); hold.Present(Hit(), new(1), 0); hold.Sample(history, 0, start);
        var run = new PersonFrame(PersonMoves.Run, Moves[PersonMoves.Run], .3, true, null, PersonGear.Sword);
        var heldRun = hold.Sample(history, .04, run);
        Assert.Equal(run.Seconds, heldRun.Seconds); Assert.Equal(run.Key, heldRun.Key);
        Assert.Equal(start.Clip, heldRun.PropClip); Assert.Equal(start.Seconds, heldRun.PropSeconds);
        var next = Moves.Swing(Moves.Swing(null).Next).Clip;
        var combo = new PersonFrame(next.Id, next, 0, false, null, PersonGear.Sword);
        Assert.Equal(combo, hold.Sample(history, .05, combo));
        Assert.Equal(run, hold.Sample(history, .06, run));
        foreach (var key in new[] { PersonMoves.Hit, PersonMoves.Death, PersonMoves.Dash, PersonMoves.Climb, PersonMoves.WallGrip })
        {
            hold.Present(Hit(), new(1), 0);
            var interrupt = new PersonFrame(key, Moves[key], 0, false, null, PersonGear.Sword);
            Assert.Equal(interrupt, hold.Sample(history, .02, interrupt));
            Assert.Equal(run, hold.Sample(history, .03, run));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnemyHoldIsPerTargetPreservesRootAndExpiresOnInterruptionOrRemoval(bool kill)
    {
        var entity = Catalog.Entities["spear-guard"];
        var role = kill ? EntityControllers.Death : EntityControllers.Hit;
        var clip = entity.Clip(role)!;
        var root = new Vector2(8, 2);
        var live = new ActorPose(PoseEvaluator.Sample(entity.Model, clip, .05), root, 1);
        var state = new EnemyState2D(new(2), EnemyKind2D.Authored, new(200, 100), new(150, 20), 0, 1, true, !kill)
        { ActionId = role, ActionSeconds = .05f, AuthoredEntity = entity, AuthoredPose = live };
        var hold = new EnemyContactHold2D(CombatHitstop2D.Curve); hold.Present(Hit(kill));
        var sampled = hold.Sample([state, state with { Id = new(3) }]);
        Assert.NotSame(live, sampled[0].AuthoredPose);
        Assert.Equal(root, sampled[0].AuthoredPose!.Position); Assert.Equal(live.Facing, sampled[0].AuthoredPose!.Facing);
        Assert.Equal(state.Position, sampled[0].Position); Assert.Equal(state.Velocity, sampled[0].Velocity);
        Assert.Same(live, sampled[1].AuthoredPose);
        Assert.Same(live, hold.Sample([state with { ActionSeconds = CombatHitstop2D.Curve.Duration }])[0].AuthoredPose);
        hold.Present(Hit(kill)); hold.Sample([state with { ActionId = EntityControllers.Idle }]);
        Assert.Same(live, hold.Sample([state])[0].AuthoredPose);
        hold.Present(Hit(kill)); hold.Sample([]);
        Assert.Same(live, hold.Sample([state])[0].AuthoredPose);
        hold.Present(Hit(kill)); hold.Reset();
        Assert.Same(live, hold.Sample([state])[0].AuthoredPose);
    }

    [Fact]
    public void EnemyPresenterKeepsTheHoldBetweenTicksAndReleasesToTheAuthoritativePose()
    {
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var scene = new Scene2D();
        using var view = new EnemyPresentation2D(scene, textures, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new SilentSounds());
        var entity = Catalog.Entities["spear-guard"];
        var pose = new ActorPose(PoseEvaluator.Sample(entity.Model, entity.Clip(EntityControllers.Hit), .03), new(3, 2), 1);
        var state = new EnemyState2D(new(2), EnemyKind2D.Authored, new(200, 100), Vector2.Zero, 0, 1, true, true)
        { ActionId = EntityControllers.Hit, ActionSeconds = .03f, AuthoredEntity = entity, AuthoredPose = pose };
        view.PresentContact(Hit()); view.ApplyState([state], [], 120);
        var visual = Assert.Single(scene);
        var shader = Assert.IsType<AuthoredCharacterShader>(visual.Shader);
        var held = shader.Pose!.Points["head"];
        view.Advance(.02f);
        Assert.Equal(held, shader.Pose.Points["head"]);
        Assert.Equal(pose.Position * GameWorldUnits2D.WorldUnitsPerAuthoredUnit, visual.Transform.Position);
        view.ResetContact(); view.ApplyState([state], [], 126);
        Assert.Same(pose.Local, shader.Pose);
        view.ApplyState([], [], 127); Assert.Empty(scene);
    }

    private sealed class SilentSounds : ISoundEffectSink2D { public void Play(SoundEffect2D effect) { } }

    [Fact]
    public void PlayerPresenterUsesTheHoldBetweenTicksAndStillFollowsPositionAndFacing()
    {
        using var view = new AuthoredPersonPresentation2D(new Scene2D(), Moves, TraversalMetricsLoader2D.Load(TestAssetPath.Root));
        view.Equip(EquipmentKind2D.Sword);
        var state = new PersonState2D
        {
            HitPoints = 5,
            MaximumHitPoints = 5,
            IsGrounded = true,
            Facing = 1,
            Action = new(PlayerAttackKind2D.Melee, .058f, Moves.Swing(null).Clip.Duration, Moves.Swing(null).Id)
        };
        view.PresentContact(Hit(), new(1), 120);
        view.ApplyState(state, 120, 0, false, true);
        var initial = view.Pose!;
        var (Guard, Tip) = PersonLoadout.Blade(initial.Local, Moves.Props[PersonLoadout.Sword], Moves.Model.Base.Sockets.Single(s => s.Id == PersonLoadout.SwordSocket));
        view.Advance(.025f);
        var held = PersonLoadout.Blade(view.Pose!.Local, Moves.Props[PersonLoadout.Sword], Moves.Model.Base.Sockets.Single(s => s.Id == PersonLoadout.SwordSocket));
        Assert.InRange(Vector3.Distance(Tip, held.Tip), 0, .0001f);
        view.ApplyState(state with { Position = new(20, 0), Facing = -1, Action = state.Action with { ElapsedSeconds = .108f } }, 126, 1, false, true);
        Assert.NotEqual(initial.Position, view.Pose!.Position); Assert.Equal(-1, view.Pose.Facing);
        view.PlayHit(); view.Advance(.01f);
        Assert.Equal(PersonMoves.Hit, view.Director.Key);
    }
}
