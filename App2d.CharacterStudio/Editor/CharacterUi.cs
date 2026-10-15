using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor;

/// <summary>Character-specific inspector fields, composed from shared editor widgets.</summary>
internal static class CharacterUi
{
    /// <summary>Edits character drawing order without presenting it as a third position axis.</summary>
    public static bool CharacterLayer(string label, ref CharacterLayer2D layer, float min = -32, float max = 32, bool offset = false)
    {
        var changed = false;
        var preset = layer == CharacterLayer2D.Front ? "Front" : layer == CharacterLayer2D.Middle ? "Middle" :
            layer == CharacterLayer2D.Back ? "Back" : "Custom";
        if (!offset && Ui.Combo(label + " preset", preset, ["Front", "Middle", "Back"]) is { } selected)
        {
            layer = selected switch
            {
                "Front" => CharacterLayer2D.Front,
                "Middle" => CharacterLayer2D.Middle,
                _ => CharacterLayer2D.Back
            };
            changed = true;
        }
        var order = layer.Order;
        if (Ui.Drag(label + (offset ? " shift (- front, + back)" : " order (- front, + back)"), ref order, .002f, min, max))
        {
            layer = new(order);
            changed = true;
        }
        return changed;
    }

    public static bool LayeredPoint(string label, ref LayeredPoint2D point, bool layerIsOffset = false)
    {
        var xy = point.Position;
        var moved = Ui.Drag2(label + " X / Y", ref xy);
        var layer = point.Layer;
        var reordered = CharacterLayer(label + " layer", ref layer, offset: layerIsOffset);
        if (moved || reordered) point = new(xy, layer);
        return moved || reordered;
    }

}

/// <summary>Dependency navigation: what an asset uses and what uses it, each a link that opens the other asset.</summary>
internal static class References
{
    public static void Draw(EditorSession session, string id)
    {
        var uses = session.Assets.Uses(id); var usedBy = session.Assets.UsedBy(id);
        if (uses.Count + usedBy.Count == 0) return;
        Ui.Header("References");
        void Rows(string title, IReadOnlyList<AssetReference> references)
        {
            if (references.Count == 0) return;
            if (!ImGui.TreeNodeEx($"{title} ({references.Count})##{title}", ImGuiTreeNodeFlags.DefaultOpen)) return;
            foreach (var group in references.GroupBy(r => r.Id))
            {
                var document = session.Assets.Find(group.Key);
                var label = document is null ? $"{group.Key} (missing)" : $"[{AssetKinds.Label(document.Kind)[0]}] {document.Name}";
                if (document is null) ImGui.PushStyleColor(ImGuiCol.Text, Ui.Warning);
                if (ImGui.Selectable($"{label}##{title}{group.Key}") && document is not null && document.Kind != AssetKind.Prop) session.Open(group.Key);
                if (document is null) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(string.Join("\n", group.Select(r => r.Relation)));
            }
            ImGui.TreePop();
        }
        Rows("Uses", uses); Rows("Used by", usedBy);
    }
}
