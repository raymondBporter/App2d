using System.Numerics;
using App2d.Engine;
using App2d.Engine.Animation;
using App2d.Engine.Geometry;
using App2d.Engine.Rendering.Textures;

namespace App2d.Gameplay;

public sealed class Shieldback2D : IEnemyActor2D
{
    private static readonly Vector2 VisualCanvasSize = new(144f, 108f);
    private static readonly Vector2 VisualOffset = new(0f, 10f);

    private readonly AnimationClip2D<Texture2D> _walkAnimation;
    private readonly AnimationPlayer2D<Texture2D> _animation = new();
    private readonly SpriteShader2D _spriteShader;
    private readonly WorldObject2D _visual;
    private bool _simulationEnabled = true;

    public Shieldback2D(
        Scene2D scene,
        TextureCache2D textures,
        PatrolEnemy2D enemy)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(enemy);

        Enemy = enemy;
        _walkAnimation = new AnimationClip2D<Texture2D>(
            LoadFrames(textures),
            framesPerSecond: 8f);
        _animation.Play(_walkAnimation);
        _spriteShader = new SpriteShader2D(_animation.CurrentFrame);
        _visual = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(VisualCanvasSize),
            _spriteShader);
        scene.Add(_visual);
        SyncPresentation();
    }

    public PatrolEnemy2D Enemy { get; }

    public void SetSimulationEnabled(bool isEnabled)
    {
        _simulationEnabled = isEnabled;
        Enemy.SetSimulationEnabled(isEnabled);
        _visual.IsVisible = isEnabled && Enemy.IsAlive;
    }

    public void Update(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);

        Enemy.Update(deltaSeconds);
        if (!_simulationEnabled || !Enemy.IsAlive)
        {
            _visual.IsVisible = false;
            return;
        }

        _animation.PlaybackSpeed = Math.Clamp(
            MathF.Abs(Enemy.Body.LinearVelocity.X) / Enemy.Speed,
            0.65f,
            1.15f);
        _animation.Update(deltaSeconds);
        SyncPresentation();
    }

    public void Update(float deltaSeconds, Vector2 targetPosition)
    {
        ArgGuard.ThrowIfNotFinite(targetPosition);
        Update(deltaSeconds);
    }

    public void SyncAfterPhysics()
    {
        if (_simulationEnabled && Enemy.IsAlive)
            SyncPresentation();
    }

    private void SyncPresentation()
    {
        _spriteShader.Texture = _animation.CurrentFrame;
        _spriteShader.FlipX = Enemy.Facing < 0f;
        _visual.Transform.Position =
            Enemy.WorldObject.Transform.Position + VisualOffset;
        _visual.IsVisible = _simulationEnabled && Enemy.IsAlive;
    }

    private static Texture2D[] LoadFrames(TextureCache2D textures)
    {
        const int frameCount = 8;
        var frames = new Texture2D[frameCount];
        for (var i = 0; i < frames.Length; i++)
        {
            frames[i] = textures.Load(
                Path.Combine(
                    "Enemies",
                    "Shieldback",
                    $"walk-{i + 1:00}.png"));
        }

        return frames;
    }
}
