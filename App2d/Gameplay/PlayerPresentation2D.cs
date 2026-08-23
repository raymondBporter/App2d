using System.Numerics;
using App2d.Engine;
using App2d.Engine.Animation;
using App2d.Engine.Geometry;
using App2d.Engine.Rendering.Textures;

namespace App2d.Gameplay;

public sealed class PlayerPresentation2D
{
    private const float ShotDuration = 0.4f;
    private static readonly Vector2 VisualCanvasSize = new(152f, 114f);
    private static readonly Vector2 VisualOffset = new(0f, 4f);

    private readonly AnimationClip2D<Texture2D> _idleAnimation;
    private readonly AnimationClip2D<Texture2D> _walkAnimation;
    private readonly AnimationClip2D<Texture2D> _swordAnimation;
    private readonly AnimationClip2D<Texture2D> _shotAnimation;
    private readonly AnimationPlayer2D<Texture2D> _animation = new();
    private readonly SpriteShader2D _spriteShader;
    private readonly WorldObject2D _visual;

    public PlayerPresentation2D(Scene2D scene, TextureCache2D textures)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(textures);

        var walkFrames = LoadFrames(textures, "walk", 6);
        _idleAnimation = new AnimationClip2D<Texture2D>([walkFrames[0]], 1f);
        _walkAnimation = new AnimationClip2D<Texture2D>(walkFrames, 10f);
        _swordAnimation = new AnimationClip2D<Texture2D>(
            LoadFrames(textures, "sword", 6),
            25f,
            isLooping: false);
        _shotAnimation = new AnimationClip2D<Texture2D>(
            LoadFrames(textures, "shotgun", 8),
            8f / ShotDuration,
            isLooping: false);
        _animation.Play(_idleAnimation);
        _spriteShader = new SpriteShader2D(_animation.CurrentFrame);
        _visual = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(VisualCanvasSize),
            _spriteShader);
        scene.Add(_visual);
    }

    public float ShotAnimationDuration => _shotAnimation.Duration;
    public float ShotAnimationElapsedSeconds => _animation.ElapsedSeconds;
    public bool IsPlayingShot => ReferenceEquals(_animation.Clip, _shotAnimation);

    public void PlaySwordAttack()
    {
        _animation.Play(_swordAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void PlayShot()
    {
        _animation.Play(_shotAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void Update(
        float deltaSeconds,
        long frameNumber,
        Vector2 playerPosition,
        float moveInputX,
        float facing,
        bool isGrounded,
        bool isSwordActive,
        float invulnerabilitySeconds)
    {
        var isPlayingShot = IsPlayingShot && !_animation.IsFinished;
        if (!isSwordActive && !isPlayingShot)
        {
            var isWalking = isGrounded && MathF.Abs(moveInputX) > 0.01f;
            var locomotionClip = isWalking ? _walkAnimation : _idleAnimation;
            _animation.Play(locomotionClip);
            _animation.PlaybackSpeed = isWalking
                ? Math.Clamp(MathF.Abs(moveInputX), 0.65f, 1.35f)
                : 1f;
        }

        _animation.Update(deltaSeconds);
        _spriteShader.Texture = _animation.CurrentFrame;
        _spriteShader.FlipX = facing < 0f;
        _visual.Transform.Position = playerPosition + VisualOffset;
        _visual.IsVisible = invulnerabilitySeconds <= 0f || frameNumber % 12 < 6;
    }

    public void Reset()
    {
        _animation.Play(_idleAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    private static Texture2D[] LoadFrames(
        TextureCache2D textures,
        string animationName,
        int frameCount)
    {
        var frames = new Texture2D[frameCount];
        for (var i = 0; i < frames.Length; i++)
        {
            frames[i] = textures.Load(
                Path.Combine("Player", "A1", $"{animationName}-{i + 1:00}.png"));
        }

        return frames;
    }
}
