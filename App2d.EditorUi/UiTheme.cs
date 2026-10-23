using ImGuiNET;
using System.Numerics;

namespace App2d.EditorUi;

/// <summary>Shared editor controls. Edit these normalized RGBA colors and restart the tool to change its appearance.</summary>
public static class UiTheme
{
    public static readonly Vector4 Accent = new(.42f, .84f, .75f, 1);
    public static readonly Vector4 Warning = new(.98f, .66f, .38f, 1);
    public static readonly Vector4 Override = new(.55f, .72f, 1, 1);

    public static readonly Vector4 Text = new(1, 1, 1, 1);
    public static readonly Vector4 TextDisabled = new(.5f, .5f, .5f, 1);
    public static readonly Vector4 WindowBackground = new(.075f, .091f, .115f, 1);
    public static readonly Vector4 ChildBackground = WindowBackground;
    public static readonly Vector4 Border = new(.43f, .43f, .50f, .50f);
    public static readonly Vector4 Frame = new(.13f, .16f, .20f, 1);
    public static readonly Vector4 FrameHovered = new(.26f, .59f, .98f, .40f);
    public static readonly Vector4 FrameActive = new(.26f, .59f, .98f, .67f);
    public static readonly Vector4 Header = new(.13f, .31f, .34f, 1);
    public static readonly Vector4 HeaderHovered = new(.17f, .40f, .42f, 1);
    public static readonly Vector4 HeaderActive = new(.26f, .59f, .98f, 1);
    public static readonly Vector4 Button = new(.16f, .27f, .31f, 1);
    public static readonly Vector4 ButtonHovered = new(.21f, .42f, .45f, 1);
    public static readonly Vector4 ButtonActive = new(.06f, .53f, .98f, 1);
    public static readonly Vector4 CheckMark = Accent;
    public static readonly Vector4 SliderGrab = Accent;
    public static readonly Vector4 SliderGrabActive = new(.26f, .59f, .98f, 1);

    internal static void Apply()
    {
        // Less common controls retain ImGui's dark defaults.
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.WindowRounding = 4;
        style.FrameRounding = 4;
        style.GrabRounding = 4;
        style.WindowPadding = new(12, 12);
        style.ItemSpacing = new(8, 8);
        style.FramePadding = new(7, 5);
        style.Colors[(int)ImGuiCol.Text] = Text;
        style.Colors[(int)ImGuiCol.TextDisabled] = TextDisabled;
        style.Colors[(int)ImGuiCol.WindowBg] = WindowBackground;
        style.Colors[(int)ImGuiCol.ChildBg] = ChildBackground;
        style.Colors[(int)ImGuiCol.Border] = Border;
        style.Colors[(int)ImGuiCol.FrameBg] = Frame;
        style.Colors[(int)ImGuiCol.FrameBgHovered] = FrameHovered;
        style.Colors[(int)ImGuiCol.FrameBgActive] = FrameActive;
        style.Colors[(int)ImGuiCol.Header] = Header;
        style.Colors[(int)ImGuiCol.HeaderHovered] = HeaderHovered;
        style.Colors[(int)ImGuiCol.HeaderActive] = HeaderActive;
        style.Colors[(int)ImGuiCol.Button] = Button;
        style.Colors[(int)ImGuiCol.ButtonHovered] = ButtonHovered;
        style.Colors[(int)ImGuiCol.ButtonActive] = ButtonActive;
        style.Colors[(int)ImGuiCol.CheckMark] = CheckMark;
        style.Colors[(int)ImGuiCol.SliderGrab] = SliderGrab;
        style.Colors[(int)ImGuiCol.SliderGrabActive] = SliderGrabActive;
    }
}
