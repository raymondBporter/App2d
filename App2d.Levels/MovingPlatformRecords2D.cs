namespace App2d.Levels;

public sealed record MovingPlatformDefinitionRecord2D(
    long DefinitionId,
    string Name,
    float Width,
    float Height,
    int ColorArgb);

public sealed record MovingPlatformThingRecord2D(
    long ThingId,
    long DefinitionId,
    string DefinitionName,
    string? Name,
    bool Enabled,
    float Width,
    float Height,
    int ColorArgb,
    float X,
    float Y,
    float Rotation,
    float TravelX,
    float TravelY,
    float Speed);

public sealed record NewMovingPlatformThing2D(
    long DefinitionId,
    string? Name,
    bool Enabled,
    float X,
    float Y,
    float Rotation,
    float TravelX,
    float TravelY,
    float Speed);
