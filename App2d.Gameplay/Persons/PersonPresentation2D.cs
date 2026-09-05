using App2d.Core;
using App2d.Core.Animation;
using App2d.Core.Geometry;
using App2d.Gameplay.Assets;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay.Persons;

/// <summary>
/// Selects between person skins and keeps animation playback observational:
/// gameplay timing remains authoritative in locomotion and actions.
/// </summary>
public sealed class PersonPresentation2D : IDisposable
{
    private const float TerminalVelocityEpsilon = 25f;
    private const string SwordCharacterId = "player-sword";
    private const string GunCharacterId = "player-gun";
    private const string UnarmedCharacterId = "player-unarmed";

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
    private AnimationClip2D<Texture2D> _deathAnimation = null!;
    private AnimationClip2D<Texture2D> _meleeAttackAnimation = null!;
    private AnimationClip2D<Texture2D> _wallMeleeAttackAnimation = null!;
    private AnimationClip2D<Texture2D> _shotAnimation = null!;
    private AnimationClip2D<Texture2D> _wallShotAnimation = null!;
    private AnimationClip2D<Texture2D> _shieldBlockAnimation = null!;
    private AnimationClip2D<Texture2D> _punchAnimation = null!;
    private AnimationClip2D<Texture2D> _kickAnimation = null!;
    private string _characterId = string.Empty;
    private bool _simulationVisible = true;
    private bool _disposed;

