using App2d.Contracts.Combat;
using App2d.Contracts.Enemies;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Player;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Animation;
using App2d.Core.Audio;
using App2d.Core.Characters;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Geometry.Transforms;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Rendering.Textures;
using App2d.Core.Validation;
using App2d.Gameplay.Characters;
using App2d.Presentation.Assets;
using App2d.Presentation.Audio;
using App2d.Presentation.Persons;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Presentation.World.Presentation;

/// <summary>Client-owned enemy views indexed by entity ID. Never reads live actors.</summary>
public sealed class EnemyPresentation2D(
    Scene2D scene, TextureCache2D textures, TraversalMetrics2D traversal, ISoundEffectSink2D<SoundEffect2D> sounds) : IDisposable
{
    private readonly Dictionary<EntityId2D, View> _views = [];
    private readonly HashSet<EntityId2D> _presentIds = [];
    private readonly List<EnemyEvent2D> _eventBuffer = [];
    private ImmutableArray<EnemyState2D> _states = [];
    private long _tick;
    private float _secondsSinceState;
    private readonly EnemyContactHold2D _hitstop = new(CombatHitstop2D.Curve);

    public void PresentContact(CombatDamage2D damage) => _hitstop.Present(damage);
    public void ResetContact() => _hitstop.Reset();

    public void ApplyState(ImmutableArray<EnemyState2D> states, IEnumerable<EnemyEvent2D> events, long tick) =>
        Update(states, events, 0f, tick);

    public void Advance(float deltaSeconds)
    {
        Ensure.NonNegative(deltaSeconds);
        _secondsSinceState += deltaSeconds;
        RenderStates(_states, [], deltaSeconds, _tick + (long)(_secondsSinceState * 120f), extrapolate: true);
    }

    public void Update(ImmutableArray<EnemyState2D> states,
        IEnumerable<EnemyEvent2D> events, float dt, long tick)
    {
        Ensure.NonNegative(dt);
        _states = states.IsDefault ? [] : states;
        _tick = tick;
        _secondsSinceState = 0f;
        RenderStates(_states, events, dt, tick);
    }

    private void RenderStates(ImmutableArray<EnemyState2D> states, IEnumerable<EnemyEvent2D> events, float dt, long tick, bool extrapolate = false)
    {
        _hitstop.Prune(states);
        _eventBuffer.Clear();
        _eventBuffer.AddRange(events);
        var occurrences = _eventBuffer.Count == 0 ? null : _eventBuffer.ToLookup(e => e.EntityId);
        foreach (var occurrence in _eventBuffer)
        {
            if (occurrence is EntityCue2D cue)
            {
                sounds.PlayAt(cue.Cue switch
                {
                    "shot" => SoundEffect2D.GunFire,
                    "heavy" => SoundEffect2D.HammerImpact,
                    "club-windup" => SoundEffect2D.HammerWindup,
                    "rock-search" => SoundEffect2D.HammerWindup,
                    "rock-throw" => SoundEffect2D.SwordSwing,
                    "rock-impact" => SoundEffect2D.HammerImpact,
                    "charge-scrape" or "charge-stop" => SoundEffect2D.PlayerLandHard,
                    "charge-rush" => SoundEffect2D.HammerWindup,
                    "pterosaur-screech" => SoundEffect2D.HammerWindup,
                    "pterosaur-rush" or "pterosaur-reset" => SoundEffect2D.SwordSwing,
                    "pterosaur-brake" or "flight-stop" => SoundEffect2D.PlayerLandHard,
                    "shield-block" => SoundEffect2D.HammerImpact,
                    "shield-pullback" => SoundEffect2D.HammerWindup,
                    "shield-bash" => SoundEffect2D.SwordSwing,
                    "hit" or "bite" => SoundEffect2D.SwordHit,
                    _ => SoundEffect2D.SwordSwing
                }, cue.Position);
            }
        }
        _presentIds.Clear();
        if (!states.IsDefault)
        {
            foreach (var observed in states)
            {
                var state = observed;
                if (extrapolate && state.IsEnabled)
                {
                    state = state with
                    {
                        AttackElapsedSeconds = state.AttackElapsedSeconds + _secondsSinceState,
                        ActionSeconds = state.ActionSeconds + (state.Kind == EnemyKind2D.Character ? 0 : _secondsSinceState),
                        Person = state.Person with
                        {
                            LandingSpeedThisFrame = 0f,
                            InvulnerabilitySeconds = Math.Max(0f, state.Person.InvulnerabilitySeconds - _secondsSinceState),
                            Action = state.Person.Action with
                            {
                                ElapsedSeconds = state.Person.Action.ElapsedSeconds + _secondsSinceState
                            }
                        }
                    };
                }

                state = _hitstop.SampleState(state);
                _presentIds.Add(state.Id);
                if (!_views.TryGetValue(state.Id, out var view))
                {
                    if (!state.IsEnabled)
                    {
                        continue;
                    }

                    view = state.Type is { } type ? new CharacterView(scene, type.Character)
                        : state.Kind switch
                        {
                            EnemyKind2D.Shieldback => new ShieldbackView(scene, textures),
                            EnemyKind2D.Rival => new RivalView(scene, textures, traversal, state.IsAlive),
                            _ => throw Ensure.CreateInvalid("An enemy that is a character needs its type.", nameof(states))
                        };
                    _views.Add(state.Id, view);
                }
                view.Update(state, occurrences?[state.Id] ?? [], state.IsEnabled ? dt : 0f, tick);
            }
        }
        foreach (var id in _views.Keys)
        {
            if (_presentIds.Contains(id))
            {
                continue;
            }

            _views[id].Dispose();
            _views.Remove(id);
        }
    }

    public void Dispose()
    {
        _hitstop.Reset();
        foreach (var view in _views.Values)
        {
            view.Dispose();
        }

        _views.Clear();
    }

    private abstract class View : IDisposable
    {
        public abstract void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick);
        public abstract void Dispose();
    }

    /// <summary>Pooled visuals for an enemy's live bolts and thrown rocks.</summary>
    private sealed class BoltViews(Scene2D scene) : IDisposable
    {
        private static readonly Vector2f[] RockVertices =
            [new(-.5f, -.15f), new(-.2f, -.5f), new(.35f, -.4f), new(.5f, .1f), new(.2f, .5f), new(-.35f, .4f)];
        private readonly List<(WorldObject2D Visual, Vector2f Size, bool Rock)> _bolts = [];

        public void Update(EnemyState2D state)
        {
            var count = state.IsEnabled && !state.Bolts.IsDefault ? state.Bolts.Length : 0;
            for (var i = 0; i < count; i++)
            {
                var bolt = state.Bolts[i];
                var rock = bolt.Gravity > 0;
                if (i == _bolts.Count)
                {
                    _bolts.Add(AddBolt(bolt.Size, rock));
                }
                else if (_bolts[i].Size != bolt.Size || _bolts[i].Rock != rock)
                {
                    var previous = _bolts[i].Visual;
                    _bolts[i] = AddBolt(bolt.Size, rock);
                    scene.Remove(previous);
                }

                var visual = _bolts[i].Visual;
                visual.Position = bolt.Position;
                visual.IsVisible = true;
            }

            for (var i = count; i < _bolts.Count; i++)
            {
                _bolts[i].Visual.IsVisible = false;
            }
        }

        private (WorldObject2D Visual, Vector2f Size, bool Rock) AddBolt(Vector2f size, bool rock)
        {
            IShape2D shape;
            if (rock)
            {
                var vertices = new Vector2f[RockVertices.Length];
                VertexTransforms2D.Transform(RockVertices, vertices, Matrix3x2.CreateScale(size.ToVector2()));
                shape = ConvexPolygon2D.Builder.Create(vertices);
            }
            else
            {
                shape = AxisAlignedRectangle2D.Builder.CreateFromSize(size);
            }

            var visual = new WorldObject2D(shape, new SolidColorShader(rock ? new Color4b(133, 139, 134) : Color4b.OrangeRed)) { ZIndex = 2 };
            scene.Add(visual);
            return (visual, size, rock);
        }

        public void Dispose()
        {
            foreach (var bolt in _bolts)
            {
                scene.Remove(bolt.Visual);
            }
        }
    }

    /// <summary>Draws the simulation's own final pose for an authored entity; nothing here samples animation.</summary>
    private sealed class CharacterView : View
    {
        private readonly Scene2D _scene;
        private readonly WorldObject2D _visual;
        private readonly CharacterShader _shader = new();
        private readonly BoltViews _bolts;
        private readonly Combatant _character;
        // What it throws is in its hand only while it winds up, and leaves at the cue that fires it.
        private readonly CharacterAction? _throw;

        public CharacterView(Scene2D scene, Combatant character)
        {
            _character = character;
            _scene = scene;
            _bolts = new(scene);
            _throw = character.Actions.FirstOrDefault(a => a.Projectile?.Gravity > 0);
            _visual = new(AxisAlignedRectangle2D.Builder.CreateFromSize(12, 12, new(0, 2)), _shader)
            {
                Scale = GameWorldUnits2D.WorldUnitsPerAuthoredUnit,
                ZIndex = 1
            };
            scene.Add(_visual);
        }

        public override void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick)
        {
            _visual.IsVisible = state.IsEnabled && state.Pose is not null;
            _bolts.Update(state);
            if (state.Pose is not { } pose)
            {
                return;
            }
            var holding = _throw is not null && state.ActionId == _throw.Name && state.ActionSeconds < _throw.Cue(CharacterAnimator.Fire)!.Seconds;
            _shader.Pose = pose;
            _shader.Drawn = _character.Drawn(pose, worn => worn.Muzzle is null || _throw is null || holding);
            _visual.Position = GameWorldUnits2D.AuthoredToWorld(pose.Position);
        }

        public override void Dispose()
        {
            _scene.Remove(_visual);
            _bolts.Dispose();
        }
    }

    private sealed class ShieldbackView : View
    {
        private readonly Scene2D _scene;
        private readonly WorldObject2D _visual;
        private readonly SpriteShader2D _shader;
        private readonly AnimationPlayer2D<Texture2D> _animation = new();

        public ShieldbackView(Scene2D scene, TextureCache2D textures)
        {
            _scene = scene;
            _animation.Play(CharacterAnimationAssets2D.LoadClip(textures, "shieldback", "walk"));
            _shader = new SpriteShader2D(_animation.CurrentFrame);
            _visual = new WorldObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(144f, 108f), _shader);
            scene.Add(_visual);
        }

        public override void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick)
        {
            _visual.IsVisible = state.IsEnabled && state.IsAlive;
            _visual.Position = state.Position + new Vector2f(0f, 10f);
            _shader.FlipX = state.Facing < 0f;
            if (!_visual.IsVisible)
            {
                return;
            }

            _animation.PlaybackSpeed = Math.Clamp(MathF.Abs(state.Velocity.X) / MathF.Max(1f, state.MoveSpeed), 0.65f, 1.15f);
            _animation.Update(dt);
            _shader.Texture = _animation.CurrentFrame;
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
            if (!isAlive)
            {
                _person.PlayDeath();
            }

            _marker = new WorldObject2D(Circle2D.Builder.Create(8f), new SolidColorShader(new Color4b(255, 46, 166))) { ZIndex = 3 };
            scene.Add(_marker);
        }

        public override void Update(EnemyState2D state, IEnumerable<EnemyEvent2D> events, float dt, long tick)
        {
            foreach (var occurrence in events)
            {
                switch (occurrence)
                {
                    case RivalAttackStarted2D attack:
                        _person.PlayUnarmedAttack(attack.Kind, attack.DurationSeconds);
                        break;
                    case RivalDamaged2D:
                        _person.PlayHit();
                        break;
                    case RivalDied2D:
                        _person.PlayDeath();
                        break;
                }
            }
            _person.SetVisible(state.IsEnabled);
            _person.Update(dt, tick, state.Person, state.MoveX, false, state.IsAttacking);
            _marker.Position = state.Position + new Vector2f(0f, 34f);
            _marker.IsVisible = state.IsEnabled && state.IsAlive;
        }

        public override void Dispose()
        {
            _person.Dispose();
            _scene.Remove(_marker);
        }
    }

}
