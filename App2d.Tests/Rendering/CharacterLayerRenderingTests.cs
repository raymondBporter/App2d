using App2d.Core.Characters.Drawing;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class CharacterLayerRenderingTests
{
    [Fact]
    public void FixedCameraEncodesLayersWithoutMovingStoredGeometry()
    {
        var drawing = new PuppetDrawing();
        drawing.Mesh.Layer = -.3f;
        drawing.Mesh.Triangle(new(-16, -16, 0), new(16, -16, 0), new(0, 16, 0), Color4b.Red);
        drawing.Mesh.Layer = .3f;
        drawing.Mesh.Triangle(new(-16, -16, 0), new(16, -16, 0), new(0, 16, 0), Color4b.Blue);
        drawing.Batches.Add(new(0, 6));
        Assert.All(drawing.Mesh.Vertices.ToArray(), v => Assert.Equal(0, v.Position.Z));
        Assert.Equal(0, drawing.Mesh.Min.Z);
        Assert.Equal(0, drawing.Mesh.Max.Z);

        using var graphics = new GraphicsTestContext();
        using var target = RenderCapture2D.CreateTarget(graphics.Device, 128, 128, DepthFormat.Depth24, 0);
        using var renderer = new PointCharacterRenderer(graphics.Device);
        using (graphics.Device.UseRenderTarget(target, Color4b.White))
            renderer.Draw(drawing, PointCharacterRenderer.Projection(128, 128, new(64), 1), Matrix.Identity);
        var pixels = new Color4b[128 * 128];
        target.GetData(pixels);
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 64]);
    }
}
