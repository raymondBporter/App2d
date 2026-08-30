using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Physics;
using App2d.Rendering.Textures;
using System.Numerics;

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
        AxisAlignedRectangle2D.FromSize(new Vector2(56f, 72f)),
        new MeleeAttackProfile2D(
            durationSeconds: 0.35f,
            damageStartSeconds: 0.10f,
            damageEndSeconds: 0.27f,
            inputBufferSeconds: 0.10f,
            forwardOffset: 52f),
        damage: 2,
        knockback: new Vector2(520f, 285f),
        combat,
        presentation,
        sounds);
