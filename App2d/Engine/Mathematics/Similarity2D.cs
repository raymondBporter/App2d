using System.Numerics;

namespace App2d.Engine.Mathematics;

/// <summary>
/// Rotation + uniform scale + optional mirror + translation — the exact family
/// of transforms 2D collision supports. Row-vector convention matching
/// <see cref="Matrix3x2"/>: XAxis/YAxis are the images of the local axes.
/// </summary>
public readonly struct Similarity2D
{
    private Similarity2D(Vector2 xAxis, Vector2 yAxis, Vector2 translation, float scale)
    {
        XAxis = xAxis;
        YAxis = yAxis;
        Translation = translation;
        Scale = scale;
    }

    public Vector2 XAxis { get; }
    public Vector2 YAxis { get; }
    public Vector2 Translation { get; }
    public float Scale { get; }

    public static bool TryFromMatrix(Matrix3x2 matrix, out Similarity2D similarity)
    {
        var xAxis = new Vector2(matrix.M11, matrix.M12);
        var yAxis = new Vector2(matrix.M21, matrix.M22);
        var xLength = xAxis.Length();
        var yLength = yAxis.Length();
        var largest = Math.Max(xLength, yLength);
        if (largest <= float.Epsilon || MathF.Abs(xLength - yLength) > largest * 0.001f)
        {
            similarity = default;
            return false;
        }

        similarity = new Similarity2D(xAxis, yAxis, matrix.Translation, (xLength + yLength) / 2f);
        return true;
    }

    public Vector2 TransformPoint(Vector2 point) => Translation + XAxis * point.X + YAxis * point.Y;

    public Vector2 TransformDirection(Vector2 direction) => XAxis * direction.X + YAxis * direction.Y;

    /// <summary>
    /// Multiplies by the transpose of the linear part: maps a world support or
    /// normal direction into local space (direction-preserving up to Scale).
    /// </summary>
    public Vector2 TransposeTransformDirection(Vector2 direction) =>
        new(Vector2.Dot(XAxis, direction), Vector2.Dot(YAxis, direction));

    public Vector2 InverseTransformPoint(Vector2 point)
    {
        var relative = point - Translation;
        return new Vector2(Vector2.Dot(XAxis, relative), Vector2.Dot(YAxis, relative)) / (Scale * Scale);
    }

    public Vector2 InverseTransformDirection(Vector2 direction) =>
        TransposeTransformDirection(direction) / (Scale * Scale);
}
