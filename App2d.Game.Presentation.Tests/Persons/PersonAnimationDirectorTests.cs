using App2d.Core.Characters;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.Tests;
using System.Numerics;
using Xunit;

namespace App2d.Game.Presentation.Tests.Persons;

public sealed class PersonAnimationDirectorTests
{
    private static readonly PersonMoves Moves = PersonMoves.From(AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored"))));
    private static PersonState2D Standing => new() { HitPoints = 5, MaximumHitPoints = 5, IsGrounded = true, Facing = 1 };

    /// <summary>Feeds states at 120 Hz with a frame per tick, the way the client does; returns the last frame.</summary>
    private sealed class Driver(EquipmentKind2D equipment = EquipmentKind2D.Sword)
    {
        public PersonAnimationDirector Director { get; } = new(Moves) { Equipment = equipment };
        private long _tick;
        public Vector2 Feet;
        public PersonFrame Step(PersonState2D state, int ticks = 1)
        {
            PersonFrame frame = default;
            for (var i = 0; i < ticks; i++)
            {
                _tick++; Feet += state.LinearVelocity / 120f;
                Director.ApplyState(state with { Position = Feet }, Feet, _tick / 120.0);
                frame = Director.Frame();
            }
            return frame;
        }
    }

    [Fact]
    public void GroundMovementPicksIdleWalkAndRunAndAdvancesByDistance()
    {
        var d = new Driver();
        Assert.Equal(PersonMoves.Idle, d.Step(Standing, 10).Key);
        var walk = d.Step(Standing with { LinearVelocity = new(.3f, 0) }, 60);
        Assert.Equal(PersonMoves.Walk, walk.Key);
        var stride = PoseEvaluator.CycleTravel(Moves.Model, Moves[PersonMoves.Walk]).X;
        Assert.Equal(59 * .3 / 120 / stride * Moves[PersonMoves.Walk].Duration, walk.Seconds, 3); // phase follows ground covered since the walk began
        Assert.True(walk.Planted);
        var run = d.Step(Standing with { LinearVelocity = new(d.Director.RunThreshold + .5f, 0) }, 30);
        Assert.Equal(PersonMoves.Run, run.Key); Assert.True(run.Planted);
        // At the game's full speed the run cannot keep pace: the cycle is capped and the feet stop pinning to the ground.
        var outrun = d.Step(Standing with { LinearVelocity = new(13, 0) }, 60);
        Assert.Equal(PersonMoves.Run, outrun.Key); Assert.False(outrun.Planted);
        Assert.InRange(outrun.Seconds - run.Seconds, 0, 60 / 120.0 * d.Director.MaxCadence + 1e-6);
        var stopped = d.Step(Standing, 1);
        Assert.Equal(PersonMoves.Idle, stopped.Key);
    }

    [Fact]
    public void AirLadderAndWallStatesPickTheirClips()
    {
        var d = new Driver();
        d.Step(Standing, 5);
        Assert.Equal(PersonMoves.Jump, d.Step(Standing with { IsGrounded = false, LinearVelocity = new(0, 5) }).Key);
        Assert.Equal(PersonMoves.Fall, d.Step(Standing with { IsGrounded = false, LinearVelocity = new(0, -5) }).Key);
        Assert.Equal(PersonMoves.Land, d.Step(Standing with { LandingSpeedThisFrame = 8 }).Key);
        Assert.Equal(PersonMoves.WallGrip, d.Step(Standing with { IsGrounded = false, IsWallGripping = true }).Key);

        var climbing = Standing with { IsGrounded = false, IsClimbingLadder = true, LinearVelocity = new(0, 1.1f) };
        Assert.Equal(PersonMoves.ClimbOn, d.Step(climbing).Key);
        var climb = d.Step(climbing, 60);
        Assert.Equal(PersonMoves.Climb, climb.Key);
        Assert.InRange(climb.Seconds, .1, .4); // phase from height climbed, not the clock
        Assert.Equal(PersonMoves.Climb, d.Step(climbing with { LinearVelocity = Vector2.Zero }, 30).Key);
        Assert.Equal(PersonMoves.ClimbOff, d.Step(Standing).Key);
        Assert.Equal(PersonMoves.Idle, d.Step(Standing, 40).Key);
    }

    [Fact]
    public void SwordSwingsDrawThenFollowUpThenSheatheWhenStill()
    {
        var d = new Driver();
        d.Step(Standing, 5);
        var swing = Standing with { Action = new(PlayerAttackKind2D.Melee, 0, .35f) };
        var first = d.Step(swing);
        Assert.Equal(PersonMoves.DrawSlash, first.Key);
        var mid = d.Step(swing with { Action = new(PlayerAttackKind2D.Melee, .175f, .35f) });
        Assert.Equal(Moves[PersonMoves.DrawSlash].Duration / 2, mid.Seconds, 3); // the controller's timing maps onto the clip
        Assert.Contains((PersonLoadout.Sword, PersonLoadout.SwordSocket), PersonLoadout.Worn(mid.Clip, (float)mid.Seconds, mid.Gear));
        d.Step(Standing, 12);
        Assert.Equal(PersonMoves.Slash, d.Step(swing with { Action = swing.Action with { FollowUp = true } }).Key); // gameplay says follow-up: sword already out
        d.Step(Standing);
        Assert.Equal(PersonMoves.Sheathe, d.Director.Key);
        var done = d.Step(Standing, 60);
        Assert.Equal(PersonMoves.Idle, done.Key);
        Assert.Equal(PersonMoves.DrawSlash, d.Step(swing).Key); // long after: sheathed again, so it draws
    }

    [Fact]
    public void GunShotsLayerTheArmsOverWhateverTheLegsAreDoing()
    {
        var d = new Driver(EquipmentKind2D.Gun);
        Assert.Equal(PersonMoves.GunAim, d.Step(Standing, 5).Key);
        var walking = d.Step(Standing with { LinearVelocity = new(.3f, 0), Action = new(PlayerAttackKind2D.Shot, .05f, .2f) });
        Assert.Equal(PersonMoves.Walk, walking.Key);
        Assert.Equal(PersonMoves.GunShot, walking.Overlay!.Clip.Id);
        Assert.Same(PersonLoadout.UpperBody, walking.Overlay.Targets);
        var wall = d.Step(Standing with { IsGrounded = false, IsWallGripping = true, Action = new(PlayerAttackKind2D.Shot, .05f, .2f) });
        Assert.Equal(PersonMoves.GunWallShot, wall.Key);
        Assert.Equal(PersonMoves.GunAim, d.Step(Standing with { IsGrounded = false, LinearVelocity = new(0, -3) }).Overlay!.Clip.Id); // aiming arms while falling
    }

    [Fact]
    public void ReactionsWinOverEverythingAndDeathHolds()
    {
        var d = new Driver();
        d.Step(Standing, 5);
        d.Director.PlayHit();
        Assert.Equal(PersonMoves.Hit, d.Step(Standing with { LinearVelocity = new(.3f, 0) }).Key);
        Assert.Equal(PersonMoves.Walk, d.Step(Standing with { LinearVelocity = new(.3f, 0) }, 60).Key);
        d.Director.PlayDeath();
        var dead = Standing with { HitPoints = 0 };
        Assert.Equal(PersonMoves.Death, d.Step(dead).Key);
        Assert.True(d.Step(dead, 400).Seconds >= Moves[PersonMoves.Death].Duration);
        d.Director.Reset();
        Assert.Equal(PersonMoves.Idle, d.Step(Standing).Key);
    }
}
