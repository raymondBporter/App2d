using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>
/// Editable affine transform: translation, rotation, independent axis scales and shear. Angles are radians.
/// Row-vector convention: local * parent. Reflections and singular transforms are allowed; inversion is explicit.
/// </summary>
public sealed record Affine2D
{
    private float _x, _y, _rotation, _shearX, _shearY;
    private float _scaleX = 1, _scaleY = 1;
    private Matrix3x2 _matrix = Matrix3x2.Identity;
    private int _matrixVersion = -1;

    public Affine2D() { }

    // A value copy owns its cache and notifications; it never inherits another owner's subscriptions.
    private Affine2D(Affine2D other)
    {
        _x = other._x; _y = other._y; _rotation = other._rotation;
        _scaleX = other._scaleX; _scaleY = other._scaleY;
        _shearX = other._shearX; _shearY = other._shearY;
        _matrix = other.Matrix; _matrixVersion = Version;
    }

    public event Action? Changed;
    [JsonIgnore] public int Version { get; private set; }
    public static Affine2D Identity => new();

    public float X { get => _x; set => Set(ref _x, value); }
    public float Y { get => _y; set => Set(ref _y, value); }
    public float Rotation { get => _rotation; set => Set(ref _rotation, value); }
    public float ScaleX { get => _scaleX; set => Set(ref _scaleX, value); }
    public float ScaleY { get => _scaleY; set => Set(ref _scaleY, value); }
    /// <summary>Additional angle of the local +X axis.</summary>
    public float ShearX { get => _shearX; set => Set(ref _shearX, value); }
    /// <summary>Additional angle of the local +Y axis.</summary>
    public float ShearY { get => _shearY; set => Set(ref _shearY, value); }

    [JsonIgnore]
    public Vector2 Position
    {
        get => new(X, Y);
        set
        {
            if (_x.Equals(value.X) && _y.Equals(value.Y)) return;
            _x = value.X; _y = value.Y; NotifyChanged();
        }
    }

    [JsonIgnore]
    public Vector2 Scale
    {
        get => new(ScaleX, ScaleY);
        set
        {
            if (_scaleX.Equals(value.X) && _scaleY.Equals(value.Y)) return;
            _scaleX = value.X; _scaleY = value.Y; NotifyChanged();
        }
    }

    /// <summary>Cached matrix. Reading it never changes the transform's logical value or version.</summary>
    [JsonIgnore]
    public Matrix3x2 Matrix
    {
        get
        {
            if (_matrixVersion == Version) return _matrix;
            _matrix = ShearX == 0 && ShearY == 0
                ? Matrix3x2.CreateScale(Scale) * Matrix3x2.CreateRotation(Rotation) * Matrix3x2.CreateTranslation(Position)
                : new(MathF.Cos(Rotation + ShearX) * ScaleX, MathF.Sin(Rotation + ShearX) * ScaleX,
                    -MathF.Sin(Rotation + ShearY) * ScaleY, MathF.Cos(Rotation + ShearY) * ScaleY, X, Y);
            _matrixVersion = Version;
            return _matrix;
        }
    }

    public Vector2 TransformPoint(Vector2 point) => Vector2.Transform(point, Matrix);
    public Vector2 TransformDirection(Vector2 direction) => Vector2.TransformNormal(direction, Matrix);
    public Vector2 TransposeTransformDirection(Vector2 direction) => Matrix.TransposeTransformDirection(direction);

    /// <summary>Apply this transform, then the next transform. Composition may introduce shear.</summary>
    public Affine2D Then(Affine2D next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return FromMatrix(Matrix * next.Matrix);
    }

    /// <summary>Returns false for singular transforms or inverses that cannot be represented with finite channels.</summary>
    public bool TryInverse([NotNullWhen(true)] out Affine2D? inverse)
    {
        inverse = null;
        var matrix = Matrix;
        if (!matrix.IsFinite()) return false;
        // Double products keep finite large/small scales from overflowing/underflowing the determinant.
        var det = (double)matrix.M11 * matrix.M22 - (double)matrix.M12 * matrix.M21;
        if (det == 0) return false;
        var result = new Matrix3x2((float)(matrix.M22 / det), (float)(-matrix.M12 / det),
            (float)(-matrix.M21 / det), (float)(matrix.M11 / det),
            (float)(((double)matrix.M21 * matrix.M32 - (double)matrix.M22 * matrix.M31) / det),
            (float)(((double)matrix.M12 * matrix.M31 - (double)matrix.M11 * matrix.M32) / det));
        return TryFromMatrix(result, out inverse);
    }

    /// <summary>Replace all channels, notifying this transform's owner once when anything changes.</summary>
    public void CopyFrom(Affine2D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Equals(other)) return;
        _x = other._x; _y = other._y; _rotation = other._rotation;
        _scaleX = other._scaleX; _scaleY = other._scaleY;
        _shearX = other._shearX; _shearY = other._shearY;
        NotifyChanged(other.Matrix);
    }

    public void Validate(string field)
    {
        foreach (var value in new[] { X, Y, Rotation, ScaleX, ScaleY, ShearX, ShearY })
            if (!float.IsFinite(value)) throw new InvalidDataException($"{field}: transform values must be finite.");
    }

    /// <summary>Decomposes a matrix, including singular matrices, fixing shearX at zero.</summary>
    public static Affine2D FromMatrix(Matrix3x2 matrix) => TryFromMatrix(matrix, out var transform)
        ? transform : throw new ArgumentOutOfRangeException(nameof(matrix), "The matrix must be representable with finite affine channels.");

    public static bool TryFromMatrix(Matrix3x2 matrix, [NotNullWhen(true)] out Affine2D? transform)
    {
        transform = null;
        if (!matrix.IsFinite()) return false;
        var scaleX = Math.Sqrt((double)matrix.M11 * matrix.M11 + (double)matrix.M12 * matrix.M12);
        var scaleY = Math.Sqrt((double)matrix.M21 * matrix.M21 + (double)matrix.M22 * matrix.M22);
        if (scaleX > float.MaxValue || scaleY > float.MaxValue) return false;
        var rotation = MathF.Atan2(matrix.M12, matrix.M11);
        transform = new()
        {
            X = matrix.M31,
            Y = matrix.M32,
            Rotation = rotation,
            ScaleX = (float)scaleX,
            ScaleY = (float)scaleY,
            ShearY = MathF.Atan2(-matrix.M21, matrix.M22) - rotation,
            // Keep the supplied map exactly until a channel is edited. Decomposition has float roundoff;
            // rebuilding a rank-one matrix could otherwise turn it into a nearly singular invertible one.
            _matrix = matrix
        };
        transform._matrixVersion = transform.Version;
        return true;
    }

    // Cache state, versions, and subscribers are ownership details, not part of the mathematical value.
    public bool Equals(Affine2D? other) => other is not null &&
        X.Equals(other.X) && Y.Equals(other.Y) && Rotation.Equals(other.Rotation) &&
        ScaleX.Equals(other.ScaleX) && ScaleY.Equals(other.ScaleY) && ShearX.Equals(other.ShearX) && ShearY.Equals(other.ShearY);
    public override int GetHashCode() => HashCode.Combine(X, Y, Rotation, ScaleX, ScaleY, ShearX, ShearY);

    private void Set(ref float channel, float value)
    {
        if (channel.Equals(value)) return;
        channel = value; NotifyChanged();
    }
    private void NotifyChanged(Matrix3x2? matrix = null)
    {
        Version++;
        if (matrix is { } value) { _matrix = value; _matrixVersion = Version; }
        Changed?.Invoke();
    }
}
