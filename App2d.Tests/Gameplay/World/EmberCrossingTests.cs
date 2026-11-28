using App2d.Contracts.Enemies;
using App2d.Contracts.Persons;
using App2d.Contracts.Simulation;
using App2d.Contracts.World;
using App2d.Core.Characters.Skeletons;
using App2d.Core.Geometry.Primitives;
using App2d.Gameplay.Characters;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Simulation;

namespace App2d.Tests.Gameplay.World;

public sealed class EmberCrossingTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void CentaurSpearCanHitAGroundedPlayerOnEitherSide(int side)
    {
        using var world = LevelBootstrap2D.LoadCampaign();
        using var game = SideScrollerSimulation2D.Create(LevelBootstrap2D.CreateSessionDefinition(world, null));
        var centaur = FindCentaur(game);
        game.Player.Reset(new(centaur.WorldObject.Position.X + side * 62f, 34f));
        var sawJab = false;
        for (var tick = 0; tick < 104; tick++)
        {
            var frame = game.Session.Advance();
            sawJab |= frame.Enemies.Any(enemy => enemy.Id == centaur.Id && enemy.IsAttacking && enemy.ActionId == CharacterAnimator.Attack);
        }

        Assert.True(sawJab);
        Assert.Equal(game.Player.Health.Maximum - 4, game.Player.Health.Current);
    }

    [Fact]
    public void CentaurTelegraphsThenGallopsWithAnIndependentSpearAttack()
    {
        using var world = LevelBootstrap2D.LoadCampaign();
        using var game = SideScrollerSimulation2D.Create(LevelBootstrap2D.CreateSessionDefinition(world, null));
        var centaur = FindCentaur(game);
        var start = centaur.WorldObject.Position;
        game.Player.Reset(new(start.X - 208f, 34f));
        var allCues = new List<string>();
        var mountedSpear = Centaur.Graft.GuestMounts[PersonSkeleton.RightGrip];
        var frontHoof = Centaur.Graft.HostJoints[QuadrupedSkeleton.NearFrontFoot];
        var lowestHoof = float.PositiveInfinity;
        var highestHoof = float.NegativeInfinity;
        var sawSpearDuringGallop = false;
        var damageEvents = 0;
        for (var tick = 0; tick < 215; tick++)
        {
            if (tick == 30)
            {
                Assert.True(centaur.TakeDamage(2, new(800f, 0f)));
                Assert.Equal(centaur.Health.Maximum - 2, centaur.Health.Current);
            }

            var frame = game.Session.Advance();
            var state = Assert.Single(frame.Enemies, enemy => enemy.Id == centaur.Id);
            damageEvents += frame.Events.Count(item => item is Damaged2D);
            allCues.AddRange(frame.Events.OfType<EnemyOccurred2D>().Select(item => item.Occurrence)
                .OfType<EntityCue2D>().Where(cue => cue.EntityId == centaur.Id).Select(cue => cue.Cue));
            if (state.ActionId == CharacterAnimator.Charge && state.Velocity.X < -240f)
            {
                var pose = state.Pose!;
                var footY = pose.World[frontHoof].Translation.Y;
                lowestHoof = MathF.Min(lowestHoof, footY);
                highestHoof = MathF.Max(highestHoof, footY);
                sawSpearDuringGallop |= centaur.Colliders.Find("spear-tip")!.Enabled && pose.Turns[mountedSpear].Z < -.7f;
            }

            if (tick < 60)
            {
                Assert.Equal(start.X, state.Position.X);
                Assert.Equal(game.Player.Health.Maximum, game.Player.Health.Current);
            }
        }

        Assert.True(highestHoof - lowestHoof > .06f);
        Assert.True(sawSpearDuringGallop);
        Assert.Contains("charge-scrape", allCues);
        Assert.True(allCues.IndexOf("charge-scrape") < allCues.IndexOf("charge-rush"));
        Assert.True(allCues.IndexOf("charge-rush") < allCues.IndexOf("charge-stop"));
        Assert.Equal(1, damageEvents);
        Assert.Equal(game.Player.Health.Maximum - 6, game.Player.Health.Current);
    }

    [Fact]
    public void StonewatchFerryCarriesAStationaryRiderAcrossTheGap()
    {
        using var world = LevelBootstrap2D.LoadCampaign();
        using var game = SideScrollerSimulation2D.Create(LevelBootstrap2D.CreateSessionDefinition(world, null));
        var ferry = Assert.Single(game.Level.Objects);
        var start = ferry.WorldObject.Position;
        game.Player.Reset(start + new Vector2f(-210f, 50f));
        for (var tick = 0; tick < 154; tick++)
        {
            game.Session.Advance();
        }

        var boarding = ferry.WorldObject.Position.X;
        Assert.InRange(boarding, 4096f, 4300f);
        game.Player.Reset(new(boarding, ferry.WorldObject.WorldBounds.Top + game.Definition.Traversal.PlayerColliderSize.Y * .5f + 2f));
        for (var tick = 0; tick < 110; tick++)
        {
            game.Session.Advance();
        }

        Assert.True(game.Player.IsGrounded);
        Assert.True(game.Player.Position.X > boarding + 65f);
        Assert.InRange(game.Player.Position.X - ferry.WorldObject.Position.X, -16f, 16f);
    }

    [Fact]
    public void SignalRequiresDefeatingTheCentaurAndTouchingTheFlag()
    {
        using var world = LevelBootstrap2D.LoadCampaign();
        using var game = SideScrollerSimulation2D.Create(LevelBootstrap2D.CreateSessionDefinition(world, null));
        var session = game.Session;
        var flag = game.Level.GoalThing!.Position;
        var centaur = FindCentaur(game);
        var atFlag = flag + new Vector2f(0f, game.Definition.Traversal.PlayerColliderSize.Y * .5f + 2f);

        game.Player.Reset(atFlag);
        var frame = session.Advance();
        Assert.False(frame.World.IsGoalUnlocked);
        Assert.Equal(centaur.Health.Maximum, frame.World.GoalGuardian?.HitPoints);
        Assert.False(Assert.Single(frame.Players).ReachedGoal);

        Assert.True(centaur.TakeDamage(centaur.Health.Maximum, Vector2f.Zero));
        game.Player.Reset(atFlag + new Vector2f(0f, 250f));
        frame = session.Advance();
        Assert.True(frame.World.IsGoalUnlocked);
        Assert.Equal(0, frame.World.GoalGuardian?.HitPoints);
        Assert.False(Assert.Single(frame.Players).ReachedGoal);

        game.Player.Reset(atFlag);
        frame = session.Advance();
        Assert.True(Assert.Single(frame.Players).ReachedGoal);
        Assert.Single(frame.Events.OfType<GoalReached2D>());
        Assert.Empty(session.Advance().Events.OfType<GoalReached2D>());
    }

    [Fact]
    public void FirstLanternRestoresThePlayerAfterAFallAndOnReload()
    {
        using var world = LevelBootstrap2D.LoadCampaign();
        var definition = LevelBootstrap2D.CreateSessionDefinition(world, null);
        using var game = SideScrollerSimulation2D.Create(definition);
        var session = game.Session;
        var command = new PlayerInput2D(game.Player.Id, default(PersonCommand2D) with { MoveX = 1f });
        var checkpoints = new List<CheckpointActivated2D>();
        for (var tick = 0; tick < 60; tick++)
        {
            checkpoints.AddRange(session.Advance(command).Events.OfType<CheckpointActivated2D>());
        }

        var first = Assert.Single(checkpoints);
        var checkpoint = game.Level.FindSavePoint(first.CheckpointId)!;
        Assert.Equal("Trailhead lantern", checkpoint.Name);
        Assert.Equal("Fernbank", WorldZone2D.FindAt(world.Zones.AsSpan(), checkpoint.Position)?.Name);

        game.Player.Reset(new(34.5f * definition.Traversal.TileSize, -40f));
        var respawns = new List<Respawned2D>();
        for (var tick = 0; tick < 180; tick++)
        {
            respawns.AddRange(session.Advance(new PlayerInput2D(game.Player.Id, default)).Events.OfType<Respawned2D>());
        }

        Assert.Equal(checkpoint.Position, Assert.Single(respawns).Position);

        using var continued = SideScrollerSimulation2D.Create(definition with { SavedProgress = new(first.CheckpointId, first.HitPoints) });
        var player = Assert.Single(continued.Session.CapturePlayers());
        Assert.Equal(first.CheckpointId, player.CheckpointId);
        Assert.Equal(checkpoint.Position, player.Person.Position);
        Assert.True(Assert.Single(continued.Session.CaptureWorld().Checkpoints, item => item.ThingId == first.CheckpointId).IsActive);
    }

    private static CharacterEnemy2D FindCentaur(SideScrollerSimulation2D game) =>
        Assert.Single(game.Level.EnemySystem.Combatants.OfType<CharacterEnemy2D>(), enemy => enemy.Type == GameCharacters.CentaurBoss);
}
