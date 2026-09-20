using App2d.Core.Geometry;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Numerics;

namespace App2d.Noodle.Rigging;

internal enum RigShapePurpose
{
    Visual,
    Collision,
    VisualAndCollision
}

internal abstract class RigShape2D(int id, string name, RigBone2D attachedBone)
{
    [Browsable(false)]
    public int Id { get; } = id;

    [Category("Shape"), DisplayName("Name")]
    public string Name { get; set; } = name;

    [Category("Attachment"), DisplayName("Bone"), ReadOnly(true)]
    public string AttachedBoneName => AttachedBone.Name;

    [Browsable(false)]
    public RigBone2D AttachedBone { get; set; } = attachedBone;

    [Category("Attachment"), DisplayName("Local X")]
    public float LocalX { get; set; }

    [Category("Attachment"), DisplayName("Local Y")]
    public float LocalY { get; set; }

    [Category("Attachment"), DisplayName("Angle (degrees)")]
    public float AngleDegrees { get; set; }

    [Category("Rendering"), DisplayName("Color")]
    public Color Color { get; set; } = Color.FromArgb(215, 151, 111, 255);

    [Category("Shape"), DisplayName("Purpose")]
    public RigShapePurpose Purpose { get; set; } = RigShapePurpose.VisualAndCollision;

    [Browsable(false)]
    public abstract string Kind { get; }

    internal abstract IShape2D CreateGeometry();

    public override string ToString() => Name;
}

internal sealed class RigRectangleShape2D(int id, string name, RigBone2D bone) : RigShape2D(id, name, bone)
{
    private float _width = 100f;
    private float _height = 40f;

    public override string Kind => "Rectangle";

    [Category("Geometry")]
    public float Width
    {
        get => _width;
        set => _width = Positive(value);
    }

    [Category("Geometry")]
    public float Height
    {
        get => _height;
        set => _height = Positive(value);
    }

    internal override IShape2D CreateGeometry() => Rectangle2D.FromSize(new Vector2(Width, Height));

    private static float Positive(float value) => float.IsFinite(value) && value > 1f ? value : 1f;
}

internal sealed class RigCircleShape2D(int id, string name, RigBone2D bone) : RigShape2D(id, name, bone)
{
    private float _radius = 35f;

    public override string Kind => "Circle";

    [Category("Geometry")]
    public float Radius
    {
        get => _radius;
        set => _radius = float.IsFinite(value) && value > 1f ? value : 1f;
    }

    internal override IShape2D CreateGeometry() => new Circle2D(Radius);
}

internal sealed class RigCapsuleShape2D(int id, string name, RigBone2D bone) : RigShape2D(id, name, bone)
{
    private float _length = 100f;
    private float _radius = 20f;

    public override string Kind => "Capsule";

    [Category("Geometry")]
    public float Length
    {
        get => _length;
        set => _length = float.IsFinite(value) && value > 1f ? value : 1f;
    }

    [Category("Geometry")]
    public float Radius
    {
        get => _radius;
        set => _radius = float.IsFinite(value) && value > 1f ? value : 1f;
    }

    internal override IShape2D CreateGeometry() =>
        new Capsule2D(new Vector2(-Length / 2f, 0f), new Vector2(Length / 2f, 0f), Radius);
}

internal sealed class RigPolygonShape2D(int id, string name, RigBone2D bone) : RigShape2D(id, name, bone)
{
    private string _vertices = "-45,-30; 45,-30; 55,15; 0,45; -55,15";

    public override string Kind => "Polygon";

    [Category("Geometry"), Description("Convex perimeter vertices formatted as x,y; x,y; ...")]
    public string Vertices
    {
        get => _vertices;
        set
        {
            _ = new ConvexPolygon2D(Parse(value));
            _vertices = value;
        }
    }

    internal override IShape2D CreateGeometry() => new ConvexPolygon2D(Parse(Vertices));

    private static IEnumerable<Vector2> Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var coordinates = pair.Split(',', StringSplitOptions.TrimEntries);
            if (coordinates.Length != 2 ||
                !float.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                throw new FormatException("Vertices must use: x,y; x,y; x,y");
            }
            yield return new Vector2(x, y);
        }
    }
}
