using System.Numerics;

namespace App2d.Gameplay;

public readonly record struct PlayerCommand2D(
    PlayerIntent2D Movement,
    bool CycleLeftWeapon,
    bool CycleRightWeapon,
    bool UseLeftWeapon,
    bool UseRightWeapon,
    Vector2? AimTarget,
    bool ToggleTraversalDebug);
