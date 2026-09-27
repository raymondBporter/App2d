namespace App2d.Tests.Assets;

public sealed class AuthoredAssetPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "app2d-assets-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void DebugUsesPulledMusicWhenExistingRuntimeTreeHasNoMusic()
    {
        var runtime = Path.Combine(_root, "Assets", "Runtime");
        Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(runtime, "content-manifest.json"), "{}");
        var authored = Path.Combine(_root, "Assets", "Static");
        var music = Path.Combine(authored, "audio", "music");
        Directory.CreateDirectory(music);
        File.WriteAllText(Path.Combine(music, "soundtrack.json"), "pulled soundtrack");

        var resolved = AssetPaths.ResolveAuthoredRoot(Path.Combine(_root, "App2d", "bin", "Debug"), runtime, true);

        Assert.Equal(authored, resolved);
        Assert.Equal("pulled soundtrack", File.ReadAllText(Path.Combine(resolved, "audio", "music", "soundtrack.json")));
        Assert.False(Directory.Exists(Path.Combine(runtime, "audio", "music")));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void PackagedGameUsesItsOwnAssets(bool useSourceAssets, bool sourceTreeExists)
    {
        Directory.CreateDirectory(_root);
        if (sourceTreeExists) Directory.CreateDirectory(Path.Combine(_root, "Assets", "Static"));
        var output = Path.Combine(_root, "output", "game");
        var packaged = Path.Combine(output, "Assets");

        Assert.Equal(packaged, AssetPaths.ResolveAuthoredRoot(output, packaged, useSourceAssets));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
