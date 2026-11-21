using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class CharacterActorRenderingTests
{
    private static readonly Matrix Projection = LayeredMeshRenderer.Projection(128, 128, new(64), 1);

    /// <summary>A puppet: a triangle centred on x at depth z. Smaller z is nearer the camera.</summary>
    private static LayeredMesh Puppet(float x, float z, Color4b color)
    {
        var drawing = new LayeredMesh();
        drawing.Triangle(new(x - 12, -12, z), new(x + 12, -12, z), new(x, 12, z), color);
        return drawing;
    }

    [Fact]
    public void DrawActorLayersEachActorAgainstItselfOnly()
    {
        using var graphics = new GraphicsTestContext();
        using var target = RenderCapture2D.CreateTarget(graphics.Device, 128, 128, DepthFormat.Depth24, 0);
        using var renderer = new PointCharacterRenderer(graphics.Device);
        using (graphics.Device.UseRenderTarget(target, Color4b.White))
        {
            // Left: a farther puppet drawn into the same layer stays hidden behind the near one.
            renderer.DrawActor(Puppet(-32, 0, Color4b.Red), Projection, Matrix.Identity);
            renderer.Draw(Puppet(-32, 10, Color4b.Blue), Projection, Matrix.Identity);
            // Right: each actor starts its own layer, so the farther one drawn later shows.
            renderer.DrawActor(Puppet(32, 0, Color4b.Red), Projection, Matrix.Identity);
            renderer.DrawActor(Puppet(32, 10, Color4b.Blue), Projection, Matrix.Identity);
        }

        var pixels = new Color4b[128 * 128];
        target.GetData(pixels);
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 32]);
        Assert.Equal(Color4b.Blue, pixels[64 * 128 + 96]);
    }

    [Fact]
    public void DrawActorDrawsTheSwooshIntoThePuppetsLayerBehindIt()
    {
        using var graphics = new GraphicsTestContext();
        using var target = RenderCapture2D.CreateTarget(graphics.Device, 128, 128, DepthFormat.Depth24, 0);
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var puppet = new LayeredMesh();
        puppet.Triangle(new(-44, -12, 0), new(-20, -12, 0), new(-32, 12, 0), Color4b.Red);
        puppet.Triangle(new(20, -12, 10), new(44, -12, 10), new(32, 12, 10), Color4b.Blue);
        var swoosh = new LayeredMesh(3);
        swoosh.Triangle(new(-60, -20, 5), new(60, -20, 5), new(0, 40, 5), Color4b.Lime);
        using (graphics.Device.UseRenderTarget(target, Color4b.White))
        {
            renderer.DrawActor(puppet, Projection, Matrix.Identity, swoosh);
            renderer.DrawActor(Puppet(0, 0, Color4b.Red), Projection, Matrix.Identity, new LayeredMesh(3));
        }

        var pixels = new Color4b[128 * 128];
        target.GetData(pixels);
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 32]);
        Assert.Equal(Color4b.Lime, pixels[64 * 128 + 96]);
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 64]);
    }

    [Fact]
    public void ClearDepthStartsANewLayerForPlainMeshes()
    {
        using var graphics = new GraphicsTestContext();
        using var target = RenderCapture2D.CreateTarget(graphics.Device, 128, 128, DepthFormat.Depth24, 0);
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var near = new LayeredMesh(3);
        near.Triangle(new(-12, -12, 0), new(12, -12, 0), new(0, 12, 0), Color4b.Red);
        var far = new LayeredMesh(3);
        far.Triangle(new(-12, -12, 10), new(12, -12, 10), new(0, 12, 10), Color4b.Blue);
        using (graphics.Device.UseRenderTarget(target, Color4b.White))
        {
            renderer.Draw(near, Projection, Matrix.Identity);
            renderer.ClearDepth();
            renderer.Draw(far, Projection, Matrix.Identity);
        }

        var pixels = new Color4b[128 * 128];
        target.GetData(pixels);
        Assert.Equal(Color4b.Blue, pixels[64 * 128 + 64]);
    }
}
