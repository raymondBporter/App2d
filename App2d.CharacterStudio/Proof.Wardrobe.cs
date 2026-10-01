using App2d.Core.Characters.Authored;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

internal sealed partial class ProofRenders
{
    private bool RenderWardrobeProof()
    {
        var catalog = ProofCatalog();
        const int width = 1500, height = 920;
        using var target = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var drawing = new PuppetDrawing();
        var subjects = new[] { "hero", "maul-brute", "cinder-gunner" };
        foreach (var poseName in new[] { "idle", "run", "attack", "back" })
        {
            GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 236, 223));
            for (var column = 0; column < subjects.Length; column++)
            {
                var entity = catalog.Entities[subjects[column]];
                var clipId = poseName switch
                {
                    "run" => "person-run",
                    "attack" => column == 0 ? "player-sword-side-cut" : column == 1 ? "person-hammer-slam" : "person-pistol-shot",
                    "back" => column == 0 ? "player-climb" : "person-death",
                    _ => column == 0 ? "player-idle" : "person-idle",
                };
                var clip = catalog.Animations[clipId];
                var at = poseName == "attack" ? (column == 1 ? .78f : column == 2 ? .6f : .12f) : clip.Duration * .3f;
                var pose = PoseEvaluator.Sample(entity.Model, clip, at, false, new() { InPlace = true });
                if (column == 0)
                {
                    drawing.Build(entity.Model, pose);
                    var placed = new ActorPose(pose, Vector2.Zero, 1);
                    foreach (var (prop, socket) in PersonLoadout.Dressed(clip, at, PersonGear.Sword, entity))
                        drawing.AddProp(catalog.Props[prop], placed.Socket(entity.Sockets[socket]));
                }
                else drawing.Build(entity, pose);
                // Large right/left views plus an actual game-size pair along the bottom.
                foreach (var (x, y, ppu, facing) in new[] { (150, 650, 215, 1), (365, 650, 130, -1), (195, 865, 48, 1), (305, 865, 48, -1) })
                {
                    GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                    _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(width, height, new(column * 500 + x, y), ppu), Matrix.CreateScale(facing, 1, 1));
                }
            }
            GraphicsDevice.SetRenderTarget(null);
            using var file = File.Create(Path.Combine(_smokePath, "wardrobe-" + poseName + ".png"));
            target.SaveAsPng(file, width, height);
        }
        File.WriteAllText(Path.Combine(_smokePath, "wardrobe.txt"), "Columns: hero, maul brute, cinder gunner. Each shows both facings, then both at 48 pixels/unit. Sheets: idle, run, attack, and climbing/back view (hero) or death recoil (enemies). Uses packaged assets and the shared depth-tested renderer.\n");
        var caveman = catalog.Entities["club-caveman"];
        GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 236, 223));
        var samples = new (string Clip, float At)[]
        {
            ("person-idle", .2f), ("person-run", .15f), ("club-caveman-slam", .55f),
            ("club-caveman-slam", .7f), ("club-caveman-slam", .78f), ("club-caveman-slam", 1.2f),
            ("person-hit", .12f), ("person-death", .5f)
        };
        for (var i = 0; i < samples.Length; i++)
        {
            var sample = samples[i];
            var pose = PoseEvaluator.Sample(caveman.Model, catalog.Animations[sample.Clip], sample.At, false, new() { InPlace = true });
            drawing.Build(caveman, pose);
            foreach (var (ppu, y, facing) in new[] { (125, 380, 1), (48, 440, -1) })
            {
                GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(width, height,
                    new(160 + i % 4 * 360, y + i / 4 * 460), ppu), Matrix.CreateScale(facing, 1, 1));
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        using (var file = File.Create(Path.Combine(_smokePath, "club-caveman.png"))) target.SaveAsPng(file, width, height);
        var thrower = catalog.Entities["rock-thrower"];
        GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 236, 223));
        var throws = new (string Clip, float At)[]
        {
            ("person-idle", .2f), ("person-run", .15f), ("rock-thrower-throw", .35f),
            ("rock-thrower-throw", .8f), ("rock-thrower-throw", 1.05f), ("rock-thrower-throw", 1.14f),
            ("person-hit", .12f), ("person-death", .5f)
        };
        for (var i = 0; i < throws.Length; i++)
        {
            var sample = throws[i];
            var pose = PoseEvaluator.Sample(thrower.Model, catalog.Animations[sample.Clip], sample.At, false, new() { InPlace = true });
            drawing.Build(thrower.Model, pose);
            var placed = new ActorPose(pose, Vector2.Zero, 1);
            foreach (var equipment in thrower.Equipment.Where(e => e.Prop.Muzzle is null || i is 2 or 3 or 4))
                drawing.AddProp(equipment.Prop, placed.Socket(equipment.Socket));
            foreach (var (ppu, y, facing) in new[] { (125, 380, 1), (48, 440, -1) })
            {
                GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(width, height,
                    new(160 + i % 4 * 360, y + i / 4 * 460), ppu), Matrix.CreateScale(facing, 1, 1));
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        using (var file = File.Create(Path.Combine(_smokePath, "rock-thrower.png"))) target.SaveAsPng(file, width, height);
        var baby = catalog.Entities["baby-triceratops"];
        GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 236, 223));
        var babySamples = new (string Clip, float At)[]
        {
            ("baby-triceratops-idle", .2f), ("baby-triceratops-charge", .4f), ("baby-triceratops-charge", .9f),
            ("baby-triceratops-charge", 1.2f), ("baby-triceratops-charge", 1.95f), ("baby-triceratops-charge", 2.4f),
            ("baby-triceratops-hit", .12f), ("baby-triceratops-death", .8f)
        };
        for (var i = 0; i < babySamples.Length; i++)
        {
            var sample = babySamples[i];
            drawing.Build(baby, PoseEvaluator.Sample(baby.Model, catalog.Animations[sample.Clip], sample.At, false, new() { InPlace = true }));
            foreach (var (ppu, y, facing) in new[] { (125, 340, 1), (40, 430, -1) })
            {
                GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(width, height,
                    new(180 + i % 4 * 360, y + i / 4 * 460), ppu), Matrix.CreateScale(facing, 1, 1));
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        using (var file = File.Create(Path.Combine(_smokePath, "baby-triceratops.png"))) target.SaveAsPng(file, width, height);
        using var animationTarget = new RenderTarget2D(GraphicsDevice, 640, 480, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var defender = catalog.Entities["shield-defender"];
        foreach (var subject in new[] { caveman, thrower, baby, defender })
        {
            var action = subject.Actions["attack"];
            var folder = Path.Combine(_smokePath, subject.Id + "-frames"); Directory.CreateDirectory(folder);
            var release = action.Events.FirstOrDefault(e => e.Event.Id == EntityControllers.Fire)?.Seconds ?? float.PositiveInfinity;
            var releasePose = new ActorPose(PoseEvaluator.Sample(subject.Model, action.Clip, Math.Min(release, action.Clip.Duration), false, new() { InPlace = true }), Vector2.Zero, 1);
            for (var frame = 0; frame < (int)Math.Ceiling(action.Clip.Duration * 24); frame++)
            {
                var at = frame / 24f;
                GraphicsDevice.SetRenderTarget(animationTarget); GraphicsDevice.Clear(new Color(241, 236, 223));
                var pose = PoseEvaluator.Sample(subject.Model, action.Clip, at, false, new() { InPlace = true });
                drawing.Build(subject.Model, pose);
                var placed = new ActorPose(pose, Vector2.Zero, 1);
                foreach (var equipment in subject.Equipment.Where(e => e.Prop.Muzzle is null || at < release))
                    drawing.AddProp(equipment.Prop, placed.Socket(equipment.Socket));
                if (at >= release && action.Projectile is { } shot)
                {
                    var rock = subject.Equipment.First(e => e.Prop.Muzzle is not null);
                    var origin = EntityCollision.Muzzle(subject, releasePose).Point;
                    var flight = shot.FlightSeconds;
                    var velocity = (new Vector3(3.8f, .65f, origin.Z) - origin) / flight + new Vector3(0, shot.Gravity * flight / 2, 0);
                    var elapsed = at - release;
                    var centre = origin + velocity * elapsed - new Vector3(0, shot.Gravity * elapsed * elapsed / 2, 0);
                    var socket = placed.Socket(rock.Socket) with { Origin = centre + new Vector3(rock.Prop.Grip.X, rock.Prop.Grip.Y, rock.Prop.Grip.Z) };
                    drawing.AddProp(rock.Prop, socket);
                }
                var charging = subject.Asset.Controller.ChargeSpeed > 0;
                var config = subject.Asset.Controller;
                var start = action.Events.FirstOrDefault(e => e.Event.Id == "rush")?.Seconds ?? config.ChargeStartSeconds;
                var finish = action.Events.FirstOrDefault(e => e.Event.Id == "brake")?.Seconds ?? config.ChargeEndSeconds;
                var rushSeconds = Math.Clamp(at - start, 0, finish - start);
                var brakeSeconds = Math.Clamp(at - finish, 0, config.BrakeSeconds);
                var travelled = charging ? config.ChargeSpeed * (rushSeconds + brakeSeconds - brakeSeconds * brakeSeconds / (2 * config.BrakeSeconds)) : 0;
                _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(640, 480,
                    new(charging ? 100 + travelled * 65 : 160, 430), charging ? 65 : 125), Matrix.Identity);
                GraphicsDevice.SetRenderTarget(null);
                using var file = File.Create(Path.Combine(folder, $"frame-{frame:D4}.png")); animationTarget.SaveAsPng(file, 640, 480);
            }
        }
        return false;
    }
}
