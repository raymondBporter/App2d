using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

/// <summary>One placed geometry entry in a Part or rig: shape, curve, textured rectangle or mesh, with one material and binding.</summary>
public sealed record PartGeometry
{
    public string Id { get; set; } = "part";
    /// <summary>The shape or curve attached to this part.</summary>
    public IGeometryValue2D? Geometry { get; set; }
    /// <summary>Mesh geometry, mutually exclusive with the planar shape/curve value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PartMesh? Mesh { get; set; }
    [JsonIgnore] public string Kind => Mesh is null ? Geometry?.Kind ?? "?" : "mesh";
    /// <summary>Authored drawing style.</summary>
    public RenderMaterialDefinition2D? Material { get; set; }
    public string A { get; set; } = "";
    public string? B { get; set; }

    /// <summary>Optional bone whose XY rotation carries this shape's local offset and direction.</summary>
    public string? Frame { get; set; }
    /// <summary>Additional shape rotation in its bone frame, in radians.</summary>
    public float Angle { get; set; }
    /// <summary>Local geometry scale, applied before its attachment frame for every shape or curve.</summary>
    public float ScaleX { get; set; } = 1;
    public float ScaleY { get; set; } = 1;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float Depth { get; set; }

    /// <summary>Optional per-point attachments. Unbound points keep the part's ordinary rigid frame.</summary>
    public List<PartPointBinding>? Bindings { get; set; }
    /// <summary>Base sampling density for rounded paths; exact fillet boundaries are added by the curve library.</summary>
    public int CurveSections { get; set; } = 20;

    /// <summary>Hidden parts keep their controls and animation; only drawing skips them.</summary>
    public bool Hidden { get; set; }

    [JsonIgnore]
    public RenderMaterialDefinition2D RenderMaterial => Material ?? throw new InvalidDataException($"Part '{Id}' needs a material.");

    /// <summary>Resolves the entry's surface and optional instance override without changing its authored data.</summary>
    public PartGeometry ResolveMaterial(IReadOnlyDictionary<string, RenderMaterialDefinition2D> materials, RenderMaterialDefinition2D? change = null)
    {
        var material = RenderMaterial.WithOverride(change).Resolve(materials);
        if (Mesh is not null && material.Texture is not null)
            throw new InvalidDataException("Mesh textures require UV coordinates; use a textured rectangle.");
        return this with { Material = material };
    }

    /// <summary>Whether this part is attached to the control (its endpoints or bone frame); deleting the control removes such parts.</summary>
    public bool AttachedTo(string controlId) => A == controlId || B == controlId || Frame == controlId;

    /// <summary>Why deleting a control would leave a point binding dangling, or null.</summary>
    public string? Dependency(string controlId) =>
        Bindings?.Any(b => b.Control == controlId || b.Toward == controlId) == true ? "point binding" : null;

    /// <summary>Checks this part against the controls it may attach to. Shared by the prototype puppet and authored models.</summary>
    public void Validate(Func<string, bool> isControl)
    {
        if (A is null || !isControl(A))
        {
            throw new InvalidDataException("Unknown control: " + A);
        }
        if (B is not null && !isControl(B))
        {
            throw new InvalidDataException("Unknown control: " + B);
        }
        if (Frame is not null && !isControl(Frame))
        {
            throw new InvalidDataException("Unknown part frame: " + Frame);
        }
        if (Frame is not null && B is not null)
        {
            throw new InvalidDataException("A part uses either a bone frame or a toward control.");
        }
        if ((Geometry is null) == (Mesh is null) || Geometry is not null and not IShape2D and not ICurve2D)
        {
            throw new InvalidDataException("A geometry entry needs exactly one shape, curve or mesh.");
        }
        Mesh?.Validate();
        if (Mesh is not null && Material?.Texture is not null)
            throw new InvalidDataException("Mesh textures require UV coordinates; use a textured rectangle.");
        if (Geometry is IShape2D shape)
        {
            var built = shape.Validated();
            if (built.GetOutlineVertCount(48) == 0)
            {
                throw new InvalidDataException($"A {shape.Kind} cannot be drawn as a part.");
            }
        }
        if (Geometry is ICurve2D curve)
        {
            _ = curve.Validated();
        }

        if (CurveSections is < 2 or > 128)
        {
            throw new InvalidDataException("Curve sections must be between 2 and 128.");
        }

        if (Geometry is RoundedPolylineCurve2D rounded && rounded.Points.Length > 64)
        {
            throw new InvalidDataException("A character curve supports at most 64 points.");
        }

        var linkedCurve = Geometry is ICurve2D;
        if (linkedCurve && (B is null || A == B))
        {
            throw new InvalidDataException("A linked curve needs two different controls.");
        }

        if (linkedCurve && Frame is not null)
        {
            throw new InvalidDataException("A linked curve cannot use a bone frame.");
        }

        new Limit(-1000, 1000).Check(Angle, "part.angle");
        new Limit(-100, 100).Check(OffsetX, "part.offsetX");
        new Limit(-100, 100).Check(OffsetY, "part.offsetY");
        new Limit(-16, 16).Check(Depth, "part.depth");
        if (Material is null)
        {
            throw new InvalidDataException("A part needs a material.");
        }

        Material.Validate("part.material");
        if (!float.IsFinite(ScaleX) || !float.IsFinite(ScaleY))
        {
            throw new InvalidDataException("Part scale must be finite.");
        }

        if (Bindings is { Count: > 0 })
        {
            var pointCount = Geometry switch
            {
                SimplePolygon2D polygon => polygon.Vertices.Length,
                ICurve2D bezier => PartCurves2D.ControlPoints(bezier).Count,
                _ => throw new InvalidDataException("Point attachments require a cutout or Bézier curve.")
            };
            var seen = new HashSet<int>();
            foreach (var binding in Bindings)
            {
                if (binding is null || binding.Point < 0 || binding.Point >= pointCount || !seen.Add(binding.Point))
                {
                    throw new InvalidDataException("Point attachments need unique valid point indices.");
                }

                if (Geometry is RoundedPolylineCurve2D && binding.Toward is not null)
                {
                    throw new InvalidDataException("Rounded path points pin to controls; they do not use a toward frame.");
                }

                if (!isControl(binding.Control) || binding.Toward is not null && !isControl(binding.Toward))
                {
                    throw new InvalidDataException("Unknown point attachment control.");
                }

                if (binding.Control == binding.Toward)
                {
                    throw new InvalidDataException("An attachment must point toward a different control.");
                }
            }
        }
    }
}

