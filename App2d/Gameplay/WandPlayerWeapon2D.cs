using App2d.Engine;
using App2d.Engine.Collision;
using App2d.Engine.Physics;
using App2d.Engine.Rendering.Textures;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

internal sealed class WandPlayerWeapon2D(
    Scene2D scene,
    PhysicsBody2D ownerBody,
    TextureCache2D textures,
    Texture2D hudTexture,
    CollisionSystem2D collision,
    uint worldLayer,
    CombatSystem2D combat,
    PlayerPresentation2D presentation,
    ISoundEffectSink2D sounds) : FireballPlayerWeapon2D(
        "WAND A",
        "right-hand-wand-a",
        scene,
        ownerBody,
        textures,
        hudTexture,
        collision,
        worldLayer,
        combat,
        presentation,
        sounds);
