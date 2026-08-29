using System.Numerics;
using App2d.Engine;
using App2d.Engine.Animation;
using App2d.Engine.Geometry;
using App2d.Engine.Rendering.Textures;

namespace App2d.Gameplay;

public sealed class PlayerPresentation2D : IDisposable
{
    private const float TerminalVelocityEpsilon = 25f;
    private const string ShieldEquipmentId = "shield-a";

    private readonly AnimationClip2D<Texture2D> _idleAnimation;
    private readonly AnimationClip2D<Texture2D> _walkAnimation;
    private readonly AnimationClip2D<Texture2D> _duckAnimation;
    private readonly AnimationClip2D<Texture2D> _jumpAnimation;
    private readonly AnimationClip2D<Texture2D> _fallAnimation;
    private readonly AnimationClip2D<Texture2D> _landingAnimation;
    private readonly AnimationClip2D<Texture2D> _hitAnimation;
    private readonly AnimationClip2D<Texture2D> _meleeChopAnimation;
    private readonly AnimationClip2D<Texture2D> _meleeAttackAnimation;
    private readonly AnimationClip2D<Texture2D> _meleeStabAnimation;
    private readonly AnimationClip2D<Texture2D> _shotAnimation;
    private readonly AnimationClip2D<Texture2D> _shieldBlockAnimation;
    private readonly Dictionary<AnimationClip2D<Texture2D>, TextureFrameSet2D> _leftCharacterFrames = [];
    private readonly Dictionary<
        AnimationClip2D<Texture2D>,
        (float Right, float Left)> _horizontalRootOffsetFractions = [];
    private readonly Dictionary<
        AnimationClip2D<Texture2D>,
        EquippedAnimationDefinition2D> _equippedAnimations = [];
    private readonly AnimationPlayer2D<Texture2D> _animation = new();
    private readonly EquippedPlayerLoadout2D _equippedLoadout;
    private readonly SpriteShader2D _spriteShader;
    private SparseCanvasSpriteShader2D? _sparseSpriteShader;
    private SparseDepthCompositeShader2D? _sparseDepthCompositeShader;
    private readonly WorldObject2D _visual;
    private readonly Vector2 _visualOffset;
    private readonly float _visualWidth;
    private readonly float _duckVisualOffsetY;
    private readonly float _maximumFallSpeed;
    private bool _disposed;

    public PlayerPresentation2D(
        Scene2D scene,
        TextureCache2D textures,
        TraversalMetrics2D traversal)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(traversal);

        _visualOffset = traversal.PlayerVisualOffset;
        _visualWidth = traversal.PlayerVisualSize.X;
        _equippedLoadout = new EquippedPlayerLoadout2D(textures);
        _duckVisualOffsetY =
            (traversal.PlayerColliderSize.Y - traversal.PlayerDuckingColliderSize.Y) * 0.5f;
        _maximumFallSpeed = traversal.MaximumFallSpeed;

