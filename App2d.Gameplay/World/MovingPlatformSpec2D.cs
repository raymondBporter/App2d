using System.Numerics;
using SkiaSharp;

namespace App2d.Gameplay.World;

/// <summary>Storage-neutral authored input used to construct one moving platform.</summary>
public sealed record MovingPlatformSpec2D(
    long ThingId,
    string? Name,
    bool Enabled,
    Vector2 Position,
    Vector2 Travel,
    Vector2 Size,
    float Speed,
    SKColor Color);
