using App2d.Core.Characters;
using ImGuiNET;

namespace App2d.CharacterStudio;

internal sealed partial class StudioGame
{
    private string _facePreviewMode = "Saved expression";
    private double _faceClock;
    private bool _pauseFaces;

    private void FacePreviewControls()
    {
        if (_document.Library.Anatomy != "person") return;
        ImGui.SeparatorText("Face animation");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##Face preview", _facePreviewMode))
        {
            foreach (var mode in new[] { "Saved expression", "Expression tour", "Hit reaction", "Long fall", "Victory" })
                if (ImGui.Selectable(mode, mode == _facePreviewMode)) { _facePreviewMode = mode; _faceClock = 0; }
            ImGui.EndCombo();
        }
        ImGui.Checkbox("Pause face", ref _pauseFaces);
        ImGui.SameLine(); if (ImGui.SmallButton("Replay face")) _faceClock = 0;
        ImGui.TextWrapped("Preview: " + PreviewExpression().Current);
        ImGui.TextDisabled("Face playback is independent of the body.");
    }

    private (string Previous, string Current, double Age) PreviewExpression()
    {
        if (_facePreviewMode == "Expression tour")
        {
            var index = (int)(_faceClock / 1.4) % FaceExpressions.Names.Count;
            return (FaceExpressions.Names[(index + FaceExpressions.Names.Count - 1) % FaceExpressions.Names.Count], FaceExpressions.Names[index], _faceClock % 1.4);
        }
        var time = _faceClock % 4;
        (double Start, string Name)[]? sequence = _facePreviewMode switch
        {
            "Hit reaction" => [(0, "relaxed"), (.7, "hurt"), (1.08, "angry"), (2, "relaxed")],
            "Long fall" => [(0, "focused"), (.7, "surprised"), (1.25, "panic"), (2.6, "strained"), (2.9, "happy")],
            "Victory" => [(0, "focused"), (.7, "surprised"), (1, "delighted"), (2.5, "smug")],
            _ => null
        };
        if (sequence is not null)
        {
            var index = Array.FindLastIndex(sequence, step => time >= step.Start);
            return (sequence[Math.Max(0, index - 1)].Name, sequence[index].Name, time - sequence[index].Start);
        }
        var name = _document.Appearance.CustomHead?.Face ?? _document.Appearance.Face;
        return (name, name, 1);
    }

    private FacePose? PreviewFace()
    {
        if (_document.Library.Anatomy != "person") return null;
        var (previous, current, age) = PreviewExpression();
        if (!FaceExpressions.Contains(current)) return null;
        var pose = FacePose.Blend(FaceExpressions.Get(previous), FaceExpressions.Get(current), (float)Math.Clamp(age / .12, 0, 1));
        return current is "hurt" or "panic" or "knocked-out" or "blink" ? pose : FaceExpressions.Blink(pose, _faceClock);
    }
}
