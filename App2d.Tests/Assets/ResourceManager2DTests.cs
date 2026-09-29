using App2d.Core.Assets;

namespace App2d.Tests.Assets;

public sealed class ResourceManager2DTests
{
    [Fact]
    public void LoadsOnceAndReportsSourceAndDistinctConsumers()
    {
        using var resources = new ResourceManager2D();
        var calls = 0;
        resources.Register("map/cavern", () => { calls++; return new object(); }, "levels/cavern/level.db");

        Assert.False(resources.Snapshot()[0].IsLoaded);
        Assert.Throws<InvalidOperationException>(() => resources.Get<object>("map/cavern"));
        resources.Load<object>("map/cavern", "simulation");
        var loaded = resources.GetLoad<object>("map/cavern", "editor");
        Assert.Same(loaded, resources.Get<object>("map/cavern", "editor"));
        Assert.Equal(1, calls);

        var info = Assert.Single(resources.Snapshot());
        Assert.Equal("levels/cavern/level.db", info.Source);
        Assert.True(info.IsLoaded);
        Assert.Equal(["editor", "simulation"], info.Owners.ToArray());
        resources.ForgetOwner("editor");
        Assert.Equal(["simulation"], resources.Snapshot()[0].Owners.ToArray());
    }

    [Fact]
    public void FailedLoadCanBeRetriedWithoutClaimingAnOwner()
    {
        using var resources = new ResourceManager2D();
        var calls = 0;
        resources.Register("sound/hit", () =>
        {
            if (++calls == 1) throw new IOException("missing file");
            return new object();
        });

        Assert.Throws<IOException>(() => resources.GetLoad<object>("sound/hit", "combat"));
        Assert.False(resources.Snapshot()[0].IsLoaded);
        Assert.Empty(resources.Snapshot()[0].Owners);
        Assert.NotNull(resources.GetLoad<object>("sound/hit", "combat"));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void RemovalAndClearReleaseOwnedValuesButNotBorrowedValues()
    {
        var owned = new Probe();
        var borrowed = new Probe();
        using var resources = new ResourceManager2D();
        resources.Register("owned", () => owned);
        resources.Register("borrowed", () => borrowed, ownsResource: false);
        resources.Load<Probe>("owned");
        resources.Load<Probe>("borrowed");

        Assert.True(resources.Remove("owned"));
        Assert.True(owned.Disposed);
        Assert.False(resources.Remove("owned"));
        resources.Clear();
        Assert.False(borrowed.Disposed);
        Assert.Empty(resources.Snapshot());
    }

    [Fact]
    public void DuplicateKeysAndWrongTypesAreRejected()
    {
        using var resources = new ResourceManager2D();
        resources.Register("character/hero", () => new object());
        Assert.Throws<ArgumentException>(() => resources.Register("character/hero", () => "another"));
        Assert.Throws<InvalidOperationException>(() => resources.GetLoad<string>("character/hero"));
        Assert.Throws<KeyNotFoundException>(() => resources.GetLoad<object>("missing"));
    }

    private sealed class Probe : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
