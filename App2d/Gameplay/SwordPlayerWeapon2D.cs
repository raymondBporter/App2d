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
        AxisAlignedRectangle2D.FromSize(new Vector2(56f, 72f)),
        new MeleeAttackProfile2D(
            CooldownSeconds: 0.32f,
            ForwardOffset: 52f),
        damage: 2,
        knockback: new Vector2(520f, 285f),
        combat,
        presentation,
        sounds);
