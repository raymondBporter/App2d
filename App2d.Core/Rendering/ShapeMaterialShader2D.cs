using App2d.Core.Characters;
using App2d.Core.Rendering.Textures;
using App2d.Core.Validation;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Core.Rendering;

/// <summary>Inline appearance for a shape, independent of character controls and skeletons.</summary>
public sealed class ShapeMaterialShader2D : IShader2D
{
    public ShapeMaterialShader2D(RenderMaterialDefinition2D material, Texture2D? texture = null)
    {
        ArgGuard.ThrowIfNull(material);
        material.Validate("material");
        Material = material;
        Texture = texture;
        var fill = texture is not null ? XnaColor.White : material.Fill is { } color ? ColorExtensions.FromHexRgb(color) : XnaColor.Transparent;
        BaseColor = material.Tint is { } tint ? Multiply(fill, ColorRgba32.ParseRgba(tint).ToColor()) : fill;
        OutlineColor = material.Outline?.Color is { } outline ? ColorExtensions.FromHexRgb(outline) : new XnaColor(34, 43, 50);
    }

    public RenderMaterialDefinition2D Material { get; }
    public Texture2D? Texture { get; }
    public XnaColor BaseColor { get; }
    public XnaColor OutlineColor { get; }
    public bool HasFill => Texture is not null || Material.Fill is not null;
    public float OutlineWidth => Material.Outline?.Width ?? 1f;

    private static XnaColor Multiply(XnaColor first, XnaColor second) => new(
        (byte)(first.R * second.R / 255), (byte)(first.G * second.G / 255),
        (byte)(first.B * second.B / 255), (byte)(first.A * second.A / 255));
}
