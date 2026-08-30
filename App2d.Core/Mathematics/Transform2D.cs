using System.Numerics;

namespace App2d.Engine.Mathematics;

public sealed class Transform2D
{
    private Vector2 _position;
    private float _rotation;
    private Vector2 _scale = Vector2.One;
    private Matrix3x2 _localToWorldMatrix = Matrix3x2.Identity;
    private int _matrixVersion = -1;

    public event Action? Changed;

    public Vector2 Position
    {
        get => _position;
        set
        {
            if (_position == value)
                return;
            _position = value;
            Version++;
            Changed?.Invoke();
        }
    }

    public float Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value)
                return;
            _rotation = value;
            Version++;
            Changed?.Invoke();
        }
    }

    public Vector2 Scale
    {
        get => _scale;
        set
        {
            if (_scale == value)
                return;
            _scale = value;
            Version++;
            Changed?.Invoke();
        }
    }

    public int Version { get; private set; }

    // System.Numerics uses row-vector order: scale, then rotate, then translate.
    public Matrix3x2 LocalToWorldMatrix
    {
        get
        {
            if (_matrixVersion == Version)
                return _localToWorldMatrix;

            _localToWorldMatrix =
                Matrix3x2.CreateScale(Scale) *
                Matrix3x2.CreateRotation(Rotation) *
                Matrix3x2.CreateTranslation(Position);
            _matrixVersion = Version;
            return _localToWorldMatrix;
        }
    }
}
