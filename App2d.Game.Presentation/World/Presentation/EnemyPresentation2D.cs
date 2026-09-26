using App2d.Core;
using App2d.Core.Animation;
using App2d.Core.Geometry;
using App2d.Gameplay.Assets;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Collections.Immutable;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Gameplay.World.Presentation;

/// <summary>Client-owned enemy views indexed by entity ID. Never reads live actors.</summary>
public sealed class EnemyPresentation2D(
    Scene2D scene, TextureCache2D textures, TraversalMetrics2D traversal, ISoundEffectSink2D sounds,
    App2d.Core.Characters.EntityCatalog? characters = null) : IDisposable
{
    private readonly Dictionary<EntityId2D, View> _views = [];
    private readonly HashSet<EntityId2D> _presentIds = [];
    private ImmutableArray<EnemyState2D> _states = [];
    private long _tick;
    private float _secondsSinceState;

    public void ApplyState(ImmutableArray<EnemyState2D> states, IEnumerable<EnemyEvent2D> events, long tick) =>
        Update(states, events, 0f, tick);

    public void Advance(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        _secondsSinceState += deltaSeconds;
        var states = _states.Select(s => !s.IsEnabled ? s : s with
        {
            AttackElapsedSeconds = s.AttackElapsedSeconds + _secondsSinceState,
            ActionSeconds = s.ActionSeconds + _secondsSinceState,
            Person = s.Person with
            {
                LandingSpeedThisFrame = 0f,
                InvulnerabilitySeconds = Math.Max(0f, s.Person.InvulnerabilitySeconds - _secondsSinceState),
                Action = s.Person.Action with { ElapsedSeconds = s.Person.Action.ElapsedSeconds + _secondsSinceState }
            }
        }).ToImmutableArray();
        RenderStates(states, [], deltaSeconds, _tick + (long)(_secondsSinceState * 120f));
    }

    public void Update(ImmutableArray<EnemyState2D> states,
        IEnumerable<EnemyEvent2D> events, float dt, long tick)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(dt);
        _states = states.IsDefault ? [] : states;
        _tick = tick;
        _secondsSinceState = 0f;
        RenderStates(_states, events, dt, tick);
    }

    private void RenderStates(ImmutableArray<EnemyState2D> states, IEnumerable<EnemyEvent2D> events, float dt, long tick)
    {
        var occurrences = events.ToLookup(e => e.EntityId);
        foreach (var occurrence in occurrences.SelectMany(group => group))
        {
            if (occurrence is HammerStarted2D) sounds.PlayAt(SoundEffect2D.HammerWindup, occurrence.Position);
            else if (occurrence is HammerStruck2D) sounds.PlayAt(SoundEffect2D.HammerImpact, occurrence.Position);
            else if (occurrence is EntityCue2D cue) sounds.PlayAt(cue.Cue switch
            { "shot" => SoundEffect2D.GunFire, "heavy" => SoundEffect2D.HammerImpact,
              "hit" or "bite" => SoundEffect2D.SwordHit, _ => SoundEffect2D.SwordSwing }, cue.Position);
        }
        _presentIds.Clear();
        if (!states.IsDefault)
        {
            foreach (var state in states)
            {
                _presentIds.Add(state.Id);
                if (!_views.TryGetValue(state.Id, out var view))
                {
                    if (!state.IsEnabled) continue;
                    view = state.AuthoredEntity is { } entity ? new AuthoredPoseView(scene, entity)
                        : state.TypeId is { } typeId && characters is not null ? new AuthoredView(scene, characters, typeId) : state.Kind switch
                    {
                        EnemyKind2D.Rival => new RivalView(scene, textures, traversal, state.IsAlive),
                        EnemyKind2D.TumbleProp => new PropView(scene),
                        _ => new AnimatedView(scene, textures, state.Kind)
                    };
                    _views.Add(state.Id, view);
                }
                view.Update(state, occurrences[state.Id], state.IsEnabled ? dt : 0f, tick);
            }
        }
        foreach (var id in _views.Keys.Where(id => !_presentIds.Contains(id)).ToArray())
        {
            _views[id].Dispose();
            _views.Remove(id);
        }
    }

    public void Dispose()
    {
        foreach (var view in _views.Values) view.Dispose();
        _views.Clear();
    }

    private abstract class View : IDisposable
    {
        public abstract void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick);
        public abstract void Dispose();
    }

    private sealed class AuthoredView : View
    {
        private readonly Scene2D _scene;
        private readonly WorldObject2D _visual;
        private readonly App2d.Rendering.Characters.PointCharacterShader _shader;
        private readonly List<WorldObject2D> _bolts = [];
        public AuthoredView(Scene2D scene, App2d.Core.Characters.EntityCatalog catalog, string typeId)
        {
            _scene = scene; _shader = new(catalog, catalog.Types[typeId]);
            _visual = new(AxisAlignedRectangle2D.FromSize(new(12, 12), new(0, 2)), _shader) { ZIndex = 1 };
            _visual.Transform.Scale = new(App2d.Core.Characters.EntityCatalog.WorldUnits);
            scene.Add(_visual);
        }
        public override void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick)
        {
            const float scale = App2d.Core.Characters.EntityCatalog.WorldUnits;
            _shader.Action = state.ActionId ?? "idle"; _shader.Seconds = state.ActionSeconds; _shader.FacingLeft = state.Facing < 0;
            _visual.Transform.Position = state.Position - new Vector2(_shader.Type.Movement.OffsetX * state.Facing, _shader.Type.Movement.Height / 2) * scale;
            _visual.IsVisible = state.IsEnabled;
            var count = state.IsEnabled && !state.Bolts.IsDefault ? state.Bolts.Length : 0;
            while (_bolts.Count < count)
            {
                var bolt = new WorldObject2D(AxisAlignedRectangle2D.FromSize(Vector2.One), new SolidColorShader(XnaColor.OrangeRed)) { ZIndex = 2 };
                _bolts.Add(bolt); _scene.Add(bolt);
            }
            for (var i = 0; i < _bolts.Count; i++)
            {
                _bolts[i].IsVisible = i < count;
                if (i >= count) continue;
                _bolts[i].Transform.Position = state.Bolts[i].Position;
                _bolts[i].Transform.Scale = state.Bolts[i].Size;
            }
        }
        public override void Dispose() { _scene.Remove(_visual); foreach (var bolt in _bolts) _scene.Remove(bolt); }
    }

    /// <summary>Draws the simulation's own final pose for an authored entity; nothing here samples animation.</summary>
    private sealed class AuthoredPoseView : View
    {
        private readonly Scene2D _scene;
        private readonly WorldObject2D _visual;
        private readonly App2d.Rendering.Characters.AuthoredCharacterShader _shader;
        public AuthoredPoseView(Scene2D scene, App2d.Core.Characters.ResolvedEntity entity)
        {
            _scene = scene; _shader = new(entity);
            _visual = new(AxisAlignedRectangle2D.FromSize(new(12, 12), new(0, 2)), _shader) { ZIndex = 1 };
            _visual.Transform.Scale = new(App2d.Core.Characters.EntityCatalog.WorldUnits);
            scene.Add(_visual);
        }
        public override void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick)
        {
            _visual.IsVisible = state.IsEnabled && state.AuthoredPose is not null;
            if (state.AuthoredPose is not { } pose) return;
            _shader.Pose = pose.Local; _shader.Facing = pose.Facing;
            _visual.Transform.Position = pose.Position * App2d.Core.Characters.EntityCatalog.WorldUnits;
        }
        public override void Dispose() => _scene.Remove(_visual);
    }

    private sealed class AnimatedView : View
    {
        private readonly Scene2D _scene;
        private readonly WorldObject2D _visual;
        private readonly SpriteShader2D _shader;
        private readonly AnimationClip2D<Texture2D> _walk;
        private readonly AnimationClip2D<Texture2D>? _attack;
        private readonly AnimationPlayer2D<Texture2D> _animation = new();
        private readonly Vector2 _offset;
        private bool _wasAttacking;

        public AnimatedView(Scene2D scene, TextureCache2D textures, EnemyKind2D kind)
        {
            _scene = scene;
            var (asset, size, offset) = kind switch
            {
                EnemyKind2D.Shieldback => ("shieldback", new Vector2(144f, 108f), new Vector2(0f, 10f)),
                EnemyKind2D.GreenDinosaur => ("green-dinosaur", new Vector2(112f), new Vector2(0f, 24f)),
                EnemyKind2D.BoilerBrute => ("boiler-brute", new Vector2(196f), new Vector2(0f, 32f)),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            _offset = offset;
            _walk = CharacterAnimationAssets2D.LoadClip(textures, asset, "walk");
            if (kind == EnemyKind2D.BoilerBrute)
                _attack = CharacterAnimationAssets2D.LoadClip(textures, asset, "hammer-attack");
            _animation.Play(_walk);
            _shader = new SpriteShader2D(_animation.CurrentFrame);
            _visual = new WorldObject2D(AxisAlignedRectangle2D.FromSize(size), _shader);
            scene.Add(_visual);
        }

        public override void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick)
        {
            _visual.IsVisible = state.IsEnabled && state.IsAlive;
            _visual.Transform.Position = state.Position + _offset;
            _shader.FlipX = state.Facing < 0f;
            if (!_visual.IsVisible) return;
            if (state.IsAttacking && _attack is not null)
            {
                // Fit authored poses to authoritative attack time. Changing art cannot change damage timing.
                var progress = Math.Clamp(state.AttackElapsedSeconds / BoilerBruteTiming2D.AttackDurationSeconds, 0f, 1f);
                _shader.Texture = _attack[_attack.GetFrameIndexAtTime(progress * _attack.Duration)];
            }
            else
            {
                if (_wasAttacking) _animation.Play(_walk, restart: true);
                _animation.PlaybackSpeed = Math.Clamp(MathF.Abs(state.Velocity.X) / MathF.Max(1f, state.MoveSpeed), 0.65f, 1.15f);
                _animation.Update(dt);
                _shader.Texture = _animation.CurrentFrame;
            }
            _wasAttacking = state.IsAttacking;
        }

        public override void Dispose() => _scene.Remove(_visual);
    }

    private sealed class RivalView : View
    {
        private readonly Scene2D _scene;
        private readonly PersonPresentation2D _person;
        private readonly WorldObject2D _marker;

        public RivalView(Scene2D scene, TextureCache2D textures, TraversalMetrics2D traversal, bool isAlive)
        {
            _scene = scene;
            _person = new PersonPresentation2D(scene, textures, traversal);
            _person.Equip(EquipmentKind2D.Unarmed);
            if (!isAlive) _person.PlayDeath();
            _marker = new WorldObject2D(new Circle2D(8f), new SolidColorShader(new XnaColor(255, 46, 166))) { ZIndex = 3 };
            scene.Add(_marker);
        }

        public override void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick)
        {
            foreach (var occurrence in events)
            {
                switch (occurrence)
                {
                    case RivalAttackStarted2D attack: _person.PlayUnarmedAttack(attack.Kind, attack.DurationSeconds); break;
                    case RivalDamaged2D: _person.PlayHit(); break;
                    case RivalDied2D: _person.PlayDeath(); break;
                }
            }
            _person.SetVisible(state.IsEnabled);
            _person.Update(dt, tick, state.Person, state.MoveX, false, state.IsAttacking);
            _marker.Transform.Position = state.Position + new Vector2(0f, 34f);
            _marker.IsVisible = state.IsEnabled && state.IsAlive;
        }

        public override void Dispose()
        {
            _person.Dispose();
            _scene.Remove(_marker);
        }
    }

    private sealed class PropView : View
    {
        private readonly Scene2D _scene;
        private readonly WorldObject2D _visual;
        public PropView(Scene2D scene)
        {
            _scene = scene;
            var shape = new CompositeShape2D([
                Rectangle2D.FromSize(new Vector2(72f, 18f)),
                new Circle2D(16f, new Vector2(-42f, 0f)),
                new Circle2D(16f, new Vector2(42f, 0f))]);
            _visual = new WorldObject2D(shape, new SolidColorShader(new XnaColor(0xFF, 0x9A, 0x3B))) { ZIndex = 1 };
            scene.Add(_visual);
        }
        public override void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick)
        {
            _visual.Transform.Position = state.Position;
            _visual.Transform.Rotation = state.Rotation;
            _visual.IsVisible = state.IsEnabled;
        }
        public override void Dispose() => _scene.Remove(_visual);
    }
}
