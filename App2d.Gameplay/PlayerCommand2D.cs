using System.Numerics;

namespace App2d.Gameplay;

public readonly record struct PlayerCommand2D(
    PlayerIntent2D Movement,
    bool UseWeapon,
    Vector2? AimTarget,
    bool SwitchWeapon,
    bool ToggleTraversalDebug);
