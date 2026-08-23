using System.Numerics;

namespace App2d.Gameplay;

public readonly record struct PlayerCommand2D(
    PlayerIntent2D Movement,
    int WeaponCycleDirection,
    bool UseWeapon,
    bool FireProjectile,
    Vector2? AimTarget,
    bool ToggleTraversalDebug);
