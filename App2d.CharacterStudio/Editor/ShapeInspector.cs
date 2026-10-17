using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>Edits the source parameters of shape values, shared by collision and drawing editors.</summary>
internal static class ShapeInspector
{
    public static void Draw(IShape2D shape, Action<IShape2D> change, Action<IShape2D>? edit = null)
    {
        var commit = edit ?? change;
        void Drag(IShape2D changed) => change(changed);
        void RectangleFields(Vector2 min, Vector2 max, Func<Vector2, Vector2, IShape2D> fromSize)
        {
            var centre = (min + max) / 2;
            var size = max - min;
            if (Ui.Drag2("Centre", ref centre))
            {
                Drag(fromSize(size, centre));
            }

            if (Ui.Drag2("Width / height", ref size, .005f, .01f, 100))
            {
                Drag(fromSize(Vector2.Max(size, new(.01f)), centre));
            }
        }
        switch (shape)
        {
            case Rectangle2D rectangle:
                RectangleFields(rectangle.Min, rectangle.Max, static (size, centre) => Rectangle2D.Builder.FromSize(size, centre));
                break;
            case AxisAlignedRectangle2D rectangle:
                RectangleFields(rectangle.Min, rectangle.Max, static (size, centre) => AxisAlignedRectangle2D.Builder.FromSize(size, centre));
                break;
            case RoundedRectangle2D rounded:
                {
                    var centre = (rounded.Min + rounded.Max) / 2;
                    var size = rounded.Max - rounded.Min;
                    if (Ui.Drag2("Centre", ref centre))
                    {
                        Drag(RoundedRectangle2D.Builder.FromSize(size, rounded.Radius, centre));
                    }

                    if (Ui.Drag2("Width / height", ref size, .005f, .01f, 100))
                    {
                        size = Vector2.Max(size, new(.01f));
                        Drag(RoundedRectangle2D.Builder.FromSize(size, MathF.Min(rounded.Radius, MathF.Min(size.X, size.Y) / 2f), centre));
                    }
                    var radius = rounded.Radius;
                    if (Ui.Drag("Corner radius", ref radius, .005f, 0, MathF.Min(size.X, size.Y) / 2f))
                    {
                        Drag(rounded with
                        {
                            Radius = Math.Clamp(radius, 0, MathF.Min(size.X, size.Y) / 2f)
                        });
                    }

                    break;
                }
            case Circle2D circle:
                {
                    var centre = circle.Center;
                    var radius = circle.Radius;
                    if (Ui.Drag2("Centre", ref centre))
                    {
                        Drag(circle with
                        {
                            Center = centre
                        });
                    }

                    if (Ui.Drag("Radius", ref radius, .005f, .005f, 50))
                    {
                        Drag(circle with
                        {
                            Radius = Math.Max(.005f, radius)
                        });
                    }

                    break;
                }
            case Capsule2D capsule:
                {
                    var start = capsule.Start;
                    var end = capsule.End;
                    var radius = capsule.Radius;
                    if (Ui.Drag2("Start", ref start))
                    {
                        Drag(capsule with
                        {
                            Start = start
                        });
                    }

                    if (Ui.Drag2("End", ref end))
                    {
                        Drag(capsule with
                        {
                            End = end
                        });
                    }

                    if (Ui.Drag("Radius", ref radius, .005f, .005f, 50))
                    {
                        Drag(capsule with
                        {
                            Radius = Math.Max(.005f, radius)
                        });
                    }

                    break;
                }
            case Ellipse2D ellipse:
                {
                    var centre = ellipse.Center;
                    var radii = ellipse.Radii;
                    if (Ui.Drag2("Centre", ref centre))
                    {
                        Drag(ellipse with
                        {
                            Center = centre
                        });
                    }

                    if (Ui.Drag2("Radii", ref radii, .005f, .005f, 50))
                    {
                        Drag(ellipse with
                        {
                            Radii = Vector2.Max(radii, new(.005f))
                        });
                    }

                    break;
                }
            case Triangle2D triangle:
                {
                    var a = triangle.A;
                    var b = triangle.B;
                    var c = triangle.C;
                    if (Ui.Drag2("A", ref a))
                    {
                        Drag(triangle with
                        {
                            A = a
                        });
                    }

                    if (Ui.Drag2("B", ref b))
                    {
                        Drag(triangle with
                        {
                            B = b
                        });
                    }

                    if (Ui.Drag2("C", ref c))
                    {
                        Drag(triangle with
                        {
                            C = c
                        });
                    }

                    break;
                }
            case ConvexPolygon2D polygon:
                {
                    for (var i = 0; i < polygon.Vertices.Length; i++)
                    {
                        var vertex = polygon.Vertices[i];
                        if (Ui.Drag2($"Vertex {i + 1}", ref vertex))
                        {
                            var vertices = polygon.Vertices.ToArray().ToList();
                            vertices[i] = vertex;
                            Drag(polygon with
                            {
                                Vertices = [.. vertices]
                            });
                        }
                    }
                    if (ImGui.SmallButton("Add vertex"))
                    {
                        var midpoint = (polygon.Vertices[^1] + polygon.Vertices[0]) / 2;
                        commit(polygon with { Vertices = [.. polygon.Vertices, midpoint] });
                    }
                    if (polygon.Vertices.Length > 3)
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Remove last"))
                        {
                            commit(polygon with { Vertices = [.. polygon.Vertices.ToArray().Take(polygon.Vertices.Length - 1)] });
                        }
                    }
                    Ui.Help("Vertices go around the perimeter and must stay convex.");
                    break;
                }
            default:
                Ui.Help("Edit this shape kind in the file.");
                break;
        }
    }
}
