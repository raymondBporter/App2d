using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering.Textures;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

internal sealed class SwordPlayerWeapon2D(
    string name,
    string equipmentId,
    PhysicsBody2D ownerBody,
    Texture2D hudTexture,
    CombatSystem2D combat,
    PlayerPresentation2D presentation,
    ISoundEffectSink2D sounds) : MeleePlayerWeapon2D(
        name,
        equipmentId,
        hudTexture,
        ownerBody,
        new Capsule2D(Vector2.Zero, new Vector2(68f, 0f), 8f),
        new MeleeAttackProfile2D(MeleeAttack2D.FastDurationSeconds, 0.32f, 1.18f, -1.08f),
        damage: 2,
        knockback: new Vector2(520f, 285f),
        combat,
        presentation,
        sounds);
