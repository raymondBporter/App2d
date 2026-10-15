using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Gameplay.Enemies;
using App2d.Core.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;
using XVector = Microsoft.Xna.Framework.Vector2;

namespace App2d.CharacterStudio;

internal sealed partial class ProofRenders
{
    private void RenderPterosaurGameplay(AuthoredCatalog catalog)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 900, 500, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        using var batch = new SpriteBatch(GraphicsDevice);
        using var pixel = new Texture2D(GraphicsDevice, 1, 1); pixel.SetData(new[] { Color.White });
        var physics = new PhysicsWorld2D { Gravity = new(0, -1200) };
        var ground = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(2000, 20));
        ground.Transform.Position = new(0, -10); physics.AddBody(ground, BodyMotionType2D.Static).CollisionLayer = 1;
        var enemy = new AuthoredEntityEnemy2D(EntityId2D.Create(), catalog.Entities["flying-pterosaur"], physics, new(-96, 154), 1, 4);
        enemy.SetSimulationEnabled(true);
        var drawing = new PuppetDrawing(); var folder = Path.Combine(_smokePath, "pterosaur-frames"); Directory.CreateDirectory(folder);
        const float zoom = 1.5f;
        for (var frame = 0; frame < 115; frame++)
        {
            for (var tick = 0; tick < 5; tick++)
            { enemy.Update(1f / 120, new(0, 26)); physics.Step(1f / 120); enemy.SyncAfterPhysics(); }
            GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 236, 223));
            batch.Begin();
            batch.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(0, 450, 900, 3), new Color(98, 105, 92));
            // A standing player collider shows attack reach and the sampled target without adding another animation.
            batch.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(430, 372, 40, 78), new Color(195, 195, 182));
            batch.End();
            drawing.Build(enemy.Entity, enemy.Pose.Local);
            var root = enemy.Pose.Position;
            _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(900, 500,
                new(450 + root.X * 40 * zoom, 450 - root.Y * 40 * zoom), (int)(40 * zoom)), Matrix.CreateScale(enemy.Pose.Facing, 1, 1));
            GraphicsDevice.SetRenderTarget(null);
            using var file = File.Create(Path.Combine(folder, $"frame-{frame:D4}.png")); target.SaveAsPng(file, 900, 500);
        }
    }
}
