using App2d.Core;
using App2d.Diagnostics;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Persons.Presentation;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Gameplay.World.Presentation;
using App2d.Gameplay.Enemies;
using System.Collections.Immutable;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Gameplay.Player;

/// <summary>Local input and presentation for one player. Reads only session value messages.</summary>
internal sealed class SideScrollerClient2D : IDisposable
{
    private const float HardLandingSpeed = 650f;
    private const float SaveFeedbackDurationSeconds = 1.1f;
    private readonly SessionClient2D _endpoint;
    private readonly PlayerInputMapper2D _input = new();
    private readonly PointPersonPresentation2D _presentation;
    private readonly SideScrollerCamera2D _cameraController;
    private readonly Camera2D _camera;
    private readonly SoundEffectBank2D _sounds;
    private readonly TraversalMetrics2D _traversal;
    private readonly TraversalDebugRenderer2D _traversalDebug;
    private readonly WeaponPresentation2D _weapons;
    private readonly EnemyPresentation2D _enemies;
    private readonly WorldPresentation2D _world;
    private SoundEffectVoice2D _jumpSound;
    private float _saveFeedbackSeconds;
    private Vector2 _saveFeedbackCenter;
    private bool _lastSaveSucceeded;
    private bool _shieldPose;

    public SideScrollerClient2D(SessionSnapshot2D initial, EntityId2D playerId, Scene2D scene,
        Camera2D camera, SideScrollerCamera2D cameraController,
        TextureCache2D textures, SoundEffectBank2D sounds, TraversalMetrics2D traversal, App2d.Core.Characters.EntityCatalog characters)
    {
        _endpoint = new SessionClient2D(initial, playerId);
        var initialState = _endpoint.State;
        _camera = camera;
        _cameraController = cameraController;
        _sounds = sounds;
        _traversal = traversal;
        _traversalDebug = new TraversalDebugRenderer2D(traversal);
        Ballistics = new BallisticsDebug2D(traversal);
        _world = new WorldPresentation2D(scene, textures);
        _world.ApplyState(initial.Content, initial.World);
        _presentation = new PointPersonPresentation2D(scene, characters, traversal);
        _presentation.Equip(initialState.Equipment);
        WorldSounds = new SpatialSoundEffectSink2D(sounds, () => State.Person.Position);
        _weapons = new WeaponPresentation2D(scene, textures, WorldSounds);
        _weapons.ApplyState(initialState.Weapons, initialState.Equipment, []);
        _enemies = new EnemyPresentation2D(scene, textures, traversal, WorldSounds, characters);
        _enemies.ApplyState(initial.Enemies, [], initial.Tick);
        ApplyPlayerState();
    }

    public PlayerState2D State => _endpoint.State;
    public void SetVisibleTerrain(ImmutableArray<TerrainChunkState2D> terrain) => _world.SetVisibleTerrain(terrain);
    public bool IsControllerConnected => _input.IsControllerConnected;
    public bool ShowTraversalDebug { get; set; }
    public BallisticsDebug2D Ballistics { get; }
    public SpatialSoundEffectSink2D WorldSounds { get; }
    public event Action<CheckpointActivated2D>? CheckpointActivated;

    public PlayerInput2D CaptureInput(InputState input)
    {
        var debug = PlayerDebugInput2D.Capture(input);
        if (debug.ToggleTraversal) ShowTraversalDebug = !ShowTraversalDebug;
        if (debug.FireBallistics) Ballistics.Launch(State.Person.Position, State.Person.Facing);
        if (debug.ClearBallistics) Ballistics.Clear();
        _shieldPose = debug.ShieldPose;
        var command = _input.Capture(input);
        return _endpoint.CreateInput(command);
    }

