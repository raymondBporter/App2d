using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Rendering.Characters;

/// <summary>A placed Part uses the shared mesh, material, image and animation drawing path.</summary>
public sealed class PartShader2D(Part part) : IShader2D
{
    private double? _time;
    public Part Part { get; } = part;
    public string TextureRoot { get; init; } = part.TextureRoot;
    public PuppetDrawing Drawing { get; } = new();
    public Color4b BaseColor => Color4b.White;

    public void SetTime(double seconds)
    {
        if (_time == seconds || _time is not null && !Part.IsAnimated) return;
        Drawing.Build(Part, seconds, TextureRoot);
        _time = seconds;
    }

    public IShape2D BoundsShape()
    {
        SetTime(0);
        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);
        foreach (var vertex in Drawing.Mesh.Vertices)
        {
            var point = new Vector2(vertex.Position.X, vertex.Position.Y);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return float.IsFinite(min.X) && min.X < max.X && min.Y < max.Y
            ? Rectangle2D.Builder.Create(min, max) : Rectangle2D.Builder.CreateFromSize(1, 1);
    }
}
