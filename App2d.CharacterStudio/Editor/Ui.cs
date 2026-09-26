using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>Small shared widgets. Labels sit above full-width controls so the narrow side panels stay readable.</summary>
internal static class Ui
{
    public static readonly Vector4 Accent = new(.42f, .84f, .75f, 1), Warning = new(.98f, .66f, .38f, 1), Override = new(.55f, .72f, 1, 1);
    public static float Scale { get; set; } = 1;

    public static uint Color(byte r, byte g, byte b, byte a = 255) => (uint)(a << 24 | b << 16 | g << 8 | r);

    public static void Header(string text) { ImGui.Spacing(); ImGui.TextColored(Accent, text.ToUpperInvariant()); }
    public static void Help(string text) { ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]); ImGui.TextWrapped(text); ImGui.PopStyleColor(); }
    public static void Problem(string text) { ImGui.PushStyleColor(ImGuiCol.Text, Warning); ImGui.TextWrapped(text); ImGui.PopStyleColor(); }

    private static void Label(string label) { ImGui.TextUnformatted(label); ImGui.SetNextItemWidth(-1); }

    public static bool Slider(string label, ref float value, float min, float max, string format = "%.3f")
    { Label(label); return ImGui.SliderFloat("##" + label, ref value, min, max, format, ImGuiSliderFlags.AlwaysClamp); }

    public static bool Drag(string label, ref float value, float speed = .005f, float min = -100, float max = 100, string format = "%.3f")
    { Label(label); return ImGui.DragFloat("##" + label, ref value, speed, min, max, format); }

    public static bool Drag2(string label, ref Vector2 value, float speed = .005f, float min = -100, float max = 100)
    { Label(label); return ImGui.DragFloat2("##" + label, ref value, speed, min, max, "%.3f"); }

    public static bool Drag3(string label, ref Vector3 value, float speed = .005f, float min = -100, float max = 100)
    { Label(label); return ImGui.DragFloat3("##" + label, ref value, speed, min, max, "%.3f"); }

    public static bool Text(string label, ref string value, uint length = 80)
    { Label(label); return ImGui.InputText("##" + label, ref value, length); }

    public static bool ColorHex(string label, ref string hex)
    {
        var value = new Vector3(Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16)) / 255;
        if (!ImGui.ColorEdit3(label, ref value, ImGuiColorEditFlags.NoInputs)) return false;
        hex = $"#{(int)MathF.Round(value.X * 255):x2}{(int)MathF.Round(value.Y * 255):x2}{(int)MathF.Round(value.Z * 255):x2}"; return true;
    }

    /// <summary>A full-width combo; returns the chosen option when it changes.</summary>
    public static string? Combo(string label, string current, IEnumerable<string> options, Func<string, string>? display = null)
    {
        Label(label); string? chosen = null;
        if (ImGui.BeginCombo("##" + label, display?.Invoke(current) ?? current))
        {
            foreach (var option in options) if (ImGui.Selectable(display?.Invoke(option) ?? option, option == current) && option != current) chosen = option;
            ImGui.EndCombo();
        }
        return chosen;
    }

    /// <summary>
    /// Marks a variant field that overrides its base. Draws the marker and a reset button on the current line; returns true
    /// when the author asked to reset to base.
    /// </summary>
    public static bool OverrideMarker(bool overridden, string id)
    {
        if (!overridden) return false;
        ImGui.SameLine(); ImGui.TextColored(Override, "overridden"); ImGui.SameLine();
        return ImGui.SmallButton("Reset to base##" + id);
    }

    /// <summary>A label line that shows an override marker and reset, followed by the widget drawn by the caller.</summary>
    public static bool OverrideLabel(string label, bool overridden)
    {
        ImGui.TextUnformatted(label);
        return OverrideMarker(overridden, label);
    }

    public static bool Button(string label, bool enabled = true)
    {
        ImGui.BeginDisabled(!enabled); var pressed = ImGui.Button(label); ImGui.EndDisabled(); return pressed;
    }
}