    public void Apply(SessionFrame2D frame)
    {
        if (!_endpoint.Apply(frame)) return;
        _world.ApplyState(frame.Content, frame.World);
        _enemies.ApplyState(frame.Enemies, frame.Events.OfType<EnemyOccurred2D>().Select(e => e.Occurrence), frame.Tick);
        _weapons.ApplyState(State.Weapons, State.Equipment,
            frame.Events.OfType<WeaponOccurred2D>().Select(e => e.Occurrence));

        foreach (var occurrence in frame.Events)
        {
            // Player-scoped facts belong to this client's player; other players' facts are not presented here yet.
            var isMine = occurrence.Stamp.EntityId == _endpoint.PlayerId;
            switch (occurrence)
            {
                case CombatDamageOccurred2D combat when combat.Damage.Faction != CombatFaction2D.Player &&
                    (!combat.Damage.WasKilled || combat.Damage.Faction == CombatFaction2D.Enemy):
                    WorldSounds.PlayAt(combat.Damage.WasKilled ? SoundEffect2D.EnemyDeath : SoundEffect2D.EnemyHurt,
                        combat.Damage.Position);
                    break;
                case JumpStarted2D when isMine:
                    EndJumpSound();
                    _jumpSound = _sounds.Begin(SoundEffect2D.PlayerJump, 0.3f);
                    break;
                case Landed2D landed when isMine:
                    EndJumpSound();
                    _sounds.Play(landed.ImpactSpeed >= HardLandingSpeed
                        ? SoundEffect2D.PlayerLandHard : SoundEffect2D.PlayerLandSoft);
                    if (landed.ImpactSpeed >= HardLandingSpeed)
                    {
                        _presentation.PlayLanding();
                        var impact = Math.Clamp((landed.ImpactSpeed - HardLandingSpeed) /
                            MathF.Max(1f, _traversal.MaximumFallSpeed - HardLandingSpeed), 0f, 1f);
                        _cameraController.Shake(float.Lerp(1f, 2.5f, impact), stabilizeVerticalFollow: true);
                    }
                    break;
                case Footstep2D when isMine: _sounds.Play(SoundEffect2D.PlayerFootstep); break;
                case Damaged2D when isMine:
                    _sounds.Play(SoundEffect2D.PlayerHurt);
                    _presentation.PlayHit();
                    _cameraController.Shake(4f);
                    break;
                case Died2D when isMine:
                    EndJumpSound();
                    _presentation.PlayDeath();
                    break;
                case Respawned2D respawn when isMine:
                    EndJumpSound();
                    _presentation.Reset();
                    _cameraController.Reset(respawn.Position);
                    _sounds.Play(SoundEffect2D.PlayerRespawn);
                    _weapons.Reset();
                    break;
                case GoalReached2D when isMine: _sounds.Play(SoundEffect2D.GoalReached); _presentation.PlayCelebrate(); break;
                case CheckpointActivated2D checkpoint when isMine: CheckpointActivated?.Invoke(checkpoint); break;
                case EquipmentChanged2D equipment when isMine:
                    _presentation.Equip(equipment.Equipment);
                    break;
                case AttackStarted2D attack when isMine: PresentAttack(attack); break;
            }
        }

        UpdateJumpSound();
        ApplyPlayerState();
    }

    private void PresentAttack(AttackStarted2D attack)
    {
        if (attack.Kind is PlayerAttackKind2D.Melee or PlayerAttackKind2D.Downward)
            _sounds.Play(SoundEffect2D.SwordSwing);
    }

    private void ApplyPlayerState()
    {
        _presentation.Equip(State.Equipment);
        _presentation.ApplyState(State.Person, _endpoint.Tick, State.MoveX,
            _shieldPose && State.Person.IsAlive, State.IsMeleeAttackActive);
    }

    public void AdvancePresentation(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        _world.Advance(deltaSeconds);
        Ballistics.Advance(deltaSeconds);
        _enemies.Advance(deltaSeconds);
        _weapons.Advance(deltaSeconds);
        _presentation.Advance(deltaSeconds);
        _cameraController.Update(State.Person.Position, State.Person.LinearVelocity, State.Person.IsGrounded, deltaSeconds);
    }

    public void RefreshEditorWorld(ImmutableArray<EnemyState2D> enemies, LevelContent2D content, WorldState2D world)
    {
        _world.ApplyState(content, world);
        _enemies.ApplyState(enemies, [], _endpoint.Tick);
    }

    public void UpdateFeedback(float dt) =>
        _saveFeedbackSeconds = Math.Max(0f, _saveFeedbackSeconds - dt);

    public void ShowSaveResult(bool succeeded, Vector2 position)
    {
        _lastSaveSucceeded = succeeded;
        _saveFeedbackCenter = position;
        _saveFeedbackSeconds = SaveFeedbackDurationSeconds;
    }

    public void Suspend()
    {
        EndJumpSound();
        _weapons.Suspend();
        _input.Reset();
    }

    public string WeaponName => _weapons.WeaponName;

    public void Draw(Renderer2D renderer)
    {
        PlayerHud2D.Draw(renderer, State.Person.HitPoints, State.Person.MaximumHitPoints,
            _weapons.HudTexture);
        if (_saveFeedbackSeconds > 0f)
        {
            var feedbackProgress = 1f - _saveFeedbackSeconds / SaveFeedbackDurationSeconds;
            var alpha = (int)Math.Clamp(230f * (1f - feedbackProgress), 0f, 230f);
            renderer.DrawWorldCircle(_saveFeedbackCenter, float.Lerp(28f, 108f, feedbackProgress),
                _lastSaveSucceeded ? new XnaColor(105, 225, 255, alpha) : new XnaColor(255, 95, 95, alpha), 4f);
            renderer.DrawScreenLabel(_lastSaveSucceeded ? "SAVED" : "SAVE FAILED", new Vector2(24f, 170f));
        }
        if (ShowTraversalDebug) _traversalDebug.Draw(renderer, State.Person.Position, State.Person.Facing);
        Ballistics.Draw(renderer);
    }

    private void UpdateJumpSound()
    {
        if (!_jumpSound.IsPlaying) return;
        if (!State.Person.IsSustainingJump) { EndJumpSound(); return; }
        var power = State.Person.JumpPower;
        _jumpSound.SetVolumeScale(float.Lerp(0.3f, 1f, power * power * (3f - 2f * power)), 0.015f);
    }

    private void EndJumpSound()
    {
        _jumpSound.Stop(0.04f);
        _jumpSound = default;
    }

    public void Dispose()
    {
        EndJumpSound();
        _world.Dispose();
        _enemies.Dispose();
        _weapons.Dispose();
        _presentation.Dispose();
    }
}
