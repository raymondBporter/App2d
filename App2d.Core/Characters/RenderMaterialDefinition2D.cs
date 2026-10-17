namespace App2d.Core.Characters;

/// <summary>Serializable appearance for an authored shape or curve. Colors are #rrggbb; null outline values inherit the asset's ink style.</summary>
public sealed record RenderMaterialDefinition2D
{
    /// <summary>Named material on the owning model. Inline values override its defaults.</summary>
    public string? Asset { get; init; }
    /// <summary>Surface artwork in normalized shape coordinates; null inherits, an empty list clears it.</summary>
    public List<PartPaint>? Paint { get; init; }
    /// <summary>Solid fill for a shape, or the center color of a thick curve. A null curve fill uses the asset's ink.</summary>
    public string? Fill
    {
        get; init;
    }
    public RenderOutlineDefinition2D? Outline
    {
        get; init;
    }
    /// <summary>Image path relative to the authored asset root.</summary>
    public string? Texture
    {
        get; init;
    }
    public string? Tint
    {
        get; init;
    }

    /// <summary>Apply supplied appearance fields; selecting a different named material replaces the surface.</summary>
    public RenderMaterialDefinition2D WithOverride(RenderMaterialDefinition2D? value) => value is null ? this
        : value.Asset is not null && value.Asset != Asset ? value : this with
    {
        Paint = value.Paint ?? Paint,
        Fill = value.Fill ?? Fill,
        Texture = value.Texture ?? Texture,
        Tint = value.Tint ?? Tint,
        Outline = value.Outline is null ? Outline : (Outline ?? new RenderOutlineDefinition2D()) with
        {
            Color = value.Outline.Color ?? Outline?.Color,
            Width = value.Outline.Width ?? Outline?.Width,
        },
    };

    /// <summary>Resolve shared surface data without changing the authored reference or copying its paint.</summary>
    public RenderMaterialDefinition2D Resolve(IReadOnlyDictionary<string, RenderMaterialDefinition2D> materials)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        RenderMaterialDefinition2D Read(RenderMaterialDefinition2D value)
        {
            if (value.Asset is not { } id) return value;
            if (!visited.Add(id)) throw new InvalidDataException($"Material reference cycle at '{id}'.");
            if (!materials.TryGetValue(id, out var source) || source is null) throw new InvalidDataException($"Unknown material '{id}'.");
            return Read(source).WithOverride(value with { Asset = null });
        }
        var resolved = Read(this);
        resolved.Validate("material");
        return resolved with { Asset = Asset };
    }

    public void Validate(string field)
    {
        if (Asset is not null) Authored.AuthoredAsset.RequireId(Asset, field + ".asset");
        PartPaint.Check(Paint);
        if (Fill is not null)
        {
            Limit.Color(Fill, field + ".fill");
        }

        Outline?.Validate(field + ".outline");
        if (Tint is not null)
        {
            Authored.SkeletonAppearance2D.Color(Tint);
        }

        if (Texture is not null && (string.IsNullOrWhiteSpace(Texture) || Path.IsPathRooted(Texture) || Texture.Replace('\\', '/').Split('/').Any(p => p is ".." or "." or "")))
        {
            throw new InvalidDataException($"{field}.texture must be a path beneath the authored asset root.");
        }
    }
}

/// <summary>World-unit outline style. Null color or width inherits the owning model or prop's ink setting.</summary>
public sealed record RenderOutlineDefinition2D
{
    public string? Color
    {
        get; init;
    }
    public float? Width
    {
        get; init;
    }

    public void Validate(string field)
    {
        if (Color is not null)
        {
            Limit.Color(Color, field + ".color");
        }

        if (Width is { } width)
        {
            new Limit(0, 1).Check(width, field + ".width");
        }
    }
}
