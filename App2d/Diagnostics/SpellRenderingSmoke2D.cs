using App2d.Core.Geometry;
using App2d.Contracts.Combat;
using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Levels;
using App2d.Presentation.Audio;
using App2d.Presentation.Persons;
using App2d.Presentation.Persons.Presentation;
using App2d.Presentation.Player;
using App2d.Rendering;
using App2d.Rendering.Textures;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Diagnostics;

/// <summary>Reproducible spell samples driven by real simulation, authored poses, HUD and effect rendering.</summary>
internal static class SpellRenderingSmoke2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string output)
    {
        var catalog = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        var moves = PersonMoves.From(catalog);
        var metrics = TraversalMetricsLoader2D.Load(textures.ContentRoot);
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision) { Gravity = Vector2.Zero };
        var ids = new EntityIdAllocator2D();
        var person = new Person2D(ids.Allocate(), collision, physics, metrics, Vector2.Zero, 2, 1, CombatFaction2D.Player, 30);
        var registry = new CombatantRegistry2D(); registry.Register(person);
        var hero = new AuthoredHero2D(catalog.Entities[AuthoredHero2D.EntityId], metrics.PlayerColliderSize);
        var arsenal = new PersonArsenal2D(ids, person.Body, metrics.GunMuzzleOffset, collision, 1, 4,
            CombatFaction2D.Player, new CombatSystem2D(collision, registry), hero: hero,
            muzzle: facing => hero.Muzzle(facing, person.IsWallGripping), health: person.Health);
        person.AttachActions(arsenal);
        var floor = physics.AddBody(new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(1000, 20))), BodyMotionType2D.Static);
        floor.WorldObject.Transform.Position = new(0, person.WorldObject.WorldBounds.Bottom - 10);
        floor.CollisionLayer = 1; floor.CollisionMask = 2;
        var scene = new Scene2D();
        var ground = new WorldObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(1000, 20)), new SolidColorShader(new Color(44, 64, 79)));
        ground.Transform.Position = floor.WorldObject.Transform.Position; scene.Add(ground);
        var audio = new SilentSounds();
        using var player = new AuthoredPersonPresentation2D(scene, moves, metrics);
        using var weapons = new WeaponPresentation2D(scene, textures, audio);
        using var spells = new SpellPresentation2D(audio);
        using var renderer = new Renderer2D(new Camera2D { Zoom = 4, Position = new(0, 15) }, device);
        using var target = new RenderTarget2D(device, 1100, 650);
        var events = new List<WeaponEvent2D>(); arsenal.WeaponOccurred += events.Add;
        long tick = 0;
        const float dt = 1f / 120;
        foreach (var facing in new[] { 1f, -1f })
        {
            person.Reset(Vector2.Zero); person.Face(facing); person.Health.Damage(12);
            player.Reset(); weapons.Reset(); spells.Reset(); events.Clear();
            for (var i = 0; i < 45; i++) Step(default); // Let the respawn blink expire before sampling art.
            for (var i = 0; i < 20; i++) Step(new() { CastHeld = true });
            Save($"shot-charge-{facing}.png", "SHOT / SHORT HOLD");
            for (var i = 0; i < 10; i++) Step(new() { CastHeld = true });
            if (person.CaptureState().Spells.Energy != 60) throw new InvalidOperationException("Shot failed to spend energy.");
            Save($"shot-release-{facing}.png", "SHOT / RELEASE");
            for (var i = 0; i < 30; i++) Step(default);
            for (var i = 0; i < 85; i++) Step(new() { HealHeld = true });
            Save($"heal-gather-{facing}.png", "HEAL / HOLD STILL");
            for (var i = 0; i < 35; i++) Step(new() { HealHeld = true });
            if (person.Health.Current != 24) throw new InvalidOperationException("Healing did not complete.");
            for (var i = 0; i < 8; i++) Step(default);
            Save($"heal-complete-{facing}.png", "HEAL / +6 HEALTH");
        }
        foreach (var cue in new[] { SoundEffect2D.GunCharge, SoundEffect2D.GunFire, SoundEffect2D.HealCharge, SoundEffect2D.HealComplete })
            if (!audio.Played.Contains(cue)) throw new InvalidOperationException($"Missing spell sound: {cue}");
        File.WriteAllLines(Path.Combine(output, "sounds.txt"), audio.Played.Select(c => c.ToString()));

        void Step(PersonCommand2D command)
        {
            person.BeginFrame(dt); person.ApplyCommand(command, dt); physics.Step(dt); person.UpdateAfterPhysics(dt);
            var state = person.CaptureState();
            weapons.Update(arsenal.CaptureWeaponState(), arsenal.Equipment, events, dt);
            spells.Apply(state, events); spells.Advance(dt); events.Clear();
            player.ApplyState(state, ++tick, command.MoveX, false, arsenal.IsMeleeAttackActive);
        }
        void Save(string name, string caption)
        {
            device.SetRenderTarget(target); renderer.BeginFrame(1100, 650, default); renderer.Clear(new Color(108, 133, 149));
            renderer.Draw(scene); spells.Draw(renderer);
            PlayerHud2D.Draw(renderer, person.Health.Current, person.Health.Maximum, weapons.HudTexture, person.CaptureState().Spells, arsenal.IsChargingPrimary);
            renderer.DrawScreenLabel(caption, new(24, 590)); renderer.EndFrame(); device.SetRenderTarget(null);
            using var stream = File.Create(Path.Combine(output, name)); target.SaveAsPng(stream, 1100, 650);
        }
    }

    private sealed class SilentSounds : ISoundEffectSink2D
    {
        public List<SoundEffect2D> Played { get; } = [];
        public void Play(SoundEffect2D effect) => Played.Add(effect);
    }
}
