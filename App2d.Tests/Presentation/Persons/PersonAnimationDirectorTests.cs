using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Simulation;
using App2d.Core.Characters.Authored;
using App2d.Presentation.Persons;
using System.Numerics;

namespace App2d.Presentation.Tests.Persons;

public sealed class PersonAnimationDirectorTests
{
    private static readonly PersonMoves Moves = PersonMoves.From(AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored"))));
    private static PersonState2D Standing => new() { HitPoints = 5, MaximumHitPoints = 5, IsGrounded = true, Facing = 1 };

    [Fact]
    public void SpellsTemporarilyShowGunOrHealingPoseWithoutChangingMeleeEquipment()
    {
        var d = new Driver();
        var state = Standing with { IsChargingPrimary = true, Spells = new(90, 90, 30, 30, false, 0, .6f, 1, .25f) };
        var charging = d.Step(state);
        Assert.Equal(PersonGear.Gun, charging.Gear);
        Assert.Equal(PersonMoves.GunCharge, charging.Overlay!.Clip.Id);
        Assert.Equal(.6, charging.Overlay.Seconds, 5);
        var firing = d.Step(Standing with { Action = new(PlayerAttackKind2D.Shot, .03f, .2f) });
        Assert.Equal(PersonGear.Gun, firing.Gear);
        Assert.Equal(PersonMoves.GunShot, firing.Overlay!.Clip.Id);
        var healing = d.Step(Standing with { Spells = state.Spells with { IsHealing = true, HealProgress = .7f } });
        Assert.Equal(PersonMoves.HealGather, healing.Key);
        Assert.Equal(PersonGear.Sword, healing.Gear);
        Assert.Equal(.7, healing.Seconds, 5);
        Assert.DoesNotContain(PersonLoadout.Worn(healing.Clip, (float)healing.Seconds, healing.Gear), prop => prop.Prop == PersonLoadout.Pistol);
        Assert.Equal(PersonMoves.Idle, d.Step(Standing).Key);
        Assert.Equal(EquipmentKind2D.Sword, d.Director.Equipment);
    }

    [Fact]
    public void DeepLandingCrouchIsReservedForHardImpacts()
    {
        var director = new PersonAnimationDirector(Moves) { HardLandingSpeed = 1045 };
        director.ApplyState(Standing with { LandingSpeedThisFrame = 760 }, Vector2.Zero, 1);
        Assert.Equal(PersonMoves.Idle, director.Frame().Key);
        director.ApplyState(Standing with { LandingSpeedThisFrame = 1060 }, Vector2.Zero, 2);
        Assert.Equal(PersonMoves.Land, director.Frame().Key);
        director.ApplyState(Standing, Vector2.Zero, 2.5);
        Assert.Equal(PersonMoves.Idle, director.Frame().Key);
    }

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
        // Compare error directly: the shorter stride puts this value on a decimal rounding boundary.
        var expected = 59 * .3 / 120 / stride * Moves[PersonMoves.Walk].Duration;
        Assert.InRange(Math.Abs(expected - walk.Seconds), 0, 1e-6); // phase follows ground covered since the walk began
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
    public void SwordSwingsPlayTheHeroActionGameplayNamesThenItsRecovery()
    {
        var d = new Driver();
        d.Step(Standing, 5);
        var cut = Moves.Swing(null); var backhand = Moves.Swing(cut.Next); var forehand = Moves.Swing(backhand.Next);
        static PersonState2D Swinging(ResolvedAction action, float elapsed = 0) => Standing with { Action = new(PlayerAttackKind2D.Melee, elapsed, action.Clip.Duration, action.Id) };
        Assert.Equal(cut.Clip.Id, d.Step(Swinging(cut)).Key);
        var mid = d.Step(Swinging(cut, cut.Clip.Duration / 2));
        Assert.Equal(cut.Clip.Duration / 2, mid.Seconds, 3); // the controller's timing maps onto the clip
        Assert.Contains((PersonLoadout.Sword, PersonLoadout.SwordSocket), PersonLoadout.Worn(mid.Clip, (float)mid.Seconds, mid.Gear));
        // A chained swing starts on the same tick the last one ends: the action changes while melee stays active.
        Assert.Equal(backhand.Clip.Id, d.Step(Swinging(backhand)).Key);
        Assert.Equal(forehand.Clip.Id, d.Step(Swinging(forehand)).Key);
        Assert.Equal(0, d.Step(Swinging(forehand)).Seconds, 3);
        d.Step(Standing);
        Assert.Equal(forehand.Recovery!.Id, d.Director.Key); // the forehand's own put-away
        Assert.Equal(PersonMoves.Idle, d.Step(Standing, 80).Key);
        Assert.Equal(cut.Clip.Id, d.Step(Swinging(cut)).Key);
    }

    [Fact]
    public void SwordRecoveryIsNotInterruptedByAnEarlierLanding()
    {
        var d = new Driver();
        d.Step(Standing with { LandingSpeedThisFrame = 8 });
        var cut = Moves.Swing(null);
        d.Step(Standing with { Action = new(PlayerAttackKind2D.Melee, .1f, cut.Clip.Duration, cut.Id) });
        for (var i = 0; i < 45; i++)
        {
            var frame = d.Step(Standing);
            Assert.Equal(cut.Recovery!.Id, frame.Key);
            Assert.True(PersonLoadout.SwordInHand(frame.PropClip, (float)frame.PropSeconds));
            Assert.Equal(i / 120.0, frame.PropSeconds, 5);
        }
    }

    [Fact]
    public void SwordRecoveryKeepsItsClockAndAttachmentThroughMovementAndLanding()
    {
        var d = new Driver();
        var cut = Moves.Swing(null);
        d.Step(Standing with { Action = new(PlayerAttackKind2D.Melee, .1f, cut.Clip.Duration, cut.Id) });
        var recovery = cut.Recovery!;
        var home = recovery.Markers.Single(m => m.Id == PersonLoadout.SheatheMarker).Time;
        for (var i = 0; i < 60; i++)
        {
            var state = i < 15 ? Standing with { LinearVelocity = new(4, 0) }
                : i < 30 ? Standing with { IsGrounded = false, LinearVelocity = new(-4, -2), Facing = -1 }
                : Standing;
            var frame = d.Step(state);
            if (i / 120.0 >= recovery.Duration) { Assert.Equal(PersonMoves.Idle, frame.Key); continue; }
            Assert.Equal(recovery.Id, frame.PropClip.Id);
            Assert.Equal(i / 120.0, frame.PropSeconds, 5);
            Assert.Equal(i / 120.0 < home, PersonLoadout.SwordInHand(frame.PropClip, (float)frame.PropSeconds));
            if (i < 30)
            {
                Assert.NotEqual(recovery.Id, frame.Key);
                Assert.Contains(PersonLoadout.SwordSocket, frame.Overlay!.Targets);
                Assert.DoesNotContain("hips", frame.Overlay.Targets);
            }
        }
    }

    [Fact]
    public void SwordSwingsLayerOverTheLegsOnceTheBodyMoves()
    {
        var d = new Driver();
        var cut = Moves.Swing(null);
        PersonState2D Swinging(PersonState2D state, float elapsed) => state with { Action = new(PlayerAttackKind2D.Melee, elapsed, cut.Clip.Duration, cut.Id) };
        var standing = d.Step(Swinging(Standing, 0));
        Assert.Equal(cut.Clip.Id, standing.Key);
        Assert.Null(standing.Overlay);
        var running = d.Step(Swinging(Standing with { LinearVelocity = new(4, 0) }, .05f));
        Assert.Equal(PersonMoves.Run, running.Key);
        Assert.Equal(cut.Clip.Id, running.PropClip.Id);
        Assert.Same(PersonLoadout.SwordUpperBody, running.Overlay!.Targets);
        // Stopping mid-swing keeps the legs' own pose rather than snapping back to the swing's planted stance.
        Assert.Equal(PersonMoves.Idle, d.Step(Swinging(Standing, .1f)).Key);
        var airborne = d.Step(Swinging(Standing with { IsGrounded = false, LinearVelocity = new(0, 3) }, 0));
        Assert.Equal(PersonMoves.Jump, airborne.Key);
        Assert.Equal(cut.Clip.Id, airborne.Overlay!.Clip.Id);
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
