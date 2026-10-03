namespace App2d.Core.Characters;

/// <summary>Serializable appearance for an authored shape or curve. Colors are #rrggbb; null outline values inherit the asset's ink style.</summary>
public sealed record RenderMaterialDefinition2D
{
    /// <summary>Solid fill for a shape, or the center color of a thick curve. A null curve fill uses the asset's ink.</summary>
    public string? Fill { get; init; }
    public RenderOutlineDefinition2D? Outline { get; init; }

    public void Validate(string field)
    {
        if (Fill is not null) Limit.Color(Fill, field + ".fill");
        Outline?.Validate(field + ".outline");
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
