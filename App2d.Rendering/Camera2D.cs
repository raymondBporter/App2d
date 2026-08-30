using System.Numerics;
using App2d.Engine.Geometry;

namespace App2d.Engine;

public sealed class Camera2D
{

    const float InitialSizeX = 800f;
    const float InitialSizeY = 600f;
    const float MinSizeX = 1f;
    const float MinSizeY = 1f;
    const float InitialZoom = 1f;
    const float MinZoom = 0.05f;
    const float MaxZoom = 20f;

    public Vector2 Position { get; set; }
    public float Rotation { get; set; }

    public Vector2 ViewportSize { get; private set; } = new(InitialSizeX, InitialSizeY);

    public float Zoom
    {
        get;
        set => field = Math.Clamp(value, MinZoom, MaxZoom);
    } = InitialZoom;

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

    /// <summary>The world-space bounding box of everything the viewport can see.</summary>
    public Bounds2D VisibleWorldBounds => new Bounds2D(Vector2.Zero, ViewportSize).TransformedBy(DeviceToWorldMatrix);

    public Vector2 WorldToDevice(Vector2 worldPoint) => Vector2.Transform(worldPoint, WorldToDeviceMatrix);

    public Vector2 DeviceToWorld(Vector2 devicePoint) => Vector2.Transform(devicePoint, DeviceToWorldMatrix);

    public void SetViewport(int width, int height)
    {
        ViewportSize = new Vector2(Math.Max(width, MinSizeX), Math.Max(height, MinSizeY));
    }
}
