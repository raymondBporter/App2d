using App2d.Core.Validation;
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

    public Vector2 Position
    {
        get;
        set => field = ClampToWorld(value);
    }

    public float Rotation
    {
        get;
        set
        {
            ArgGuard.ThrowIfNotFinite(value);
            field = value;
            Position = Position;
        }
    }

    public Vector2 ViewportSize { get; private set; } = new(InitialSizeX, InitialSizeY);

    /// <summary>Optional world limit for the viewport center and its visible area.</summary>
    public Bounds2D? WorldBounds
    {
        get;
        set
        {
            if (value is { } bounds)
                ArgGuard.ThrowIf(!bounds.IsFinite || bounds.Min.X > bounds.Max.X || bounds.Min.Y > bounds.Max.Y,
                    "World bounds must be finite and ordered.", nameof(value));
            field = value;
            Position = Position;
        }
    }

    /// <summary>
    /// When set, resizing preserves vertical world framing and scales the scene uniformly.
    /// Null keeps pixel-based sizing for tools and standalone rendering.
    /// </summary>
    public float? ReferenceViewportHeight
    {
        get;
        set
        {
            if (value is { } height) ArgGuard.ThrowIfNotFiniteOrNotPositive(height);
            field = value;
            Position = Position;
        }
    }

    /// <summary>Actual device pixels per world unit, including viewport scaling and zoom.</summary>
    public float PixelsPerWorldUnit => Zoom *
        (ReferenceViewportHeight is { } height ? ViewportSize.Y / height : 1f);

    public float WorldUnitsToPixels(float worldUnits) => worldUnits * PixelsPerWorldUnit;
    public float PixelsToWorldUnits(float pixels) => pixels / PixelsPerWorldUnit;

    public float Zoom
    {
        get;
        set
        {
            ArgGuard.ThrowIfNotFiniteOrNotPositive(value);
            field = Math.Clamp(value, MinZoom, MaxZoom);
            Position = Position;
        }
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

    /// <summary>Constrain a desired center, with optional room for render effects such as shake.</summary>
    public Vector2 ClampToWorld(Vector2 desiredPosition, float padding = 0f)
    {
        ArgGuard.ThrowIfNotFinite(desiredPosition);
        ArgGuard.ThrowIfNotFiniteOrNegative(padding);
        if (WorldBounds is not { } bounds)
            return desiredPosition;

        var halfView = VisibleWorldBounds.Size / 2f + new Vector2(padding);
        return new Vector2(
            ClampCenter(desiredPosition.X, bounds.Min.X, bounds.Max.X, halfView.X),
            ClampCenter(desiredPosition.Y, bounds.Min.Y, bounds.Max.Y, halfView.Y));
    }

    private static float ClampCenter(float value, float min, float max, float halfView) =>
        max - min <= halfView * 2f ? min + (max - min) / 2f : Math.Clamp(value, min + halfView, max - halfView);

    public void SetViewport(int width, int height)
    {
        ViewportSize = new Vector2(Math.Max(width, MinSizeX), Math.Max(height, MinSizeY));
        Position = Position;
    }
}
