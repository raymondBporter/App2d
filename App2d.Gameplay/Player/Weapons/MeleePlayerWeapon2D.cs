using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Physics;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay;

internal abstract class MeleePlayerWeapon2D(
    string name,
    string equipmentId,
    Texture2D hudTexture,
    PhysicsBody2D ownerBody,
    IShape2D hitboxShape,
    MeleeAttackProfile2D attackProfile,
    int damage,
    Vector2 knockback,
    CombatSystem2D combat,
    PlayerPresentation2D presentation,
    ISoundEffectSink2D sounds,
    SoundEffect2D swingSound = SoundEffect2D.SwordSwing,
    SoundEffect2D impactSound = SoundEffect2D.SwordHit) : PlayerWeapon2DBase(name, equipmentId, hudTexture)
{
    private readonly PhysicsBody2D _ownerBody = ArgGuard.RequireNotNull(ownerBody);
    private readonly CombatSystem2D _combat = ArgGuard.RequireNotNull(combat);
    private readonly PlayerPresentation2D _presentation = ArgGuard.RequireNotNull(presentation);
    private readonly ISoundEffectSink2D _sounds = ArgGuard.RequireNotNull(sounds);
    private readonly MeleeAttack2D _attack = new(new SpatialObject2D(hitboxShape), attackProfile);

    public bool IsAttackActive => _attack.IsInProgress;

    public override IEnumerable<SpatialObject2D> ActiveHitboxes
    {
        get
        {
            if (_attack.IsVisible)
                yield return _attack.WorldObject;
        }
    }

    public override float Use(Vector2? aimTarget, float facing)
    {
        if (_attack.TryStart())
            PlayAttackFeedback();
        return facing;
    }

    public override void OnDeselected() => _attack.Cancel();
    public override void Reset() => _attack.Cancel();

    public override void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        if (_attack.Update(
            deltaSeconds,
            _ownerBody.WorldObject.Transform.Position,
            facing))
        {
            PlayAttackFeedback();
        }

        if (_attack.IsDamageActive &&
            _combat.ResolveAttack(
                _attack.WorldObject,
                _attack,
                _attack.AttackId,
                damage,
                _ => new Vector2(facing * knockback.X, knockback.Y)))
        {
            _sounds.Play(impactSound);
        }
    }

    private void PlayAttackFeedback()
    {
        _presentation.PlayMeleeAttack(_attack.DurationSeconds);
        _sounds.Play(swingSound);
    }
}
