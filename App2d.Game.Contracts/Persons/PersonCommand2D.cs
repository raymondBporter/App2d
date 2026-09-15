namespace App2d.Gameplay.Persons;

public readonly record struct PersonCommand2D(
    PersonMovementIntent2D Movement,
    bool UsePrimaryAction,
    bool SwitchEquipment,
    bool UseSecondaryAction = false,
    bool PrimaryActionHeld = false,
    bool PrimaryActionReleased = false,
    bool DownHeld = false);
