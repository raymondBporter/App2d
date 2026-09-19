using App2d.Noodle.Rigging;
using System.Drawing;

namespace App2d.Noodle;

internal sealed class BoneEditorPanel : UserControl
{
    private readonly RigDocument2D _document;
    private readonly TreeView _hierarchy = new();
    private readonly PropertyGrid _properties = new();
    private readonly ComboBox _attachmentBone = new();
    private readonly Button _attach = MakeButton("Attach shape");
    private readonly Label _selection = new();
    private bool _refreshing;

    public BoneEditorPanel(RigDocument2D document)
    {
        _document = document;
        Dock = DockStyle.Right;
        Width = 420;
        BackColor = Color.FromArgb(17, 24, 37);
        ForeColor = Color.FromArgb(244, 247, 252);
        Padding = new Padding(12);

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 38,
            Text = "ENTITY EDITOR  /  BONES",
            ForeColor = ForeColor,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var boneButtons = MakeRow(42);
        var addRoot = MakeButton("+ Root bone");
        var addChild = MakeButton("+ Child bone");
        addRoot.Click += (_, _) => AddBone(parent: null);
        addChild.Click += (_, _) => AddBone(GetSelectedBone());
        boneButtons.Controls.Add(addRoot);
        boneButtons.Controls.Add(addChild);

        var shapeButtons = MakeRow(74);
        foreach (var kind in new[] { "Rectangle", "Circle", "Capsule", "Polygon" })
        {
            var button = MakeButton($"+ {kind}");
            button.Click += (_, _) => AddShape(kind);
            shapeButtons.Controls.Add(button);
        }

        _hierarchy.Dock = DockStyle.Top;
        _hierarchy.Height = 270;
        _hierarchy.BackColor = Color.FromArgb(11, 18, 29);
        _hierarchy.ForeColor = ForeColor;
        _hierarchy.BorderStyle = BorderStyle.FixedSingle;
        _hierarchy.HideSelection = false;
        _hierarchy.AfterSelect += OnHierarchySelectionChanged;

        _selection.Dock = DockStyle.Top;
        _selection.Height = 30;
        _selection.ForeColor = Color.FromArgb(160, 174, 196);
        _selection.TextAlign = ContentAlignment.MiddleLeft;

        var attachmentRow = MakeRow(42);
        _attachmentBone.DropDownStyle = ComboBoxStyle.DropDownList;
        _attachmentBone.Width = 225;
        _attachmentBone.DisplayMember = nameof(BoneItem.Label);
        _attach.Click += (_, _) => AttachSelectedShape();
        var delete = MakeButton("Delete");
        delete.Click += (_, _) => DeleteSelected();
        attachmentRow.Controls.Add(_attachmentBone);
        attachmentRow.Controls.Add(_attach);
        attachmentRow.Controls.Add(delete);

        _properties.Dock = DockStyle.Fill;
        _properties.HelpVisible = true;
        _properties.ToolbarVisible = false;
        _properties.PropertySort = PropertySort.Categorized;
        _properties.PropertyValueChanged += OnPropertyValueChanged;

        Controls.Add(_properties);
        Controls.Add(attachmentRow);
        Controls.Add(_selection);
        Controls.Add(_hierarchy);
        Controls.Add(shapeButtons);
        Controls.Add(boneButtons);
        Controls.Add(header);

        RefreshAll(_document.Bones.FirstOrDefault());
    }

    public event Action? DocumentChanged;
    public event Action? SelectionChanged;
    public object? SelectedItem { get; private set; }

    public void Select(object? item) => RefreshAll(item);

    private void AddBone(RigBone2D? parent)
    {
        var bone = _document.AddBone(parent);
        RefreshAll(bone);
        DocumentChanged?.Invoke();
    }

    private void AddShape(string kind)
    {
        var bone = GetSelectedBone() ?? _document.Bones.FirstOrDefault() ?? _document.AddBone(name: "root");
        var shape = _document.AddShape(kind, bone);
        RefreshAll(shape);
        DocumentChanged?.Invoke();
    }

    private RigBone2D? GetSelectedBone() => SelectedItem switch
    {
        RigBone2D bone => bone,
        RigShape2D shape => shape.AttachedBone,
        _ => null
    };

    private void AttachSelectedShape()
    {
        if (SelectedItem is not RigShape2D shape || _attachmentBone.SelectedItem is not BoneItem boneItem)
            return;

        shape.AttachedBone = boneItem.Bone;
        RefreshAll(shape);
        DocumentChanged?.Invoke();
    }

