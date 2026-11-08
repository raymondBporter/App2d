using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Rendering;

/// <summary>One view of world space in target-local pixels, shared by rendering, picking, and culling.</summary>
public readonly record struct FrameView2D
{
    public FrameView2D(Vector2 targetSize, Matrix3x2 worldToDevice, float pixelsPerWorldUnit)
    {
        Ensure.Positive(targetSize);
        Ensure.Positive(pixelsPerWorldUnit);
        var frame = new Frame2D(worldToDevice);
        if (!frame.TryInverse(out var inverse))
            throw new ArgumentException("The view must have a finite inverse.", nameof(worldToDevice));

        TargetBounds = new(Vector2.Zero, targetSize);
        WorldToDeviceMatrix = frame.Matrix;
        DeviceToWorldMatrix = inverse.Matrix;
        PixelsPerWorldUnit = pixelsPerWorldUnit;
        VisibleWorldBounds = TargetBounds.TransformedBy(DeviceToWorldMatrix);
    }

    public Rect2D TargetBounds { get; }
    public Matrix3x2 WorldToDeviceMatrix { get; }
    public Matrix3x2 DeviceToWorldMatrix { get; }
    public Rect2D VisibleWorldBounds { get; }
    public float PixelsPerWorldUnit { get; }

    public Vector2 WorldToDevice(Vector2 point) => Vector2.Transform(point, WorldToDeviceMatrix);
    public Vector2 DeviceToWorld(Vector2 point) => Vector2.Transform(point, DeviceToWorldMatrix);

    /// <summary>Creates a Y-up view with the world origin at the given target pixel.</summary>
    public static FrameView2D FromAnchor(Vector2 targetSize, Vector2 anchor, float pixelsPerWorldUnit) =>
        new(targetSize, new(pixelsPerWorldUnit, 0, 0, -pixelsPerWorldUnit, anchor.X, anchor.Y), pixelsPerWorldUnit);
}
