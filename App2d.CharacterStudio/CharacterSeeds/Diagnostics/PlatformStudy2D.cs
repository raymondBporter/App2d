using App2d.Contracts.Persons;
using App2d.Contracts.Simulation;
using App2d.Core.Assets;
using App2d.Core.Audio;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Objects;
using App2d.Core.Physics;
using App2d.Core.Physics.Motion;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Core.Tiles;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Presentation.Audio;
using App2d.Presentation.Persons;
using App2d.Presentation.World.Presentation;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.CharacterStudio.CharacterSeeds.Diagnostics;

/// <summary>
/// Real simulation and renderer: the player and an enemy standing on authored lifts, exported as frames. Planted feet
/// should ride the lift; a still lift is the reference for how an enemy stands.
/// </summary>
internal static class PlatformStudy2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string directory)
    {
        var authored = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        var traversal = TraversalMetricsLoader2D.Load(textures.ContentRoot);
        var camera = new Camera2D { Zoom = 2.2f };
        using var renderer = new Renderer2D(camera, device);
        using var target = RenderCapture2D.CreateTarget(device, 400, 300);
        var scenarios = new (string Name, Vector2 Focus, ObjectPlacement2D Lift, bool Enemy)[]
        {
            ("player-sinking", new(-300, 70), Lift("player-sinking", new(-300, 90), new(0, -80), 120, 70), false),
            ("player-sideways", new(-240, 70), Lift("player-sideways", new(-300, 60), new(140, 0), 120, 90), false),
            ("enemy-still", new(300, 70), Lift("enemy-still", new(300, 40), new(0, -1), 160, .001f), true),
            ("enemy-sinking", new(300, 70), Lift("enemy-sinking", new(300, 90), new(0, -80), 160, 70), true),
        };
        foreach (var (name, focus, lift, withEnemy) in scenarios)
        {
            var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["ink-medieval-ground"]);
            for (var x = 0; x < 640; x++)
            {
                map.SetTileKind(x, 19, TileKind2D.Solid);
            }

            var things = new List<WorldThingSpec2D> { new(1, WorldThingKind2D.PlayerSpawn, null, true, withEnemy ? new(-600, 40) : lift.Position + new Vector2(0, 90)) };
            if (withEnemy)
            {
                things.Add(new(2, WorldThingKind2D.Shieldback, null, true, lift.Position + new Vector2(0, 110)));
            }

            using var game = SideScrollerSimulation2D.Create(new(traversal, map, things)
            {
                AuthoredCharacters = authored,
                PlayerMaximumHealth = 30,
                Objects = [lift]
            });
            var scene = new Scene2D();
            using var player = new AuthoredPersonPresentation2D(scene, PersonMoves.From(authored), traversal);
            using var enemies = new EnemyPresentation2D(scene, textures, traversal, new Silent());
            var ground = new WorldObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(5000, 2), new SolidColorShader(new Color4b(70, 88, 93)));
            scene.Add(ground);
            var platform = new WorldObject2D(Core.Characters.Geometry.PartShapes2D.Collision(lift.Asset), new SolidColorShader(new Color4b(37, 210, 190)));
            scene.Add(platform);
            camera.Position = focus;
            for (var step = 0; step < 240; step++)
            {
                var frame = game.Session.Advance(new PlayerInput2D(game.Player.Id, new PersonCommand2D()));
                var state = frame.Players[0];
                player.Equip(state.Equipment);
                player.ApplyState(state.Person, frame.Tick, state.MoveX, false, state.IsMeleeAttackActive);
                enemies.ApplyState(frame.Enemies, [], frame.Tick);
                platform.Transform.Position = frame.World.Objects[0].Position;
                if (step % 4 != 0)
                {
                    continue;
                }

                target.SavePng(Path.Combine(directory, $"{name}-{step / 4:000}.png"), new Color4b(145, 176, 190), () =>
                {
                    renderer.BeginFrame(400, 300, default);
                    renderer.Draw(scene);
                    renderer.EndFrame();
                });
            }
        }
    }

    /// <summary>A kinematic one-way lift, 12 units tall, ping-ponging along <paramref name="travel"/> at the August platform's pace.</summary>
    private static ObjectPlacement2D Lift(string id, Vector2 position, Vector2 travel, float width, float speed) =>
        new(id, new Part
        {
            Id = id,
            Name = id,
            Geometry = [new() { Id = "shape", A = "origin", Frame = "origin", Collision = new(), Geometry = Rectangle2D.Builder.Create(new(-width / 2f, -6f), new(width / 2f, 6f)), Material = new RenderMaterialDefinition2D { Fill = "#25d2be" }, }],
            Physics = new PhysicsBodySettings2D { MotionType = BodyMotionType2D.Kinematic, Restitution = 0f, TransfersContactMotion = true },
            Surface = new ObjectSurface2D { OneWayNormal = Vector2.UnitY },
            Motion = new CurveMotionDefinition2D
            {
                Curve = LineSegmentCurve2D.Builder.Create(Vector2.Zero, travel),
                PeriodSeconds = 2 * travel.Length() / speed,
                Progress = [ProgressMapKind.PingPong, ProgressMapKind.Smooth]
            }
        }, position);

    private sealed class Silent : ISoundEffectSink2D<SoundEffect2D>
    {
        public void Play(SoundEffect2D effect)
        {
        }
    }
}
