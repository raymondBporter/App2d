namespace App2d.Core.Characters;

/// <summary>Serializable appearance for an authored shape or curve. Colors are #rrggbb; null outline values inherit the asset's ink style.</summary>
public sealed record RenderMaterialDefinition2D
{
    /// <summary>Solid fill for a shape, or the center color of a thick curve. A null curve fill uses the asset's ink.</summary>
    public string? Fill { get; init; }
    public RenderOutlineDefinition2D? Outline { get; init; }
    /// <summary>Image path relative to the authored asset root.</summary>
    public string? Texture { get; init; }
    public string? Tint { get; init; }

    /// <summary>Apply only the values supplied by an appearance override.</summary>
    public RenderMaterialDefinition2D WithOverride(RenderMaterialDefinition2D? value) => value is null ? this : this with
    {
        Fill = value.Fill ?? Fill,
        Texture = value.Texture ?? Texture,
        Tint = value.Tint ?? Tint,
        Outline = value.Outline is null ? Outline : (Outline ?? new RenderOutlineDefinition2D()) with
        {
            Color = value.Outline.Color ?? Outline?.Color,
            Width = value.Outline.Width ?? Outline?.Width,
        },
    };

    public void Validate(string field)
    {
        if (Fill is not null) Limit.Color(Fill, field + ".fill");
        Outline?.Validate(field + ".outline");
        if (Tint is not null) Authored.SkeletonAppearance2D.Color(Tint);
        if (Texture is not null && (string.IsNullOrWhiteSpace(Texture) || Path.IsPathRooted(Texture) || Texture.Replace('\\', '/').Split('/').Any(p => p is ".." or "." or "")))
            throw new InvalidDataException($"{field}.texture must be a path beneath the authored asset root.");
    }
}

/// <summary>World-unit outline style. Null color or width inherits the owning model or prop's ink setting.</summary>
public sealed record RenderOutlineDefinition2D
{
    public string? Color { get; init; }
    public float? Width { get; init; }

    public void Validate(string field)
    {
        if (Color is not null) Limit.Color(Color, field + ".color");
        if (Width is { } width) new Limit(0, 1).Check(width, field + ".width");
    }
}
