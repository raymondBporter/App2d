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
        // The widget shows degrees; shapes store radians.
        void AngleField(float angleRadians, Func<float, IShape2D> withAngle)
        {
            var degrees = float.RadiansToDegrees(angleRadians);
            if (Ui.Drag("Angle (deg)", ref degrees, .5f, -180, 180))
            {
                Drag(withAngle(float.DegreesToRadians(degrees)));
            }
        }
        void RectangleFields(Vector2 centre, Vector2 size, float? angleRadians, Func<Vector2, Vector2, float, IShape2D> fromSize)
        {
            var angle = angleRadians ?? 0f;
            if (Ui.Drag2("Centre", ref centre))
            {
                Drag(fromSize(size, centre, angle));
            }

            if (Ui.Drag2("Width / height", ref size, .005f, .01f, 100))
            {
                Drag(fromSize(Vector2.Max(size, new(.01f)), centre, angle));
            }

            if (angleRadians is { } radians)
            {
                AngleField(radians, turned => fromSize(size, centre, turned));
            }
        }
        switch (shape)
        {
            case Rectangle2D rectangle:
                RectangleFields(rectangle.Center, rectangle.Size, rectangle.AngleRadians, static (size, centre, angle) => Rectangle2D.Builder.FromSize(size, centre, angle));
                break;
            case AxisAlignedRectangle2D rectangle:
                RectangleFields((rectangle.Min + rectangle.Max) / 2, rectangle.Max - rectangle.Min, null, static (size, centre, _) => AxisAlignedRectangle2D.Builder.FromSize(size, centre));
                break;
            case RoundedRectangle2D rounded:
                {
                    var centre = rounded.Center;
                    var size = rounded.Size;
                    if (Ui.Drag2("Centre", ref centre))
                    {
                        Drag(RoundedRectangle2D.Builder.FromSize(size, rounded.Radius, centre, rounded.AngleRadians));
                    }

                    if (Ui.Drag2("Width / height", ref size, .005f, .01f, 100))
                    {
                        size = Vector2.Max(size, new(.01f));
                        Drag(RoundedRectangle2D.Builder.FromSize(size, MathF.Min(rounded.Radius, MathF.Min(size.X, size.Y) / 2f), centre, rounded.AngleRadians));
                    }
                    var radius = rounded.Radius;
                    if (Ui.Drag("Corner radius", ref radius, .005f, 0, MathF.Min(size.X, size.Y) / 2f))
                    {
                        Drag(rounded with
                        {
                            Radius = Math.Clamp(radius, 0, MathF.Min(size.X, size.Y) / 2f)
                        });
                    }
                    AngleField(rounded.AngleRadians, angle => RoundedRectangle2D.Builder.FromSize(size, rounded.Radius, centre, angle));

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
                    AngleField(ellipse.AngleRadians, angle => Ellipse2D.Builder.Create(radii, centre, angle));

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
