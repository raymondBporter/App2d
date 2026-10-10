using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Graphics-free edits to a base model and a variant. Each structural edit validates the whole model before returning; callers
/// that must keep the previous state on failure (the editor's documents) roll back from their own snapshot.
/// </summary>
public static class ModelAuthoring
{
    public static CharacterModel Empty(string id, string name)
    {
        var model = new CharacterModel { Id = id, Name = name }; model.Validate(); return model;
    }

    /// <summary>The Person template under a new ID: a separate base, not a variant.</summary>
    public static CharacterModel FromPerson(string id, string name)
    {
        var model = PersonTemplate.Model(); model.Id = id; model.Name = name; model.Validate(); return model;
    }

    /// <summary>Flattens a resolved variant into a new independent base. Its clips must be checked or converted explicitly.</summary>
    public static CharacterModel Flatten(ResolvedModel resolved, string id, string name)
    {
        var model = CharacterModel.FromJson(resolved.Base.ToJson());
        model.Id = id; model.Name = name; model.StructureRevision = 1; model.Build = null;
        foreach (var control in model.Controls)
        {
            control.Rest = PuppetPoint.From(resolved.Rest[control.Id]);
            if (resolved.SetupTransforms.TryGetValue(control.Id, out var setup)) control.Transform = setup with { };
        }
        model.Parts = [.. resolved.Parts.Select(p => p with { })];
        model.Validate(); return model;
    }

    /// <summary>What decides clip compatibility: controls and parents, chains, frames and default scales. Proportions and appearance never change it.</summary>
    public static string StructureSignature(CharacterModel model) => string.Join("|",
        model.Controls.Select(c => $"c:{c.Id}<{c.Parent}~{c.Scale}~affine:{c.Transform is not null}").Order(StringComparer.Ordinal)
            .Concat(model.IkChains.Select(c => $"k:{c.Id}={c.Root}>{c.Joint}>{c.End}@{c.Frame}~{c.Scale}~{c.Solver}").Order(StringComparer.Ordinal))
            .Concat(model.Measures.Select(m => $"m:{m.Id}").Order(StringComparer.Ordinal))
            .Concat(model.Slots.Select(s => $"s:{s.Id}={s.Bone}").Order(StringComparer.Ordinal)));

