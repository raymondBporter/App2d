using App2d.Gameplay.World;
using App2d.Gameplay.World.Presentation;
using App2d.Rendering.Vegetation;
using App2d.Tiles;
using System.Numerics;
using Xunit;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Game.Presentation.Tests.World;

public sealed class GrassCuttingTests
{
    [Fact]
    public void SeveredTopsStartExactlyAtCutHeightAndKeepTheirOriginalLength()
    {
        var patch = Patch();
        var tips = patch.CreateClippings(12f, Wind()).ToArray();
        Assert.Equal(patch.BladeCount, tips.Length);
        foreach (var tip in tips)
        {
            var bounds = tip.WorldBounds(tip.ReleasePosition, 0f);
            Assert.Equal(19f, bounds.Bottom, 4); // Ground 7 + cut 12.
            Assert.Equal(37f, bounds.Top, 4); // Ground 7 + original blade 30.
        }
        Assert.Empty(patch.CreateClippings(30f, Wind()));
        Assert.Empty(patch.CreateClippings(40f, Wind()));
    }

    [Fact]
    public void FlowersRemainPartOfTheReleasedTop()
    {
        var patch = new VegetationPatch2D(0f, 10f, 7f,
            new(30f, 30f, 3f, 3f, 6f, 0f, Color.DarkGreen, Color.LightGreen, 1f, Color.Pink), 107);
        var tip = patch.CreateClippings(12f, Wind(0f)).First();
        Assert.Equal(37f + 3f * 1.15f, tip.WorldBounds(tip.ReleasePosition, 0f).Top, 4);
    }

    [Fact]
    public void ReleaseUsesTheCurrentWindBentGeometry()
    {
        var patch = Patch();
        var calm = patch.CreateClippings(12f, Wind(0f)).First();
        var bent = patch.CreateClippings(12f, Wind(20f)).First();
        Assert.NotEqual(calm.ReleasePosition.X, bent.ReleasePosition.X);
        Assert.Equal(calm.ReleasePosition.Y, bent.ReleasePosition.Y);
    }

    [Fact]
    public void ClippingRisesThenFallsFluttersAndFadesOutAfterOneSecond()
    {
        var piece = Clipping();
        var start = piece.Position;
        piece.Advance(0.15f, Wind() with { TotalSeconds = 0.15 });
        Assert.True(piece.Position.Y > start.Y);
        Assert.NotEqual(start.X, piece.Position.X);
        Assert.NotEqual(0f, piece.Rotation);
        Assert.Equal(1f, piece.Opacity);
        piece.Advance(0.7f, Wind() with { TotalSeconds = 0.85 });
        Assert.True(piece.Velocity.Y < 0f);
        Assert.InRange(piece.Opacity, 0.01f, 0.99f);
        piece.Advance(0.2f, Wind() with { TotalSeconds = 1.05 });
        Assert.True(piece.IsExpired);
        Assert.Equal(0f, piece.Opacity);
    }

    [Fact]
    public void WindAffectsFlightAndFrameRateDoesNotChangeTheToss()
    {
        var calm = Clipping();
        var windy = Clipping();
        calm.Advance(0.4f, Wind(0f) with { TotalSeconds = 0.4 });
        windy.Advance(0.4f, Wind(15f) with { TotalSeconds = 0.4 });
        Assert.True(MathF.Abs(calm.Position.X - windy.Position.X) > 0.01f);
        Assert.Equal(calm.Position.Y, windy.Position.Y);
        var fast = Clipping();
        var slow = Clipping();
        for (var i = 1; i <= 48; i++) fast.Advance(1f / 120f, Wind() with { TotalSeconds = i / 120d });
        for (var i = 1; i <= 12; i++) slow.Advance(1f / 30f, Wind() with { TotalSeconds = i / 30d });
        Assert.InRange(Vector2.Distance(fast.Position, slow.Position), 0f, 0.002f);
    }

    [Fact]
    public void LiveCutsBurstOnlyOnceAndLandingRemovesPiecesBeforeTheirTimeout()
    {
        var map = new EditableTileMap2D(16, 16, 32f, 16, Vector2.Zero, ["kenney-grassland"]);
        for (var x = 0; x < map.Width; x++) map.SetTileKind(x, 1, TileKind2D.Solid);
        var terrain = TerrainChunkState2D.Capture(map, new(0, 0), 1);
        var view = new VegetationPresentation2D();
        view.SetTerrain([terrain]);
        view.ApplyCuts([]);
        view.ApplyCuts([new(4, 1)]);
        var count = view.ClippingCount;
        Assert.True(count > 0);
        view.ApplyCuts([new(4, 1)]);
        Assert.Equal(count, view.ClippingCount);
        for (var i = 0; i < 108; i++) view.Advance(1f / 120f);
        Assert.True(view.ClippingCount < count); // 0.9 s: ground, not the 1 s timeout.
        view.Advance(0.2f);
        Assert.Equal(0, view.ClippingCount);
        var attached = new VegetationPresentation2D();
        attached.SetTerrain([terrain]);
        attached.ApplyCuts([new(4, 1)]);
        Assert.Equal(0, attached.ClippingCount); // Snapshot attachment never replays old cuts.
    }

    private static VegetationPatch2D Patch() => new(0f, 32f, 7f,
        new(30f, 30f, 2f, 3f, 6f, 5f, Color.DarkGreen, Color.LightGreen, 0f, Color.Pink), 107);
    private static VegetationWind2D Wind(float strength = 5f) => new(0f, strength, 1.15f, 0.027f, 0.12f);
    private static GrassClipping2D Clipping() =>
        new(Patch().CreateClippings(12f, Wind()).First(), new(0f, 100f), 4f, 0.7f, 1f);
}
