using App2d.Gameplay.Persons;
using System.Numerics;

namespace App2d.Gameplay.Player;

public readonly record struct PlayerCommand2D(
    PersonCommand2D Person,
    bool ToggleTraversalDebug);
