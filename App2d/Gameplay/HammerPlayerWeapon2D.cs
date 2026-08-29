using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering.Textures;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

internal sealed class HammerPlayerWeapon2D(
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
        new Circle2D(23f),
        new MeleeAttackProfile2D(0.36f, 0.58f, 1.55f, -1.35f, 6f, 3f, 54f),
        damage: 4,
        knockback: new Vector2(720f, 390f),
        combat,
        presentation,
        sounds,
        SoundEffect2D.HammerWindup,
        SoundEffect2D.HammerImpact);
