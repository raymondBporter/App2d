using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.IO;
using App2d.Core.Json;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

/// <summary>Planar rig authoring coordinates: Y up, serialized Z is drawing layer. Spatial mesh and part points use Vector3f.</summary>
public readonly record struct PuppetPoint(float X = 0, float Y = 0, float Z = 0)
{
    [JsonIgnore] public Vector2 XY => new(X, Y);
    [JsonIgnore] public Vector3 XYZ => new(X, Y, Z);
    [JsonIgnore] public LayeredPoint2D Layered => new(XY, new(Z));
    public static PuppetPoint From(Vector3 p) => new(p.X, p.Y, p.Z);
    public static PuppetPoint From(LayeredPoint2D p) => new(p.Position.X, p.Position.Y, p.Layer.Order);
    public static PuppetPoint Lerp(PuppetPoint a, PuppetPoint b, float t) => From(Vector3.Lerp(a.XYZ, b.XYZ, t));
    public void Check(string field)
    {
        new Limit(-10000, 10000).Check(X, field + ".x");
        new Limit(-10000, 10000).Check(Y, field + ".y");
        new Limit(-32, 32).Check(Z, field + ".z");
    }
}
