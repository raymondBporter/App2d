using App2d.Core.Characters.Drawing;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Rendering.Characters;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

/// <summary>
/// The optional procedural face payload of a shape part (P2b of the character-system migration): the default expression,
/// the feature offset along the part frame's X axis and the drawing of the fixed vocabulary (<see cref="FaceExpressions"/>)
/// over the shape's bounds. Only the part that renders a face owns one, so the shared part type carries no face fields;
/// the payload owns its validation, its control references (none: the features follow the part's own frame), its
/// evaluation (<see cref="Evaluate"/>) and its drawing (<see cref="Draw"/>). The vocabulary and this procedural style
/// retire in P7/P11, when faces become animated parts with profile bindings; until then the Person head keeps its look.
/// </summary>
public sealed record FaceAppearance
{
    /// <summary>The expression that draws nothing: a clip's back view, or a variant clearing the face.</summary>
    public const string None = "none";
    public static readonly Limit OffsetLimit = new(-1, 1);

    /// <summary>The expression shown when neither gameplay nor a clip supplies one. <see cref="None"/> keeps the payload and draws nothing.</summary>
    public string Expression { get; set; } = "relaxed";
    /// <summary>Offset of the features along the part frame's X axis, in authored units.</summary>
    public float OffsetX { get; set; }

    /// <summary>A relaxed face for a payload the author has just added.</summary>
    public static FaceAppearance Starter => new();

    /// <summary>Whether a geometry can carry a face: the features are laid out over a shape's bounds, so curves cannot.</summary>
    public static bool Supports(IGeometryValue2D? geometry) => geometry is IShape2D;

    /// <summary>Whether an evaluated expression draws anything.</summary>
    public static bool Shows(string expression) => expression != None;

    /// <summary>A procedural face follows its part's frame and references no control; deleting a control never leaves it dangling.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The payload contract: every optional part payload answers for its own control references.")]
    public bool References(string controlId) => false;

    /// <summary>The payload's own vocabulary and limits, checked with the part that owns it.</summary>
    public void Validate(Func<string, bool> isControl, string field)
    {
        if (Expression is null || Shows(Expression) && !FaceExpressions.Contains(Expression))
        {
            throw new InvalidDataException($"{field}: unknown expression '{Expression}'.");
        }

        OffsetLimit.Check(OffsetX, field + ".offsetX");
    }

    /// <summary>The expression to show: a gameplay request, else the clip's animated expression, else this default.</summary>
    public string Evaluate(string? requested, string? animated) => requested ?? animated ?? Expression;

    /// <summary>
    /// Draws the features over the part's shape bounds in its frame: the same layout the Person head has always used, the
    /// features offset along the frame's X axis and lifted slightly toward the viewer so they sit on the shape.
    /// </summary>
    public void Draw(CharacterMesh mesh, FacePose pose, LayeredFrame2D frame, Rect2D bounds, float lineWidth, Color4b ink)
    {
        var offset = new Vector3(frame.XAxis * (OffsetX / MathF.Max(frame.XAxis.Length(), 1e-10f)), -.002f);
        FaceDrawing.Build(mesh, pose, p => frame.At(bounds.Center + new Vector2(p.X * bounds.Width, -p.Y * bounds.Height)) + offset, lineWidth * .6f, ink);
    }
}
