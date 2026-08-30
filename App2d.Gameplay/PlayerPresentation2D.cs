using App2d.Core;
using App2d.Core.Animation;
using App2d.Core.Geometry;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay;

/// <summary>
/// Selects between the baked sword and gun character sheets and keeps visual
/// animation independent from the player's physics collider.
/// </summary>
public sealed class PlayerPresentation2D : IDisposable
{
    private const float TerminalVelocityEpsilon = 25f;
    private const string SwordCharacterId = "player-sword";
    private const string GunCharacterId = "player-gun";

    private readonly TextureCache2D _textures;
    private readonly AnimationPlayer2D<Texture2D> _animation = new();
    private readonly SpriteShader2D _spriteShader;
    private readonly WorldObject2D _visual;
    private readonly Vector2 _visualOffset;
    private readonly float _maximumFallSpeed;

    private AnimationClip2D<Texture2D> _idleAnimation = null!;
    private AnimationClip2D<Texture2D> _walkAnimation = null!;
    private AnimationClip2D<Texture2D> _jumpAnimation = null!;
    private AnimationClip2D<Texture2D> _fallAnimation = null!;
    private AnimationClip2D<Texture2D> _wallGripAnimation = null!;
    private AnimationClip2D<Texture2D> _dashAnimation = null!;
    private AnimationClip2D<Texture2D> _landingAnimation = null!;
    private AnimationClip2D<Texture2D> _hitAnimation = null!;
    private AnimationClip2D<Texture2D> _meleeAttackAnimation = null!;
    private AnimationClip2D<Texture2D> _shotAnimation = null!;
    private AnimationClip2D<Texture2D> _shieldBlockAnimation = null!;
    private string _characterId = string.Empty;
    private bool _disposed;

