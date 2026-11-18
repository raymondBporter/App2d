using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Rendering;

/// <summary>One view of world space in target-local pixels, shared by rendering, picking, and culling.</summary>
public readonly record struct FrameView2D
{
    public FrameView2D(Vector2f targetSize, Matrix3x2 worldToDevice, float pixelsPerWorldUnit)
    {
        Ensure.Positive(targetSize);
        Ensure.Positive(pixelsPerWorldUnit);
        var frame = new Frame2D(worldToDevice);
        if (!frame.TryInverse(out var inverse))
            throw new ArgumentException("The view must have a finite inverse.", nameof(worldToDevice));

        TargetBounds = new(Vector2f.Zero, targetSize);
        WorldToDeviceTransform = frame.ColumnMatrix;
        DeviceToWorldTransform = inverse.ColumnMatrix;
        PixelsPerWorldUnit = pixelsPerWorldUnit;
        VisibleWorldBounds = TargetBounds.TransformedBy(DeviceToWorldMatrix);
    }

    public Rect2f TargetBounds { get; }
    [JsonIgnore]
    public Matrix2x3f WorldToDeviceTransform { get; }
    [JsonIgnore]
    public Matrix2x3f DeviceToWorldTransform { get; }
    public Matrix3x2 WorldToDeviceMatrix => WorldToDeviceTransform.ToNumerics();
    public Matrix3x2 DeviceToWorldMatrix => DeviceToWorldTransform.ToNumerics();
    public Rect2f VisibleWorldBounds { get; }
    public float PixelsPerWorldUnit { get; }

    public Vector2f WorldToDevice(Vector2f point) => WorldToDeviceTransform.TransformPoint(point);
    public Vector2f DeviceToWorld(Vector2f point) => DeviceToWorldTransform.TransformPoint(point);

    /// <summary>Composes the object's placement with the view's world-to-device transform.</summary>
    public Matrix3x2 LocalToDevice(Matrix3x2 localToWorld) =>
        (WorldToDeviceTransform * Matrix2x3f.FromNumerics(localToWorld)).ToNumerics();

    /// <summary>Creates a Y-up view with the world origin at the given target pixel.</summary>
    public static FrameView2D FromAnchor(Vector2f targetSize, Vector2f anchor, float pixelsPerWorldUnit) =>
        new(targetSize, new(pixelsPerWorldUnit, 0, 0, -pixelsPerWorldUnit, anchor.X, anchor.Y), pixelsPerWorldUnit);
}
