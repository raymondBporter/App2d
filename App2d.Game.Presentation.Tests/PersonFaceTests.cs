using App2d.Core.Characters;
using App2d.Gameplay.Persons;
using System.Numerics;
using Xunit;

namespace App2d.Game.Presentation.Tests;

public sealed class PersonFaceTests
{
    private static PersonState2D Standing => new() { HitPoints = 30, MaximumHitPoints = 30, IsGrounded = true };

    [Fact]
    public void DamageOverridesCelebrationAndDeathOverridesDamage()
    {
        var face = new PersonFace2D(); face.Celebrate(); face.Hit();
        face.Update(Standing, .02f); Assert.Equal("hurt", face.Expression);
        face.Update(Standing with { HitPoints = 0 }, 0); Assert.Equal("knocked-out", face.Expression); Assert.True(face.Pose.CrossEyes);
        face.Reset(); face.Update(Standing, .1f); Assert.Equal("relaxed", face.Expression);
    }

    [Fact]
    public void SustainedDescentEscalatesButWallGripAndLandingEndPanic()
    {
        var face = new PersonFace2D(); var falling = Standing with { IsGrounded = false, LinearVelocity = new Vector2(0, -100) };
        face.Update(falling, .3f); Assert.Equal("surprised", face.Expression);
        face.Update(falling, .4f); Assert.Equal("panic", face.Expression);
        face.Update(falling with { IsWallGripping = true }, .01f); Assert.Equal("strained", face.Expression);
        face.Update(falling, .1f); Assert.NotEqual("panic", face.Expression);
        face.Land(); face.Update(Standing, .01f); Assert.Equal("strained", face.Expression);
        face.Update(Standing, .4f); Assert.Equal("relaxed", face.Expression);
    }

    [Fact]
    public void ObservationsDoNotAdvanceReactionTimeAndLowHealthReturnsAfterHit()
    {
        var face = new PersonFace2D(); var injured = Standing with { HitPoints = 5 };
        face.Hit(); for (var i = 0; i < 100; i++) face.Update(injured, 0);
        Assert.Equal("hurt", face.Expression);
        face.Update(injured, .4f); Assert.Equal("worried", face.Expression);
        Assert.Equal(FaceExpressions.Get("relaxed").Eyes, FaceExpressions.Blink(FaceExpressions.Get("relaxed"), 1).Eyes);
        Assert.InRange(FaceExpressions.Blink(FaceExpressions.Get("relaxed"), 3.62).Eyes, 0, .001f);
    }
}
