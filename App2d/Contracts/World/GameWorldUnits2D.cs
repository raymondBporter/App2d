namespace App2d.Contracts.World;

/// <summary>
/// Converts authored character coordinates to game world coordinates. World units are
/// independent of device pixels; the camera owns that separate conversion.
/// </summary>
public static class GameWorldUnits2D
{
    public const float WorldUnitsPerAuthoredUnit = 40f;

    public static float AuthoredToWorld(float value) => value * WorldUnitsPerAuthoredUnit;
    public static Vector2f AuthoredToWorld(Vector2f value) => value * WorldUnitsPerAuthoredUnit;
    public static float WorldToAuthored(float value) => value / WorldUnitsPerAuthoredUnit;
    public static Vector2f WorldToAuthored(Vector2f value) => value / WorldUnitsPerAuthoredUnit;
}
