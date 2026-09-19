using App2d.Core;
using App2d.Gameplay.Simulation;
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

    private readonly Scene2D _scene;
    private readonly TextureCache2D _textures;
    private readonly AnimationPlayer2D<Texture2D> _animation = new();
    private readonly SpriteShader2D _spriteShader;
    private readonly WorldObject2D _visual;
    private readonly Vector2 _visualOffset;
    private readonly float _maximumFallSpeed;

    private AnimationClip2D<Texture2D> _idleAnimation = null!;
    private AnimationClip2D<Texture2D> _balanceLeftFootAnimation = null!;
    private AnimationClip2D<Texture2D> _balanceRightFootAnimation = null!;
    private AnimationClip2D<Texture2D> _walkAnimation = null!;
    private AnimationClip2D<Texture2D> _jumpAnimation = null!;
    private AnimationClip2D<Texture2D> _fallAnimation = null!;
    private AnimationClip2D<Texture2D> _wallGripAnimation = null!;
    private AnimationClip2D<Texture2D> _climbAnimation = null!;
    private AnimationClip2D<Texture2D> _dashAnimation = null!;
    private AnimationClip2D<Texture2D> _landingAnimation = null!;
    private AnimationClip2D<Texture2D> _hitAnimation = null!;
    private AnimationClip2D<Texture2D> _deathAnimation = null!;
    private AnimationClip2D<Texture2D> _meleeAttackAnimation = null!;
    private AnimationClip2D<Texture2D> _wallMeleeAttackAnimation = null!;
    private AnimationClip2D<Texture2D> _downAttackAnimation = null!;
    private AnimationClip2D<Texture2D> _shotAnimation = null!;
    private AnimationClip2D<Texture2D> _wallShotAnimation = null!;
    private AnimationClip2D<Texture2D> _shieldBlockAnimation = null!;
    private AnimationClip2D<Texture2D> _punchAnimation = null!;
    private AnimationClip2D<Texture2D> _kickAnimation = null!;
    private string _characterId = string.Empty;
    private bool _simulationVisible = true;
    private bool _disposed;
    private bool _observedAction;
    private PersonState2D _latestState;
    private float _latestMoveX;
    private bool _latestShield;
    private bool _latestMelee;
    private long _latestTick;
    private float _secondsSinceState;
    private bool _hasState;

    public void ApplyState(PersonState2D state, long tick, float moveX, bool shield, bool melee)
    {
        _latestState = state;
        _latestTick = tick;
        _latestMoveX = moveX;
        _latestShield = shield;
        _latestMelee = melee;
        _secondsSinceState = 0f;
        _hasState = true;
        Update(0f, tick, state, moveX, shield, melee);
    }

    public void Advance(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        if (!_hasState) return;
        _secondsSinceState += deltaSeconds;
        var state = _latestState with
        {
            LandingSpeedThisFrame = 0f,
            InvulnerabilitySeconds = Math.Max(0f, _latestState.InvulnerabilitySeconds - _secondsSinceState),
            Action = _latestState.Action with { ElapsedSeconds = _latestState.Action.ElapsedSeconds + _secondsSinceState }
        };
        Update(deltaSeconds, _latestTick + (long)(_secondsSinceState * 120f), state,
            _latestMoveX, _latestShield, _latestMelee && (!_latestState.Action.IsActive || state.Action.IsActive));
    }

    public PersonPresentation2D(
        Scene2D scene,
        TextureCache2D textures,
        TraversalMetrics2D traversal)
    {
        _scene = ArgGuard.RequireNotNull(scene);
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

    public void Equip(EquipmentKind2D equipment)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var replacement = equipment switch
        {
            EquipmentKind2D.Gun => GunCharacterId,
            EquipmentKind2D.Unarmed => UnarmedCharacterId,
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

    public void PlayDownAttack(float durationSeconds) =>
        PlayTimedMeleeAnimation(_downAttackAnimation, durationSeconds);

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
        PersonState2D person,
        float moveInputX,
        bool isShieldBlocking,
        bool isMeleeAttackActive)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        if (!person.IsAlive)
        {
            if (!ReferenceEquals(_animation.Clip, _deathAnimation)) PlayDeath();
            _observedAction = false;
            UpdateVisual(deltaSeconds, frameNumber, person.Position, person.Facing, 0f);
            return;
        }
        if (ReferenceEquals(_animation.Clip, _deathAnimation)) Reset();
        var action = person.Action;
        var shotClip = person.IsWallGripping ? _wallShotAnimation : _shotAnimation;
        // Recovery controls gameplay; the authored recoil can finish after recovery.
        var showShot = action.Kind == PlayerAttackKind2D.Shot && action.DurationSeconds > 0f &&
            action.ElapsedSeconds < shotClip.Duration;
        if (action.IsActive || showShot)
        {
            var clip = action.Kind switch
            {
                PlayerAttackKind2D.Downward => _downAttackAnimation,
                PlayerAttackKind2D.Shot => shotClip,
                PlayerAttackKind2D.Punch => _punchAnimation,
                PlayerAttackKind2D.Kick => _kickAnimation,
                _ => person.IsWallGripping ? _wallMeleeAttackAnimation : _meleeAttackAnimation
            };
            PlayTimedMeleeAnimation(clip, action.Kind == PlayerAttackKind2D.Shot ? clip.Duration : action.DurationSeconds);
            _animation.Update(Math.Max(0f, action.ElapsedSeconds));
            _observedAction = true;
            UpdateVisual(0f, frameNumber, person.Position, person.Facing, person.InvulnerabilitySeconds);
            return;
        }
        if (_observedAction) { Reset(); _observedAction = false; }
        if (person.IsChargingPrimary)
        {
            // The baked idle's first frame is also the gun's authored muzzle pose.
            _animation.Play(_idleAnimation, restart: true);
            _animation.PlaybackSpeed = 0f;
            UpdateVisual(0f, frameNumber, person.Position, person.Facing, person.InvulnerabilitySeconds);
            return;
        }
        var isPlayingShot = IsPlayingShot && !_animation.IsFinished;
        var isPlayingHit =
            ReferenceEquals(_animation.Clip, _hitAnimation) &&
            !_animation.IsFinished;
        var isPlayingMeleeAnimation =
            (ReferenceEquals(_animation.Clip, _meleeAttackAnimation) ||
             ReferenceEquals(_animation.Clip, _wallMeleeAttackAnimation) ||
             ReferenceEquals(_animation.Clip, _downAttackAnimation)) &&
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
            var locomotionClip = person.IsClimbingLadder
                ? _climbAnimation
                : person.IsWallGripping
                ? _wallGripAnimation
                : person.IsGrounded
                ? isWalking
                    ? _walkAnimation
                    : SelectStandingAnimation(person)
                : person.LinearVelocity.Y <= 0f
                    ? _fallAnimation
                    : _jumpAnimation;
            if (!ReferenceEquals(_animation.Clip, locomotionClip))
                _animation.Play(locomotionClip);
            _animation.PlaybackSpeed = person.IsClimbingLadder
                ? MathF.Abs(person.LinearVelocity.Y) > 0.01f ? 1f : 0f
                : isWalking
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
        _scene.Remove(_visual);
        GC.SuppressFinalize(this);
    }

    private void LoadCharacter(string characterId)
    {
        _idleAnimation = LoadAnimation(characterId, "idle");
        var hasBalanceAnimations = string.Equals(characterId, SwordCharacterId, StringComparison.Ordinal);
        _balanceLeftFootAnimation = hasBalanceAnimations
            ? LoadAnimation(characterId, "balance-left-foot") : _idleAnimation;
        _balanceRightFootAnimation = hasBalanceAnimations
            ? LoadAnimation(characterId, "balance-right-foot") : _idleAnimation;
        _walkAnimation = LoadAnimation(characterId, "walk");
        _jumpAnimation = LoadAnimation(characterId, "jump-start");
        _fallAnimation = LoadAnimation(characterId, "fall");
        _wallGripAnimation = LoadAnimation(characterId, "wall-grip");
        _climbAnimation = LoadAnimation(characterId, "climb");
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
            if (string.Equals(characterId, GunCharacterId, StringComparison.Ordinal))
            {
                // Keep the barrel at its socket for the flash, then play baked recoil.
                var durations = Enumerable.Repeat(
                    0.12f / _shotAnimation.FrameCount, _shotAnimation.FrameCount + 1).ToArray();
                durations[0] = 0.06f;
                _shotAnimation = new AnimationClip2D<Texture2D>(
                    new[] { _idleAnimation[0] }.Concat(_shotAnimation.Frames),
                    durations,
                    isLooping: false);
            }
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
        _downAttackAnimation = string.Equals(characterId, SwordCharacterId, StringComparison.Ordinal)
            ? LoadAnimation(characterId, "sword-down-attack") : _meleeAttackAnimation;
        _characterId = characterId;
    }

    private AnimationClip2D<Texture2D> SelectStandingAnimation(PersonState2D person)
    {
        // Clip names describe the planted foot in the right-facing source image.
        // Mirroring the sprite swaps which world edge each pose balances over.
        var localEdgeDirection = person.BalanceDirection * person.Facing;
        return localEdgeDirection > 0f ? _balanceLeftFootAnimation
            : localEdgeDirection < 0f ? _balanceRightFootAnimation
            : _idleAnimation;
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
