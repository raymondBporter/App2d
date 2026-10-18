using App2d.Core.Hosting;
using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;

namespace App2d.Diagnostics;

/// <summary>
/// A floating ImGui window over the game's registered debug controls, toggled with F5. It opts out of keyboard
/// navigation, so gameplay keys keep working unless a slider or value field is being edited.
/// </summary>
internal sealed class DebugPanelUi2D(DebugPanel panel)
{
    public const ImGuiKey ToggleKey = ImGuiKey.F5;
    private readonly Dictionary<DebugControl, (string Text, bool IsProblem)> _notes = [];
    public bool IsOpen { get; set; }

    public void Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        ImGui.SetNextWindowPos(new Vector2(24, 24) * Ui.Scale, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(380, 640) * Ui.Scale, ImGuiCond.FirstUseEver);
        var open = true;
        var visible = ImGui.Begin("Debug [F5]", ref open, ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoSavedSettings);
        try
        {
            if (visible)
            {
                foreach (var section in panel.Sections)
                {
                    Ui.CollapsibleSection(section.Title, () => DrawControls(section), defaultOpen: true);
                }
            }
        }
        finally
        {
            ImGui.End();
        }

        IsOpen = open;
    }

    private void DrawControls(DebugSection section)
    {
        foreach (var control in section.Controls)
        {
            ImGui.PushID(control.Label);
            try
            {
                DrawControl(control);
            }
            finally
            {
                ImGui.PopID();
            }

            if (control.Help.Length > 0 && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(control.Help);
            }

            if (_notes.TryGetValue(control, out var note))
            {
                if (note.IsProblem)
                {
                    Ui.Problem(note.Text);
                }
                else
                {
                    Ui.Help(note.Text);
                }
            }
        }
    }

    private void DrawControl(DebugControl control)
    {
        switch (control)
        {
            case DebugToggle toggle:
                var isOn = toggle.Get();
                if (ImGui.Checkbox(toggle.Label, ref isOn))
                {
                    Apply(toggle, () => toggle.Set(isOn));
                }
                break;
            case DebugSlider slider:
                var value = slider.Get();
                if (Ui.Slider(slider.Label, ref value, slider.Min, slider.Max, logarithmic: slider.Logarithmic))
                {
                    Apply(slider, () => slider.Set(value));
                }
                break;
            case DebugChoice choice:
                if (Ui.Combo(choice.Label, choice.Get(), choice.Options) is { } chosen)
                {
                    Apply(choice, () => choice.Set(chosen));
                }
                break;
            case DebugButton button:
                Ui.Button(button.Label, () => Apply(button, () => _notes[button] = (button.Run(), false)));
                break;
            case DebugReadout readout:
                ImGui.TextDisabled(readout.Label);
                ImGui.TextWrapped(readout.Text());
                break;
        }
    }

    /// <summary>Runs a change; a failure is shown under the control instead of ending the game, as the console did.</summary>
    private void Apply(DebugControl control, Action change)
    {
        _notes.Remove(control);
        try
        {
            change();
        }
        catch (Exception exception)
        {
            _notes[control] = (exception.Message, true);
        }
    }
}
