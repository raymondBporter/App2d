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
        ArgGuard.ThrowIf(!Finite(view) || !Finite(projection), "Camera matrices must be finite.");
        View = view;
        Projection = projection;
        ViewProjection = view * projection;
        ArgGuard.ThrowIf(!Matrix4x4.Invert(ViewProjection, out _inverse) || !Finite(_inverse), "Camera matrices must have a finite inverse.");
        ArgGuard.ThrowIf(!Matrix4x4.Invert(view, out var world) || !Finite(world), "The view must have a finite inverse.");
        Position = world.Translation;
        Forward = Vector3.Normalize(new(-world.M31, -world.M32, -world.M33));
    }

    public Matrix4x4 View { get; }
    public Matrix4x4 Projection { get; }
    public Matrix4x4 ViewProjection { get; }
    public Vector3 Position { get; }
    public Vector3 Forward { get; }

    public static Camera3D Perspective(Vector3 position, Vector3 target, Vector3 up, float aspectRatio,
        float verticalFieldOfView = MathF.PI / 4, float near = .05f, float far = 1000f)
    {
        ValidateView(position, target, up);
        ArgGuard.Positive(aspectRatio);
        ArgGuard.GreaterThan(verticalFieldOfView, 0f);
        ArgGuard.LessThan(verticalFieldOfView, MathF.PI);
        ValidateDepth(near, far);
        return new(Matrix4x4.CreateLookAt(position, target, up), Matrix4x4.CreatePerspectiveFieldOfView(verticalFieldOfView, aspectRatio, near, far));
    }

    public static Camera3D Orthographic(Vector3 position, Vector3 target, Vector3 up, float width, float height,
        float near = .05f, float far = 1000f)
    {
        ValidateView(position, target, up);
        ArgGuard.Positive(width);
        ArgGuard.Positive(height);
        ValidateDepth(near, far);
        return new(Matrix4x4.CreateLookAt(position, target, up), Matrix4x4.CreateOrthographic(width, height, near, far));
    }

    /// <summary>Projects a world point to pixels and depth [0,1]. False when behind the camera or outside its depth range.</summary>
    public bool TryProject(Vector3 world, Vector2 viewportSize, out Vector3 screen)
    {
        ArgGuard.Finite(world);
        ArgGuard.Positive(viewportSize);
        var clip = Vector4.Transform(new Vector4(world, 1), ViewProjection);
        screen = default;
        if (!(clip.W > 0)) return false;
        var depth = clip.Z / clip.W;
        if (depth < 0 || depth > 1) return false;
        screen = new((clip.X / clip.W + 1f) * viewportSize.X / 2f, (1f - clip.Y / clip.W) * viewportSize.Y / 2f, depth);
        return float.IsFinite(screen.X) && float.IsFinite(screen.Y) && float.IsFinite(screen.Z);
    }

    /// <summary>Unprojects a pixel to a ray starting on the near clipping plane. Works with either projection mode.</summary>
    public Ray3D ScreenRay(Vector2 screen, Vector2 viewportSize)
    {
        ArgGuard.Finite(screen);
        ArgGuard.Positive(viewportSize);
        var x = 2f * screen.X / viewportSize.X - 1f;
        var y = 1f - 2f * screen.Y / viewportSize.Y;
        var near = Unproject(new(x, y, 0, 1));
        var far = Unproject(new(x, y, 1, 1));
        return new(near, far - near);
    }

    private Vector3 Unproject(Vector4 clip)
    {
        var point = Vector4.Transform(clip, _inverse);
        return new(point.X / point.W, point.Y / point.W, point.Z / point.W);
    }

    private static void ValidateView(Vector3 position, Vector3 target, Vector3 up)
    {
        ArgGuard.Finite(position);
        ArgGuard.Finite(target);
        ArgGuard.NonZero(up);
        ArgGuard.NonZero(target - position);
        ArgGuard.NonZero(Vector3.Cross(target - position, up));
    }

    private static void ValidateDepth(float near, float far)
    {
        ArgGuard.Positive(near);
        ArgGuard.GreaterThan(far, near);
    }

    private static bool Finite(Matrix4x4 matrix)
    {
        for (var row = 0; row < 4; row++)
            for (var column = 0; column < 4; column++)
                if (!float.IsFinite(matrix[row, column])) return false;
        return true;
    }
}
