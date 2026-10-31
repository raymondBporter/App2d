using App2d.Core.Characters.Geometry;
using App2d.Core.Json;
using App2d.Core.Mathematics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One occurrence of a library part on a character: the instance ID (stable, addressed by overrides), the asset ID
/// (which library resource) and the socket ID (the wearer's named attachment location), plus this instance's placement on
/// the socket, mirroring, leaf fitting and local overrides. Two instances of one asset are independent; neither edits it.
/// </summary>
public sealed record PartInstance
{
    public string Id { get; set; } = "";
    public string Asset { get; set; } = "";
    public string Socket { get; set; } = "";
    /// <summary>Offset of the asset's origin in the socket's axes.</summary>
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>Rotation in radians in the socket's axes.</summary>
    public float Angle { get; set; }
    /// <summary>Uniform size, part of the similarity.</summary>
    public float Scale { get; set; } = 1;
    /// <summary>Reflects the asset across the socket's Y axis: the same glove on the other hand.</summary>
    public bool Mirror { get; set; }
    /// <summary>Drawing-layer offset from the socket.</summary>
    public float Layer { get; set; }
    /// <summary>Leaf geometry fit (plan section 3.4): stretches this instance's artwork, or repositions its layout, as the asset declares; nothing inherits it.</summary>
    public float FitX { get; set; } = 1;
    public float FitY { get; set; } = 1;
    /// <summary>Inline material override over every piece of the payload (a fill, a shared material of the asset, a tint); null inherits.</summary>
    public RenderMaterialDefinition2D? Material { get; set; }
    public bool Hidden { get; set; }
    /// <summary>Animated assets: the expression this instance shows by default, over the asset's own default (P7a).</summary>
    public string? Expression { get; set; }
    /// <summary>The module's dimension inputs this instance sets (P10b): parameter id → value, each exposed as the attachment property <c>input.&lt;id&gt;</c>.</summary>
    public Dictionary<string, float> Inputs { get; set; } = [];

    /// <summary>The placement with the fit in the frame (<see cref="PartFittings.Stretch"/>) or left to the asset's layout (<see cref="PartFittings.Layout"/>).</summary>
    public LayeredFrame2D Placement(bool stretch)
    {
        var fit = stretch ? new Vector2(FitX, FitY) : Vector2.One;
        var local = LayeredFrame2D.Local(fit * Scale, Angle, new(X, Y), Layer);
        return Mirror ? local.Then(LayeredFrame2D.Local(new(-1, 1), 0, Vector2.Zero, 0)) : local;
    }

    public void Validate(string field)
    {
        AuthoredAsset.RequireId(Id, field + " id");
        AuthoredAsset.RequireId(Asset, field + " asset");
        AuthoredAsset.RequireId(Socket, field + " socket");
        new Limit(-100, 100).Check(X, field + " x");
        new Limit(-100, 100).Check(Y, field + " y");
        new Limit(-1000, 1000).Check(Angle, field + " angle");
        new Limit(.001f, 100).Check(Scale, field + " scale");
        new Limit(-16, 16).Check(Layer, field + " layer");
        new Limit(-100, 100).Check(FitX, field + " fitX");
        new Limit(-100, 100).Check(FitY, field + " fitY");
        if (FitX == 0 || FitY == 0)
        {
            throw new InvalidDataException($"{field}: a zero fit collapses the artwork.");
        }

        if (Expression is not null)
        {
            AuthoredAsset.RequireId(Expression, field + " expression");
        }

        if (Inputs is null)
        {
            throw new InvalidDataException($"{field}: inputs cannot be null.");
        }

        foreach (var (input, value) in Inputs)
        {
            AuthoredAsset.RequireId(input, field + " input");
            new Limit(-10000, 10000).Check(value, $"{field} input '{input}'");
        }

        Material?.Validate(field + " material");
    }
}
