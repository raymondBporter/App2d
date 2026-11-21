using App2d.Core.Animation;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using System.Collections.Immutable;

namespace App2d.Core.Characters;

/// <summary>What one piece of a face is drawn as, which says how an expression's points for it are read.</summary>
public enum FaceShape
{
    /// <summary>Two points: the centre, then the two radii.</summary>
    Ellipse,
    /// <summary>The corners of a filled polygon.</summary>
    Polygon,
    /// <summary>The two ends of a stroke.</summary>
    Line,
    /// <summary>The start, control point and end of a curved stroke.</summary>
    Curve,
}

/// <summary>
/// One piece of a face: a pupil, a brow, the mouth. Its look and layer never change; its points come from the expression. A
/// piece that blinks has its radii scaled over time by <see cref="Blink"/>.
/// </summary>
public sealed record FacePiece(string Name, FaceShape Shape, RenderMaterialDefinition2D Material, float Layer = 0, Track<Vector2f>? Blink = null);

/// <summary>
/// One expression: the points of every piece, in the order of <see cref="Face.Pieces"/>, with none for a piece that is hidden.
/// Because an expression is only numbers, two can be mixed.
/// </summary>
public sealed record FaceExpression(string Name, bool Blinks, ImmutableArray<ImmutableArray<Vector2f>> Pieces);

/// <summary>
/// A face as a few simple shapes and a table of expressions, in the axes of the mount it is drawn on. It has no skeleton:
/// showing it turns one expression into ordinary attachments.
/// </summary>
public sealed record Face(Vector2f Origin, float BlinkSeconds, ImmutableArray<FacePiece> Pieces, ImmutableArray<FaceExpression> Expressions)
{
    /// <summary>How many straight segments a curved stroke is cut into.</summary>
    public const int CurveSegments = 32;

    public FaceExpression Expression(string name) => Expressions.First(e => e.Name == name);

    /// <summary>
    /// The face showing one expression, as attachments on a mount. <paramref name="seconds"/> drives the blink.
    /// <paramref name="size"/> scales the face about its <see cref="Origin"/>, for a bigger or smaller head.
    /// </summary>
    public ImmutableArray<Attachment> Attachments(FaceExpression expression, int mount, double seconds = 0, float size = 1, float layer = 0)
    {
        Vector2f Place(Vector2f point) => Origin + (point - Origin) * size;
        var pieces = ImmutableArray.CreateBuilder<Attachment>(Pieces.Length);
        for (var i = 0; i < Pieces.Length; i++)
        {
            var piece = Pieces[i];
            var points = expression.Pieces[i];
            if (points.IsEmpty) continue;
            var name = "face/" + piece.Name;
            switch (piece.Shape)
            {
                case FaceShape.Ellipse:
                    var radii = points[1] * size;
                    if (expression.Blinks && piece.Blink is { } blink) radii *= blink.Sample((float)(seconds % BlinkSeconds));
                    // A blink closes an eye to nothing for a moment; nothing is drawn then.
                    if (radii.X > 0 && radii.Y > 0) pieces.Add(new MountedShape(name, mount, Ellipse2D.Builder.Create(radii, Place(points[0])), piece.Material, layer + piece.Layer));
                    break;
                case FaceShape.Polygon:
                    pieces.Add(new MountedShape(name, mount, SimplePolygon2D.Builder.Create(points.Select(Place)), piece.Material, layer + piece.Layer));
                    break;
                case FaceShape.Line:
                    pieces.Add(new MountedPath(name, mount, [Place(points[0]), Place(points[1])], piece.Material, layer + piece.Layer));
                    break;
                default:
                    var stroke = ImmutableArray.CreateBuilder<Vector2f>(CurveSegments + 1);
                    for (var step = 0; step <= CurveSegments; step++)
                    {
                        stroke.Add(BezierMath.Quadratic(Place(points[0]), Place(points[1]), Place(points[2]), step / (float)CurveSegments));
                    }
                    pieces.Add(new MountedPath(name, mount, stroke.MoveToImmutable(), piece.Material, layer + piece.Layer));
                    break;
            }
        }
        return pieces.ToImmutable();
    }
}
