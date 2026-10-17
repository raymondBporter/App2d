using App2d.Core.Rendering.Characters;
using System.Numerics;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;

namespace App2d.Tests.Rendering;

public sealed class PointCharacterProjectionTests
{
    [Theory]
    [InlineData(1280, 720)]
    [InlineData(960, 640)]
    [InlineData(128, 128)]
    public void DeviceProjectionMapsDevicePixelsToClipSpaceAndPuppetDepth(int width, int height)
    {
        var projection = PointCharacterRenderer.DeviceProjection(width, height);
        AssertClose(new(-1, 1, 0), XnaVector3.Transform(new(0, 0, -64), projection));
        AssertClose(new(1, -1, 1), XnaVector3.Transform(new(width, height, 64), projection));
        AssertClose(new(0, 0, .5f), XnaVector3.Transform(new(width / 2f, height / 2f, 0), projection));
    }

    [Fact]
    public void ViewMapsModelUnitsToDevicePixelsWithYDownAndDepthUntouched()
    {
        var view = PointCharacterRenderer.View(new Vector2(64, 48), 240);
        AssertClose(new(64, 48, 3), XnaVector3.Transform(new(0, 0, 3), view));
        AssertClose(new(304, -192, -2), XnaVector3.Transform(new(1, 1, -2), view));
    }

    [Theory]
    [InlineData(1280, 720, 640, 650, 48)]
    [InlineData(128, 128, 64, 48, 240)]
    [InlineData(320, 225, 160, 112, 260)]
    public void ProjectionPutsTheModelOriginOnTheAnchorAtPixelsPerUnit(int width, int height, float anchorX, float anchorY, float pixelsPerUnit)
    {
        var projection = PointCharacterRenderer.Projection(width, height, new Vector2(anchorX, anchorY), pixelsPerUnit);
        AssertClose(Clip(anchorX, anchorY, width, height, .5f), XnaVector3.Transform(new(0, 0, 0), projection));
        AssertClose(Clip(anchorX + pixelsPerUnit, anchorY - pixelsPerUnit, width, height, .5f), XnaVector3.Transform(new(1, 1, 0), projection));
        AssertClose(Clip(anchorX, anchorY, width, height, 0), XnaVector3.Transform(new(0, 0, -64), projection));
        var composed = PointCharacterRenderer.View(new Vector2(anchorX, anchorY), pixelsPerUnit) * PointCharacterRenderer.DeviceProjection(width, height);
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(composed[i], projection[i], 1e-4);
        }
    }

    private static XnaVector3 Clip(float pixelX, float pixelY, int width, int height, float depth) => new(2 * pixelX / width - 1, 1 - 2 * pixelY / height, depth);

    private static void AssertClose(XnaVector3 expected, XnaVector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 1e-4);
        Assert.Equal(expected.Y, actual.Y, 1e-4);
        Assert.Equal(expected.Z, actual.Z, 1e-4);
    }
}
