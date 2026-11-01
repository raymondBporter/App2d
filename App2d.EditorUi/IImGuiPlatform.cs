using ImGuiNET;

namespace App2d.EditorUi;

/// <summary>Window input, DPI and the mouse cursor for an ImGui host. Input is submitted with the host's context current.</summary>
public interface IImGuiPlatform : IDisposable
{
    float DpiScale { get; }
    void UpdateInput(ImGuiIOPtr io);
    /// <summary>Shows the cursor the frame asked for, such as a resize arrow over a drag handle. Called once per rendered frame.</summary>
    void SetCursor(ImGuiMouseCursor cursor);
}