        _idleAnimation = LoadAnimation(textures, "idle");
        _walkAnimation = LoadAnimation(textures, "walk");
        _duckAnimation = LoadAnimation(textures, "crouch");
        _jumpAnimation = LoadAnimation(textures, "jump-start");
        _fallAnimation = LoadAnimation(textures, "fall");
        _landingAnimation = LoadAnimation(textures, "land");
        _hitAnimation = LoadAnimation(textures, "hit-a");
        _meleeChopAnimation = LoadAnimation(textures, "melee-chop");
        _meleeAttackAnimation = LoadAnimation(textures, "sword-attack");
        _meleeStabAnimation = LoadAnimation(textures, "melee-stab");
        _shotAnimation = LoadAnimation(textures, "magic-shot");
        _shieldBlockAnimation = LoadAnimation(textures, "shield-block");
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
        _equippedLoadout.Equip(equipmentId, _equippedAnimations.Values);
    }

    public void PlayMeleeAttack()
    {
        _animation.Play(_meleeAttackAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void PlayShot()
    {
        _animation.Play(_shotAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void PreviewMeleeChop()
    {
        _animation.Play(_meleeChopAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    public void PreviewMeleeStab()
    {
        _animation.Play(_meleeStabAnimation, restart: true);
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
        bool isDucking,
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
            (ReferenceEquals(_animation.Clip, _meleeChopAnimation) ||
             ReferenceEquals(_animation.Clip, _meleeAttackAnimation) ||
             ReferenceEquals(_animation.Clip, _meleeStabAnimation)) &&
            !_animation.IsFinished;
        var isPlayingLanding =
            ReferenceEquals(_animation.Clip, _landingAnimation) &&
            !_animation.IsFinished;
        var isPlayingShieldBlock =
            isShieldBlocking &&
            ReferenceEquals(_animation.Clip, _shieldBlockAnimation);
        if (isShieldBlocking &&
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
        if (!isMeleeAttackActive &&
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

        if (!isMeleeAttackActive &&
            !isPlayingMeleeAnimation &&
            !isShieldBlocking &&
            !isPlayingShot &&
            !isPlayingHit &&
            !isPlayingLanding)
        {
            var isWalking = isGrounded && MathF.Abs(moveInputX) > 0.01f;
            var locomotionClip = isDucking
                ? _duckAnimation
                : isGrounded
                ? isWalking
                    ? _walkAnimation
                    : _idleAnimation
                : verticalVelocity <= 0f
                    ? _fallAnimation
                    : _jumpAnimation;
            if (!ReferenceEquals(_animation.Clip, locomotionClip))
                _animation.Play(locomotionClip);
            _animation.PlaybackSpeed = isWalking && !isDucking
                ? Math.Clamp(MathF.Abs(moveInputX), 0.65f, 1.35f)
                : 1f;
        }

        _animation.Update(deltaSeconds);
        UpdateVisualShader(facing);
        var activeClip = StateGuard.RequireNotNull(_animation.Clip, "The player presentation requires an active animation clip.");
        var (Right, Left) = _horizontalRootOffsetFractions[activeClip];
        var horizontalRootOffset = (facing < 0f ? Left : Right) * _visualWidth;
        _visual.Transform.Position = playerPosition + _visualOffset + new Vector2(horizontalRootOffset, 0f) + (isDucking ? new Vector2(0f, _duckVisualOffsetY) : Vector2.Zero);
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

        _sparseDepthCompositeShader?.Dispose();
        _equippedLoadout.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private AnimationClip2D<Texture2D> LoadAnimation(TextureCache2D textures, string animationId)
    {
        var animation = CharacterAnimationAssets2D.LoadClip(textures, "player", animationId);
        var additionalEquipmentIds = animationId == "shield-block"
            ? new[] { ShieldEquipmentId }
            : [];
        _equippedAnimations.Add(
            animation,
            new EquippedAnimationDefinition2D(animationId, animation.FrameCount, additionalEquipmentIds));
        _horizontalRootOffsetFractions.Add(
            animation,
            (CharacterAnimationAssets2D.LoadHorizontalRootOffsetFraction(textures, "player", animationId, "right"),
             CharacterAnimationAssets2D.LoadHorizontalRootOffsetFraction(textures, "player", animationId, "left")));
        _leftCharacterFrames.Add(
            animation,
            DirectionalCharacterAnimationAssets2D.LoadFacing(textures, "player", animationId, "left", animation.FrameCount));
        return animation;
    }

    private void UpdateVisualShader(float facing)
    {
        var facesLeft = facing < 0f;
        var currentClip = StateGuard.RequireNotNull(
            _animation.Clip,
            "The player presentation requires an active animation clip.");
        if (_equippedLoadout.IsEquipped)
        {
            var frame = _equippedLoadout.GetFrame(
                _equippedAnimations[currentClip],
                facesLeft ? "left" : "right",
                _animation.CurrentFrameIndex,
                _animation.ElapsedSeconds);
            if (frame.LayeredFrame is { } layeredFrame)
            {
                if (_sparseDepthCompositeShader is null)
                {
                    _sparseDepthCompositeShader = new SparseDepthCompositeShader2D(
                        layeredFrame,
                        frame.SourceCanvasSize,
                        frame.SourceRoot);
                }
                else
                {
                    _sparseDepthCompositeShader.SetFrame(
                        layeredFrame,
                        frame.SourceCanvasSize,
                        frame.SourceRoot);
                }
                _visual.Shader = _sparseDepthCompositeShader;
                return;
            }

            if (frame.SparseFrame is { } sparseFrame)
            {
                if (_sparseSpriteShader is null)
                {
                    _sparseSpriteShader = new SparseCanvasSpriteShader2D(
                        sparseFrame,
                        frame.SourceCanvasSize,
                        frame.SourceRoot);
                }
                else
                {
                    _sparseSpriteShader.SetFrame(
                        sparseFrame,
                        frame.SourceCanvasSize,
                        frame.SourceRoot);
                }
                _visual.Shader = _sparseSpriteShader;
                return;
            }

            _spriteShader.Texture = StateGuard.RequireNotNull(
                frame.Texture,
                "An equipped legacy frame did not provide a texture.");
            _spriteShader.FlipX = false;
            _visual.Shader = _spriteShader;
            return;
        }

        _spriteShader.Texture = facesLeft
            ? _leftCharacterFrames[currentClip][_animation.CurrentFrameIndex]
            : _animation.CurrentFrame;
        _spriteShader.FlipX = false;
        _visual.Shader = _spriteShader;
    }

}
