using App2d.Core;
using App2d.Core.Animation;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Persons.Actions;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay.Persons.Presentation;

/// <summary>Consumes value observations only; effects never control weapon simulation.</summary>
public sealed class WeaponPresentation2D : IDisposable
{
    private readonly Scene2D _scene;
    private readonly ISoundEffectSink2D _sounds;
    private readonly WorldObject2D _glow;
    private readonly WorldObject2D _flash;
    private readonly AnimationPlayer2D<Texture2D> _chargeAnimation = new();
    private readonly Texture2D _swordHud;
    private readonly Texture2D _unarmedHud;
    private readonly Texture2D[] _chargeHud;
    private readonly Texture2D _boltTexture;
    private readonly Texture2D[] _trailTextures;
    private readonly List<BoltVisual> _bolts = [];
    private readonly HashSet<EntityId2D> _activeIds = [];
    private SoundEffectVoice2D _chargeVoice;
    private bool _charging;
    private float _flashSeconds;
    private float _cancelSeconds;
    private float _cancelProgress;
    private WeaponState2D _state = WeaponState2D.Empty;
    private string _equipmentId = "sword";

    public WeaponPresentation2D(Scene2D scene, TextureCache2D textures, ISoundEffectSink2D sounds)
    {
        _scene = scene;
        _sounds = sounds;
        _swordHud = textures.Load("ui/hud/weapons/sword.png");
        _unarmedHud = textures.Load("ui/hud/weapons/unarmed.png");
        _chargeHud = Enumerable.Range(0, 61)
            .Select(i => textures.Load($"ui/hud/gun-charge/frame-{i:0000}.png")).ToArray();
        _glow = CreateVisual(textures.Load("effects/gun/charge.png"), new Vector2(30f));
        _flash = CreateVisual(textures.Load("effects/gun/flash.png"), new Vector2(42.5f, 27.5f));
        _chargeAnimation.Play(new AnimationClip2D<Texture2D>(Enumerable.Range(0, 24)
            .Select(i => textures.Load($"effects/gun/ready/frame-{i:0000}.png")), framesPerSecond: 30f));
        _chargeAnimation.Stop();
        _boltTexture = textures.Load("effects/gun/bolt.png");
        _trailTextures = Enumerable.Range(0, 8)
            .Select(i => textures.Load($"effects/gun/trail-{i:00}.png")).ToArray();
    }

    public string WeaponName => _equipmentId switch { "gun" => "GUN", "unarmed" => "FISTS", _ => "SWORD" };
    public Texture2D HudTexture => _equipmentId switch
    {
        "unarmed" => _unarmedHud,
        "gun" => _chargeHud[Math.Clamp((int)MathF.Round(60f * (_state.IsCharging
            ? _state.ChargeProgress : _flashSeconds > 0f ? 1f : _cancelProgress * _cancelSeconds / 0.14f)), 0, 60)],
        _ => _swordHud
    };

    public void ApplyState(WeaponState2D state, string equipmentId, IEnumerable<WeaponEvent2D> occurrences) =>
        Update(state, equipmentId, occurrences, 0f);

    public void Advance(float deltaSeconds) => Update(_state, _equipmentId, [], deltaSeconds);

