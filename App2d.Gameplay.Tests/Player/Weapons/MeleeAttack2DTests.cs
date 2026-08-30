using System.Numerics;
using App2d.Core;
using App2d.Core.Geometry;
using Xunit;

namespace App2d.Gameplay.Tests.Player.Weapons;

public sealed class MeleeAttack2DTests
{
    [Fact]
    public void DamageWindowTracksTheVisibleMiddleOfTheSwing()
    {
        var attack = CreateAttack();

        Assert.True(attack.TryStart());
        Assert.True(attack.IsInProgress);
        Assert.False(attack.IsDamageActive);

        Assert.False(attack.Update(0.09f, Vector2.Zero, 1f));
        Assert.False(attack.IsDamageActive);

        Assert.False(attack.Update(0.02f, Vector2.Zero, 1f));
        Assert.True(attack.IsDamageActive);

        Assert.False(attack.Update(0.15f, Vector2.Zero, 1f));
        Assert.True(attack.IsDamageActive);

        Assert.False(attack.Update(0.02f, Vector2.Zero, 1f));
        Assert.True(attack.IsDamageActive);

        Assert.False(attack.Update(0.01f, Vector2.Zero, 1f));
        Assert.False(attack.IsDamageActive);
        Assert.True(attack.IsInProgress);
    }

    [Fact]
    public void LatePressStartsBufferedSwingAfterRecovery()
    {
        var attack = CreateAttack();
        attack.TryStart();
        attack.Update(0.27f, Vector2.Zero, 1f);

        Assert.False(attack.TryStart());
        Assert.Equal(1, attack.AttackId);

        Assert.True(attack.Update(0.081f, Vector2.Zero, 1f));
        Assert.Equal(2, attack.AttackId);
        Assert.True(attack.IsInProgress);
        Assert.False(attack.IsDamageActive);
    }

    [Fact]
    public void EarlyPressExpiresInsteadOfRestartingTheAnimation()
    {
        var attack = CreateAttack();
        attack.TryStart();

        Assert.False(attack.TryStart());
        Assert.False(attack.Update(0.11f, Vector2.Zero, 1f));
        Assert.Equal(1, attack.AttackId);

        Assert.False(attack.Update(0.25f, Vector2.Zero, 1f));
        Assert.Equal(1, attack.AttackId);
        Assert.False(attack.IsInProgress);

        Assert.False(attack.Update(0.001f, Vector2.Zero, 1f));
        Assert.False(attack.IsDamageActive);
    }

    [Fact]
    public void DamageWindowUsesOneAttackIdAcrossMultipleUpdates()
    {
        var attack = CreateAttack();
        attack.TryStart();
        attack.Update(0.11f, Vector2.Zero, 1f);

        var attackId = attack.AttackId;
        Assert.True(attack.IsDamageActive);

        attack.Update(0.05f, Vector2.Zero, 1f);

        Assert.True(attack.IsDamageActive);
        Assert.Equal(attackId, attack.AttackId);
    }

    private static MeleeAttack2D CreateAttack() =>
        new(
            new SpatialObject2D(
                AxisAlignedRectangle2D.FromSize(new Vector2(56f, 72f))),
            new MeleeAttackProfile2D(
                durationSeconds: 0.35f,
                damageStartSeconds: 0.10f,
                damageEndSeconds: 0.27f,
                inputBufferSeconds: 0.10f,
                forwardOffset: 52f));
}
