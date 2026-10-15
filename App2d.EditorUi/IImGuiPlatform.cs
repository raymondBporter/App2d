using ImGuiNET;

namespace App2d.EditorUi;

/// <summary>Window input and DPI for an ImGui host. Input is submitted with the host's context current.</summary>
public interface IImGuiPlatform : IDisposable
{
    float DpiScale
    {
        get;
    }
    void UpdateInput(ImGuiIOPtr io);
}
