using System.Numerics;

namespace App2d.Gameplay.Persons;

public readonly record struct PersonCommand2D(
    PersonMovementIntent2D Movement,
    bool UsePrimaryAction,
    Vector2? AimTarget,
    bool SwitchEquipment,
    bool UseSecondaryAction = false);
