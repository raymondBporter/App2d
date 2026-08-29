using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering.Textures;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

internal sealed class DaggerPlayerWeapon2D(string name, string equipmentId, PhysicsBody2D ownerBody, Texture2D hudTexture, CombatSystem2D combat, PlayerPresentation2D presentation, ISoundEffectSink2D sounds)
    : MeleePlayerWeapon2D(
        name,
        equipmentId,
        hudTexture,
        ownerBody,
        new Capsule2D(Vector2.Zero, new Vector2(44f, 0f), 5f),
        new MeleeAttackProfile2D(MeleeAttack2D.FastDurationSeconds, 0.21f, 0.7f, -0.45f, 14f, 1f),
        damage: 1,
        knockback: new Vector2(360f, 180f),
        combat,
        presentation,
        sounds);
