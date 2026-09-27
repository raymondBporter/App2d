using App2d.Game.Presentation.World.Presentation;
using App2d.Gameplay.Combat;
using App2d.Rendering;
using System.Numerics;
using Xunit;

namespace App2d.Game.Presentation.Tests.Enemies;

public sealed class CombatContactPresentationTests
{
    private static CombatDamage2D Hit(Vector2 direction, bool kill = false) => new(new(2), CombatFaction2D.Enemy, new(100, 50), kill)
        { Contact = new(new(1), 4, new(90, 60), direction, CombatImpactKind2D.Sword) };

    [Fact]
    public void BurstsFollowContactDirectionExpireAndReuseTheirSceneObjects()
    {
        var scene = new Scene2D();
        using var effects = new CombatContactPresentation2D(scene);
        effects.Present(Hit(Vector2.UnitX) with { Contact = null });
        Assert.Empty(scene);
        effects.Present(Hit(Vector2.UnitX));
        var right = scene.Select(v => v.Transform.Position).ToArray();
        Assert.All(scene, v => Assert.True(v.IsVisible));
        effects.Advance(.2f);
        Assert.All(scene, v => Assert.False(v.IsVisible));
        effects.Present(Hit(-Vector2.UnitX));
        var left = scene.Select(v => v.Transform.Position).ToArray();
        Assert.Equal(right.Length, left.Length);
        for (var i = 0; i < right.Length; i++) Assert.True(Vector2.Distance(new(180, 120), right[i] + left[i]) < .001f);
        effects.Reset();
        Assert.All(scene, v => Assert.False(v.IsVisible));
        effects.Dispose();
        Assert.Empty(scene);
    }

    [Fact]
    public void ZeroDirectionUsesTheSameHeadingAsPositiveX()
    {
        var scene = new Scene2D();
        using var effects = new CombatContactPresentation2D(scene);
        effects.Present(Hit(Vector2.UnitX));
        var expected = scene.Select(v => (v.Transform.Position, v.Transform.Rotation)).ToArray();
        effects.Reset();

        effects.Present(Hit(Vector2.Zero));

        Assert.All(scene, v => Assert.True(v.IsVisible));
        Assert.Equal(expected, scene.Select(v => (v.Transform.Position, v.Transform.Rotation)).ToArray());
    }

    [Fact]
    public void MultipleTargetsHaveIndependentEffectsAndKillsHoldLonger()
    {
        var scene = new Scene2D();
        using var effects = new CombatContactPresentation2D(scene);
        effects.Present(Hit(Vector2.UnitX));
        var count = scene.Count();
        effects.Present(Hit(-Vector2.UnitX, true));
        Assert.Equal(count * 2, scene.Count());
        effects.Advance(.13f);
        Assert.Equal(count, scene.Count(v => v.IsVisible));
        effects.Enabled = false; effects.Advance(0);
        Assert.DoesNotContain(scene, v => v.IsVisible);
        effects.Enabled = true; effects.Advance(.1f);
        Assert.DoesNotContain(scene, v => v.IsVisible);
    }
}
