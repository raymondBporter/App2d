using App2d.Contracts.World;
using App2d.Core.Assets;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Gameplay.World;
using App2d.Presentation.World.Presentation;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.CharacterSeeds.Diagnostics;

/// <summary>Exercises JSON loading, generic shapes, actual physics, observations and material rendering.</summary>
internal static class ObjectRenderingSmoke2D
{
    public static void Run(GraphicsDevice device, string directory)
    {
        var asset = Part.Load(Path.Combine(AssetPaths.Current.Levels, "cavern", "objects", "lift.json"));
        var circle = asset with
        {
            Geometry = [new() { Id = "circle", A = "origin", Collision = new(), Geometry = Circle2D.Builder.Create(18), Material = new() { Fill = "#25d2be" } }],
            Surface = null,
            Motion = asset.Motion! with
            {
                Curve = Arc2D.Builder.Create(Vector2.Zero, 48, 0, MathF.Tau),
                Progress = [ProgressMapKind.Linear]
            }
        };
        var physics = new PhysicsWorld2D { Gravity = new(0, -240) };
        using var lift = new AuthoredObject2D(new(1), new("lift", asset, new(-180, -40)), physics, 1, uint.MaxValue);
        using var orbit = new AuthoredObject2D(new(2), new("orbit", circle, new(100, 20)), physics, 1, uint.MaxValue);
        var catalog = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        using var dumbbell = new AuthoredObject2D(new(3), new("dumbbell", catalog.Parts["dumbbell"], new(-20, 95)), physics, 1, uint.MaxValue);
        using var crate = new AuthoredObject2D(new(4), new("crate", catalog.Parts["tumbling-crate"], new(130, 100)), physics, 1, uint.MaxValue);
        dumbbell.WorldObject.Transform.Rotation = .3f;
        crate.WorldObject.Transform.Rotation = -.2f;
        var floorPart = new Part { Id = "floor", Name = "Floor", Geometry = [new() { Id = "floor", A = "origin", Collision = new(), Geometry = Rectangle2D.Builder.Create(new(-240, -10), new(240, 0)), Material = new() { Fill = "#677d8b" } }], Physics = new() { MotionType = BodyMotionType2D.Static, Friction = .6f, Restitution = 0 } };
        using var floor = new AuthoredObject2D(new(5), new("floor", floorPart, new(-30, -65)), physics, 1, uint.MaxValue);
        AuthoredObject2D[] items = [lift, orbit, dumbbell, crate, floor];
        var content = LevelContent2D.Empty with { Objects = [.. items.Select(o => o.CaptureDefinition())] };
        var scene = new Scene2D();
        using var textures = new TextureCache2D(AssetPaths.Current.Runtime);
        using var view = new WorldPresentation2D(scene, textures);
        var camera = new Camera2D { Zoom = 2f, Position = new(-30, 20) };
        using var renderer = new Renderer2D(camera, device);
        using var target = RenderCapture2D.CreateTarget(device, 900, 450);
        for (var step = 0; step <= 360; step++)
        {
            if (step > 0)
            {
                foreach (var item in items) item.Update(1f / 60);
                physics.Step(1f / 60);
            }
            if (step % 90 != 0) continue;
            view.ApplyState(content, WorldState2D.Empty with { Objects = [.. items.Select(o => o.CaptureState())] });
            target.SavePng(Path.Combine(directory, $"objects-{step:000}.png"), new Color4b(235, 242, 245), () =>
            {
                renderer.BeginFrame(900, 450, default);
                foreach (var item in items)
                {
                    if (item.Placement.Asset.Motion is not { } motion) continue;
                    var curve = motion.Curve;
                    var origin = item.Placement.Position;
                    for (var segment = 1; segment <= 64; segment++)
                        renderer.DrawWorldSegment(origin + curve.Evaluate((segment - 1) / 64f), origin + curve.Evaluate(segment / 64f), new Color4b(145, 165, 180), 1);
                }
                renderer.Draw(scene);
                renderer.EndFrame();
            });
        }
        Console.WriteLine($"JSON object rendering passed: {Path.GetFullPath(directory)}");
    }
}
