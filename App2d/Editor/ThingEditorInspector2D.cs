using App2d.EditorUi;
using App2d.Levels;
using App2d.Things;
using ImGuiNET;

namespace App2d.Editor;

/// <summary>Thing inspector view and draft state. Persistence and undo remain in TileEditor2D.</summary>
internal sealed class ThingEditorInspector2D(TileEditor2D editor)
{
    private readonly TileEditor2D _editor = editor;
    private readonly PropertyInspector _inspector = new();
    private object? _properties;
    private string _selection = "Select or create a definition";
    private string _typeKey = ThingTypeRegistry2D.All[0].TypeKey;
    private long? _editingDefinitionId, _editingThingId;
    private string? _editingTypeKey, _error, _assetPath;
    private bool _isNewDefinition;

    public void Draw()
    {
        var deleteRequested = false;
        Ui.Section("Definitions", () =>
        {
            ImGui.TextUnformatted("Definition");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##definition", _isNewDefinition ? "New draft" : _editor.SelectedDefinition?.Name ?? "Choose a definition"))
            {
                foreach (var definition in _editor.ThingDefinitions)
                {
                    if (ImGui.Selectable($"{definition.Name} [{ThingTypeRegistry2D.Require(definition.TypeKey).DisplayName}]##{definition.DefinitionId}",
                        definition.DefinitionId == _editor.SelectedDefinitionId))
                    {
                        _editor.SelectDefinition(definition.DefinitionId);
                        ShowDefinitionCore(definition);
                    }
                }

                ImGui.EndCombo();
            }
            if (_isNewDefinition && Ui.Combo("New type", _typeKey, ThingTypeRegistry2D.All.Select(t => t.TypeKey),
                key => ThingTypeRegistry2D.Require(key).DisplayName) is { } type)
            {
                _typeKey = type;
                BeginNewDefinition();
            }
            if (_editingThingId is null && _editingTypeKey == ThingTypeRegistry2D.Object.TypeKey &&
                Ui.Combo("Asset", _assetPath ?? "Choose an asset", _editor.ObjectAssetPaths) is { } chosen)
            {
                _assetPath = chosen;
                if (_properties is PositionThingDefinitionProperties2D draft && (string.IsNullOrWhiteSpace(draft.Name) || draft.Name == ThingTypeRegistry2D.Object.DisplayName))
                {
                    draft.Name = Path.GetFileNameWithoutExtension(chosen);
                }
            }
            Ui.Row(
                () => Ui.Button("New definition", BeginNewDefinition),
                () => Ui.Button("Delete definition", DeleteDefinition, _editor.SelectedDefinitionId is not null));
        });
        Ui.Section("Placement", () => Ui.Row(
            () => Ui.Button(_editor.IsPlacingThing ? "Placing…" : "Place", _editor.BeginThingPlacement, _editor.SelectedDefinitionId is not null),
            () => Ui.Button("Delete thing", () => deleteRequested = true, _editor.HasSelectedThing),
            () => Ui.Button("Undo", _editor.UndoThingEdit, _editor.CanUndoThingEdit)));
        Ui.Help(_selection);
        Ui.Panel("properties", () => _inspector.Draw(_properties),
            size: new(0, MathF.Max(70 * Ui.Scale, ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing() - 28 * Ui.Scale)));
        Ui.Row(
            () => Ui.Button("Apply", Apply, _properties is not null),
            () => Ui.Button("Cancel", CancelEdit, _properties is not null));
        if (_error is { } error)
        {
            Ui.Problem(error);
        }

        if (deleteRequested)
        {
            ImGui.OpenPopup("delete-thing");
        }

        if (ImGui.BeginPopupModal("delete-thing", ImGuiWindowFlags.AlwaysAutoResize))
        {
            Ui.Help($"Delete {_editor.SelectedPositionThing?.Name ?? "this thing"}?");
            Ui.Row(
                () => Ui.Button("Delete", () =>
                {
                    _editor.DeleteSelectedThing();
                    ImGui.CloseCurrentPopup();
                }),
                () => Ui.Button("Keep", ImGui.CloseCurrentPopup));
            ImGui.EndPopup();
        }
    }

    public void RefreshFromEditor()
    {
        if (_editor.SelectedPositionThing is { } position)
        {
            ShowPositionThingCore(position);
        }
        else if (!_isNewDefinition && _editor.SelectedDefinition is { } definition)
        {
            ShowDefinitionCore(definition);
        }
        else if (!_isNewDefinition)
        {
            ClearProperties();
        }
    }
    public void ShowPositionThing(PositionThingRecord2D thing) => ShowPositionThingCore(thing);

    internal void BeginNewDefinition()
    {
        var descriptor = ThingTypeRegistry2D.Require(_typeKey);
        _isNewDefinition = true;
        _editingDefinitionId = null;
        _editingThingId = null;
        _editingTypeKey = _typeKey;
        _assetPath = null;
        _selection = $"New {descriptor.DisplayName} definition";
        _properties = new PositionThingDefinitionProperties2D { Name = descriptor.DisplayName };
    }

    private void ShowDefinitionCore(ThingDefinitionRecord2D definition)
    {
        _isNewDefinition = false;
        _editingDefinitionId = definition.DefinitionId;
        _editingThingId = null;
        _editingTypeKey = definition.TypeKey;
        _ = ThingTypeRegistry2D.Require(definition.TypeKey);
        _selection = $"Definition: {definition.Name}";
        _assetPath = definition.Asset;
        _properties = PositionThingDefinitionProperties2D.From(definition);
    }

    private void ShowPositionThingCore(PositionThingRecord2D thing)
    {
        _isNewDefinition = false;
        _editingDefinitionId = null;
        _editingThingId = thing.ThingId;
        _editingTypeKey = thing.TypeKey;
        _selection = $"Thing {thing.ThingId}: {thing.Name ?? thing.DefinitionName}";
        _properties = PositionThingInstanceProperties2D.From(thing);
    }

    private void Apply()
    {
        if (!_inspector.Commit(_properties))
        {
            return;
        }

        try
        {
            switch (_properties)
            {
                case PositionThingDefinitionProperties2D definition when _editingTypeKey is { } type:
                    if (type == ThingTypeRegistry2D.Object.TypeKey && _assetPath is null)
                    {
                        _error = "Choose an asset file.";
                        return;
                    }
                    _editor.ApplyPositionDefinition(_isNewDefinition ? null : _editingDefinitionId, type, definition, _assetPath);
                    break;
                case PositionThingInstanceProperties2D thing when _editingThingId is { } id:
                    _editor.ApplyPositionThing(id, thing);
                    break;
            }
            _isNewDefinition = false;
            _error = null;
            RefreshFromEditor();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _error = exception.Message;
        }
    }
    private void CancelEdit()
    {
        _isNewDefinition = false;
        _error = null;
        RefreshFromEditor();
    }
    private void DeleteDefinition()
    {
        if (_editor.SelectedDefinitionId is not { } id)
        {
            return;
        }

        try
        {
            _editor.DeleteDefinition(id);
            _error = null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            _error = "This definition is still used by a placed thing.";
        }
    }
    private void ClearProperties()
    {
        _editingDefinitionId = null;
        _editingThingId = null;
        _editingTypeKey = null;
        _selection = "Select or create a definition";
        _properties = null;
    }
}
