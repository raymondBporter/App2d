using System.Numerics;

namespace App2d.Engine;

public sealed class Camera2D
{
    private float _zoom = 1f;

    public Vector2 Position { get; set; }
    public float Rotation { get; set; }
    public Vector2 ViewportSize { get; private set; } = new(800f, 600f);

    public float Zoom
    {
        get => _zoom;
        set => _zoom = Math.Clamp(value, 0.05f, 20f);
    }

    public Matrix3x2 WorldToDeviceMatrix =>
        Matrix3x2.CreateTranslation(-Position) *
        Matrix3x2.CreateRotation(-Rotation) *
        Matrix3x2.CreateScale(Zoom, -Zoom) *
        Matrix3x2.CreateTranslation(ViewportSize / 2f);

    public Matrix3x2 DeviceToWorldMatrix
    {
        get
        {
            Matrix3x2.Invert(WorldToDeviceMatrix, out var inverse);
            return inverse;
        }
    }

    public Vector2 WorldToDevice(Vector2 worldPoint) =>
        Vector2.Transform(worldPoint, WorldToDeviceMatrix);

    public Vector2 DeviceToWorld(Vector2 devicePoint) =>
        Vector2.Transform(devicePoint, DeviceToWorldMatrix);

    internal void SetViewport(int width, int height)
    {
        ViewportSize = new Vector2(Math.Max(width, 1), Math.Max(height, 1));
    }
}
