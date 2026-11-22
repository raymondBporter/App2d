using App2d.Core.Animation;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using System.Collections.Immutable;

namespace App2d.Core.Characters;

/// <summary>
/// One piece of a face: a pupil, a brow, the mouth. Its look and layer never change; its geometry comes from the expression. A
/// piece that blinks has the radii of its ellipse scaled over time by <see cref="Blink"/>.
/// </summary>
public sealed record FacePiece(string Name, AttachmentMaterial Material, float Layer = 0, Track<Vector2f>? Blink = null);

/// <summary>
/// One expression: the geometry of each piece it shows, by the piece's name. A shape is filled and a curve is stroked; a piece
/// it does not name is hidden. Because each piece keeps one kind of geometry in every expression, two expressions can be mixed.
/// </summary>
public sealed record FaceExpression(string Name, bool Blinks, params ImmutableArray<(string Piece, IGeometryValue2D Geometry)> Shows)
{
    /// <summary>The geometry this expression gives a piece, or null when it hides it.</summary>
    public IGeometryValue2D? Geometry(string piece)
    {
        foreach (var (name, geometry) in Shows)
        {
            if (name == piece) return geometry;
        }
        return null;
    }
}

/// <summary>
/// A face as a few simple shapes and a table of expressions, in the axes of the mount it is drawn on. It has no skeleton:
/// showing it turns one expression into ordinary attachments.
/// </summary>
public sealed record Face(Vector2f Origin, float BlinkSeconds, ImmutableArray<FacePiece> Pieces, ImmutableArray<FaceExpression> Expressions)
{
    /// <summary>How many straight segments a curved stroke is cut into.</summary>
    public const int CurveSegments = 32;

    /// <summary>
    /// How far a blink's scale may be from where it starts and still count as not blinking. Between two keys that hold the
    /// same value a track's sample is out by a rounding step or two, never by this much.
    /// </summary>
    private const float BlinkRest = 1e-6f;

    /// <summary>
    /// Whether an expression looks at a moment as it does at the start: no piece it shows is part-way through a blink. What it
    /// shows then does not change, so it can be built once and kept.
    /// </summary>
    public bool AtRest(FaceExpression expression, double seconds)
    {
        if (!expression.Blinks) return true;
        foreach (var piece in Pieces)
        {
            if (piece.Blink is not { } blink || expression.Geometry(piece.Name) is null) continue;
            if (Vector2f.DistanceSquared(blink.Sample((float)(seconds % BlinkSeconds)), blink.Sample(0)) > BlinkRest * BlinkRest) return false;
        }
        return true;
    }

    /// <summary>
    /// The face showing one expression, as attachments on a mount, in the order of <see cref="Pieces"/>.
    /// <paramref name="seconds"/> drives the blink. <paramref name="size"/> scales the face about its <see cref="Origin"/>, for a
    /// bigger or smaller head.
    /// </summary>
    public ImmutableArray<Attachment> Attachments(FaceExpression expression, int mount, double seconds = 0, float size = 1, float layer = 0)
    {
        Vector2f Place(Vector2f point) => Origin + (point - Origin) * size;
        var pieces = ImmutableArray.CreateBuilder<Attachment>(expression.Shows.Length);
        var shown = 0;
        foreach (var piece in Pieces)
        {
            if (expression.Geometry(piece.Name) is not { } geometry) continue;
            shown++;
            var name = "face/" + piece.Name;
            switch (geometry)
            {
                case Ellipse2D ellipse:
                    var radii = ellipse.Radii * size;
                    if (expression.Blinks && piece.Blink is { } blink) radii *= blink.Sample((float)(seconds % BlinkSeconds));
                    // A blink closes an eye to nothing for a moment; nothing is drawn then.
                    if (radii.X > 0 && radii.Y > 0) pieces.Add(new MountedShape(name, mount, ellipse with { Radii = radii, Center = Place(ellipse.Center) }, piece.Material, layer + piece.Layer));
                    break;
                case SimplePolygon2D polygon:
                    pieces.Add(new MountedShape(name, mount, SimplePolygon2D.Builder.Create(polygon.Vertices.ToArray().Select(Place)), piece.Material, layer + piece.Layer));
                    break;
                case LineSegmentCurve2D line:
                    pieces.Add(new MountedPath(name, mount, [Place(line.Start), Place(line.End)], piece.Material, layer + piece.Layer));
                    break;
                case ICurve2D curve:
                    // The curve is placed by its control points and then cut into straight pieces.
                    var stroke = new Vector2f[CurveSegments + 1];
                    Curve2D.Sample(CurveControlPoints2D.With(curve, [.. CurveControlPoints2D.Get(curve).Select(Place)]), stroke);
                    pieces.Add(new MountedPath(name, mount, [.. stroke], piece.Material, layer + piece.Layer));
                    break;
                default:
                    throw new InvalidDataException($"Expression '{expression.Name}' gives '{piece.Name}' a {geometry.GetKind()}, which a face cannot draw.");
            }
        }
        if (shown != expression.Shows.Length) throw new InvalidDataException($"Expression '{expression.Name}' names a piece this face does not have, or names one twice.");
        return pieces.ToImmutable();
    }
}
