using System.ComponentModel;

namespace App2d.Noodle.Rigging;

internal sealed class RigBone2D(int id, string name, RigBone2D? parent)
{
    private float _length = 100f;

    [Browsable(false)]
    public int Id { get; } = id;

    [Category("Bone"), DisplayName("Name")]
    public string Name { get; set; } = name;

    [Category("Bone"), DisplayName("Parent"), ReadOnly(true)]
    public string ParentName => Parent?.Name ?? "<root>";

    [Browsable(false)]
    public RigBone2D? Parent { get; } = parent;

    [Category("Transform"), DisplayName("Local X")]
    public float LocalX { get; set; }

    [Category("Transform"), DisplayName("Local Y")]
    public float LocalY { get; set; }

    [Category("Transform"), DisplayName("Angle (degrees)")]
    public float AngleDegrees { get; set; }

    [Category("Bone"), DisplayName("Length")]
    public float Length
    {
        get => _length;
        set => _length = float.IsFinite(value) && value > 1f ? value : 1f;
    }

    public override string ToString() => Name;
}
