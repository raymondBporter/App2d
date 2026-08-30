using System.Numerics;

namespace App2d.Gameplay;

public readonly record struct PlayerCommand2D(
    PersonCommand2D Person,
    bool ToggleTraversalDebug);
