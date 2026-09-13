using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Diagnostics;

/// <summary>Isolated, deterministic visual checks for the real gun state and muzzle pose.</summary>
internal static class GunRenderingSmoke2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string outputDirectory)
    {
        const int width = 960, height = 640;
        const float dt = 1f / 120f;
        var scene = new Scene2D();
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision) { Gravity = Vector2.Zero };
        var metrics = TraversalMetrics2D.FromPlayerAsset(textures.ContentRoot);
        var person = new Person2D(collision, physics, metrics, Vector2.Zero, 2, 1, CombatFaction2D.Player);
        var floor = new WorldObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(800f, 20f)),
            new SolidColorShader(new XnaColor(54, 74, 85)));
        floor.Transform.Position = new Vector2(0, person.WorldObject.WorldBounds.Bottom - 10f);
        scene.Add(floor);
        var floorBody = physics.AddBody(floor, BodyMotionType2D.Static);
        floorBody.CollisionLayer = 1;
        floorBody.CollisionMask = 2;
        var sounds = new SilentSounds();
        var arsenal = new PersonArsenal2D(scene, person.Body, textures, collision, 1, 4,
            CombatFaction2D.Player, new CombatSystem2D(collision, sounds, new CombatantRegistry2D()), sounds);
        person.AttachActions(arsenal);
        using var presentation = new PersonPresentation2D(scene, textures, metrics);
        arsenal.EquipmentChanged += presentation.Equip;
        arsenal.ShotStarted += () => presentation.PlayShot(false);
        arsenal.SelectNext();
        var camera = new Camera2D { Zoom = 4f };
        using var renderer = new Renderer2D(camera, device);
        using var target = new RenderTarget2D(device, width, height);
        var hold = new PersonCommand2D(default, false, null, false, PrimaryActionHeld: true);

        foreach (var facing in new[] { 1f, -1f })
        {
            camera.Position = Vector2.Zero;
            person.Reset(Vector2.Zero);
            person.Face(facing);
            presentation.Reset();
            Step(default);
            Step(hold with { UsePrimaryAction = true });
            for (var frame = 1; frame < 36; frame++) Step(hold);
            Save($"gun-charge-{facing}.png", "CHARGING / 0.30s");
            for (var frame = 36; frame < 72; frame++) Step(hold);
            if (!arsenal.GetActiveAttackHitboxes().Any())
                throw new InvalidOperationException("The gun did not fire automatically at full charge.");
            Save($"gun-fire-{facing}.png", "FIRED / CHARGE COMPLETE");
            for (var frame = 0; frame < 4; frame++) Step(hold);
            Save($"gun-trail-{facing}.png", "FLIGHT / THIN GHOST TRAIL");
            camera.Zoom = 1f;
            Save($"gun-trail-scale1-{facing}.png", "FLIGHT / 1x SCALE");
            camera.Zoom = 4f;
            for (var frame = 0; frame < 4; frame++) Step(hold);
            camera.Position = new Vector2(facing * 60f, 0f);
            Save($"gun-recoil-{facing}.png", "RECOIL");

            var wall = new WorldObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(8f, 70f)),
                new SolidColorShader(new XnaColor(54, 74, 85)));
            wall.Transform.Position = new Vector2(facing * 170f, 7f);
            scene.Add(wall);
            var wallBody = physics.AddBody(wall, BodyMotionType2D.Static);
            wallBody.CollisionLayer = 1;
            for (var frame = 0; frame < 2; frame++) Step(hold);
            if (arsenal.GetActiveAttackHitboxes().Any())
                throw new InvalidOperationException("The gun smoke projectile failed to hit the wall.");
            Save($"gun-trail-impact-{facing}.png", "IMPACT / TRAIL STOPS IN PLACE");
            for (var frame = 0; frame < 8; frame++) Step(hold);
            if (scene.Any(item => item.ZIndex == 2 && item.IsVisible))
                throw new InvalidOperationException("A gun effect remained visible after the impact fade.");
            Save($"gun-trail-cleared-{facing}.png", "IMPACT / TRAIL CLEARED");
            wallBody.IsCollider = false;
            scene.Remove(wall);
        }

        void Step(PersonCommand2D command)
        {
            person.BeginFrame(dt);
            person.ApplyCommand(command, dt);
            physics.Step(dt);
            person.UpdateAfterPhysics(dt);
            presentation.Update(dt, 0, person, command.Movement.MoveX, false, false);
        }

        void Save(string name, string caption)
        {
            device.SetRenderTarget(target);
            renderer.BeginFrame(width, height, default);
            renderer.Clear(new XnaColor(145, 176, 190));
            renderer.Draw(scene);
            PlayerHud2D.Draw(renderer, 5, 5, arsenal.WeaponHudTexture, arsenal.WeaponStatus);
            renderer.DrawScreenLabel(caption, new Vector2(24f, 540f));
            renderer.EndFrame();
            device.SetRenderTarget(null);
            using var stream = File.Create(Path.Combine(outputDirectory, name));
            target.SaveAsPng(stream, width, height);
        }
    }

    private sealed class SilentSounds : ISoundEffectSink2D
    {
        public void Play(SoundEffect2D effect) { }
    }
}
