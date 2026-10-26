using App2d.EditorUi;
using App2d.Tests.Rendering;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Tests.EditorUi;

[Collection("ImGui")]
public sealed class ImGuiSamplingTests
{
    [Fact]
    public void PixelTexturesCanUseNearestSamplingWithoutChangingOtherRegistrations()
    {
        using var graphics = new GraphicsTestContext();
        using var texture = new Texture2D(graphics.Device, 2, 1);
        texture.SetData(new[] { Color4b.Black, Color4b.White });
        using var gui = new ImGuiHost(graphics.Device, new TestPlatform());
        var point = gui.Register(texture, pointSample: true);
        var linear = gui.Register(texture);
        graphics.Device.Clear(Color4b.Black);
        gui.Begin(1f / 60);
        var draw = ImGui.GetBackgroundDrawList();
        draw.AddImage(point, new(0, 0), new(64, 128));
        draw.AddImage(linear, new(64, 0), new(128, 128));
        gui.Render();
        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Black, pixels[64 * 128 + 28]);
        Assert.Equal(Color4b.White, pixels[64 * 128 + 36]);
        Assert.InRange(pixels[64 * 128 + 64 + 28].R, 60, 140);
        gui.Unregister(point);
        gui.Unregister(linear);
    }

    private sealed class TestPlatform : IImGuiPlatform
    {
        public float DpiScale => 1;
        public void UpdateInput(ImGuiIOPtr io) { }
        public void Dispose() { }
    }
}
