using App2d.Core.Characters.Authored;
using App2d.Rendering.Characters;
using App2d.Tests.Authored;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class WardrobeDepthTests
{
    [Theory]
    [InlineData("maul-brute", 1)]
    [InlineData("maul-brute", -1)]
    [InlineData("cinder-gunner", 1)]
    [InlineData("cinder-gunner", -1)]
    public void NearForearmStaysVisibleAcrossTheWholeBelt(string id, int facing)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot); var entity = catalog.Entities[id];
        var pose = PoseEvaluator.Sample(entity.Model, catalog.Animations["person-idle"], .6f);
        var placed = new ActorPose(pose, Vector2.Zero, 1);
        var frame = placed.Socket(entity.Sockets[PersonWardrobe.BodySocket]);
        using var graphics = new GraphicsTestContext();
        using var target = new RenderTarget2D(graphics.Device, 128, 128, false, SurfaceFormat.Color, DepthFormat.Depth24);
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var hips = pose.World("hips");
        var projection = PointCharacterRenderer.Projection(128, 128, new(64, 48), 240);
        var world = Matrix.CreateTranslation(-hips.X, -hips.Y, 0) * Matrix.CreateScale(facing, 1, 1);
        var drawing = new PuppetDrawing(); drawing.Build(entity, pose);
        graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.White);
        renderer.Draw(drawing.Mesh, projection, world); graphics.Device.SetRenderTarget(null);
        var pixels = new Color[128 * 128]; target.GetData(pixels);
        var elbow = pose.World("left-elbow"); var hand = pose.World("left-hand");
        var y0 = Vector3.Dot(elbow - frame.Origin, frame.Across3);
        var y1 = Vector3.Dot(hand - frame.Origin, frame.Across3);
        var torso = PersonBuild.From(entity.Model.Variant!.Build).Torso;
        foreach (var height in new[] { .032f, .052f, .075f })
        {
            var t = (height * torso - y0) / (y1 - y0); Assert.InRange(t, 0, 1);
            var sample = Vector3.Lerp(elbow, hand, t) - hips;
            var x = (int)MathF.Round(64 + facing * sample.X * 240); var y = (int)MathF.Round(48 - sample.Y * 240);
            Assert.True(pixels[y * 128 + x].R < 70, $"{id}: near forearm is hidden at belt height {height}.");
        }
    }

    [Theory]
    [InlineData("maul-brute", 1)]
    [InlineData("maul-brute", -1)]
    [InlineData("cinder-gunner", 1)]
    [InlineData("cinder-gunner", -1)]
    public void BothUpperLegsAreHiddenInsideTheWrap(string id, int facing)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot); var entity = catalog.Entities[id];
        var pose = PoseEvaluator.Sample(entity.Model, catalog.Animations["person-idle"], 0);
        using var graphics = new GraphicsTestContext();
        using var target = new RenderTarget2D(graphics.Device, 128, 128, false, SurfaceFormat.Color, DepthFormat.Depth24);
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var hips = pose.World("hips");
        var projection = PointCharacterRenderer.Projection(128, 128, new(64, 48), 160);
        var world = Matrix.CreateTranslation(-hips.X, -hips.Y, 0) * Matrix.CreateScale(facing, 1, 1);
        var drawing = new PuppetDrawing();
        Color[] Render(bool dressed)
        {
            if (dressed) drawing.Build(entity, pose); else drawing.Build(entity.Model, pose);
            graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.White);
            renderer.Draw(drawing.Mesh, projection, world); graphics.Device.SetRenderTarget(null);
            var pixels = new Color[128 * 128]; target.GetData(pixels); return pixels;
        }
        var bare = Render(false); var clothed = Render(true);
        foreach (var side in new[] { "left", "right" })
        {
            var sample = Vector3.Lerp(pose.World(side + "-hip"), pose.World(side + "-knee"), .08f) - hips;
            var x = (int)MathF.Round(64 + facing * sample.X * 160); var y = (int)MathF.Round(48 - sample.Y * 160);
            Assert.True(bare[y * 128 + x].R < 70, "The sample must hit the dark leg stroke before clothing.");
            Assert.True(clothed[y * 128 + x].R > 90, "The garment must cover the leg, including the near leg, in the depth buffer.");
        }
    }
}
