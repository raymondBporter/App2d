using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Rotation + uniform scale + optional mirror + translation: the exact family of transforms 2D collision supports.
/// Row-vector convention matching <see cref="Matrix3x2"/>: <see cref="XAxis"/> and <see cref="YAxis"/> are the images
/// of the local axes, each of length <see cref="Scale"/>.
/// </summary>
public readonly record struct Similarity2D
{
    private Similarity2D(Vector2 xAxis, Vector2 yAxis, Vector2 translation, float scale)
    {
        XAxis = xAxis;
        YAxis = yAxis;
        Translation = translation;
        Scale = scale;
    }

    /// <summary>The pose that leaves everything where it is.</summary>
    public static Similarity2D Identity { get; } = new(Vector2.UnitX, Vector2.UnitY, Vector2.Zero, 1f);

    /// <summary>The image of local +X, of length <see cref="Scale"/>.</summary>
    public Vector2 XAxis { get; }

    /// <summary>The image of local +Y, of length <see cref="Scale"/>.</summary>
    public Vector2 YAxis { get; }

    /// <summary>Where the local origin lands.</summary>
    public Vector2 Translation { get; }

    /// <summary>The uniform scale factor.</summary>
    public float Scale { get; }

    /// <summary>True when the pose flips handedness, such as a character mirrored to face the other way.</summary>
    public bool IsMirrored => CrossProduct2D.Of(XAxis, YAxis) < 0d;

    /// <summary>A pose with unit scale and no rotation, placed at a finite position.</summary>
    /// <param name="translation">Where the local origin lands.</param>
    /// <returns>A translation-only pose.</returns>
    public static Similarity2D FromTranslation(Vector2 translation)
    {
        ArgGuard.ThrowIfNotFinite(translation);
        return new Similarity2D(Vector2.UnitX, Vector2.UnitY, translation, 1f);
    }

    /// <summary>
    /// A pose whose local +X points along an axis. Local +Y is the counter-clockwise perpendicular, or the clockwise
    /// one when mirrored, so a mirrored actor keeps its up direction while its facing flips.
    /// </summary>
    /// <param name="translation">Where the local origin lands.</param>
    /// <param name="xAxis">A finite, nonzero direction for local +X; its length is ignored.</param>
    /// <param name="mirror">Flip the handedness so local +Y is the clockwise perpendicular of the axis.</param>
    /// <param name="scale">The finite, positive uniform scale.</param>
    /// <returns>The pose.</returns>
    public static Similarity2D FromAxis(Vector2 translation, Vector2 xAxis, bool mirror = false, float scale = 1f)
    {
        ArgGuard.ThrowIfNotFinite(translation);
        ArgGuard.ThrowIfNotFiniteOrZero(xAxis);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(scale);
        var x = new Direction2D(xAxis).Vector * scale;
        return new Similarity2D(x, mirror ? x.PerpCw : x.PerpCcw, translation, scale);
    }

    /// <summary>Reads a pose from a matrix whose linear part is a rotation, uniform scale and optional mirror.</summary>
    /// <param name="matrix">The local-to-world matrix.</param>
    /// <param name="similarity">The pose, or default when the matrix shears or scales non-uniformly.</param>
    /// <returns>True when the matrix is a similarity.</returns>
    public static bool TryFromMatrix(Matrix3x2 matrix, out Similarity2D similarity)
    {
        var xAxis = new Vector2(matrix.M11, matrix.M12);
        var yAxis = new Vector2(matrix.M21, matrix.M22);
        var xLength = xAxis.Length();
        var yLength = yAxis.Length();
        var largest = Math.Max(xLength, yLength);
        var dot = (double)xAxis.X * yAxis.X + (double)xAxis.Y * yAxis.Y;
        if (!matrix.IsFinite() || !float.IsFinite(largest) || largest <= float.Epsilon ||
            MathF.Abs(xLength - yLength) > largest * 0.001f || Math.Abs(dot) > (double)xLength * yLength * 0.001)
        {
            similarity = default;
            return false;
        }

        similarity = new Similarity2D(xAxis, yAxis, matrix.Translation, (xLength + yLength) / 2f);
        return true;
    }

    /// <summary>The same pose moved by an offset.</summary>
    /// <param name="offset">A finite translation added in world space.</param>
    /// <returns>The moved pose.</returns>
    public Similarity2D Translated(Vector2 offset)
    {
        ArgGuard.ThrowIfNotFinite(offset);
        return new Similarity2D(XAxis, YAxis, Translation + offset, Scale);
    }

    /// <summary>The same pose scaled about the world origin: the axes and the translation all grow by the factor.</summary>
    /// <param name="factor">A finite, positive factor, such as a unit conversion.</param>
    /// <returns>The scaled pose.</returns>
    public Similarity2D ScaledBy(float factor)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(factor);
        return new Similarity2D(XAxis * factor, YAxis * factor, Translation * factor, Scale * factor);
    }

    /// <summary>The pose as a row-vector matrix.</summary>
    /// <returns>A matrix that transforms points the same way as <see cref="TransformPoint"/>.</returns>
    public Matrix3x2 ToMatrix() => new(XAxis.X, XAxis.Y, YAxis.X, YAxis.Y, Translation.X, Translation.Y);

    /// <summary>A new editable affine transform carrying this pose; a mirror becomes a negative X scale.</summary>
    public Affine2D ToAffine()
    {
        var mirrored = IsMirrored;
        var forward = mirrored ? -XAxis : XAxis;
        return new() { Position = Translation, Rotation = MathF.Atan2(forward.Y, forward.X), Scale = new(mirrored ? -Scale : Scale, Scale) };
    }

    /// <summary>Maps a local point into world space.</summary>
    /// <param name="point">The local point.</param>
    /// <returns>The world point.</returns>
    public Vector2 TransformPoint(Vector2 point) => Translation + XAxis * point.X + YAxis * point.Y;

    /// <summary>Maps a local direction into world space, scaling its length by <see cref="Scale"/>.</summary>
    /// <param name="direction">The local direction.</param>
    /// <returns>The world direction.</returns>
    public Vector2 TransformDirection(Vector2 direction) => XAxis * direction.X + YAxis * direction.Y;

    /// <summary>
    /// Multiplies by the transpose of the linear part: maps a world support or normal direction into local space
    /// (direction-preserving up to <see cref="Scale"/>).
    /// </summary>
    /// <param name="direction">The world direction.</param>
    /// <returns>The local direction, scaled by <see cref="Scale"/>.</returns>
    public Vector2 TransposeTransformDirection(Vector2 direction) => new(Vector2.Dot(XAxis, direction), Vector2.Dot(YAxis, direction));

    /// <summary>Maps a world point into local space.</summary>
    /// <param name="point">The world point.</param>
    /// <returns>The local point.</returns>
    public Vector2 InverseTransformPoint(Vector2 point)
    {
        var relative = point - Translation;
        return new Vector2(Vector2.Dot(XAxis, relative), Vector2.Dot(YAxis, relative)) / (Scale * Scale);
    }

    /// <summary>Maps a world direction into local space, dividing its length by <see cref="Scale"/>.</summary>
    /// <param name="direction">The world direction.</param>
    /// <returns>The local direction.</returns>
    public Vector2 InverseTransformDirection(Vector2 direction) => TransposeTransformDirection(direction) / (Scale * Scale);
}
