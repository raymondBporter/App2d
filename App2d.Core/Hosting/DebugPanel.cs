using App2d.Core.Validation;

namespace App2d.Core.Hosting;

/// <summary>Runtime debug controls a game exposes, grouped into titled sections. The game's tool UI draws them; the registry knows nothing about widgets.</summary>
public sealed class DebugPanel
{
    private readonly List<DebugSection> _sections = [];
    public IReadOnlyList<DebugSection> Sections => _sections;

    /// <summary>The section with this title, created on first use so a game can extend an engine section.</summary>
    public DebugSection Section(string title)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(title);
        var section = _sections.Find(existing => existing.Title == title);
        if (section is null)
        {
            _sections.Add(section = new DebugSection(title));
        }

        return section;
    }
}

/// <summary>Controls in registration order. Each registration returns the section so calls chain.</summary>
public sealed class DebugSection
{
    private readonly List<DebugControl> _controls = [];
    internal DebugSection(string title) => Title = title;
    public string Title { get; }
    public IReadOnlyList<DebugControl> Controls => _controls;

    public DebugSection Toggle(string label, Func<bool> get, Action<bool> set, string help = "") => Add(new DebugToggle(label, help, get, set));
    public DebugSection Slider(string label, Func<float> get, Action<float> set, float min, float max, string help = "", bool logarithmic = false) => Add(new DebugSlider(label, help, get, set, min, max, logarithmic));
    public DebugSection Choice(string label, Func<string> get, Action<string> set, IReadOnlyList<string> options, string help = "") => Add(new DebugChoice(label, help, get, set, options));
    /// <summary>An action whose returned text is shown beneath the button until it runs again.</summary>
    public DebugSection Button(string label, Func<string> run, string help = "") => Add(new DebugButton(label, help, run));
    /// <summary>Live text, re-read every frame.</summary>
    public DebugSection Readout(string label, Func<string> text) => Add(new DebugReadout(label, text));

    private DebugSection Add(DebugControl control)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(control.Label);
        StateGuard.ThrowIf(_controls.Any(existing => existing.Label == control.Label), $"Section '{Title}' already has a control labelled '{control.Label}'.");
        _controls.Add(control);
        return this;
    }
}

public abstract record DebugControl(string Label, string Help);
public sealed record DebugToggle(string Label, string Help, Func<bool> Get, Action<bool> Set) : DebugControl(Label, Help);
public sealed record DebugSlider(string Label, string Help, Func<float> Get, Action<float> Set, float Min, float Max, bool Logarithmic) : DebugControl(Label, Help);
public sealed record DebugChoice(string Label, string Help, Func<string> Get, Action<string> Set, IReadOnlyList<string> Options) : DebugControl(Label, Help);
public sealed record DebugButton(string Label, string Help, Func<string> Run) : DebugControl(Label, Help);
public sealed record DebugReadout(string Label, Func<string> Text) : DebugControl(Label, "");
