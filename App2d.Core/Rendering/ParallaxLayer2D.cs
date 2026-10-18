using App2d.Core.Validation;

namespace App2d.Core.Rendering;

/// <summary>Visuals viewed at a fraction of the game camera's translation, without moving their transforms.</summary>
public sealed class ParallaxLayer2D(float depth)
{
    private readonly Camera2D _view = new();

    /// <summary>Zero moves with the world; larger depths scroll more slowly. Must be finite and nonnegative.</summary>
    public float Depth
    {
        get;
        set
        {
            ArgGuard.NonNegative(value);
            field = value;
        }
    } = ArgGuard.NonNegative(depth);

    public float ScrollFactor => 1f / (1f + Depth);
    public Scene2D Scene { get; } = [];

    /// <summary>Optional horizontal repeat period in layer world units. Null draws the scene once.</summary>
    public float? RepeatWidth
    {
        get;
        set
        {
            if (value is { } width)
            {
                ArgGuard.Positive(width);
            }
            field = value;
        }
    }

    internal Camera2D GetView(Camera2D camera)
    {
        _view.ReferenceViewportHeight = camera.ReferenceViewportHeight;
        _view.Zoom = camera.Zoom;
        _view.Rotation = camera.Rotation;
        _view.SetViewport((int)camera.ViewportSize.X, (int)camera.ViewportSize.Y);
        // Layer cameras are unbounded: gameplay's level bounds must not clamp distant scenery.
        _view.Position = camera.Position * ScrollFactor;
        return _view;
    }
}
