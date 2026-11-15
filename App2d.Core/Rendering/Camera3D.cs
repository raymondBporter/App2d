using App2d.Core.Geometry.Primitives;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Rendering;

/// <summary>Immutable view/projection matrices and their inverse. Screen coordinates are viewport-local pixels, Y down.</summary>
public sealed class Camera3D
{
    private readonly Matrix4x4 _inverse;

    public Camera3D(Matrix4x4 view, Matrix4x4 projection)
    {
        Ensure.Finite(view);
        Ensure.Finite(projection);
        View = view;
        Projection = projection;
        ViewProjection = view * projection;
        Ensure.Invertible(ViewProjection, out _inverse);
        Ensure.Invertible(view, out var world);
        Position = new(world.Translation);
        Forward = Vector3f.Normalize(new(-world.M31, -world.M32, -world.M33));
    }

    public Matrix4x4 View { get; }
    public Matrix4x4 Projection { get; }
    public Matrix4x4 ViewProjection { get; }
    public Vector3f Position { get; }
    public Vector3f Forward { get; }

    public static Camera3D Perspective(Vector3f position, Vector3f target, Vector3f up, float aspectRatio,
        float verticalFieldOfView = MathF.PI / 4, float near = .05f, float far = 1000f)
    {
        ValidateView(position, target, up);
        Ensure.Positive(aspectRatio);
        Ensure.GreaterThan(verticalFieldOfView, 0f);
        Ensure.LessThan(verticalFieldOfView, MathF.PI);
        ValidateDepth(near, far);
        return new(Matrix4x4.CreateLookAt(position.ToVector3(), target.ToVector3(), up.ToVector3()),
            Matrix4x4.CreatePerspectiveFieldOfView(verticalFieldOfView, aspectRatio, near, far));
    }

    public static Camera3D Orthographic(Vector3f position, Vector3f target, Vector3f up, float width, float height,
        float near = .05f, float far = 1000f)
    {
        ValidateView(position, target, up);
        Ensure.Positive(width);
        Ensure.Positive(height);
        ValidateDepth(near, far);
        return new(Matrix4x4.CreateLookAt(position.ToVector3(), target.ToVector3(), up.ToVector3()),
            Matrix4x4.CreateOrthographic(width, height, near, far));
    }

    /// <summary>Projects a world point to pixels and depth [0,1]. False when behind the camera or outside its depth range.</summary>
    public bool TryProject(Vector3f world, Vector2f  viewportSize, out Vector3f screen)
    {
        Ensure.Finite(world);
        Ensure.Positive(viewportSize);
        var clip = Vector4.Transform(new Vector4(world.ToVector3(), 1), ViewProjection);
        screen = default;
        if (!(clip.W > 0)) return false;
        var depth = clip.Z / clip.W;
        if (depth < 0 || depth > 1) return false;
        screen = new((clip.X / clip.W + 1f) * viewportSize.X / 2f, (1f - clip.Y / clip.W) * viewportSize.Y / 2f, depth);
        return float.IsFinite(screen.X) && float.IsFinite(screen.Y) && float.IsFinite(screen.Z);
    }

    /// <summary>Unprojects a pixel to a ray starting on the near clipping plane. Works with either projection mode.</summary>
    public Ray3D ScreenRay(Vector2f  screen, Vector2f  viewportSize)
    {
        Ensure.Finite(screen);
        Ensure.Positive(viewportSize);
        var x = 2f * screen.X / viewportSize.X - 1f;
        var y = 1f - 2f * screen.Y / viewportSize.Y;
        var near = Unproject(new(x, y, 0, 1));
        var far = Unproject(new(x, y, 1, 1));
        return new(near, far - near);
    }

    private Vector3f Unproject(Vector4 clip)
    {
        var point = Vector4.Transform(clip, _inverse);
        return new(point.X / point.W, point.Y / point.W, point.Z / point.W);
    }

    private static void ValidateView(Vector3f position, Vector3f target, Vector3f up)
    {
        Ensure.Finite(position);
        Ensure.Finite(target);
        Ensure.NonZero(up);
        Ensure.NonZero(target - position);
        Ensure.NonZero(Vector3f.Cross(target - position, up));
    }

    private static void ValidateDepth(float near, float far)
    {
        Ensure.Positive(near);
        Ensure.GreaterThan(far, near);
    }
}
