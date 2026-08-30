using System.Numerics;

namespace App2d.Gameplay;

public readonly record struct PersonCommand2D(
    PersonMovementIntent2D Movement,
    bool UseWeapon,
    Vector2? AimTarget,
    bool SwitchWeapon);