    public PersonPresentation2D(
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

    public bool IsPlayingShot =>
        ReferenceEquals(_animation.Clip, _shotAnimation) ||
        ReferenceEquals(_animation.Clip, _wallShotAnimation);

    public void Equip(string equipmentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssetId2D.Validate(equipmentId);

        var replacement = equipmentId switch
        {
            "gun" => GunCharacterId,
            "unarmed" => UnarmedCharacterId,
            _ => SwordCharacterId
        };
        if (string.Equals(_characterId, replacement, StringComparison.Ordinal))
            return;

        LoadCharacter(replacement);
        _animation.Play(_idleAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
        _spriteShader.Texture = _animation.CurrentFrame;
    }

    public void PlayMeleeAttack(float durationSeconds, bool isWallGripping) =>
        PlayTimedMeleeAnimation(
            isWallGripping
                ? _wallMeleeAttackAnimation
                : _meleeAttackAnimation,
            durationSeconds);

    public void PlayShot(bool isWallGripping)
    {
        _animation.Play(
            isWallGripping ? _wallShotAnimation : _shotAnimation,
            restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void PlayUnarmedAttack(
        UnarmedAttackKind2D kind,
        float durationSeconds)
    {
        var animation = kind == UnarmedAttackKind2D.Punch
            ? _punchAnimation
            : _kickAnimation;
        PlayTimedMeleeAnimation(animation, durationSeconds);
    }

    public void PlayHit()
    {
        _animation.Play(_hitAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void PlayDeath()
    {
        _animation.Play(_deathAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void Update(
        float deltaSeconds,
        long frameNumber,
        Person2D person,
        float moveInputX,
        bool isShieldBlocking,
        bool isMeleeAttackActive)
    {
        ArgGuard.ThrowIfNull(person);
        if (ReferenceEquals(_animation.Clip, _deathAnimation))
        {
            UpdateVisual(deltaSeconds, frameNumber, person.Position, person.Facing, 0f);
            return;
        }

        var isPlayingShot = IsPlayingShot && !_animation.IsFinished;
        var isPlayingHit =
            ReferenceEquals(_animation.Clip, _hitAnimation) &&
            !_animation.IsFinished;
        var isPlayingMeleeAnimation =
            (ReferenceEquals(_animation.Clip, _meleeAttackAnimation) ||
             ReferenceEquals(_animation.Clip, _wallMeleeAttackAnimation)) &&
            !_animation.IsFinished;
        var isPlayingLanding =
            ReferenceEquals(_animation.Clip, _landingAnimation) &&
            !_animation.IsFinished;
        var isPlayingShieldBlock =
            isShieldBlocking &&
            ReferenceEquals(_animation.Clip, _shieldBlockAnimation);

        if (person.IsDashing && !ReferenceEquals(_animation.Clip, _dashAnimation))
        {
            _animation.Play(_dashAnimation, restart: true);
            _animation.PlaybackSpeed = 1f;
        }

        if (!person.IsDashing &&
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
            person.IsGrounded &&
            person.LandingSpeedThisFrame >= _maximumFallSpeed - TerminalVelocityEpsilon;
        if (!person.IsDashing &&
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

        if (!person.IsDashing &&
            !isMeleeAttackActive &&
            !isPlayingMeleeAnimation &&
            !isShieldBlocking &&
            !isPlayingShot &&
            !isPlayingHit &&
            !isPlayingLanding)
        {
            var isWalking = person.IsGrounded && MathF.Abs(moveInputX) > 0.01f;
            var locomotionClip = person.IsWallGripping
                ? _wallGripAnimation
                : person.IsGrounded
                ? isWalking
                    ? _walkAnimation
                    : _idleAnimation
                : person.Body.LinearVelocity.Y <= 0f
                    ? _fallAnimation
                    : _jumpAnimation;
            if (!ReferenceEquals(_animation.Clip, locomotionClip))
                _animation.Play(locomotionClip);
            _animation.PlaybackSpeed = isWalking
                ? Math.Clamp(MathF.Abs(moveInputX), 0.65f, 1.35f)
                : 1f;
        }

        UpdateVisual(
            deltaSeconds,
            frameNumber,
            person.Position,
            person.Facing,
            person.InvulnerabilitySeconds);
    }

    public void SetVisible(bool visible)
    {
        _simulationVisible = visible;
        _visual.IsVisible = visible;
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
        _deathAnimation = LoadAnimation(characterId, "death");
        if (string.Equals(characterId, UnarmedCharacterId, StringComparison.Ordinal))
        {
            _punchAnimation = LoadAnimation(characterId, "punch");
            _kickAnimation = LoadAnimation(characterId, "kick");
            _meleeAttackAnimation = _punchAnimation;
            _wallMeleeAttackAnimation = _punchAnimation;
            _shotAnimation = _kickAnimation;
            _wallShotAnimation = _kickAnimation;
        }
        else
        {
            _meleeAttackAnimation = LoadAnimation(characterId, "sword-attack");
            _wallMeleeAttackAnimation = string.Equals(
                characterId,
                SwordCharacterId,
                StringComparison.Ordinal)
                ? LoadAnimation(characterId, "wall-sword-attack")
                : _meleeAttackAnimation;
            _shotAnimation = LoadAnimation(characterId, "magic-shot");
            _wallShotAnimation = string.Equals(
                characterId,
                GunCharacterId,
                StringComparison.Ordinal)
                ? LoadAnimation(characterId, "wall-shot")
                : _shotAnimation;
            _punchAnimation = _meleeAttackAnimation;
            _kickAnimation = _meleeAttackAnimation;
        }
        _shieldBlockAnimation = LoadAnimation(characterId, "shield-block");
        _characterId = characterId;
    }

    private AnimationClip2D<Texture2D> LoadAnimation(
        string characterId,
        string animationId) =>
        CharacterAnimationAssets2D.LoadClip(_textures, characterId, animationId);

    private void UpdateVisual(
        float deltaSeconds,
        long frameNumber,
        Vector2 playerPosition,
        float facing,
        float invulnerabilitySeconds)
    {
        _animation.Update(deltaSeconds);
        _spriteShader.Texture = _animation.CurrentFrame;
        _spriteShader.FlipX = facing < 0f;
        _visual.Transform.Position = playerPosition + _visualOffset;
        _visual.IsVisible = _simulationVisible &&
            (invulnerabilitySeconds <= 0f || frameNumber % 12 < 6);
    }

    private void PlayTimedMeleeAnimation(
        AnimationClip2D<Texture2D> animation,
        float durationSeconds)
    {
        ArgGuard.ThrowIfNotPositive(durationSeconds);
        _animation.Play(animation, restart: true);
        _animation.PlaybackSpeed = animation.Duration / durationSeconds;
    }
}
