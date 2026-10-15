using App2d.Noodle.Rigging;
using System.Numerics;

namespace App2d.Noodle;

internal sealed class BoneEditorPanel
{
    private readonly RigDocument2D _document;
    private readonly PropertyInspector _properties = new();
    private RigBone2D? _attachmentBone;
    private string? _error;

    public BoneEditorPanel(RigDocument2D document)
    {
        _document = document;
        Select(document.Bones.FirstOrDefault());
    }
    public object? SelectedItem
    {
        get; private set;
    }
    public void Select(object? item)
    {
        SelectedItem = item;
        _attachmentBone = GetSelectedBone();
        _properties.Reset();
        _error = null;
    }
    public bool Commit() => _properties.Commit(SelectedItem);

    public void Draw(Action export)
    {
        Ui.Header("Entity editor / bones");
        Ui.CollapsibleSection("Create bones and shapes", () =>
        {
            Ui.Row(() => Ui.Button("+ Root", () => AddBone(null)), () => Ui.Button("+ Child", () => AddBone(GetSelectedBone())));
            Ui.Row(() => Ui.Button("+ Rectangle", () => AddShape("Rectangle")), () => Ui.Button("+ Circle", () => AddShape("Circle")));
            Ui.Row(() => Ui.Button("+ Capsule", () => AddShape("Capsule")), () => Ui.Button("+ Polygon", () => AddShape("Polygon")));
        });
        Ui.CollapsibleSection("Hierarchy", () => Ui.Panel("hierarchy", () =>
        {
            ImGui.TextUnformatted("BONES");
            foreach (var bone in _document.Bones.Where(b => b.Parent is null))
            {
                DrawBone(bone);
            }

            ImGui.Separator();
            ImGui.TextUnformatted("SHAPES");
            foreach (var shape in _document.Shapes)
            {
                ImGui.PushID($"shape-{shape.Id}");
                if (ImGui.Selectable($"{shape.Name} [{shape.Kind}]", ReferenceEquals(SelectedItem, shape)))
                {
                    Select(shape);
                }

                ImGui.PopID();
            }
        }, size: new Vector2(0, MathF.Min(150 * Ui.Scale, ImGui.GetIO().DisplaySize.Y * .22f)), border: true), defaultOpen: true);
        if (SelectedItem is RigShape2D)
        {
            Ui.Section("Attachment", () =>
        {
            // Names may be edited or duplicated; IDs identify the actual destination bone.
            ImGui.TextUnformatted("Bone");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##bone", _attachmentBone?.Name ?? "Choose bone"))
            {
                foreach (var bone in _document.Bones)
                {
                    ImGui.PushID(bone.Id);
                    if (ImGui.Selectable(bone.Name, bone == _attachmentBone))
                    {
                        _attachmentBone = bone;
                    }

                    ImGui.PopID();
                }
                ImGui.EndCombo();
            }
            Ui.Button("Attach shape", AttachSelectedShape);
            ImGui.SameLine();
            Ui.Button("Delete", DeleteSelected);
        });
        }
        else
        {
            Ui.Button("Delete bone", DeleteSelected, SelectedItem is RigBone2D);
        }

        Ui.Panel("properties", () =>
        {
            if (_properties.Draw(SelectedItem))
            {
                NormalizeName();
            }
        },
            size: new(0, MathF.Max(70 * Ui.Scale, ImGui.GetContentRegionAvail().Y - 115 * Ui.Scale)));
        if (_error is { } error)
        {
            Ui.Problem(error);
        }

        Ui.Section("Export", () =>
        {
            Ui.Button("Export model (Ctrl+S)", export);
            Ui.Help("Middle drag pans. Wheel zooms. Select a bone or shape to edit its properties.");
        });
    }

    private void DrawBone(RigBone2D bone)
    {
        ImGui.PushID(bone.Id);
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (ReferenceEquals(SelectedItem, bone))
        {
            flags |= ImGuiTreeNodeFlags.Selected;
        }

        var open = ImGui.TreeNodeEx("bone", flags, $"{bone.Name} ({bone.Length:0.#})");
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            Select(bone);
        }

        if (open)
        {
            foreach (var child in _document.Bones.Where(b => b.Parent == bone))
            {
                DrawBone(child);
            }

            ImGui.TreePop();
        }
        ImGui.PopID();
    }
    private RigBone2D? GetSelectedBone() => SelectedItem switch { RigBone2D bone => bone, RigShape2D shape => shape.AttachedBone, _ => null };
    private void AddBone(RigBone2D? parent) => Select(_document.AddBone(parent));
    private void AddShape(string kind) => Select(_document.AddShape(kind, GetSelectedBone() ?? _document.Bones.FirstOrDefault() ?? _document.AddBone(name: "root")));
    private void AttachSelectedShape()
    {
        if (SelectedItem is RigShape2D shape && _attachmentBone is { } bone)
        {
            shape.AttachedBone = bone;
        }
    }
    private void DeleteSelected()
    {
        if (SelectedItem is null)
        {
            return;
        }

        try
        {
            if (_document.Remove(SelectedItem))
            {
                Select(_document.Bones.FirstOrDefault());
            }
        }
        catch (InvalidOperationException exception)
        {
            _error = exception.Message;
        }
    }
    private void NormalizeName()
    {
        if (SelectedItem is RigBone2D bone && string.IsNullOrWhiteSpace(bone.Name))
        {
            bone.Name = $"bone-{bone.Id}";
        }

        if (SelectedItem is RigShape2D shape && string.IsNullOrWhiteSpace(shape.Name))
        {
            shape.Name = $"{shape.Kind.ToLowerInvariant()}-{shape.Id}";
        }
    }
}
