using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Physics;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

internal abstract class MeleePersonWeapon2D(
    string name,
    string equipmentId,
    Texture2D hudTexture,
    PhysicsBody2D ownerBody,
    IShape2D hitboxShape,
    MeleeAttackProfile2D attackProfile,
    int damage,
    Vector2 knockback,
    CombatFaction2D ownerFaction,
    uint targetLayer,
    CombatSystem2D combat,
    Action<float> attackStarted,
    ISoundEffectSink2D sounds,
    SoundEffect2D swingSound = SoundEffect2D.SwordSwing,
    SoundEffect2D impactSound = SoundEffect2D.SwordHit) : PersonWeapon2DBase(name, equipmentId, hudTexture)
{
    private readonly PhysicsBody2D _ownerBody = ArgGuard.RequireNotNull(ownerBody);
    private readonly CombatSystem2D _combat = ArgGuard.RequireNotNull(combat);
    private readonly Action<float> _attackStarted = ArgGuard.RequireNotNull(attackStarted);
    private readonly ISoundEffectSink2D _sounds = ArgGuard.RequireNotNull(sounds);
    private readonly MeleeAttack2D _attack = new(new SpatialObject2D(hitboxShape), attackProfile);
    private float _attackDirection = 1f;
    private float _bufferedAttackDirection = 1f;

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
        var direction = MathF.Sign(facing);
        if (_attack.IsInProgress)
            _bufferedAttackDirection = direction;
        if (_attack.TryStart())
        {
            _attackDirection = direction;
            PlayAttackFeedback();
        }
        return facing;
    }

    public override void OnDeselected() => _attack.Cancel();
    public override void Reset() => _attack.Cancel();

    public override void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        if (_attack.Update(
            deltaSeconds,
            _ownerBody.WorldObject.Transform.Position,
            _attackDirection))
        {
            _attackDirection = _bufferedAttackDirection;
            _attack.Update(
                0f,
                _ownerBody.WorldObject.Transform.Position,
                _attackDirection);
            PlayAttackFeedback();
        }

        if (_attack.IsDamageActive &&
            _combat.ResolveAttack(
                _attack.WorldObject,
                _attack,
                _attack.AttackId,
                ownerFaction,
                targetLayer,
                damage,
                _ => new Vector2(_attackDirection * knockback.X, knockback.Y)))
        {
            _sounds.Play(impactSound);
        }
    }

    private void PlayAttackFeedback()
    {
        _attackStarted(_attack.DurationSeconds);
        _sounds.Play(swingSound);
    }
}
