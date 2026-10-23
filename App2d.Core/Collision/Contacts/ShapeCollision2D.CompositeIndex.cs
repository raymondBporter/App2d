using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static readonly ConditionalWeakTable<object, CompositeIndex> CompositeIndexes = [];

    private static CollisionResult IndexedCompositeAgainst(CompositeShape2D composite, Similarity2D compositePose, IShape2D other, Similarity2D otherPose)
    {
        var index = CompositeIndexes.GetValue(composite.PartsIdentity, _ => new CompositeIndex(composite));
        Matrix3x2.Invert(compositePose.ToMatrix(), out var worldToLocal);
        var localBounds = ShapeBounds2D.CalculateTransformed(other, otherPose.ToMatrix() * worldToLocal);
        var best = CollisionResult.None;
        var bestPart = int.MaxValue;
        Visit(0);
        return best;

        void Visit(int nodeIndex)
        {
            var node = index.Nodes[nodeIndex];
            if (!node.Bounds.Intersects(localBounds)) return;
            if (node.Part < 0)
            {
                Visit(node.Left);
                Visit(node.Right);
                return;
            }
            var result = Dispatch(index.Parts[node.Part], compositePose, other, otherPose);
            if (result.HasContact && (!best.HasContact || result.Contact.PenetrationDepth > best.Contact.PenetrationDepth
                || result.Contact.PenetrationDepth == best.Contact.PenetrationDepth && node.Part < bestPart))
            {
                best = result;
                bestPart = node.Part;
            }
        }
    }

    // Immutable shapes permit a reusable local-space BVH. Transform the query into its coordinates;
    // moving the body does not rebuild the tree. Ties retain the original part ordering.
    private sealed class CompositeIndex
    {
        internal readonly IShape2D[] Parts;
        internal readonly List<CompositeNode> Nodes = [];

        internal CompositeIndex(CompositeShape2D composite)
        {
            Parts = composite.Parts.ToArray();
            var bounds = Parts.Select(ShapeBounds2D.Calculate).ToArray();
            var parts = Enumerable.Range(0, Parts.Length).ToArray();
            Build(0, parts.Length);

            int Build(int start, int count)
            {
                var box = bounds[parts[start]];
                var centers = Rect2D.FromPoints(box.Center, box.Center);
                for (var i = start + 1; i < start + count; i++)
                {
                    box = box.Union(bounds[parts[i]]);
                    centers = centers.Union(Rect2D.FromPoints(bounds[parts[i]].Center, bounds[parts[i]].Center));
                }
                var nodeIndex = Nodes.Count;
                Nodes.Add(new(box, -1, -1, count == 1 ? parts[start] : -1));
                if (count == 1) return nodeIndex;
                var sortX = centers.Max.X - centers.Min.X >= centers.Max.Y - centers.Min.Y;
                Array.Sort(parts, start, count, Comparer<int>.Create((a, b) =>
                    (sortX ? bounds[a].Center.X : bounds[a].Center.Y).CompareTo(sortX ? bounds[b].Center.X : bounds[b].Center.Y)));
                var half = count / 2;
                var left = Build(start, half);
                var right = Build(start + half, count - half);
                Nodes[nodeIndex] = new(box, left, right, -1);
                return nodeIndex;
            }
        }
    }

    private readonly record struct CompositeNode(Rect2D Bounds, int Left, int Right, int Part);
}
