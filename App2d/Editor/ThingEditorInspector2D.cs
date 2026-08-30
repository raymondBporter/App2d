using App2d.Levels;
using System.Drawing;

namespace App2d.Editor;

internal sealed class ThingEditorInspector2D : UserControl
{
    private readonly TileEditor2D _editor;
    private readonly ListBox _definitions = new();
    private readonly PropertyGrid _properties = new();
    private readonly Label _selection = new();
    private readonly Button _place = new();
    private readonly Button _deleteThing = new();
    private readonly Button _undo = new();
    private readonly Button _apply = new();
    private readonly Button _cancel = new();
    private bool _refreshing;
    private long? _editingDefinitionId;
    private long? _editingThingId;
    private bool _isNewDefinition;

    public ThingEditorInspector2D(TileEditor2D editor)
    {
        _editor = editor;
        Visible = false;
        BackColor = Color.FromArgb(17, 24, 37);
        ForeColor = Color.FromArgb(244, 247, 252);
        Padding = new Padding(12);

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Text = "THINGS  /  MOVING PLATFORM",
            ForeColor = ForeColor,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var back = MakeButton("Tiles");
        back.Dock = DockStyle.Top;
        back.Click += (_, _) => _editor.SelectMode(LevelEditorMode2D.Tiles);

        _definitions.Dock = DockStyle.Top;
        _definitions.Height = 118;
        _definitions.DisplayMember = nameof(DefinitionItem.Label);
        _definitions.SelectedIndexChanged += OnDefinitionSelected;

        var definitionButtons = MakeRow();
        var createDefinition = MakeButton("New definition");
        var deleteDefinition = MakeButton("Delete definition");
        createDefinition.Click += (_, _) => BeginNewDefinition();
        deleteDefinition.Click += (_, _) => DeleteDefinition();
        definitionButtons.Controls.Add(createDefinition);
        definitionButtons.Controls.Add(deleteDefinition);

        var placementButtons = MakeRow();
        _place.Text = "Place";
        StyleButton(_place);
        _deleteThing.Text = "Delete thing";
        StyleButton(_deleteThing);
        _undo.Text = "Undo";
        StyleButton(_undo);
        _place.Click += (_, _) => _editor.BeginThingPlacement();
        _deleteThing.Click += (_, _) => DeleteThing();
        _undo.Click += (_, _) => _editor.UndoThingEdit();
        placementButtons.Controls.Add(_place);
        placementButtons.Controls.Add(_deleteThing);
        placementButtons.Controls.Add(_undo);

        _selection.Dock = DockStyle.Top;
        _selection.Height = 32;
        _selection.ForeColor = Color.FromArgb(160, 174, 196);
        _selection.TextAlign = ContentAlignment.MiddleLeft;

        _properties.Dock = DockStyle.Fill;
        _properties.HelpVisible = true;
        _properties.ToolbarVisible = false;
        _properties.PropertySort = PropertySort.Categorized;

        var commitButtons = MakeRow();
        _apply.Text = "Apply";
        StyleButton(_apply);
        _cancel.Text = "Cancel";
        StyleButton(_cancel);
        _apply.Click += (_, _) => Apply();
        _cancel.Click += (_, _) => CancelEdit();
        commitButtons.Controls.Add(_apply);
        commitButtons.Controls.Add(_cancel);

        Controls.Add(_properties);
        Controls.Add(commitButtons);
        Controls.Add(_selection);
        Controls.Add(placementButtons);
        Controls.Add(_definitions);
        Controls.Add(definitionButtons);
        Controls.Add(back);
        Controls.Add(header);
        UpdateButtons();
    }

    public void RefreshFromEditor()
    {
        _refreshing = true;
        try
        {
            var selectedDefinitionId = _editor.SelectedDefinitionId;
            _definitions.Items.Clear();
            foreach (var definition in _editor.MovingPlatformDefinitions)
            {
                var item = new DefinitionItem(definition);
                _definitions.Items.Add(item);
                if (definition.DefinitionId == selectedDefinitionId)
                    _definitions.SelectedItem = item;
            }

            if (_editor.SelectedThing is { } thing)
                ShowThingCore(thing);
            else if (!_isNewDefinition && selectedDefinitionId is { } definitionId)
                ShowDefinitionCore(_editor.MovingPlatformDefinitions.Single(item => item.DefinitionId == definitionId));
            else if (!_isNewDefinition)
                ClearProperties();
        }
        finally
        {
            _refreshing = false;
            UpdateButtons();
        }
    }

