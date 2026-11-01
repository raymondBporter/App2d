using App2d.Core.Rendering;
using ImGuiNET;
using System.Numerics;

namespace App2d.EditorUi;

/// <summary>Small shared widgets. Labels sit above full-width controls so the narrow side panels stay readable.</summary>
public static class Ui
{
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

    /// <summary>The thickness of a drag handle between panels: the same gap the layout leaves between them.</summary>
    public static float SplitterThickness => 8 * Scale;

    /// <summary>
    /// A vertical drag handle flush between the previous panel and the next one on the same line. Dragging it moves their shared
    /// edge: <paramref name="width"/> follows the mouse rightward, or leftward when the sized panel is the one on the right.
    /// Returns true while the author holds it. Zero <paramref name="height"/> spans the available height.
    /// </summary>
    public static bool ColumnSplitter(string id, ref float width, float height, float min, float max, bool panelOnRight = false)
    {
        ImGui.SameLine(0, 0);
        var dragging = Splitter(id, ref width, new(SplitterThickness, height > 0 ? height : ImGui.GetContentRegionAvail().Y), min, max, panelOnRight ? -1 : 1, vertical: true);
        ImGui.SameLine(0, 0);
        return dragging;
    }

    /// <summary>
    /// A horizontal drag handle flush under the previous panel, with the next panel flush under it. Dragging it moves their shared
    /// edge: <paramref name="height"/> follows the mouse downward, or upward when the sized panel is the one below.
    /// Returns true while the author holds it. Zero <paramref name="width"/> spans the available width.
    /// </summary>
    public static bool RowSplitter(string id, ref float height, float width, float min, float max, bool panelBelow = false)
    {
        // Item spacing would open a gap above and below the handle; it takes the gap's place instead.
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - spacing);
        var dragging = Splitter(id, ref height, new(width > 0 ? width : ImGui.GetContentRegionAvail().X, SplitterThickness), min, max, panelBelow ? -1 : 1, vertical: false);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - spacing);
        return dragging;
    }

    // The size a drag started from. One handle is held at a time, so one value serves every splitter.
    private static float _splitterStart;

    private static bool Splitter(string id, ref float size, Vector2 extent, float min, float max, float direction, bool vertical)
    {
        ImGui.InvisibleButton(id, Vector2.Max(extent, Vector2.One));
        var active = ImGui.IsItemActive();
        if (active || ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(vertical ? ImGuiMouseCursor.ResizeEW : ImGuiMouseCursor.ResizeNS);
            var lo = ImGui.GetItemRectMin();
            var hi = ImGui.GetItemRectMax();
            var center = (lo + hi) / 2;
            var a = vertical ? new Vector2(center.X - Scale, lo.Y) : new Vector2(lo.X, center.Y - Scale);
            var b = vertical ? new Vector2(center.X + Scale, hi.Y) : new Vector2(hi.X, center.Y + Scale);
            ImGui.GetWindowDrawList().AddRectFilled(a, b, ImGui.GetColorU32(active ? ImGuiCol.SeparatorActive : ImGuiCol.SeparatorHovered));
        }

        if (!active)
        {
            return false;
        }

        if (ImGui.IsItemActivated())
        {
            _splitterStart = size;
        }

        // Measured from where the drag began, so a handle held past its limit follows the mouse straight back.
        var travel = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0);
        size = Math.Clamp(_splitterStart + direction * (vertical ? travel.X : travel.Y), min, MathF.Max(min, max));
        return true;
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
        ImGui.TextColored(UiTheme.Accent, text.ToUpperInvariant());
    }
    public static void Help(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }
    public static void Problem(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Warning);
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
        var color = ColorExtensions.FromHexRgb(hex);
        var value = new Vector3(color.R, color.G, color.B) / 255;
        if (!ImGui.ColorEdit3(label, ref value, ImGuiColorEditFlags.NoInputs))
        {
            return false;
        }

        hex = ColorExtensions.FromVector4(new(value, 1)).ToHexRgb();
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

    /// <summary>A combo over an explicit subset of enum values.</summary>
    public static T? EnumCombo<T>(string label, T current, IEnumerable<T> options) where T : struct, Enum
    {
        Label(label);
        T? chosen = null;
        if (ImGui.BeginCombo("##" + label, current.ToString()))
        {
            foreach (var option in options)
            {
                var selected = EqualityComparer<T>.Default.Equals(option, current);
                if (ImGui.Selectable(option.ToString(), selected) && !selected)
                    chosen = option;
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
        ImGui.TextColored(UiTheme.Override, "overridden");
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

