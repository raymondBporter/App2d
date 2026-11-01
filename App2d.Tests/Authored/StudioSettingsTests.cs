using App2d.CharacterStudio.Editing;
using App2d.CharacterStudio.Editor;

namespace App2d.Tests.Authored;

public sealed class StudioSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "app2d-studio-settings-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_root, "CharacterStudio", "settings.json");

    [Fact]
    public void MissingFileGivesTheDesignSizes()
    {
        var settings = StudioSettings.Load(SettingsPath);
        Assert.Equal(1f, settings.UiScale);
        Assert.Equal(250f, settings.Layout.LeftWidth);
        Assert.Equal(330f, settings.Layout.RightWidth);
        Assert.Equal(.5f, settings.Layout.BrowserShare);
        Assert.Equal(230f, settings.Layout.TimelineHeight(Workspace.Animate, 230));
    }

    [Fact]
    public void AFileWithOnlyTheScaleKeepsItAndDefaultsTheLayout()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ \"uiScale\": 1.25 }");
        var settings = StudioSettings.Load(SettingsPath);
        Assert.Equal(1.25f, settings.UiScale);
        Assert.Equal(250f, settings.Layout.LeftWidth);
    }

    [Fact]
    public void SavedSizesAndScaleRoundTrip()
    {
        var settings = new StudioSettings { UiScale = 1.5f };
        settings.Layout.LeftWidth = 312;
        settings.Layout.RightWidth = 400;
        settings.Layout.BrowserShare = .3f;
        settings.Layout.SetTimelineHeight(Workspace.Animate, 300);
        settings.Save(SettingsPath);

        var loaded = StudioSettings.Load(SettingsPath);
        Assert.Equal(1.5f, loaded.UiScale);
        Assert.Equal(312f, loaded.Layout.LeftWidth);
        Assert.Equal(400f, loaded.Layout.RightWidth);
        Assert.Equal(.3f, loaded.Layout.BrowserShare);
        Assert.Equal(300f, loaded.Layout.TimelineHeight(Workspace.Animate, 230));
        Assert.Equal(64f, loaded.Layout.TimelineHeight(Workspace.Model, 64));
        Assert.Contains("\"uiScale\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void FitColumnsKeepsTheAskedWidthsWhileTheStageHasRoom()
    {
        var (left, right) = EditorLayout.FitColumns(1500, 250, 330, 100, 200);
        Assert.Equal(250f, left);
        Assert.Equal(330f, right);
    }

    [Fact]
    public void FitColumnsShrinksBothPanelsInProportionToKeepTheStage()
    {
        var (left, right) = EditorLayout.FitColumns(700, 250, 330, 100, 200);
        Assert.Equal(500f, left + right, .001f);
        Assert.Equal(250f / 330, left / right, .001f);
    }

    [Fact]
    public void FitColumnsNeverGoesBelowTheSmallestPanel()
    {
        var (left, right) = EditorLayout.FitColumns(300, 250, 330, 100, 200);
        Assert.Equal(100f, left);
        Assert.Equal(100f, right);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
