using App2d.Core.Geometry.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Json;
using App2d.Core.Physics;
using App2d.Core.Physics.Motion;
using App2d.Core.Rendering;
using System.Collections.Immutable;
using System.Text.Json;

namespace App2d.Core.Objects;

/// <summary>One filled shape of an object, in the object's own axes. A shape that collides is part of the object's body.</summary>
public sealed record ObjectShape2D
{
    public required IShape2D Shape { get; init; }
    /// <summary>The fill colour, as "#rrggbb".</summary>
    public string Fill { get; init; } = "#b8bdc5";
    /// <summary>A line drawn round the shape, or null for none.</summary>
    public ObjectOutline2D? Outline { get; init; }
    public bool Collides { get; init; } = true;
}

/// <summary>The line round a shape: its colour, as "#rrggbb", and its width in the object's units.</summary>
public sealed record ObjectOutline2D(string Color, float Width);

/// <summary>
/// A thing placed in a level, such as a lift or a crate: one or more filled shapes, and optionally a physics body, a surface
/// that is solid from one side only, and a path it moves along. It is stored as one JSON file beside the level.
/// </summary>
public sealed record ObjectAsset2D
{
    public const string FormatId = "app2d-object";
    public const int CurrentVersion = 1;

    public string Format { get; init; } = FormatId;
    public int Version { get; init; } = CurrentVersion;
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<ObjectShape2D> Shapes { get; init; } = [];
    public PhysicsBodySettings2D? Physics { get; init; }
    public ObjectSurface2D? Surface { get; init; }
    public CurveMotionDefinition2D? Motion { get; init; }

    /// <summary>Checks the object as read from a file. Anything wrong with its contents is reported as invalid data.</summary>
    public void Validate()
    {
        if (Format != FormatId || Version != CurrentVersion) throw new InvalidDataException($"'{Id}' is not an {FormatId} file of version {CurrentVersion}.");
        if (string.IsNullOrWhiteSpace(Id)) throw new InvalidDataException("An object needs an id.");
        if (Shapes is null || Shapes.Count == 0) throw new InvalidDataException($"Object '{Id}' needs at least one shape.");
        foreach (var item in Shapes)
        {
            if (item?.Shape is null) throw new InvalidDataException($"Object '{Id}' has a shape entry with no shape.");
            item.Shape.Validate();
            if (!ShapeBounds2D.Calculate(item.Shape).IsFinite) throw new InvalidDataException($"Object '{Id}' needs shapes of finite size.");
            ColorExtensions.FromHexRgb(item.Fill);
        }
        Physics?.Validate();
        Surface?.Validate();
        Motion?.Validate();
        if (Physics is not null) CollisionShape();
    }

    /// <summary>The shape the object's body collides as: its one colliding shape, or all of them together.</summary>
    public IShape2D CollisionShape()
    {
        var shapes = Shapes.Where(s => s.Collides).Select(s => s.Shape).ToList();
        return shapes.Count switch
        {
            0 => throw new InvalidDataException($"Object '{Id}' has a body but no shape that collides."),
            1 => shapes[0],
            _ => CompositeShape2D.Builder.Create(shapes.SelectMany(shape => shape switch
            {
                IConvexShape2D convex => [convex],
                CompositeShape2D composite => composite.Parts.ToArray().Cast<IConvexShape2D>(),
                _ => throw new InvalidDataException($"Object '{Id}' has several colliding shapes; each must be convex."),
            })),
        };
    }

    /// <summary>The rectangle around everything the object draws.</summary>
    public Rectangle2D Bounds()
    {
        var bounds = ShapeBounds2D.Calculate(Shapes[0].Shape);
        var (min, max) = (bounds.Min, bounds.Max);
        foreach (var item in Shapes.Skip(1))
        {
            bounds = ShapeBounds2D.Calculate(item.Shape);
            (min, max) = (Vector2f.Min(min, bounds.Min), Vector2f.Max(max, bounds.Max));
        }
        return Rectangle2D.Builder.Create(min, max);
    }

    /// <summary>The shape to pick and outline the object by: its body when it has one, otherwise its bounds.</summary>
    public IShape2D Outline() => Physics is not null ? CollisionShape() : Bounds();

    public string ToJson() => JsonSerializer.Serialize(this, JsonProfiles.Objects);

    public static ObjectAsset2D FromJson(string json)
    {
        var asset = JsonSerializer.Deserialize<ObjectAsset2D>(json, JsonProfiles.Objects) ?? throw new InvalidDataException("The object file is empty.");
        asset.Validate();
        return asset;
    }

    public static ObjectAsset2D Load(string path) => FromJson(File.ReadAllText(path));

    public void Save(string path)
    {
        Validate();
        File.WriteAllText(path, ToJson());
    }
}

/// <summary>One shape of an object as it is drawn: its colours and bounds, read once. The outline's width is in the object's units.</summary>
public readonly record struct ObjectPaint2D(IShape2D Shape, Rect2f Bounds, SolidColorShader Fill, Color4b? Outline, float OutlineWidth);

/// <summary>Draws an object: its shapes in order, each filled and then outlined, in the visual's own axes.</summary>
public sealed class ObjectShader2D(ObjectAsset2D asset) : IShader2D
{
    public ObjectAsset2D Asset { get; } = asset;

    public ImmutableArray<ObjectPaint2D> Paints { get; } = [.. asset.Shapes.Select(item => new ObjectPaint2D(item.Shape, ShapeBounds2D.Calculate(item.Shape),
        new SolidColorShader(ColorExtensions.FromHexRgb(item.Fill)), item.Outline is { Width: > 0 } outline ? ColorExtensions.FromHexRgb(outline.Color) : null, item.Outline?.Width ?? 0))];

    public Color4b BaseColor => Color4b.White;
}
