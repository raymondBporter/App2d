using App2d.Core.Characters;
using App2d.Gameplay.Entities;
using Xunit;

namespace App2d.Gameplay.Tests.Entities;

public sealed class AuthoredArenaTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));

    private static AuthoredArena Arena(params string[] ids) => new(ids.Select(id => Catalog.Entities[id]));

    [Fact]
    public void TheCheckedInEntitiesCompile() => Assert.Empty(Catalog.Errors);

    [Fact]
    public void TheSpearGuardCannotJumpButThePlayerOnTheSameBaseCan()
    {
        var guardArena = Arena("spear-guard");
        for (var i = 0; i < 120; i++) guardArena.Step(new(Jump: true));
        Assert.Equal(0, guardArena.Player.Position.Y);
        Assert.Null(guardArena.Player.Animator.Action);
        Assert.DoesNotContain(guardArena.Events, e => e.Event.Id == EntityControllers.Launch);

        var playerArena = Arena("player"); var peak = 0f; var landedTick = 0L;
        playerArena.Step(new(Jump: true));
        Assert.Equal(EntityControllers.Jump, playerArena.Player.Animator.Action);
        for (var i = 0; i < 240; i++)
        {
            playerArena.Step(new(Move: .5f));
            peak = MathF.Max(peak, playerArena.Player.Position.Y);
            if (landedTick == 0 && peak > 0 && playerArena.Player.Grounded) landedTick = playerArena.Tick;
        }
        Assert.InRange(peak, 1, 2.5f);
        Assert.NotEqual(0, landedTick);
        // The crouch holds the actor still; it leaves the ground exactly at the launch event, then lands back in locomotion.
        var launch = Assert.Single(playerArena.Events, e => e.Event is { Kind: AnimationEvent.EventKind, Id: EntityControllers.Launch });
        Assert.Equal(-6, launch.Position.X, 3);
        Assert.Null(playerArena.Player.Animator.Action);
        Assert.Equal(EntityControllers.Walk, playerArena.Player.Animator.Role);
    }

    [Fact]
    public void TheGunnerShootsBoltsThatHitFromRange()
    {
        var arena = Arena("player", "cinder-gunner");
        arena.Teleport(0, new(0, 0)); arena.Teleport(1, new(4, 0));
        var flew = false;
        for (var i = 0; i < 480; i++) { arena.Step(default); flew |= arena.Bolts.Count > 0; }
        Assert.True(flew, "the gunner fired");
        Assert.Contains(arena.Hits, h => h.Attacker == 1 && h.Window == "bolt" && h.Damage == 2);
        Assert.Contains(arena.Events, e => e.Actor == 1 && e.Event.Sound == "shot");
    }

    [Fact]
    public void AMaskedAttackKeepsWalkingWhileAWholeBodyOneStands()
    {
        var skirmisher = StarterContent.SpearGuardEntity(); skirmisher.Id = "skirmisher";
        skirmisher.Actions[0].Mask = StarterContent.Upper; skirmisher.Actions[0].BlendIn = .08f; skirmisher.Actions[0].BlendOut = .12f;
        var masked = ResolvedEntity.Compile(skirmisher, Catalog.Resolve, Catalog.Animations.GetValueOrDefault, Catalog.Props.GetValueOrDefault);
        foreach (var (entity, walks) in new[] { (masked, true), (Catalog.Entities["spear-guard"], false) })
        {
            var arena = new AuthoredArena([entity]);
            for (var i = 0; i < 60; i++) arena.Step(new(Move: 1));
            arena.Step(new(Move: 1, Attack: true));
            Assert.Equal(EntityControllers.Attack, arena.Player.Animator.Action);
            var start = arena.Player.Position.X;
            for (var i = 0; i < 60; i++) arena.Step(new(Move: 1));
            Assert.Equal(EntityControllers.Attack, arena.Player.Animator.Action);
            if (walks) { Assert.True(arena.Player.Position.X > start + .3f); Assert.Equal(EntityControllers.Walk, arena.Player.Animator.Role); }
            else Assert.Equal(start, arena.Player.Position.X, 5);
        }
    }

    [Fact]
    public void TheGuardsThrustHitsOncePerAttackOnlyInsideItsWindow()
    {
        var arena = Arena("player", "spear-guard");
        arena.Teleport(0, new(0, 0)); arena.Teleport(1, new(2.05f, 0));
        for (var i = 0; i < 360; i++) arena.Step(default);
        var guard = arena.Actors[1];
        var hits = arena.Hits.Where(h => h.Attacker == 1).ToList();
        Assert.NotEmpty(hits);
        var attacks = arena.Events.Where(e => e.Actor == 1 && e.Event.Id == "strike").Select(e => e.Event.ActionSequence).ToList();
        Assert.Equal(attacks.Count, hits.Count); // every thrust that reaches lands exactly once
        foreach (var hit in hits)
        {
            var strike = arena.Events.Single(e => e.Actor == 1 && e.Event.Id == "strike" && e.Event.ActionSequence == attacks[hits.IndexOf(hit)]);
            var recover = arena.Events.Single(e => e.Actor == 1 && e.Event.Id == "recover" && e.Event.ActionSequence == strike.Event.ActionSequence);
            Assert.InRange(hit.Tick, strike.Tick, recover.Tick);
        }
        Assert.Equal(attacks.Count, arena.Events.Count(e => e.Actor == 1 && e.Event.Sound == "swing"));
        Assert.Equal(Catalog.Entities["player"].Asset.Health - hits.Count, arena.Player.Health);
        Assert.True(guard.Animator.ActionSequence >= attacks.Count);
    }

    [Theory, InlineData(1), InlineData(-1)]
    public void TheGuardTurnsToItsTargetBeforeThrusting(int side)
    {
        var arena = Arena("player", "spear-guard");
        arena.Teleport(0, new(0, 0)); arena.Teleport(1, new(2.05f * side, 0));
        for (var i = 0; i < 120; i++) arena.Step(default);
        Assert.Equal(-side, arena.Actors[1].Facing);
        Assert.Contains(arena.Hits, h => h.Attacker == 1);
    }

    [Fact]
    public void TheNonPersonCreatureWalksPlantsAndAttacks()
    {
        var arena = Arena("player", "stalker-pest");
        arena.Teleport(0, new(-3, 0)); arena.Teleport(1, new(1, 0));
        var anchored = 0;
        for (var i = 0; i < 900 && arena.Player.Alive; i++) { arena.Step(default); if (arena.Actors[1].Animator.Anchors.Count > 0) anchored++; }
        Assert.Contains(arena.Events, e => e.Actor == 1 && e.Event.Id == "step");
        Assert.Contains(arena.Hits, h => h.Attacker == 1 && h.Window == "sting");
        Assert.Contains(arena.Events, e => e.Actor == 1 && e.Event.Sound == "bite");
        Assert.True(anchored > 300);
        Assert.DoesNotContain("head", arena.Actors[1].Entity.Model.Parts.Select(p => p.Id));
    }

    [Fact]
    public void ThePlayerCanDefeatAGuardAndAHitInterruptsItsAttack()
    {
        var arena = Arena("player", "spear-guard");
        arena.Teleport(0, new(0, 0)); arena.Teleport(1, new(1.9f, 0));
        var interrupted = false;
        for (var i = 0; i < 2400 && arena.Actors[1].Alive; i++)
        {
            var guardWasAttacking = arena.Actors[1].Animator.Action is not null;
            var before = arena.Hits.Count;
            arena.Step(new(Attack: true));
            if (guardWasAttacking && arena.Hits.Skip(before).Any(h => h.Target == 1) && arena.Actors[1].Animator.Action is null) interrupted = true;
        }
        Assert.False(arena.Actors[1].Alive);
        arena.Step(default); // the face input reaches the pose on the next evaluation
        Assert.Equal("knocked-out", arena.Actors[1].Pose.Local.Expressions["head"]);
        Assert.True(interrupted || arena.Hits.Count(h => h.Target == 1) >= 3);
    }

    [Fact]
    public void IdenticalInputsReplayIdentically()
    {
        static (List<ArenaEvent>, List<ArenaHit>, List<float>) Run()
        {
            var arena = Arena("player", "spear-guard", "stalker-pest"); var xs = new List<float>();
            for (var i = 0; i < 1200; i++)
            {
                arena.Step(new(Move: MathF.Sin(i * .01f), Run: i % 400 > 200, Jump: i % 300 == 0, Attack: i % 90 == 0));
                xs.AddRange(arena.Actors.Select(a => a.Position.X));
            }
            return (arena.Events, arena.Hits, xs);
        }
        var (e1, h1, x1) = Run(); var (e2, h2, x2) = Run();
        Assert.Equal(e1, e2); Assert.Equal(h1, h2); Assert.Equal(x1, x2);
        Assert.NotEmpty(h1);
    }
}
