using System.Numerics;

namespace App2d.Gameplay.World;

public enum WorldThingKind2D
{
    PlayerSpawn,
    Goal,
    Shieldback,
    BoilerBrute,
    Rival,
    TumbleProp
}

/// <summary>Minimal storage-neutral input for a code-configured positioned thing.</summary>
public sealed record WorldThingSpec2D(
    long ThingId,
    WorldThingKind2D Kind,
    string? Name,
    bool Enabled,
    Vector2 Position);
