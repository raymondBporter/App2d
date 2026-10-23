namespace App2d.Levels;

/// <summary><paramref name="Asset"/> is the object file an <c>object</c> definition places, relative to the world root; null for every other type.</summary>
public sealed record ThingDefinitionRecord2D(
    long DefinitionId,
    string TypeKey,
    string Name,
    string? Asset = null);

public sealed record PositionThingRecord2D(
    long ThingId,
    long DefinitionId,
    string TypeKey,
    string DefinitionName,
    string? Name,
    bool Enabled,
    float X,
    float Y,
    float Rotation,
    string? Asset = null);

public sealed record NewPositionThing2D(
    long DefinitionId,
    string? Name,
    bool Enabled,
    float X,
    float Y,
    float Rotation = 0f);
