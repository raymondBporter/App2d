using App2d.Core.Characters;
using App2d.Core.Characters.Authored;

namespace App2d.Tests.Authored;

internal static class ColliderFixtures
{
    public static void AddCollider(this Part part, PartGeometry geometry, string use = ColliderUses.Hurt)
    {
        geometry.Collision ??= new();
        part.Geometry.Add(geometry);
        part.Behavior!.Colliders.Add(geometry.Id, use);
    }

    public static void RemoveColliders(this Part part, string use)
    {
        var ids = part.Behavior!.Colliders.Where(c => c.Value == use).Select(c => c.Key).ToHashSet();
        part.Geometry.RemoveAll(g => ids.Contains(g.Id));
        foreach (var id in ids) part.Behavior.Colliders.Remove(id);
    }
}
