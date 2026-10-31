using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A finite, nonzero 2D vector normalized to unit length. The default value is invalid;
/// construct one from a vector or angle before using it as a direction.
/// </summary>
public readonly record struct Direction2D
{
    public Direction2D(Vector2 vector)
    {
        ArgGuard.NonZero(vector);
        // Float squares can overflow or underflow for valid input vectors.
        var wide = vector.ToVector2d();
        Vector = (wide / wide.Length()).ToVector2();
    }

    public Direction2D(float x, float y)
    {
        // Float squares can overflow or underflow for valid input vectors.
        var wide = new Vector2d(x, y);
        ArgGuard.NonZero(wide);
        Vector = (wide / wide.Length()).ToVector2();
    }

    public Vector2 Vector { get; }
    public bool IsValid => Vector != Vector2.Zero;

    public float AngleRadians
    {
        get
        {
            Validate();
            return Polar2D.AngleOf(Vector);
        }
    }

    public Direction2D PerpCcw => new(new(-Vector.Y, Vector.X));

    public Direction2D PerpCw => new(Vector.Y, -Vector.X);

    public Direction2D Reversed => new(-Vector);

    public Vector2 ScaledBy(float distance)
    {
        Validate();
        return Vector * ArgGuard.Finite(distance);
    }

    public static Direction2D FromAngle(float angleRadians) => new(Polar2D.Direction(angleRadians));

    /// <summary>Unit direction from one finite point toward another, without float subtraction overflow.</summary>
    public static Direction2D FromPoints(Vector2 from, Vector2 to)
    {
        ArgGuard.Finite(from);
        ArgGuard.Finite(to);
        var delta = to.ToVector2d() - from.ToVector2d();
        var length = ArgGuard.NonZero(delta.Length(), nameof(to));

        return new((delta / length).ToVector2());
    }

    internal void Validate() => ArgGuard.NonZero(Vector);
}
