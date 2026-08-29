using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering.Textures;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

internal sealed class AxePlayerWeapon2D(string name, string equipmentId, PhysicsBody2D ownerBody, Texture2D hudTexture, CombatSystem2D combat, PlayerPresentation2D presentation, ISoundEffectSink2D sounds)
    : MeleePlayerWeapon2D(
        name,
        equipmentId,
        hudTexture,
        ownerBody,
        new Capsule2D(Vector2.Zero, new Vector2(60f, 0f), 14f),
        new MeleeAttackProfile2D(MeleeAttack2D.FastDurationSeconds, 0.42f, 1.35f, -1.2f),
        damage: 3,
        knockback: new Vector2(610f, 315f),
        combat,
        presentation,
        sounds);