    public void ShowThing(MovingPlatformThingRecord2D thing)
    {
        _refreshing = true;
        try
        {
            ShowThingCore(thing);
        }
        finally
        {
            _refreshing = false;
            UpdateButtons();
        }
    }

    private void BeginNewDefinition()
    {
        _isNewDefinition = true;
        _editingDefinitionId = null;
        _editingThingId = null;
        _selection.Text = "New moving-platform definition";
        _properties.SelectedObject = new MovingPlatformDefinitionProperties2D();
        UpdateButtons();
    }

    private void OnDefinitionSelected(object? sender, EventArgs e)
    {
        if (_refreshing || _definitions.SelectedItem is not DefinitionItem item)
            return;
        _isNewDefinition = false;
        _editor.SelectDefinition(item.Record.DefinitionId);
        ShowDefinitionCore(item.Record);
        UpdateButtons();
    }

    private void ShowDefinitionCore(MovingPlatformDefinitionRecord2D definition)
    {
        _isNewDefinition = false;
        _editingDefinitionId = definition.DefinitionId;
        _editingThingId = null;
        _selection.Text = $"Definition: {definition.Name}";
        _properties.SelectedObject = MovingPlatformDefinitionProperties2D.From(definition);
    }

    private void ShowThingCore(MovingPlatformThingRecord2D thing)
    {
        _isNewDefinition = false;
        _editingDefinitionId = null;
        _editingThingId = thing.ThingId;
        _selection.Text = $"Thing {thing.ThingId}: {thing.Name ?? thing.DefinitionName}";
        _properties.SelectedObject = MovingPlatformInstanceProperties2D.From(thing);
    }

    private void Apply()
    {
        try
        {
            _properties.Refresh();
            switch (_properties.SelectedObject)
            {
                case MovingPlatformDefinitionProperties2D definition:
                    var definitionId = _isNewDefinition ? null : _editingDefinitionId;
                    _isNewDefinition = false;
                    _editor.ApplyDefinition(definitionId, definition);
                    break;
                case MovingPlatformInstanceProperties2D thing when _editingThingId is { } thingId:
                    _editor.ApplyThing(thingId, thing);
                    break;
            }
            RefreshFromEditor();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Cannot apply properties", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CancelEdit()
    {
        _isNewDefinition = false;
        RefreshFromEditor();
    }

    private void DeleteDefinition()
    {
        if (_editor.SelectedDefinitionId is not { } definitionId)
            return;
        try
        {
            _editor.DeleteDefinition(definitionId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show(this, "This definition is still used by a placed thing.", "Cannot delete definition", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void DeleteThing()
    {
        if (_editor.SelectedThing is not { } thing)
            return;
        var result = MessageBox.Show(
            this,
            $"Delete {thing.Name ?? $"thing {thing.ThingId}"}?",
            "Delete moving platform",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
            _editor.DeleteSelectedThing();
    }

    private void ClearProperties()
    {
        _editingDefinitionId = null;
        _editingThingId = null;
        _selection.Text = "Select or create a definition";
        _properties.SelectedObject = null;
    }

    private void UpdateButtons()
    {
        _place.Enabled = _editor.SelectedDefinitionId is not null;
        _place.Text = _editor.IsPlacingThing ? "Placing…" : "Place";
        _deleteThing.Enabled = _editor.SelectedThing is not null;
        _undo.Enabled = _editor.CanUndoThingEdit;
        _apply.Enabled = _properties.SelectedObject is not null;
        _cancel.Enabled = _properties.SelectedObject is not null;
    }

    private static FlowLayoutPanel MakeRow() => new()
    {
        Dock = DockStyle.Top,
        Height = 42,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        Padding = new Padding(0, 4, 0, 4)
    };

    private static Button MakeButton(string text)
    {
        var button = new Button { Text = text };
        StyleButton(button);
        return button;
    }

    private static void StyleButton(Button button)
    {
        button.AutoSize = true;
        button.Height = 32;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Color.FromArgb(35, 47, 66);
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderColor = Color.FromArgb(62, 77, 99);
    }

    private sealed record DefinitionItem(MovingPlatformDefinitionRecord2D Record)
    {
        public string Label => Record.Name;
    }
}
