using App2d.Core.Meshes;

namespace App2d.Core.Shapes;

/// <summary>Explicit bridges from mesh data to runtime shapes.</summary>
public static class ShapeConversions2D
{
    /// <summary>Turns the filled triangles into a non-convex collision shape without filling concavities.</summary>
    /// <remarks>Collision resolves per triangle, so dynamic bodies may encounter internal edges.</remarks>
    public static CompositeShape2D ToCompositeShape(this TriangleMesh2D mesh) => CompositeShape2D.Builder.Create(
        Enumerable.Range(0, mesh.TriangleCount).Select(index =>
        {
            var (a, b, c) = mesh.TriangleAt(index);
            return (IConvexShape2D)Triangle2D.Builder.Create(a, b, c);
        }));

}