    public static string UniqueId(string basis, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.Ordinal); var id = basis;
        for (var index = 2; used.Contains(id); index++) id = $"{basis}-{index}";
        return id;
    }

    private static IEnumerable<string> Names(CharacterModel model) =>
        model.Controls.Select(c => c.Id).Concat([CharacterModel.Locomotion, CharacterModel.Unit]);

    public static ModelControl AddControl(CharacterModel model, string? parent, Vector3 rest, string? id = null)
    {
        var control = new ModelControl { Id = id ?? UniqueId(parent is null ? "control" : parent + "-child", Names(model)), Parent = parent, Rest = PuppetPoint.From(rest) };
        if (parent is not null && Control(model, parent).Transform is not null)
        {
            var frame = ResolvedModel.From(model).RestTransforms[parent];
            if (!Matrix3x2.Invert(frame, out var inverse)) throw new InvalidOperationException("Cannot add a bone beneath a collapsed parent transform.");
            var local = Vector2.Transform(new(rest.X, rest.Y), inverse);
            control.Transform = new() { X = local.X, Y = local.Y };
        }
        model.Controls.Add(control); model.Validate(); return control;
    }

    /// <summary>Adds a bone at a parent's tip. Its own local +X axis points toward its tip.</summary>
    public static ModelControl AddBone(CharacterModel model, string? parent, string? id = null)
    {
        var basis = parent is null ? null : Control(model, parent);
        var origin = basis is null ? Vector3.Zero : basis.Rest.XYZ + Rotation2D.ApplyXY(new(MathF.Max(basis.Length, .3f), 0, 0), basis.RestAngle);
        var bone = AddControl(model, parent, origin, id ?? UniqueId("bone", Names(model)));
        bone.RestAngle = basis?.RestAngle ?? 0;
        bone.Length = .5f;
        if (basis is null || basis.Transform is not null)
            bone.Transform = new() { X = basis is null ? origin.X : MathF.Max(basis.Length, .3f) };
        if (bone.Transform is not null) SyncAffineRest(model);
        model.Validate();
        return bone;
    }

    /// <summary>
    /// Removes a control and the drawing parts attached to it, which are returned. Refused while other structure depends on it:
    /// children, chains, measures or chain frames must be changed first, so nothing is discarded silently.
    /// </summary>
    public static IReadOnlyList<PuppetPart> RemoveControl(CharacterModel model, string id)
    {
        var owner = $"Model '{model.Id}'";
        if (model.Controls.FirstOrDefault(c => c.Parent == id) is { } child) throw new InvalidOperationException($"{owner}: '{id}' still has child '{child.Id}'.");
        if (model.IkChains.FirstOrDefault(c => c.Root == id || c.Joint == id || c.End == id || c.Frame == id) is { } chain) throw new InvalidOperationException($"{owner}: chain '{chain.Id}' uses '{id}'.");
        if (model.Measures.FirstOrDefault(m => m.Path.Contains(id)) is { } measure) throw new InvalidOperationException($"{owner}: measure '{measure.Id}' uses '{id}'.");
        if (model.Slots.FirstOrDefault(s => s.Bone == id) is { } slot) throw new InvalidOperationException($"{owner}: slot '{slot.Id}' uses '{id}'. Move or remove the slot first.");
        var parts = model.Parts.Where(p => p.A == id || p.B == id || p.Frame == id).ToList();
        model.Parts.RemoveAll(parts.Contains); model.Controls.RemoveAll(c => c.Id == id);
        foreach (var group in model.Groups) group.Targets.Remove(id);
        model.Validate(); return parts;
    }

    public static void Reparent(CharacterModel model, string id, string? parent)
    {
        var control = Control(model, id);
        if (control.Transform is not null)
        {
            var resolved = ResolvedModel.From(model);
            var frame = parent is null ? Matrix3x2.Identity : resolved.RestTransforms[parent];
            if (!Matrix3x2.Invert(frame, out var inverse)) throw new InvalidOperationException("Cannot reparent a bone beneath a collapsed parent transform.");
            control.Transform = Affine2D.FromMatrix(resolved.RestTransforms[id] * inverse);
        }
        control.Parent = parent; model.Validate();
        if (control.Transform is not null) SyncAffineRest(model);
    }

    /// <summary>Makes <paramref name="end"/> the tip of a two-bone chain over its parent and grandparent.</summary>
    public static ModelChain AddChain(CharacterModel model, string end) => AddIk(model, end, bone: false);

    /// <summary>Adds a typed bone-frame IK constraint. Both segment origins must meet their parent's +X tip.</summary>
    public static ModelChain AddBoneIk(CharacterModel model, string end) => AddIk(model, end, bone: true);

    private static ModelChain AddIk(CharacterModel model, string end, bool bone)
    {
        var joint = Control(model, end).Parent ?? throw new InvalidOperationException($"'{end}' needs a parent and grandparent to end a chain.");
        var root = Control(model, joint).Parent ?? throw new InvalidOperationException($"'{joint}' needs a parent to root a chain.");
        var chain = new ModelChain { Id = UniqueId(end + "-chain", model.IkChains.Select(c => c.Id)), Root = root, Joint = joint, End = end, Solver = bone ? ModelChain.BoneSolver : ModelChain.PointSolver };
        if (bone) model.Constraints.Add(chain); else model.Chains.Add(chain);
        // A group that owned the newly solved controls now owns the chain, whole.
        foreach (var group in model.Groups.Where(g => g.Targets.RemoveAll(t => t == joint || t == end) > 0)) group.Targets.Add(chain.Id);
        model.Validate(); return chain;
    }

    public static void RemoveChain(CharacterModel model, string id)
    {
        if (model.Chains.RemoveAll(c => c.Id == id) + model.Constraints.RemoveAll(c => c.Id == id) == 0) throw new InvalidOperationException($"No chain '{id}'.");
        foreach (var group in model.Groups) group.Targets.Remove(id);
        model.Validate();
    }

    /// <summary>A measure along a chain's two bones, which its channels can then scale with.</summary>
    public static ModelMeasure AddMeasure(CharacterModel model, string id, IReadOnlyList<string> path)
    {
        AuthoredAsset.RequireId(id, "measure id");
        var measure = new ModelMeasure { Id = id, Path = [.. path] };
        model.Measures.Add(measure); model.Validate(); return measure;
    }

    public static void RemoveMeasure(CharacterModel model, string id)
    {
        if (model.Controls.Any(c => c.Scale == id) || model.IkChains.Any(c => c.Scale == id)) throw new InvalidOperationException($"Measure '{id}' is still used as a scale.");
        model.Measures.RemoveAll(m => m.Id == id); model.Validate();
    }

    public static PuppetPart AddPart(CharacterModel model, string kind, string a, string? b = null)
    {
        var part = new PuppetPart { Id = UniqueId(kind, model.Parts.Select(p => p.Id)), Kind = kind, A = a, B = b, Face = "none" };
        if (PuppetPartKinds.IsStroke(kind)) part.Width = model.LineWidth;
        part.Geometry = PartGeometry.FromPreset(part);
        part.Material = PuppetPartKinds.IsStroke(kind) ? new RenderMaterialDefinition2D()
            : new RenderMaterialDefinition2D { Fill = "#fff8e7", Outline = new() };
        model.Parts.Add(part); model.Validate(); return part;
    }

    /// <summary>Creates a drawing part centered on a bone and carried by its local XY frame.</summary>
    public static PuppetPart AddPartToBone(CharacterModel model, string kind, string boneId)
    {
        if (PuppetPartKinds.IsStroke(kind)) throw new InvalidOperationException("Strokes connect two controls; choose their endpoints instead.");
        var bone = Control(model, boneId);
        if (bone.Length <= 0) throw new InvalidOperationException($"'{boneId}' has no bone length.");
        var part = AddPart(model, kind, boneId);
        part.Frame = boneId;
        part.OffsetX = bone.Length / 2;
        if (kind == PuppetPartKinds.Box) { part.Width = bone.Length; part.Height = .2f; }
        part.Geometry = PartGeometry.FromPreset(part);
        model.Validate();
        return part;
    }

    /// <summary>Changes drawing geometry while keeping the part ID. Face tracks may need repair when a shape becomes a stroke.</summary>
    public static void SetPartKind(CharacterModel model, string id, string kind)
    {
        EntityVocabulary.Require(kind, PuppetPartKinds.All, "part.kind");
        var part = model.Parts.First(p => p.Id == id);
        if (PuppetPartKinds.IsStroke(kind) && (part.B is null || part.B == part.A))
        {
            part.B = model.Controls.FirstOrDefault(control => control.Parent == part.A)?.Id
                ?? model.Controls.FirstOrDefault(control => control.Id != part.A)?.Id
                ?? throw new InvalidOperationException("A stroke needs a second control.");
        }

        if (PuppetPartKinds.IsStroke(kind)) part.Frame = null;
        part.Kind = kind;
        part.Geometry = PartGeometry.FromPreset(part);
        if (part.Geometry is ShapeDefinition2D && part.Material is { Fill: null, Outline: null })
            part.Material = new RenderMaterialDefinition2D { Fill = "#fff8e7", Outline = new() };
        model.Validate();
    }

    public static void RemovePart(CharacterModel model, string id)
    {
        if (model.Parts.RemoveAll(p => p.Id == id) == 0) throw new InvalidOperationException($"No part '{id}'.");
        foreach (var skin in model.Skins)
        {
            foreach (var entries in skin.Attachments.Values)
            foreach (var key in entries.Where(p => p.Value == id).Select(p => p.Key).ToList()) entries.Remove(key);
        }
    }

    /// <summary>Moves a control's rest position, and with <paramref name="children"/> its whole subtree by the same amount.</summary>
    public static void MoveRest(CharacterModel model, string id, Vector3 position, bool children)
    {
        if (Control(model, id).Transform is not null)
        {
            var resolved = ResolvedModel.From(model);
            var deltaAffine = position - resolved.Rest[id];
            var movedIds = (children ? Subtree(model.Controls, id) : [Control(model, id)]).Select(c => c.Id).ToHashSet();
            var frames = new Dictionary<string, Matrix3x2>();
            foreach (var control in resolved.Order)
            {
                var desired = resolved.Rest[control.Id] + (movedIds.Contains(control.Id) ? deltaAffine : Vector3.Zero);
                if (control.Transform is { } local)
                {
                    var parent = control.Parent is null ? Matrix3x2.Identity : frames[control.Parent];
                    if (!Matrix3x2.Invert(parent, out var inverse)) throw new InvalidOperationException("Cannot move a bone through a collapsed parent transform.");
                    var point = Vector2.Transform(new(desired.X, desired.Y), inverse); local.X = point.X; local.Y = point.Y;
                    frames[control.Id] = local.Matrix * parent;
                }
                else
                {
                    frames[control.Id] = resolved.RestTransforms[control.Id] with { M31 = desired.X, M32 = desired.Y };
                }

                control.Rest = PuppetPoint.From(desired);
            }
            return;
        }
        var delta = position - Control(model, id).Rest.XYZ;
        var moved = children ? Subtree(model.Controls, id) : [Control(model, id)];
        foreach (var control in moved) control.Rest = PuppetPoint.From(control.Rest.XYZ + delta);
    }

    /// <summary>Turns a bone in the rest pose, carrying descendant origins and their rest directions.</summary>
    public static void RotateRestBone(CharacterModel model, string id, float angle)
    {
        var bone = Control(model, id);
        if (bone.Transform is { } affine)
        {
            var resolved = ResolvedModel.From(model);
            var parent = bone.Parent is null ? Matrix3x2.Identity : resolved.RestTransforms[bone.Parent];
            if (!Matrix3x2.Invert(parent, out var inverse)) throw new InvalidOperationException("Cannot rotate a bone through a collapsed parent transform.");
            var axis = Vector2.TransformNormal(new(MathF.Cos(angle), MathF.Sin(angle)), inverse);
            affine.Rotation = MathF.Atan2(axis.Y, axis.X) - affine.ShearX - (affine.ScaleX < 0 ? MathF.PI : 0);
            SyncAffineRest(model);
            return;
        }
        var delta = angle - bone.RestAngle;
        var pivot = bone.Rest.XY;
        foreach (var control in Subtree(model.Controls, id))
        {
            if (control != bone)
                control.Rest = PuppetPoint.From(Rotation2D.ApplyXYAround(control.Rest.XYZ, pivot, delta));
            control.RestAngle += delta;
        }
        model.Validate();
    }

    /// <summary>Changes a segment's length, carrying the connected IK joint and its descendants to the new tip.</summary>
    public static void ResizeBone(CharacterModel model, string id, float length)
    {
        new Limit(.01f, 100).Check(length, "bone length");
        var bone = Control(model, id);
        var next = model.IkChains.Where(c => c.Solver == ModelChain.BoneSolver && (c.Root == id || c.Joint == id))
            .Select(c => c.Root == id ? c.Joint : c.End).Distinct().ToArray();
        if (next.Length == 0) { bone.Length = length; model.Validate(); return; }
        var resolved = ResolvedModel.From(model);
        var delta = new Vector3(Vector2.TransformNormal(new(length - bone.Length, 0), resolved.RestTransforms[id]), 0);
        bone.Length = length;
        foreach (var child in next)
        {
            if (Control(model, child).Transform is { } local) { local.X = length; local.Y = 0; }
            foreach (var member in Subtree(model.Controls, child)) member.Rest = PuppetPoint.From(resolved.Rest[member.Id] + delta);
        }
        SyncAffineRest(model); model.Validate();
    }

    /// <summary>
    /// Moves a control of a variant: writes rest overrides from its resolved positions, so unmoved controls keep following the base.
    /// </summary>
    public static void MoveRest(ResolvedModel resolved, ModelVariant variant, string id, Vector3 position, bool children)
    {
        var delta = position - resolved.Rest[id];
        var moved = children ? Subtree(resolved.Base.Controls, id) : [resolved.Controls[id]];
        foreach (var control in moved)
            variant.Rest[control.Id] = PuppetPoint.From(resolved.Rest[control.Id] + delta);
        if (!children && resolved.Controls[id].Transform is not null)
        {
            foreach (var child in Subtree(resolved.Base.Controls, id).Where(c => c.Id != id))
                variant.Rest[child.Id] = PuppetPoint.From(resolved.Rest[child.Id]);
        }
    }

    private static IEnumerable<ModelControl> Subtree(IReadOnlyList<ModelControl> controls, string id)
    {
        var members = new HashSet<string>(StringComparer.Ordinal) { id };
        // Parents precede children after enough passes; the tree is small, so repeat until nothing is added.
        for (var added = true; added;)
        {
            added = false;
            foreach (var control in controls) if (control.Parent is { } parent && members.Contains(parent) && members.Add(control.Id)) added = true;
        }
        return controls.Where(c => members.Contains(c.Id));
    }

    private static ModelControl Control(CharacterModel model, string id) =>
        model.Controls.FirstOrDefault(c => c.Id == id) ?? throw new InvalidOperationException($"Model '{model.Id}' has no control '{id}'.");

    public static void SyncAffineRest(CharacterModel model)
    {
        var resolved = ResolvedModel.From(model);
        foreach (var control in model.Controls.Where(c => c.Transform is not null))
        {
            control.Rest = PuppetPoint.From(resolved.Rest[control.Id]);
            var frame = resolved.RestTransforms[control.Id]; control.RestAngle = MathF.Atan2(frame.M12, frame.M11);
        }
    }
}
