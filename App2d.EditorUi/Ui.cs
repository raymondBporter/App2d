using App2d.Core.Rendering;
using ImGuiNET;
using System.Numerics;

namespace App2d.EditorUi;

/// <summary>Small shared widgets. Labels sit above full-width controls so the narrow side panels stay readable.</summary>
public static class Ui
{
    public static readonly Vector4 Accent = new(.42f, .84f, .75f, 1), Warning = new(.98f, .66f, .38f, 1), Override = new(.55f, .72f, 1, 1);
    public static float Scale { get; internal set; } = 1;

    public static uint Color(byte r, byte g, byte b, byte a = 255) => (uint)(a << 24 | b << 16 | g << 8 | r);

    /// <summary>A window filled by the supplied view. Begin/End are balanced even when hidden or the view throws.</summary>
    public static void Window(string id, Action content, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        var visible = ImGui.Begin(id, flags);
        try
        {
            if (visible)
            {
                content();
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    /// <summary>A fixed root window covering the host's drawing surface.</summary>
    public static void Root(string id, Action content)
    {
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        Window(id, content, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
    }

    /// <summary>A fixed right-hand tool window. The world stays visible and interactive beside it.</summary>
    public static float Sidebar(string id, Action content, float width = 340)
    {
        var display = ImGui.GetIO().DisplaySize;
        var pixels = MathF.Min(width * Scale, display.X * .45f);
        ImGui.SetNextWindowPos(new(display.X - pixels, 0));
        ImGui.SetNextWindowSize(new(pixels, display.Y));
        Window(id, content, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
        return pixels;
    }

    /// <summary>A child panel, optionally with a header. Zero size fills the available space; size is in device pixels.</summary>
    public static void Panel(string id, Action content, Action? header = null, Vector2 size = default, bool border = true, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        var visible = ImGui.BeginChild(id, size, border ? ImGuiChildFlags.Borders : ImGuiChildFlags.None, flags);
        try
        {
            if (!visible)
            {
                return;
            }

            if (header is not null)
            {
                header();
                ImGui.Separator();
            }
            content();
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    /// <summary>Places each child callback beside the previous one. A callback may draw a widget or a whole panel.</summary>
    public static void Row(params Action[] children)
    {
        ImGui.BeginGroup();
        try
        {
            for (var i = 0; i < children.Length; i++)
            {
                if (i > 0)
                {
                    ImGui.SameLine();
                }

                children[i]();
            }
        }
        finally
        {
            ImGui.EndGroup();
        }
    }

    /// <summary>A titled vertical group. Its ID scopes repeated field labels independently from other sections.</summary>
    public static void Section(string title, Action content, string? id = null)
    {
        ImGui.PushID(id ?? title);
        try
        {
            Header(title);
            content();
        }
        finally
        {
            ImGui.PopID();
        }
    }

    public static void CollapsibleSection(string title, Action content, bool defaultOpen = false)
    {
        ImGui.PushID(title);
        try
        {
            if (ImGui.CollapsingHeader(title, defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
            {
                content();
            }
        }
        finally
        {
            ImGui.PopID();
        }
    }

    public static void Header(string text)
    {
        ImGui.Spacing();
        ImGui.TextColored(Accent, text.ToUpperInvariant());
    }
    public static void Help(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }
    public static void Problem(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Warning);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private static void Label(string label)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
    }

    public static bool Slider(string label, ref float value, float min, float max, string format = "%.3f", bool logarithmic = false)
    {
        Label(label);
        return ImGui.SliderFloat("##" + label, ref value, min, max, format, ImGuiSliderFlags.AlwaysClamp | (logarithmic ? ImGuiSliderFlags.Logarithmic : ImGuiSliderFlags.None));
    }

    public static bool Drag(string label, ref float value, float speed = .005f, float min = -100, float max = 100, string format = "%.3f")
    {
        Label(label);
        return ImGui.DragFloat("##" + label, ref value, speed, min, max, format);
    }

    public static bool Drag2(string label, ref Vector2 value, float speed = .005f, float min = -100, float max = 100)
    {
        Label(label);
        return ImGui.DragFloat2("##" + label, ref value, speed, min, max, "%.3f");
    }

    public static bool Drag3(string label, ref Vector3 value, float speed = .005f, float min = -100, float max = 100)
    {
        Label(label);
        return ImGui.DragFloat3("##" + label, ref value, speed, min, max, "%.3f");
    }

    public static bool Text(string label, ref string value, uint length = 80)
    {
        Label(label);
        return ImGui.InputText("##" + label, ref value, length);
    }

    public static bool ColorHex(string label, ref string hex)
    {
        var color = ColorRgba32.ParseRgb(hex);
        var value = new Vector3(color.R, color.G, color.B) / 255;
        if (!ImGui.ColorEdit3(label, ref value, ImGuiColorEditFlags.NoInputs))
        {
            return false;
        }

        hex = ColorRgba32.FromVector4(new(value, 1)).ToHexRgb();
        return true;
    }

    /// <summary>A full-width combo; returns the chosen option when it changes.</summary>
    public static string? Combo(string label, string current, IEnumerable<string> options, Func<string, string>? display = null)
    {
        Label(label);
        string? chosen = null;
        if (ImGui.BeginCombo("##" + label, display?.Invoke(current) ?? current))
        {
            foreach (var option in options)
            {
                if (ImGui.Selectable(display?.Invoke(option) ?? option, option == current) && option != current)
                {
                    chosen = option;
                }
            }

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
        if (!overridden)
        {
            return false;
        }

        ImGui.SameLine();
        ImGui.TextColored(Override, "overridden");
        ImGui.SameLine();
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
        ImGui.BeginDisabled(!enabled);
        var pressed = ImGui.Button(label);
        ImGui.EndDisabled();
        return pressed;
    }

    public static void Button(string label, Action onClick, bool enabled = true)
    {
        if (Button(label, enabled))
        {
            onClick();
        }
    }
}

