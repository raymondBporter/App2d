namespace App2d.Core.Characters;

/// <summary>
/// Hit and death reactions for one actor. Gameplay decides when they happen; the entity's optional <c>hit</c> and
/// <c>death</c> roles decide what they look like. A hit interrupts the action and staggers the actor for the hit clip's
/// length (or <see cref="DefaultStaggerSeconds"/> without one), during which the controller should not steer, so knockback
/// carries. Death plays its clip once and holds the last frame; without one the pose holds where it was.
/// </summary>
public sealed class EntityReaction
{
    public const float DefaultStaggerSeconds = .3f;

    public float Stagger { get; private set; }
    public bool Staggered => Stagger > 0;

    public void Hit(EntityAnimator animator)
    {
        animator.EndAction();
        var clip = animator.Entity.Clip(EntityControllers.Hit);
        Stagger = clip?.Duration ?? DefaultStaggerSeconds;
        if (clip is not null) animator.Play(EntityControllers.Hit);
    }

    public void Die(EntityAnimator animator)
    {
        animator.EndAction(); Stagger = 0;
        if (animator.Entity.Clip(EntityControllers.Death) is not null) animator.Play(EntityControllers.Death);
    }

    /// <summary>Counts the stagger down. True on the step it ends, when the controller applies its recovery cooldown.</summary>
    public bool Tick(float dt)
    {
        if (!Staggered) return false;
        Stagger = MathF.Max(0, Stagger - dt);
        return !Staggered;
    }

    /// <summary>The role the animator should play instead of locomotion: death when dead, hit while staggered, when assigned.</summary>
    public string? Role(ResolvedEntity entity, bool alive)
    {
        var role = !alive ? EntityControllers.Death : Staggered ? EntityControllers.Hit : null;
        return role is not null && entity.Clip(role) is not null ? role : null;
    }

    public float Capture() => Stagger;
    public void Restore(float stagger) => Stagger = stagger;
}
