using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>A starting point for a new entity: gameplay defaults it copies, never a live parent.</summary>
public sealed record EntityTemplate(string Id, string Name, string Description);

/// <summary>
/// Graphics-free entity, motion-set and look edits for the editor. Each edit either applies completely or throws with a
/// message naming what refused it, so a document edit can roll back. Nothing here reads files.
/// </summary>
public static class EntityAuthoring
{
    public const string Guard = "guard", Platformer = "platformer", Stationary = "stationary";

    /// <summary>Roles the editor always lists: every controller's requirements, then the common optional ones.</summary>
    public static readonly IReadOnlyList<string> CommonRoles =
        [EntityControllers.Idle, EntityControllers.Walk, EntityControllers.Run, EntityControllers.Jump, EntityControllers.Fall, EntityControllers.Hit, EntityControllers.Death, "attack"];

    public static IReadOnlyList<EntityTemplate> Templates { get; } =
    [
        new(Guard, "Guard", "Walks toward targets and attacks in range. The attack plays the 'attack' role until you choose its clip."),
        new(Platformer, "Platformer", "Walks, runs and jumps; the jump leaves the ground on a 'launch' event."),
        new(Stationary, "Stationary", "Stays put: needs only an idle."),
    ];

    /// <summary>
    /// A new entity on <paramref name="model"/> (a model or variant, resolved) with a template's gameplay defaults: the base's
    /// first motion set and hurt layout, and a movement box fitted to the rest pose. References it cannot fill stay visible
    /// as compile problems for the author to repair, rather than being invented.
    /// </summary>
    public static EntityAsset New(string template, string id, string name, ResolvedModel model)
    {
        var entity = new EntityAsset
        {
            Id = id, Name = name, Model = model.Id,
            MotionSet = model.Base.MotionSets.FirstOrDefault()?.Id ?? "standard",
            Movement = FitMovement(model),
            Hurt = new() { Layout = model.Base.HurtLayouts.FirstOrDefault()?.Id },
        };
        switch (template)
        {
            case Guard:
                entity.Controller = new() { Kind = EntityControllers.Walker, WalkSpeed = .9f, Range = 1.6f, Cooldown = 1 };
                entity.Actions = [new() { Id = EntityControllers.Attack, Role = "attack" }];
                break;
            case Platformer:
                entity.Controller = new() { Kind = EntityControllers.Platformer, WalkSpeed = 1.5f, RunSpeed = 3, JumpSpeed = 6, Range = 1.5f, Cooldown = .4f };
                entity.Actions = [new() { Id = EntityControllers.Jump, Role = EntityControllers.Jump, Events = [new() { Id = EntityControllers.Launch, At = new() { At = .25f } }] }];
                break;
            case Stationary:
                entity.Controller = new() { Kind = EntityControllers.Stationary, WalkSpeed = 0, Range = 1.2f, Cooldown = 1 };
                break;
            default: throw new InvalidDataException($"Unknown entity template '{template}' (known: {string.Join(", ", Templates.Select(t => t.Id))}).");
        }
        entity.Validate(); return entity;
    }

    /// <summary>A copy that keeps every model, clip and prop reference: a sibling, not a child. There is no entity inheritance.</summary>
    public static EntityAsset Duplicate(EntityAsset source, string id, string name)
    {
        var copy = AuthoredAsset.Parse<EntityAsset>(source.ToJson(), "entity"); copy.Id = id; copy.Name = name; return copy;
    }

    /// <summary>The movement box around the rest pose's visible parts: full height from the feet, a narrow body width. Explicit, never automatic.</summary>
    public static MovementBox FitMovement(ResolvedModel model)
    {
        var points = model.Parts.Where(p => !p.Hidden).SelectMany(p => PartGeometry.Contour(p, id => model.Rest[id]))
            .Concat(model.Rest.Values).DefaultIfEmpty(Vector3.Zero).ToList();
        var height = MathF.Max(.2f, points.Max(p => p.Y));
        var width = Math.Clamp((points.Max(p => p.X) - points.Min(p => p.X)) * .6f, .2f, height);
        return new() { Width = MathF.Round(width, 3), Height = MathF.Round(height, 3) };
    }

    // ---- Motion sets ---------------------------------------------------------------------------------------------

    /// <summary>A new set, empty or starting as a copy of another set's assignments. Sets never inherit from each other.</summary>
    public static MotionSet AddMotionSet(CharacterModel model, string id, string name, string? copyFrom)
    {
        AuthoredAsset.RequireId(id, "motion set id");
        if (model.MotionSets.Any(s => s.Id == id)) throw new InvalidDataException($"'{model.Id}' already has a motion set '{id}'.");
        var roles = copyFrom is null ? [] : new Dictionary<string, string>(Set(model, copyFrom).Roles, StringComparer.Ordinal);
        var set = new MotionSet { Id = id, Name = name, Roles = roles };
        model.MotionSets.Add(set); model.Validate(); return set;
    }

    /// <summary>Assigns a clip to a role, or unassigns it with null. The clip must be written for this base.</summary>
    public static void Assign(CharacterModel model, string setId, string role, MotionClip? clip)
    {
        AuthoredAsset.RequireId(role, "role");
        var set = Set(model, setId);
        if (clip is null) { set.Roles.Remove(role); return; }
        if (clip.Model != model.Id) throw new InvalidDataException($"Animation '{clip.Id}' is for model '{clip.Model}', not '{model.Id}'.");
        set.Roles[role] = clip.Id; model.Validate();
    }

    public static MotionSet Set(CharacterModel model, string id) =>
        model.MotionSets.FirstOrDefault(s => s.Id == id) ?? throw new InvalidDataException($"'{model.Id}' has no motion set '{id}'.");

    // ---- Looks ---------------------------------------------------------------------------------------------------

    /// <summary>Writes a look's part overrides onto a variant, field by field; fields the look does not set are kept.</summary>
    public static void ApplyLook(CharacterModel model, ModelVariant variant, string lookId)
    {
        var look = model.Looks.FirstOrDefault(l => l.Id == lookId) ?? throw new InvalidDataException($"'{model.Id}' has no look '{lookId}'.");
        foreach (var (part, change) in look.Parts)
            variant.Parts[part] = (variant.Parts.GetValueOrDefault(part) ?? new()).Merge(change);
        variant.Validate();
    }

    /// <summary>
    /// Records a variant's appearance overrides (fill, face, face offset, visibility; never sizes) as a look on its base, replacing
    /// a look with the same ID. This edits the base model, which the caller must present as a base edit.
    /// </summary>
    public static LookPreset SaveLook(CharacterModel model, ModelVariant variant, string id, string name)
    {
        AuthoredAsset.RequireId(id, "look id");
        var parts = new Dictionary<string, PartOverride>(StringComparer.Ordinal);
        foreach (var (part, change) in variant.Parts)
        {
            var look = new PartOverride { Fill = change.Fill, Face = change.Face, FaceX = change.FaceX, Hidden = change.Hidden };
            if (!look.IsEmpty) parts[part] = look;
        }
        if (parts.Count == 0) throw new InvalidDataException($"Variant '{variant.Id}' overrides no colors, faces or visibility to save as a look.");
        var preset = new LookPreset { Id = id, Name = name, Parts = parts };
        model.Looks.RemoveAll(l => l.Id == id); model.Looks.Add(preset); model.Validate();
        return preset;
    }
}
