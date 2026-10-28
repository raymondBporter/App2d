using App2d.Core.Json;
using App2d.Beamwright;
using App2d.Beamwright.Optics;
using App2d.Core.Mathematics;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Beamwright;

public sealed class BenchCoordinateTests
{
    [Fact]
    public void TypedCoordinatesRetainTheOriginalCampaignJsonFormat()
    {
        var options = JsonProfiles.CamelCase;
        var level = BeamwrightContent.Default.Levels[0];
        var json = JsonSerializer.Serialize(level, options);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(7, document.RootElement.GetProperty("w").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("size", out _));
        var source = document.RootElement.GetProperty("sources")[0];
        Assert.True(source.TryGetProperty("x", out _));
        Assert.False(source.TryGetProperty("cell", out _));
        Assert.False(source.TryGetProperty("center", out _));
        var restored = JsonSerializer.Deserialize<BenchLevel>(json, options)!;
        Assert.Equal((7, 5), (restored.W, restored.H));
        Assert.True(new BeamTracer(BeamwrightContent.Default).Trace(restored, restored.Solution).Win);
    }

    [Fact]
    public void IntegerCoordinatesAndPlainDimensionsGiveTheSameBenchPositions()
    {
        var source = new LightSource { X = 2, Y = 3 };
        Assert.Equal(new Vector2i(2, 3), source.Cell);
        Assert.Equal(new Vector2(2.5f, 3.5f), source.Center);
        var target = new Photocell { X = 2, Y = 3, Ox = .25f, Oy = -.125f };
        Assert.Equal(new Vector2(2.75f, 3.375f), target.Center);
        var zone = new MirageZone { X = 6, Y = 5, W = 4, H = 2 };
        Assert.Equal(new Vector2i(6, 5), zone.Cell);
        Assert.Equal(new Vector2(6, 5), zone.AirBounds.Min);
        Assert.Equal(new Vector2(10, 6.88f), zone.AirBounds.Max);
    }
}
