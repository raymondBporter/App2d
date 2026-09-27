using App2d.Core.Assets;

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

        var locations = AssetLocations.ForGame(Path.Combine(_root, "App2d", "bin", "Debug"), useSourceAssets: true);
        var resolved = locations.Authored;

        Assert.Equal(authored, resolved);
        Assert.Equal("pulled soundtrack", File.ReadAllText(Path.Combine(resolved, "audio", "music", "soundtrack.json")));
        Assert.False(Directory.Exists(Path.Combine(runtime, "audio", "music")));
        Assert.Equal(runtime, locations.Runtime);
        Assert.Equal(music, locations.Music);
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

        var locations = AssetLocations.ForGame(output, useSourceAssets);
        Assert.Equal(packaged, locations.Authored);
        Assert.Equal(packaged, locations.Runtime);
        Assert.Equal(Path.Combine(packaged, "PointCharacters", "authored"), locations.AuthoredCharacters);
    }

    [Fact]
    public void SourceGameUsesDurableCharactersAndLevelsAlongsideGeneratedArt()
    {
        var runtime = Path.Combine(_root, "Assets", "Runtime");
        var authored = Path.Combine(_root, "Assets", "Static");
        var characters = Path.Combine(_root, "Assets", "Characters");
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(authored);
        Directory.CreateDirectory(Path.Combine(characters, "authored"));
        var output = Path.Combine(_root, "App2d", "bin", "Debug");

        var locations = AssetLocations.ForGame(output, useSourceAssets: true);
        Assert.Equal(characters, locations.CharacterLibrary);
        Assert.Equal(Path.Combine(characters, "authored"), locations.AuthoredCharacters);
        Assert.Equal(Path.Combine(authored, "levels"), locations.Levels);
        Assert.Equal(Path.Combine(runtime, "audio", "sfx"), locations.SoundEffects);
        Assert.Equal(Path.Combine(runtime, "environments", "tilesets"), locations.Tilesets);
        Assert.Equal(Path.Combine(runtime, "ui"), locations.UI);

        // A packaged game nested under a checkout must not silently read its source files.
        var deployed = AssetLocations.ForGame(output, useSourceAssets: false);
        Assert.Equal(Path.Combine(output, "Assets", "PointCharacters", "authored"), deployed.AuthoredCharacters);
        Assert.Equal(Path.Combine(output, "Assets", "levels"), deployed.Levels);
    }

    [Fact]
    public void StudioKeepsItsPackagedFirstDiscoveryAndCanFallBackToTheWorkspace()
    {
        var characters = Path.Combine(_root, "Assets", "Characters");
        var output = Path.Combine(_root, "studio-output");
        var working = Path.Combine(_root, "tools", "nested");
        Directory.CreateDirectory(Path.Combine(characters, "authored"));
        Assert.Equal(characters, AssetLocations.FindCharacterLibrary(output, working));

        var packaged = Path.Combine(output, "Assets", "Characters");
        Directory.CreateDirectory(Path.Combine(packaged, "authored"));
        Assert.Equal(packaged, AssetLocations.FindCharacterLibrary(output, working));
    }

    [Fact]
    public void AuthoredContentDoesNotRequireGeneratedArtAndMissingStudioAssetsAreReported()
    {
        var authored = Path.Combine(_root, "Assets", "Static");
        Directory.CreateDirectory(authored);
        var output = Path.Combine(_root, "output");
        Assert.Equal(authored, AssetLocations.ForGame(output, true).Authored);
        Assert.Throws<DirectoryNotFoundException>(() => AssetLocations.FindCharacterLibrary(output, _root));
    }

    [Fact]
    public void UserPathsKeepExistingSaveAndSettingsNamesWithoutCreatingDirectories()
    {
        Directory.CreateDirectory(_root);
        var userRoot = Path.Combine(_root, "user-data");
        var locations = new UserDataLocations(userRoot);
        Assert.Equal(Path.Combine(userRoot, "save.json"), locations.PlayerSave);
        Assert.Equal(Path.Combine(userRoot, "CharacterStudio", "settings.json"), locations.CharacterStudioSettings);
        Assert.False(Directory.Exists(userRoot));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
