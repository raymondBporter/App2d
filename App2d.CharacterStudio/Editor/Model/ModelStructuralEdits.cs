using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Confirms structural changes when other assets depend on the model being edited.</summary>
internal sealed class ModelStructuralEdits(EditorSession session)
{
    private (AssetDocument<CharacterModel> Document, string Description, Action Apply)? _structural;
    private bool _confirmOpen;

    public void Request(AssetDocument<CharacterModel> model, string description, Action apply)
    {
        var (variants, clips) = session.Assets.Dependents(model.Id);
        if (variants.Count + clips.Count == 0)
        {
            session.Edit(model, apply);
            return;
        }
        _structural = (model, description, apply);
        _confirmOpen = true;
    }

    public void Draw()
    {
        if (_structural is not { } pending)
        {
            return;
        }

        var model = pending.Document;
        if (_confirmOpen)
        {
            ImGui.OpenPopup("Structural edit");
            _confirmOpen = false;
        }
        if (!ImGui.BeginPopupModal("Structural edit", ImGuiWindowFlags.AlwaysAutoResize))
        {
            _structural = null;
            return;
        }
        var (variants, clips) = session.Assets.Dependents(model.Id);
        ImGui.TextUnformatted($"This will {pending.Description} on '{model.Name}', changing its structure.");
        ImGui.TextUnformatted($"Dependents: {variants.Count} variant(s), {clips.Count} animation(s).");
        foreach (var clip in clips)
        {
            ImGui.BulletText(clip.Name + " (" + clip.Id + ")");
        }

        foreach (var variant in variants)
        {
            ImGui.BulletText(variant.Name + " (" + variant.Id + ")");
        }

        Ui.Help("Animations that stay compatible move to the new structure revision when the model is saved. Any that no longer fit are reported for repair; nothing is deleted. The edit is undoable.");
        if (ImGui.Button("Apply"))
        {
            session.Edit(model, pending.Apply);
            _structural = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            _structural = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
}