    public PlayerPresentation2D(
        Scene2D scene,
        TextureCache2D textures,
        TraversalMetrics2D traversal)
    {
        ArgGuard.ThrowIfNull(scene);
        _textures = ArgGuard.RequireNotNull(textures);
        ArgGuard.ThrowIfNull(traversal);

        _visualOffset = traversal.PlayerVisualOffset;
        _maximumFallSpeed = traversal.MaximumFallSpeed;

        LoadCharacter(SwordCharacterId);
        _animation.Play(_idleAnimation);
        _spriteShader = new SpriteShader2D(_animation.CurrentFrame);
        _visual = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(traversal.PlayerVisualSize),
            _spriteShader)
        {
            ZIndex = 1
        };
        scene.Add(_visual);
    }

    public float ShotAnimationDuration => _shotAnimation.Duration;
    public float ShotAnimationElapsedSeconds => _animation.ElapsedSeconds;
    public bool IsPlayingShot => ReferenceEquals(_animation.Clip, _shotAnimation);

    public void EquipRightHandWeapon(string equipmentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssetId2D.Validate(equipmentId);

        var replacement = string.Equals(equipmentId, "gun", StringComparison.Ordinal)
            ? GunCharacterId
            : SwordCharacterId;
        if (string.Equals(_characterId, replacement, StringComparison.Ordinal))
            return;

        LoadCharacter(replacement);
        _animation.Play(_idleAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
        _spriteShader.Texture = _animation.CurrentFrame;
    }

    public void PlayMeleeAttack() => PlayFastMeleeAnimation(_meleeAttackAnimation);

    public void PlayShot()
    {
        _animation.Play(_shotAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void PlayHit()
    {
        _animation.Play(_hitAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void Update(
        float deltaSeconds,
        long frameNumber,
        Vector2 playerPosition,
        float moveInputX,
        float facing,
        bool isGrounded,
        bool isWallGripping,
        bool isDashing,
        bool isShieldBlocking,
        float verticalVelocity,
        float landingSpeed,
        bool isMeleeAttackActive,
        float invulnerabilitySeconds)
    {
        var isPlayingShot = IsPlayingShot && !_animation.IsFinished;
        var isPlayingHit =
            ReferenceEquals(_animation.Clip, _hitAnimation) &&
            !_animation.IsFinished;
        var isPlayingMeleeAnimation =
            ReferenceEquals(_animation.Clip, _meleeAttackAnimation) &&
            !_animation.IsFinished;
        var isPlayingLanding =
            ReferenceEquals(_animation.Clip, _landingAnimation) &&
            !_animation.IsFinished;
        var isPlayingShieldBlock =
            isShieldBlocking &&
            ReferenceEquals(_animation.Clip, _shieldBlockAnimation);

        if (isDashing && !ReferenceEquals(_animation.Clip, _dashAnimation))
        {
            _animation.Play(_dashAnimation, restart: true);
            _animation.PlaybackSpeed = 1f;
        }

        if (!isDashing &&
            isShieldBlocking &&
            !isMeleeAttackActive &&
            !isPlayingMeleeAnimation &&
            !isPlayingShot &&
            !isPlayingHit &&
            !isPlayingShieldBlock)
        {
            _animation.Play(_shieldBlockAnimation);
            _animation.PlaybackSpeed = 1f;
            isPlayingShieldBlock = true;
        }

        var landedAtTerminalVelocity =
            isGrounded &&
            landingSpeed >= _maximumFallSpeed - TerminalVelocityEpsilon;
        if (!isDashing &&
            !isMeleeAttackActive &&
            !isPlayingMeleeAnimation &&
            !isShieldBlocking &&
            !isPlayingShot &&
            !isPlayingHit &&
            landedAtTerminalVelocity)
        {
            _animation.Play(_landingAnimation, restart: true);
            _animation.PlaybackSpeed = 1f;
            isPlayingLanding = true;
        }

        if (!isDashing &&
            !isMeleeAttackActive &&
            !isPlayingMeleeAnimation &&
            !isShieldBlocking &&
            !isPlayingShot &&
            !isPlayingHit &&
            !isPlayingLanding)
        {
            var isWalking = isGrounded && MathF.Abs(moveInputX) > 0.01f;
            var locomotionClip = isWallGripping
                ? _wallGripAnimation
                : isGrounded
                ? isWalking
                    ? _walkAnimation
                    : _idleAnimation
                : verticalVelocity <= 0f
                    ? _fallAnimation
                    : _jumpAnimation;
            if (!ReferenceEquals(_animation.Clip, locomotionClip))
                _animation.Play(locomotionClip);
            _animation.PlaybackSpeed = isWalking
                ? Math.Clamp(MathF.Abs(moveInputX), 0.65f, 1.35f)
                : 1f;
        }

        _animation.Update(deltaSeconds);
        _spriteShader.Texture = _animation.CurrentFrame;
        _spriteShader.FlipX = facing < 0f;
        _visual.Transform.Position = playerPosition + _visualOffset;
        _visual.IsVisible = invulnerabilitySeconds <= 0f || frameNumber % 12 < 6;
    }

    public void Reset()
    {
        _animation.Play(_idleAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void LoadCharacter(string characterId)
    {
        _idleAnimation = LoadAnimation(characterId, "idle");
        _walkAnimation = LoadAnimation(characterId, "walk");
        _jumpAnimation = LoadAnimation(characterId, "jump-start");
        _fallAnimation = LoadAnimation(characterId, "fall");
        _wallGripAnimation = LoadAnimation(characterId, "wall-grip");
        _dashAnimation = LoadAnimation(characterId, "dash");
        _landingAnimation = LoadAnimation(characterId, "land");
        _hitAnimation = LoadAnimation(characterId, "hit-a");
        _meleeAttackAnimation = LoadAnimation(characterId, "sword-attack");
        _shotAnimation = LoadAnimation(characterId, "magic-shot");
        _shieldBlockAnimation = LoadAnimation(characterId, "shield-block");
        _characterId = characterId;
    }

    private AnimationClip2D<Texture2D> LoadAnimation(
        string characterId,
        string animationId) =>
        CharacterAnimationAssets2D.LoadClip(_textures, characterId, animationId);

    private void PlayFastMeleeAnimation(AnimationClip2D<Texture2D> animation)
    {
        _animation.Play(animation, restart: true);
        _animation.PlaybackSpeed = animation.Duration / MeleeAttack2D.FastDurationSeconds;
    }
}
