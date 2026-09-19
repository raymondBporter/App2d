using App2d.Core;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Rendering;

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

    /// <summary>
    /// When set, resizing preserves vertical world framing and scales the scene uniformly.
    /// Null keeps pixel-based sizing for tools and standalone rendering.
    /// </summary>
    public float? ReferenceViewportHeight
    {
        get;
        set
        {
            if (value is { } height) ArgGuard.ThrowIfNotPositive(height);
            field = value;
        }
    }

    /// <summary>Actual device pixels per world unit, including viewport scaling and zoom.</summary>
    public float PixelsPerWorldUnit => Zoom *
        (ReferenceViewportHeight is { } height ? ViewportSize.Y / height : 1f);

    public float Zoom
    {
        get;
        set => field = Math.Clamp(value, MinZoom, MaxZoom);
    } = InitialZoom;

    public Matrix3x2 WorldToDeviceMatrix =>
        Matrix3x2.CreateTranslation(-Position) *
        Matrix3x2.CreateRotation(-Rotation) *
        Matrix3x2.CreateScale(PixelsPerWorldUnit, -PixelsPerWorldUnit) *
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
