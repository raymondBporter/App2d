using App2d.Core.Characters.Authored;

namespace App2d.Tests.Authored;

/// <summary>
/// Builds the version-2 form of a shipped (converted) entity for the conversion tests, now that no shipped entity file is
/// legacy any more (P11a): colliders fold back into the windows and the guard, hurt colliders give way to the model's first
/// hurt layout, the typed behaviors back into the loose combat fields. The inverse of the P8c and P9a conversions, for tests only.
/// </summary>
internal static class LegacyEntities
{
    public static EntityAsset Strip(EntityAsset typed, CharacterModel model)
    {
        var entity = EntityAsset.FromJson(typed.ToJson());
        var colliders = entity.Colliders.ToDictionary(c => c.Id, StringComparer.Ordinal);
        foreach (var action in entity.Actions)
        {
            foreach (var hit in action.Hits)
            {
                if (hit.Collider is { } id && colliders.TryGetValue(id, out var collider))
                {
                    hit.Shape = collider.Shape;
                    hit.Socket = collider.Socket;
                    hit.Prop = collider.Prop;
                    hit.Point = collider.Point;
                    hit.Along = collider.Along;
                    hit.OffsetX = collider.OffsetX;
                    hit.OffsetY = collider.OffsetY;
                    hit.Collider = null;
                }

                if (entity.Behaviors?.Strikes.TryGetValue(hit.Id, out var strike) == true)
                {
                    hit.Damage = strike.Damage;
                    hit.Sound = strike.Sound;
                }
            }

            if (action.Projectile is { } projectile && entity.Behaviors?.Strikes.TryGetValue(action.Id, out var shot) == true)
            {
                projectile.Damage = shot.Damage;
            }
        }

        if (entity.Guard is { Collider: { } guardId } guard && colliders.TryGetValue(guardId, out var guardCollider))
        {
            guard.Shape = guardCollider.Shape;
            guard.Prop = guardCollider.Prop ?? "";
            guard.Collider = null;
        }

        if (entity.Colliders.Any(c => c.Use == ColliderUses.Hurt))
        {
            entity.Hurt = new() { Layout = model.HurtLayouts.FirstOrDefault()?.Id };
        }

        entity.Colliders.Clear();
        if (entity.Behaviors is { } behaviors)
        {
            entity.Health = behaviors.Damage.Health;
            entity.Mass = behaviors.Damage.Mass;
            entity.Behaviors = null;
        }

        entity.Version = EntityAsset.LegacyVersion;
        entity.Validate();
        return entity;
    }

    /// <summary>A scratch copy of the authored tree whose named entities are written in their version-2 form, for file conversions that need a legacy source.</summary>
    public static string ScratchTree(AuthoredCatalog catalog, params string[] legacyEntities)
    {
        var scratch = Path.Combine(Path.GetTempPath(), $"app2d-legacy-{Guid.NewGuid():N}");
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(scratch, Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        foreach (var id in legacyEntities)
        {
            var entity = catalog.Entities[id];
            Strip(entity.Asset, entity.Model.Base).Save(Path.Combine(scratch, "entities", id + ".json"));
        }

        return scratch;
    }
}
