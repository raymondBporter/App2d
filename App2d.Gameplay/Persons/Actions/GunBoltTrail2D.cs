using App2d.Core.Geometry;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

/// <summary>A purely visual, pooled streak of recent flight, with a short impact fade.</summary>
internal sealed class GunBoltTrail2D
{
    // Cover a 30 Hz sample interval plus a modest cartoon stretch. Using seconds
    // instead of frame counts keeps the silhouette stable at different render rates.
    private const float HistorySeconds = 1f / 30f;
    private const float Exaggeration = 1.35f;
    private const float FadeSeconds = 0.05f;
    private readonly WorldObject2D _visual;
    private readonly SpriteShader2D _shader;
    private readonly Texture2D[] _fadeTextures;
    private Vector2 _origin;
    private float _direction;
    private float _maximumLength;
    private float _fadeRemaining;

    public GunBoltTrail2D(Scene2D scene, Texture2D[] fadeTextures)
    {
        _fadeTextures = fadeTextures;
        _shader = new SpriteShader2D(fadeTextures[0]);
        _visual = new WorldObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(1f, 4f)), _shader)
        { IsVisible = false, ZIndex = 2 };
        scene.Add(_visual);
    }

    public bool IsVisible => _visual.IsVisible;

    public void Begin(Vector2 muzzle, float direction, float speed)
    {
        Reset();
        _origin = muzzle;
        _direction = direction;
        _maximumLength = Math.Min(72f, speed * HistorySeconds * Exaggeration);
        _shader.FlipX = direction < 0f;
        _shader.Texture = _fadeTextures[0];
    }

    public void Follow(Vector2 head)
    {
        // Grow only over distance actually traveled: never draw back through the gun.
        var length = Math.Clamp((head.X - _origin.X) * _direction, 0f, _maximumLength);
        _visual.IsVisible = length > 0.01f;
        _visual.Transform.Position = head - new Vector2(_direction * length * 0.5f, 0f);
        _visual.Transform.Scale = new Vector2(Math.Max(0.001f, length), 1f);
        _fadeRemaining = FadeSeconds;
    }

    public void Fade(float deltaSeconds)
    {
        // Freeze the last clear segment instead of advancing a ghost past the impact.
        _fadeRemaining = Math.Max(0f, _fadeRemaining - deltaSeconds);
        _visual.IsVisible = _fadeRemaining > 0f;
        var frame = Math.Min(_fadeTextures.Length - 1,
            (int)((1f - _fadeRemaining / FadeSeconds) * _fadeTextures.Length));
        _shader.Texture = _fadeTextures[frame];
    }

    public void Reset()
    {
        _fadeRemaining = 0f;
        _visual.IsVisible = false;
    }
}
