using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Rendering;

public sealed class Camera2D
{

    const float InitialSizeX = 800f;
    const float InitialSizeY = 600f;
    const float MinSizeX = 1f;
    const float MinSizeY = 1f;
    const float InitialZoom = 1f;
    const float MinZoom = 0.05f;
    const float MaxZoom = 20f;

    public Vector2f Position
    {
        get;
        set => field = ClampToWorld(value);
    }

    public float Rotation
    {
        get;
        set
        {
            Ensure.Finite(value);
            field = value;
            Position = Position;
        }
    }

    public Vector2f ViewportSize { get; private set; } = new(InitialSizeX, InitialSizeY);

    /// <summary>Optional world limit for the viewport center and its visible area.</summary>
    public Rect2f? WorldBounds
    {
        get;
        set
        {
            Ensure.ThrowIf(value is null, "World bounds must be specified.", nameof(value));
            var bounds = value.GetValueOrDefault();
            Ensure.Finite(bounds.Min);
            Ensure.Finite(bounds.Max);
            Ensure.LessThanOrEqual(bounds.Min, bounds.Max);
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
            if (value is { } height)
            {
                Ensure.Positive(height);
            }

            field = value;
            Position = Position;
        }
    }

    /// <summary>Actual device pixels per world unit, including viewport scaling and zoom.</summary>
    public float PixelsPerWorldUnit => Zoom * (ReferenceViewportHeight is { } height ? ViewportSize.Y / height : 1f);

    public float WorldUnitsToPixels(float worldUnits) => worldUnits * PixelsPerWorldUnit;
    public float PixelsToWorldUnits(float pixels) => pixels / PixelsPerWorldUnit;

    public float Zoom
    {
        get;
        set
        {
            Ensure.Positive(value);
            field = Math.Clamp(value, MinZoom, MaxZoom);
            Position = Position;
        }
    } = InitialZoom;

    [JsonIgnore]
    public Matrix2x3f WorldToDeviceTransform =>
        Matrix2x3f.CreateTranslation(-Position)
            .Then(Matrix2x3f.CreateRotation(-Rotation))
            .Then(Matrix2x3f.CreateScale(new(PixelsPerWorldUnit, -PixelsPerWorldUnit)))
            .Then(Matrix2x3f.CreateTranslation(ViewportSize / 2f));

    public Matrix3x2 WorldToDeviceMatrix => WorldToDeviceTransform.ToNumerics();

    public Matrix3x2 DeviceToWorldMatrix
    {
        get
        {
            Matrix3x2.Invert(WorldToDeviceMatrix, out var inverse);
            return inverse;
        }
    }

    /// <summary>The world-space bounding box of everything the viewport can see.</summary>
    public Rect2f VisibleWorldBounds => new Rect2f(Vector2f.Zero, ViewportSize).TransformedBy(DeviceToWorldMatrix);

    public Vector2f WorldToDevice(Vector2f worldPoint) => WorldToDeviceTransform.TransformPoint(worldPoint);

    public Vector2f DeviceToWorld(Vector2f devicePoint) => Vector2f.Transform(devicePoint, DeviceToWorldMatrix);

    public FrameView2D GetFrameView() => new(ViewportSize, WorldToDeviceMatrix, PixelsPerWorldUnit);

    /// <summary>Constrain a desired center, with optional room for render effects such as shake.</summary>
    public Vector2f ClampToWorld(Vector2f desiredPosition, float padding = 0f)
    {
        Ensure.Finite(desiredPosition);
        Ensure.NonNegative(padding);
        if (WorldBounds is not { } bounds)
        {
            return desiredPosition;
        }

        var halfView = VisibleWorldBounds.Size / 2f + new Vector2f(padding);
        return new Vector2f(
            ClampCenter(desiredPosition.X, bounds.Min.X, bounds.Max.X, halfView.X),
            ClampCenter(desiredPosition.Y, bounds.Min.Y, bounds.Max.Y, halfView.Y));
    }

    private static float ClampCenter(float value, float min, float max, float halfView) =>
        max - min <= halfView * 2f ? min + (max - min) / 2f : Math.Clamp(value, min + halfView, max - halfView);

    public void SetViewport(int width, int height)
    {
        ViewportSize = new Vector2f(Math.Max(width, MinSizeX), Math.Max(height, MinSizeY));
        Position = Position;
    }
}
