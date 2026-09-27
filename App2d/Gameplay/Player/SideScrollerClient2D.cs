using App2d.Core;
using App2d.Diagnostics;
using App2d.Game.Presentation.Audio;
using App2d.Game.Presentation.Player;
using App2d.Game.Presentation.World.Presentation;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Presentation;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Rendering.Vegetation;
using System.Collections.Immutable;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Gameplay.Player;

/// <summary>Local input and presentation for one player. Reads only session value messages.</summary>
internal sealed class SideScrollerClient2D : IDisposable
{
    private const float SaveFeedbackDurationSeconds = 1.1f;
    private readonly SessionClient2D _endpoint;
    private readonly PlayerInputMapper2D _input = new();
    private readonly AuthoredPersonPresentation2D _presentation;
    private readonly SideScrollerCamera2D _cameraController;
    private readonly Camera2D _camera;
    private readonly SoundEffectBank2D _sounds;
    private readonly TraversalMetrics2D _traversal;
    private readonly TraversalDebugRenderer2D _traversalDebug;
    private readonly WeaponPresentation2D _weapons;
    private readonly EnemyPresentation2D _enemies;
    private readonly CombatContactPresentation2D _contacts;
    private readonly WorldPresentation2D _world;
    private SoundEffectVoice2D _jumpSound;
    private float _saveFeedbackSeconds;
    private Vector2 _saveFeedbackCenter;
    private bool _lastSaveSucceeded;
    private bool _shieldPose;

    public SideScrollerClient2D(SessionSnapshot2D initial, EntityId2D playerId, Scene2D scene,
        Camera2D camera, SideScrollerCamera2D cameraController,
        TextureCache2D textures, SoundEffectBank2D sounds, TraversalMetrics2D traversal, PersonMoves moves)
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
        _presentation = new AuthoredPersonPresentation2D(scene, moves, traversal);
        _presentation.Equip(initialState.Equipment);
        WorldSounds = new SpatialSoundEffectSink2D(sounds, () => State.Person.Position);
        _weapons = new WeaponPresentation2D(scene, textures, WorldSounds);
        _weapons.ApplyState(initialState.Weapons, initialState.Equipment, []);
        _enemies = new EnemyPresentation2D(scene, textures, traversal, WorldSounds);
        _contacts = new CombatContactPresentation2D(scene);
        _enemies.ApplyState(initial.Enemies, [], initial.Tick);
        ApplyPlayerState();
    }

    public PlayerState2D State => _endpoint.State;
    public LevelContent2D Content => _endpoint.Snapshot.Content;
    public void SetVisibleTerrain(ImmutableArray<TerrainChunkState2D> terrain) => _world.SetVisibleTerrain(terrain);
    public void DrawTrees(Renderer2D renderer) => _world.DrawTrees(renderer, _camera.VisibleWorldBounds);
    public void DrawGrass(Renderer2D renderer, VegetationLayer2D layer) =>
        _world.DrawGrass(renderer, _camera.VisibleWorldBounds, layer);
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
        _weapons.ApplyState(State.Weapons, State.Equipment,
            frame.Events.OfType<WeaponOccurred2D>().Select(e => e.Occurrence));

        foreach (var occurrence in frame.Events)
        {
            // Player-scoped facts belong to this client's player; other players' facts are not presented here yet.
            var isMine = occurrence.Stamp.EntityId == _endpoint.PlayerId;
            if (occurrence is CombatDamageOccurred2D confirmed)
            {
                _contacts.Present(confirmed.Damage);
                _presentation.PresentContact(confirmed.Damage, _endpoint.PlayerId, frame.Tick);
                _enemies.PresentContact(confirmed.Damage);
                if (confirmed.Damage.Contact is { Kind: CombatImpactKind2D.Sword } contact)
                    WorldSounds.PlayAt(SoundEffect2D.SwordHit, contact.Position);
            }
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
                    _sounds.Play(landed.ImpactSpeed >= _traversal.HardLandingSpeed
                        ? SoundEffect2D.PlayerLandHard : SoundEffect2D.PlayerLandSoft);
                    if (landed.ImpactSpeed >= _traversal.HardLandingSpeed)
                    {
                        _presentation.PlayLanding();
                        var impact = _traversal.HardLandingIntensity(landed.ImpactSpeed);
                        _cameraController.Shake(float.Lerp(1f, 1.75f, impact), stabilizeVerticalFollow: true);
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
                    _contacts.Reset();
                    _enemies.ResetContact();
                    break;
                case GoalReached2D when isMine: _sounds.Play(SoundEffect2D.GoalReached); _presentation.PlayCelebrate(); break;
                case CheckpointActivated2D checkpoint when isMine: CheckpointActivated?.Invoke(checkpoint); break;
                case EquipmentChanged2D equipment when isMine:
                    _presentation.Equip(equipment.Equipment);
                    break;
                case AttackStarted2D attack when isMine: PresentAttack(attack); break;
            }
        }

        _enemies.ApplyState(frame.Enemies, frame.Events.OfType<EnemyOccurred2D>().Select(e => e.Occurrence), frame.Tick);
        UpdateJumpSound();
        ApplyPlayerState();
    }

    private void PresentAttack(AttackStarted2D attack)
    {
        _presentation.ResetContact();
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
        ArgGuard.ThrowIfNotFiniteOrNegative(deltaSeconds);
        _world.Advance(deltaSeconds);
        Ballistics.Advance(deltaSeconds);
        _enemies.Advance(deltaSeconds);
        _contacts.Advance(deltaSeconds);
        _weapons.Advance(deltaSeconds);
        _presentation.Advance(deltaSeconds);
        _cameraController.Update(State.Person.Position, State.Person.LinearVelocity, State.Person.IsGrounded, deltaSeconds);
    }

    public void RefreshEditorWorld(ImmutableArray<EnemyState2D> enemies, LevelContent2D content, WorldState2D world)
    {
        _enemies.ResetContact(); _presentation.ResetContact();
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
        _contacts.Reset();
        _enemies.ResetContact(); _presentation.ResetContact();
        _input.Reset();
    }

    public string WeaponName => _weapons.WeaponName;

    public void DrawWorldEffects(Renderer2D renderer)
    {
        if (_saveFeedbackSeconds > 0f)
        {
            var feedbackProgress = 1f - _saveFeedbackSeconds / SaveFeedbackDurationSeconds;
            var alpha = (int)Math.Clamp(230f * (1f - feedbackProgress), 0f, 230f);
            renderer.DrawWorldCircle(_saveFeedbackCenter, float.Lerp(28f, 108f, feedbackProgress),
                _lastSaveSucceeded ? new XnaColor(105, 225, 255, alpha) : new XnaColor(255, 95, 95, alpha), 4f);
        }
    }

    public void DrawWorldDebug(Renderer2D renderer)
    {
        if (ShowTraversalDebug) _traversalDebug.DrawWorldDebug(renderer, State.Person.Position, State.Person.Facing);
        Ballistics.DrawWorldDebug(renderer);
    }

    public void DrawUI(Renderer2D renderer)
    {
        PlayerHud2D.Draw(renderer, State.Person.HitPoints, State.Person.MaximumHitPoints,
            _weapons.HudTexture);
        if (_saveFeedbackSeconds > 0f)
            renderer.DrawScreenLabel(_lastSaveSucceeded ? "SAVED" : "SAVE FAILED", new Vector2(24f, 170f));
        if (ShowTraversalDebug) _traversalDebug.DrawUI(renderer);
        Ballistics.DrawUI(renderer);
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
        _contacts.Dispose();
        _weapons.Dispose();
        _presentation.Dispose();
    }
}
