using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

/// <summary>Planar rig authoring coordinates: Y up, serialized Z is drawing layer. Spatial mesh and part points use Vector3f.</summary>
public readonly record struct PuppetPoint(float X = 0, float Y = 0, float Z = 0)
{
    [JsonIgnore] public Vector2f XY => new(X, Y);
    [JsonIgnore] public Vector3f XYZ => new(X, Y, Z);
    public static PuppetPoint From(Vector3f p) => new(p.X, p.Y, p.Z);
    public static PuppetPoint Lerp(PuppetPoint a, PuppetPoint b, float t) => From(Vector3f.Lerp(a.XYZ, b.XYZ, t));
    public void Check(string field)
    {
        new Limit(-10000, 10000).Check(X, field + ".x");
        new Limit(-10000, 10000).Check(Y, field + ".y");
        new Limit(-32, 32).Check(Z, field + ".z");
    }
}
