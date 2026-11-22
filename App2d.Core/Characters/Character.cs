using App2d.Core.Animation;
using App2d.Core.Kinematics;
using System.Collections.Immutable;

namespace App2d.Core.Characters;

/// <summary>
/// A thing worn or held: its drawn pieces on a mount. <see cref="Muzzle"/> is the point a shot leaves it from, in that
/// mount's axes, on a thing that shoots. <see cref="Behind"/> is what is drawn in its place while the character is seen from behind, such as the
/// back of its hair.
/// </summary>
public sealed record Worn(string Name, int Mount, ImmutableArray<Attachment> Pieces, Vector2f? Muzzle = null, ImmutableArray<Attachment> Behind = default);

/// <summary>
/// A character's face: its pieces and expressions, the mount it is drawn on, its size and how far in front of that mount it
/// sits. What each expression draws is built once here and handed out again every frame; only while an eye is part-way
/// through a blink is it built afresh.
/// </summary>
public sealed class CharacterFace
{
    private readonly Dictionary<string, (FaceExpression Expression, ImmutableArray<Attachment> AtRest)> _expressions = [];

    public CharacterFace(Face face, int mount, float size = 1, float layer = 0, string @default = "relaxed")
    {
        (Face, Mount, Size, Layer, Default) = (face, mount, size, layer, @default);
        foreach (var expression in face.Expressions) _expressions.Add(expression.Name, (expression, face.Attachments(expression, mount, 0, size, layer)));
    }

    public Face Face { get; }
    public int Mount { get; }
    public float Size { get; }
    public float Layer { get; }
    /// <summary>The expression worn when nothing asks for another.</summary>
    public string Default { get; }

    public bool Has(string expression) => _expressions.ContainsKey(expression);

    /// <summary>The pieces to draw for an expression at a moment; <paramref name="seconds"/> drives the blink.</summary>
    public ImmutableArray<Attachment> Attachments(string expression, double seconds)
    {
        var (shown, atRest) = _expressions[expression];
        return Face.AtRest(shown, seconds) ? atRest : Face.Attachments(shown, Mount, seconds, Size, Layer);
    }
}

/// <summary>
/// What a character looks like and how it moves: a skeleton, what is drawn on it and the clips it plays by role. One is made
/// per kind of character and shared by every one of that kind.
/// </summary>
public class Character
{
    public required string Name { get; init; }
    public required Skeleton2D Skeleton { get; init; }
    /// <summary>The body itself.</summary>
    public ImmutableArray<Attachment> Body { get; init; } = [];
    /// <summary>What it wears and holds. Whoever draws the character may leave some out at a moment: a rock already thrown.</summary>
    public ImmutableArray<Worn> Worn { get; init; } = [];
    public CharacterFace? Face { get; init; }
    /// <summary>The clip for each thing it does on its own: "idle", "walk", "hit", "death".</summary>
    public required IReadOnlyDictionary<string, SkeletonClip2D> Roles { get; init; }

    public SkeletonClip2D? Clip(string role) => Roles.GetValueOrDefault(role);
    private float? _height;

    /// <summary>How tall the body is drawn at rest, from the feet to the top of its highest shape: what a character is scaled by to fit a size in the world.</summary>
    public float Height => _height ??= RestHeight();

    private float RestHeight()
    {
        var points = new List<Vector2f>();
        var world = Skeleton.RestWorld.AsSpan();
        foreach (var piece in Body)
        {
            if (piece is MountedShape shape) shape.Trace(Skeleton, world, points);
            else if (piece is JointPath path) path.Trace(world, points);
        }
        return points.Count == 0 ? 1 : points.Max(point => point.Y);
    }

    /// <summary>
    /// Everything drawn for a pose, in order: the body, the face in the pose's expression, then what is worn and passes
    /// <paramref name="shows"/>. Seen from behind there is no face, and a worn thing shows its back.
    /// </summary>
    public ImmutableArray<Attachment> Drawn(CharacterPose pose, Func<Worn, bool>? shows = null)
    {
        var drawn = ImmutableArray.CreateBuilder<Attachment>();
        drawn.AddRange(Body);
        if (Face is { } face && !pose.SeenFromBehind && (pose.Expression ?? face.Default) is { Length: > 0 } expression)
        {
            drawn.AddRange(face.Attachments(expression, pose.Clock));
        }
        foreach (var worn in Worn)
        {
            if (shows?.Invoke(worn) != false) drawn.AddRange(pose.SeenFromBehind && !worn.Behind.IsDefault ? worn.Behind : worn.Pieces);
        }
        return drawn.ToImmutable();
    }
}