    // Combined helper for diagnostics. Deliver occurrences only once.
    public void Update(WeaponState2D state, string equipmentId,
        IEnumerable<WeaponEvent2D> occurrences, float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        _state = state;
        _equipmentId = equipmentId;
        _flashSeconds = Math.Max(0f, _flashSeconds - deltaSeconds);
        _cancelSeconds = Math.Max(0f, _cancelSeconds - deltaSeconds);
        foreach (var occurrence in occurrences)
        {
            switch (occurrence)
            {
                case ChargeStarted2D started: StartCharge(started.Position); break;
                case ChargeCancelled2D cancelled:
                    StopCharge();
                    _cancelProgress = cancelled.Progress;
                    _cancelSeconds = 0.14f;
                    _sounds.PlayAt(SoundEffect2D.GunCancel, cancelled.Position);
                    break;
                case GunFired2D fired:
                    StopCharge();
                    _flashSeconds = 0.06f;
                    _cancelSeconds = 0f;
                    _flash.Transform.Position = fired.Position;
                    _sounds.PlayAt(SoundEffect2D.GunFire, fired.Position);
                    break;
                case ProjectileImpact2D impact:
                    _sounds.PlayAt(SoundEffect2D.GunImpact, impact.Position);
                    break;
                case SwordImpact2D impact:
                    _sounds.PlayAt(SoundEffect2D.SwordHit, impact.Position);
                    break;
            }
        }

        // Ongoing effects follow state, including after pause or a newly attached view.
        if (state.IsCharging)
        {
            if (!_charging) StartCharge(state.MuzzlePosition);
            _chargeVoice.SetPosition(state.MuzzlePosition);
            _chargeAnimation.Update(deltaSeconds);
            ((SpriteShader2D)_glow.Shader).Texture = _chargeAnimation.CurrentFrame;
        }
        else StopCharge();
        if (equipmentId != "gun") _flashSeconds = _cancelSeconds = 0f;
        _glow.IsVisible = state.IsCharging || _cancelSeconds > 0f;
        _glow.Transform.Position = state.MuzzlePosition;
        var scale = state.IsCharging ? 0.2f + 0.8f * state.ChargeProgress
            : (0.2f + 0.8f * _cancelProgress) * _cancelSeconds / 0.14f;
        _glow.Transform.Scale = new Vector2(Math.Max(0.001f, scale));
        _flash.IsVisible = _flashSeconds > 0f;

        _activeIds.Clear();
        if (!state.Projectiles.IsDefault)
            foreach (var projectile in state.Projectiles) _activeIds.Add(projectile.Id);
        foreach (var bolt in _bolts)
        {
            if (_activeIds.Contains(bolt.Id)) continue;
            bolt.Visual.IsVisible = false;
            bolt.Trail.Fade(deltaSeconds);
        }
        if (state.Projectiles.IsDefault) return;
        foreach (var projectile in state.Projectiles)
        {
            var bolt = _bolts.Find(b => b.Id == projectile.Id);
            if (bolt is null)
            {
                bolt = _bolts.Find(b => !_activeIds.Contains(b.Id) && !b.Trail.IsVisible);
                if (bolt is null)
                {
                    bolt = new BoltVisual(CreateVisual(_boltTexture, new Vector2(30f, 10f)),
                        new GunBoltTrail2D(_scene, _trailTextures));
                    _bolts.Add(bolt);
                }
                bolt.Id = projectile.Id;
                bolt.Trail.Begin(projectile.Origin, MathF.Sign(projectile.Velocity.X), projectile.Velocity.Length());
            }
            bolt.Visual.IsVisible = true;
            bolt.Visual.Transform.Position = projectile.Position;
            ((SpriteShader2D)bolt.Visual.Shader).FlipX = projectile.Velocity.X < 0f;
            // Bright core sits at x=.675 in the authored bolt texture.
            bolt.Trail.Follow(projectile.Position + new Vector2(MathF.Sign(projectile.Velocity.X) * 30f * 0.175f, 0f));
        }
    }

    private WorldObject2D CreateVisual(Texture2D texture, Vector2 size)
    {
        var visual = new WorldObject2D(AxisAlignedRectangle2D.FromSize(size), new SpriteShader2D(texture))
            { IsVisible = false, ZIndex = 2 };
        _scene.Add(visual);
        return visual;
    }

    private void StartCharge(Vector2 position)
    {
        StopCharge();
        _charging = true;
        _cancelSeconds = 0f;
        _chargeAnimation.Stop();
        _chargeAnimation.Resume();
        _chargeVoice = _sounds.BeginAt(SoundEffect2D.GunCharge, position);
    }

    private void StopCharge()
    {
        _charging = false;
        _chargeVoice.Stop();
        _chargeVoice = default;
    }

    public void Suspend()
    {
        StopCharge();
        _glow.IsVisible = _flash.IsVisible = false;
        _flashSeconds = _cancelSeconds = 0f;
    }

    public void Reset()
    {
        Suspend();
        foreach (var bolt in _bolts)
        {
            bolt.Id = EntityId2D.None;
            bolt.Visual.IsVisible = false;
            bolt.Trail.Reset();
        }
    }

    public void Dispose()
    {
        Reset();
        _scene.Remove(_glow);
        _scene.Remove(_flash);
        foreach (var bolt in _bolts)
        {
            _scene.Remove(bolt.Visual);
            bolt.Trail.Dispose();
        }
        _bolts.Clear();
    }

    private sealed class BoltVisual(WorldObject2D visual, GunBoltTrail2D trail)
    {
        public EntityId2D Id { get; set; }
        public WorldObject2D Visual { get; } = visual;
        public GunBoltTrail2D Trail { get; } = trail;
    }
}
