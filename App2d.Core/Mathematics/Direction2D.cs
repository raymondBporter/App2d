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
        ArgGuard.ThrowIfNotFiniteOrZero(vector);
        // Float squares can overflow or underflow for valid input vectors.
        var length = Math.Sqrt((double)vector.X * vector.X + (double)vector.Y * vector.Y);
        Vector = new((float)(vector.X / length), (float)(vector.Y / length));
    }

    public Vector2 Vector { get; }
    public bool IsValid => Vector != Vector2.Zero;

    public float AngleRadians
    {
        get { Validate(); return Polar2D.AngleOf(Vector); }
    }

    public Direction2D PerpCcw
    {
        get { Validate(); return new(new(-Vector.Y, Vector.X)); }
    }

    public Direction2D PerpCw
    {
        get { Validate(); return new(new(Vector.Y, -Vector.X)); }
    }

    public Direction2D Reversed
    {
        get { Validate(); return new(-Vector); }
    }

    public Vector2 ScaledBy(float distance)
    {
        Validate();
        ArgGuard.ThrowIfNotFinite(distance);
        return Vector * distance;
    }

    public static Direction2D FromAngle(float angleRadians) => new(Polar2D.Direction(angleRadians));

    /// <summary>Unit direction from one finite point toward another, without float subtraction overflow.</summary>
    public static Direction2D FromPoints(Vector2 from, Vector2 to)
    {
        ArgGuard.ThrowIfNotFinite(from);
        ArgGuard.ThrowIfNotFinite(to);
        var x = (double)to.X - from.X;
        var y = (double)to.Y - from.Y;
        if (x == 0d && y == 0d)
            throw new ArgumentOutOfRangeException(nameof(to), "The two points must differ.");

        var length = Math.Sqrt(x * x + y * y);
        return new(new Vector2((float)(x / length), (float)(y / length)));
    }

    internal void Validate() => ArgGuard.ThrowIfNotFiniteOrZero(Vector);
}