    private void DeleteSelected()
    {
        if (SelectedItem is null)
            return;
        try
        {
            if (!_document.Remove(SelectedItem))
                return;
            RefreshAll(_document.Bones.FirstOrDefault());
            DocumentChanged?.Invoke();
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(this, exception.Message, "Cannot delete bone", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnPropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
    {
        if (SelectedItem is RigBone2D bone && string.IsNullOrWhiteSpace(bone.Name))
            bone.Name = $"bone-{bone.Id}";
        if (SelectedItem is RigShape2D shape && string.IsNullOrWhiteSpace(shape.Name))
            shape.Name = $"{shape.Kind.ToLowerInvariant()}-{shape.Id}";
        RefreshAll(SelectedItem);
        DocumentChanged?.Invoke();
    }

    private void OnHierarchySelectionChanged(object? sender, TreeViewEventArgs e)
    {
        if (_refreshing || e.Node?.Tag is null)
            return;
        SetSelection(e.Node.Tag);
    }

    private void RefreshAll(object? selection)
    {
        _refreshing = true;
        try
        {
            SelectedItem = selection;
            _hierarchy.BeginUpdate();
            _hierarchy.Nodes.Clear();
            var bonesRoot = _hierarchy.Nodes.Add("BONES");
            foreach (var rootBone in _document.Bones.Where(bone => bone.Parent is null))
                AddBoneNode(bonesRoot, rootBone);
            var shapesRoot = _hierarchy.Nodes.Add("SHAPES");
            foreach (var shape in _document.Shapes)
                shapesRoot.Nodes.Add($"{shape.Name}  [{shape.Kind}]  ->  {shape.AttachedBone.Name}").Tag = shape;
            _hierarchy.EndUpdate();
            bonesRoot.ExpandAll();
            shapesRoot.Expand();

            _attachmentBone.Items.Clear();
            foreach (var bone in _document.Bones)
                _attachmentBone.Items.Add(new BoneItem(bone));
            SelectAttachmentBone();
            SelectTreeNode(selection);
            UpdateSelectionUi();
        }
        finally
        {
            _refreshing = false;
        }
        SelectionChanged?.Invoke();
    }

    private void AddBoneNode(TreeNode parentNode, RigBone2D bone)
    {
        var node = parentNode.Nodes.Add($"{bone.Name}  ({bone.Length:0.#})");
        node.Tag = bone;
        foreach (var child in _document.Bones.Where(candidate => candidate.Parent == bone))
            AddBoneNode(node, child);
    }

    private void SetSelection(object item)
    {
        SelectedItem = item;
        SelectAttachmentBone();
        UpdateSelectionUi();
        SelectionChanged?.Invoke();
    }

    private void SelectAttachmentBone()
    {
        var attachedBone = SelectedItem is RigShape2D shape ? shape.AttachedBone : null;
        _attachmentBone.SelectedItem = _attachmentBone.Items.Cast<BoneItem>()
            .FirstOrDefault(item => item.Bone == attachedBone);
    }

    private void UpdateSelectionUi()
    {
        _properties.SelectedObject = SelectedItem;
        _selection.Text = SelectedItem switch
        {
            RigBone2D bone => $"Bone: {bone.Name}",
            RigShape2D shape => $"{shape.Kind}: {shape.Name}",
            _ => "Select a bone or shape"
        };
        _attachmentBone.Enabled = SelectedItem is RigShape2D;
        _attach.Enabled = SelectedItem is RigShape2D;
    }

    private void SelectTreeNode(object? item)
    {
        foreach (TreeNode root in _hierarchy.Nodes)
        {
            var node = FindNode(root, item);
            if (node is null)
                continue;
            _hierarchy.SelectedNode = node;
            node.EnsureVisible();
            return;
        }
    }

    private static TreeNode? FindNode(TreeNode node, object? item)
    {
        if (ReferenceEquals(node.Tag, item))
            return node;
        foreach (TreeNode child in node.Nodes)
        {
            var result = FindNode(child, item);
            if (result is not null)
                return result;
        }
        return null;
    }

    private static FlowLayoutPanel MakeRow(int height) => new()
    {
        Dock = DockStyle.Top,
        Height = height,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        Padding = new Padding(0, 4, 0, 4)
    };

    private static Button MakeButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(35, 47, 66),
            ForeColor = Color.White
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(62, 77, 99);
        return button;
    }

    private sealed record BoneItem(RigBone2D Bone)
    {
        public string Label => Bone.Name;
    }
}
