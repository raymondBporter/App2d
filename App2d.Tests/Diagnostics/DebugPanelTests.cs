using App2d.Core;
using App2d.Core.Hosting;
using App2d.Core.Input;

namespace App2d.Tests.Diagnostics;

public sealed class DebugPanelTests
{
    [Fact]
    public void SectionsAreCreatedOnceAndKeepRegistrationOrder()
    {
        var panel = new DebugPanel();
        var audio = panel.Section("Audio").Toggle("Mute", () => false, _ => { });
        panel.Section("Camera");
        var again = panel.Section("Audio").Slider("Volume", () => 1f, _ => { }, 0f, 1f);

        Assert.Same(audio, again);
        Assert.Equal(["Audio", "Camera"], panel.Sections.Select(section => section.Title));
        Assert.Equal(["Mute", "Volume"], audio.Controls.Select(control => control.Label));
    }

    [Fact]
    public void ControlsReadAndWriteThroughTheirAccessors()
    {
        var value = 0.25f;
        var choice = "auto";
        var runs = 0;
        var section = new DebugPanel().Section("Test")
            .Slider("Level", () => value, v => value = v, 0f, 1f, "help", logarithmic: true)
            .Choice("Mood", () => choice, v => choice = v, ["auto", "combat"])
            .Button("Run", () => $"ran {++runs}")
            .Readout("Status", () => $"value {value}");

        var slider = Assert.IsType<DebugSlider>(section.Controls[0]);
        slider.Set(0.75f);
        Assert.Equal(0.75f, slider.Get());
        Assert.Equal((0f, 1f, true, "help"), (slider.Min, slider.Max, slider.Logarithmic, slider.Help));

        var combo = Assert.IsType<DebugChoice>(section.Controls[1]);
        combo.Set("combat");
        Assert.Equal("combat", combo.Get());
        Assert.Equal(["auto", "combat"], combo.Options);

        Assert.Equal("ran 1", Assert.IsType<DebugButton>(section.Controls[2]).Run());
        Assert.Equal("value 0.75", Assert.IsType<DebugReadout>(section.Controls[3]).Text());
    }

    [Fact]
    public void DuplicateLabelsInOneSectionAreRejected()
    {
        var section = new DebugPanel().Section("Draw").Toggle("Grid", () => false, _ => { });

        Assert.Throws<InvalidOperationException>(() => section.Toggle("Grid", () => true, _ => { }));
        Assert.Throws<ArgumentException>(() => section.Toggle(" ", () => true, _ => { }));
    }

    [Fact]
    public void GameDrawTogglesAreOnThePanel()
    {
        using var game = new PanelGame();
        var draw = Assert.Single(game.DebugPanel.Sections);
        var toggles = draw.Controls.OfType<DebugToggle>().ToDictionary(toggle => toggle.Label);

        Assert.Equal("Draw", draw.Title);
        Assert.Equal(["Graphics", "Grid", "FPS", "Collision shapes"], draw.Controls.Select(control => control.Label));
        Assert.True(toggles["Graphics"].Get());
        Assert.False(toggles["Grid"].Get());

        toggles["Grid"].Set(true);
        game.DrawGraphics = false;

        Assert.True(game.DrawGrid);
        Assert.False(toggles["Graphics"].Get());
    }

    private sealed class PanelGame : Game2D
    {
        public override void Update(FrameTime time, InputState input)
        {
        }
    }
}
