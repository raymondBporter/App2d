using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
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
        var model = new CharacterModel { Id = id, Name = name };
        model.Validate();
        return model;
    }

    /// <summary>
    /// Flattens a resolved variant into a new independent base: every resolved setup, length, link and part becomes authored,
    /// and the parameters that produced them are dropped (the new base exposes none; declare its own). Its clips must be
    /// checked or converted explicitly.
    /// </summary>
    public static CharacterModel Flatten(ResolvedModel resolved, string id, string name)
    {
        var model = CharacterModel.FromJson(resolved.Base.ToJson());
        model.Id = id;
        model.Name = name;
        model.StructureRevision = 1;
        model.Parameters = [];
        model.Presets = [];
        foreach (var control in model.Controls)
        {
            var node = resolved.Nodes[control.Id];
            if (node.Kind == RigNodeKinds.Bone)
            {
                // A bone node's resolved setup, length and link (after parameter bindings) are its new authored setup.
                control.Local = node.Setup with { };
                control.Length = node.Length;
                control.Link = node.Link is { } link ? link with { } : null;
                control.Limit = node.Limit is { } limit ? limit with { } : null;
                continue;
            }

            control.Rest = PuppetPoint.From(resolved.Rest[control.Id]);
            if (resolved.SetupTransforms.TryGetValue(control.Id, out var setup))
            {
                control.Transform = setup with
                {
                };
            }
        }
        model.Parts = [.. resolved.Parts.Select(p => p with { })];
        // The variant's attachment overrides (swaps, placement, fit, materials) become the new base's instances; the assets stay references.
        model.Attachments = [.. resolved.Attachments.Select(a => a.Instance with { })];
        // Its animation-role overrides become the motion sets' assignments.
        foreach (var set in model.MotionSets)
        {
            foreach (var (role, clip) in resolved.RoleClips)
            {
                if (set.Roles.ContainsKey(role))
                {
                    set.Roles[role] = clip;
                }
            }
        }
        model.Validate();
        return model;
    }

    /// <summary>What decides clip compatibility: controls and parents, chains, frames and default scales. Proportions and appearance never change it.</summary>
    public static string StructureSignature(CharacterModel model) => string.Join("|",
        model.Controls.Select(c => $"c:{c.Id}<{c.Parent}~{c.Scale}~node:{c.Node}").Order(StringComparer.Ordinal)
            .Concat(model.IkChains.Select(c => $"k:{c.Id}={c.Root}>{c.Joint}>{c.End}@{c.Frame}~{c.Scale}~{c.Solver}").Order(StringComparer.Ordinal))
            .Concat(model.Measures.Select(m => $"m:{m.Id}").Order(StringComparer.Ordinal))
            .Concat(model.Slots.Select(s => $"s:{s.Id}={s.Bone}").Order(StringComparer.Ordinal)));

    // ---- Parameters (P4a) --------------------------------------------------------------------------------------------

    /// <summary>Declares a parameter on the model; the model validates the declaration and the catalog as a whole.</summary>
    public static ModelParameter AddParameter(CharacterModel model, string id, string name, string units, float @default, float min, float max)
    {
        if (model.Parameters.Any(p => p.Id == id))
        {
            throw new InvalidDataException($"Model '{model.Id}' already declares parameter '{id}'.");
        }

        var parameter = new ModelParameter { Id = id, Name = name, Units = units, Default = @default, Min = min, Max = max };
        model.Parameters.Add(parameter);
        model.Validate();
        return parameter;
    }

    /// <summary>Removes a parameter with its bindings, and its values from every preset.</summary>
    public static void RemoveParameter(CharacterModel model, string id)
    {
        if (model.Parameters.RemoveAll(p => p.Id == id) == 0)
        {
            throw new InvalidDataException($"Model '{model.Id}' declares no parameter '{id}'.");
        }

        foreach (var preset in model.Presets)
        {
            preset.Values.Remove(id);
        }

        model.Validate();
    }

    /// <summary>Binds an exposed property to a parameter; the catalog refuses a property that is bound already, not exposed or part of a cycle.</summary>
    public static ParameterBinding AddBinding(CharacterModel model, string parameter, ParameterBinding binding)
    {
        var declared = model.Parameters.FirstOrDefault(p => p.Id == parameter) ?? throw new InvalidDataException($"Model '{model.Id}' declares no parameter '{parameter}'.");
        declared.Bindings.Add(binding);
        model.Validate();
        return binding;
    }

    /// <summary>Removes the binding on a property from whichever parameter holds it; the property keeps its authored value.</summary>
    public static void RemoveBinding(CharacterModel model, PropertyRef target)
    {
        var removed = model.Parameters.Sum(p => p.Bindings.RemoveAll(b => b.Target.Key == target.Key));
        if (removed == 0)
        {
            throw new InvalidDataException($"Model '{model.Id}': {target} is not bound.");
        }

        model.Validate();
    }

    /// <summary>Writes a preset's values onto a variant, replacing its parameter values; values equal to the declared default are not stored.</summary>
    public static void ApplyPreset(CharacterModel model, ModelVariant variant, string preset)
    {
        var values = (model.Presets.FirstOrDefault(p => p.Id == preset) ?? throw new InvalidDataException($"Model '{model.Id}' has no preset '{preset}'.")).Values;
        variant.Parameters.Clear();
        foreach (var (id, value) in values)
        {
            var parameter = model.Parameters.FirstOrDefault(p => p.Id == id) ?? throw new InvalidDataException($"Model '{model.Id}' preset '{preset}': no parameter '{id}'.");
            if (value != parameter.Default)
            {
                variant.Parameters[id] = value;
            }
        }
    }

    // ---- Attachment instances (P5a) ------------------------------------------------------------------------------------

    /// <summary>Instances a library part on a socket with a new stable instance ID. The asset must exist in <paramref name="partOf"/>; the socket on the model.</summary>
    public static PartInstance AddAttachment(CharacterModel model, string asset, string socket, Func<string, PartAsset?> partOf, string? id = null, bool mirror = false)
    {
        if (partOf(asset) is null)
        {
            throw new InvalidDataException($"No part asset '{asset}'.");
        }

        var instance = new PartInstance { Id = id ?? UniqueId(asset, model.Attachments.Select(a => a.Id).Concat(model.Parts.Select(p => p.Id))), Asset = asset, Socket = socket, Mirror = mirror };
        model.Attachments.Add(instance);
        try
        {
            model.Validate();
        }
        catch
        {
            model.Attachments.Remove(instance);
            throw;
        }

        return instance;
    }

    /// <summary>Removes an instance from the base. A variant override of it becomes an error on that variant, reported by name, never silently dropped.</summary>
    public static void RemoveAttachment(CharacterModel model, string id)
    {
        if (model.Attachments.RemoveAll(a => a.Id == id) == 0)
        {
            throw new InvalidDataException($"Model '{model.Id}' has no attachment '{id}'.");
        }

        model.Validate();
    }

    /// <summary>Replaces the library asset of one base instance; every wearer that does not override it follows.</summary>
    public static void SetAttachmentAsset(CharacterModel model, string id, string asset, Func<string, PartAsset?> partOf)
    {
        var instance = model.Attachments.FirstOrDefault(a => a.Id == id) ?? throw new InvalidDataException($"Model '{model.Id}' has no attachment '{id}'.");
        if (partOf(asset) is null)
        {
            throw new InvalidDataException($"No part asset '{asset}'.");
        }

        var previous = instance.Asset;
        instance.Asset = asset;
        try
        {
            model.Validate();
        }
        catch
        {
            instance.Asset = previous;
            throw;
        }
    }

    /// <summary>
    /// Edits one wearer's view of an attachment instance: the fields <paramref name="change"/> alters on the resolved instance
    /// become typed overrides of this variant (asset swap, placement, fit, mirror, material, hidden); untouched fields keep
    /// inheriting. A material edit merges into the existing material override.
    /// </summary>
    public static void EditAttachmentOverride(ModelVariant variant, ResolvedModel resolved, string id, Action<PartInstance> change)
    {
        var current = resolved.Attachment(id)?.Instance ?? throw new InvalidDataException($"'{resolved.Id}' has no attachment '{id}'.");
        var edited = current with { };
        change(edited);
        var changes = new List<PropertyOverride>();
        PropertyRef Target(string property) => PropertyRef.OfAttachment(id, property);
        if (edited.Asset != current.Asset) changes.Add(PropertyOverride.Of(Target(RigProperties.Asset), edited.Asset));
        if (edited.X != current.X) changes.Add(PropertyOverride.Of(Target(RigProperties.X), edited.X));
        if (edited.Y != current.Y) changes.Add(PropertyOverride.Of(Target(RigProperties.Y), edited.Y));
        if (edited.Angle != current.Angle) changes.Add(PropertyOverride.Of(Target(RigProperties.Angle), edited.Angle));
        if (edited.Scale != current.Scale) changes.Add(PropertyOverride.Of(Target(RigProperties.Scale), edited.Scale));
        if (edited.Mirror != current.Mirror) changes.Add(PropertyOverride.Of(Target(RigProperties.Mirror), edited.Mirror));
        if (edited.Layer != current.Layer) changes.Add(PropertyOverride.Of(Target(RigProperties.Layer), edited.Layer));
        if (edited.FitX != current.FitX) changes.Add(PropertyOverride.Of(Target(RigProperties.FitX), edited.FitX));
        if (edited.FitY != current.FitY) changes.Add(PropertyOverride.Of(Target(RigProperties.FitY), edited.FitY));
        if (edited.Hidden != current.Hidden) changes.Add(PropertyOverride.Of(Target(RigProperties.Hidden), edited.Hidden));
        if (edited.Expression != current.Expression) changes.Add(edited.Expression is null ? PropertyOverride.Cleared(Target(RigProperties.Expression)) : PropertyOverride.Of(Target(RigProperties.Expression), edited.Expression));
        if (!Equals(edited.Material, current.Material))
        {
            var target = Target(RigProperties.Material);
            if (edited.Material is null)
            {
                changes.Add(PropertyOverride.Cleared(target));
            }
            else
            {
                var existing = variant.Override(target)?.Material;
                changes.Add(PropertyOverride.Of(target, existing is null ? edited.Material : existing.WithOverride(edited.Material)));
            }
        }
        SetOverrides(variant, resolved.Base, changes);
    }

    /// <summary>
    /// Sets one explicit override on a variant, checked against the base's property registry (the owner exists, the property
    /// is exposed, the value matches its kind, range and vocabulary); the variant is restored when the check fails.
    /// </summary>
    public static void SetOverride(ModelVariant variant, CharacterModel basis, PropertyOverride change) => SetOverrides(variant, basis, [change]);

    private static void SetOverrides(ModelVariant variant, CharacterModel basis, IReadOnlyList<PropertyOverride> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var before = variant.Overrides.ConvertAll(o => o with { });
        try
        {
            foreach (var change in changes)
            {
                var field = $"Variant '{variant.Id}' overrides";
                change.Validate(field);
                change.Check(RigProperties.Declaration(change.Target, basis, field), field);
                variant.Set(change);
            }
            variant.Validate();
        }
        catch
        {
            variant.Overrides = before;
            throw;
        }
    }

    /// <summary>Removes a variant's override so the property inherits again; nothing is written in its place.</summary>
    public static bool ResetOverride(ModelVariant variant, PropertyRef target) => variant.Reset(target);

    /// <summary>Removes every override on one owner (a control, part, attachment or role).</summary>
    public static int ResetOverrides(ModelVariant variant, string ownerKind, string owner) => variant.Overrides.RemoveAll(o => o.Target.OwnerKind == ownerKind && o.Target.Owner == owner);

    /// <summary>Explicitly clears a property on a variant: the authored value before any binding for a number, the empty value otherwise.</summary>
    public static void ClearProperty(ModelVariant variant, CharacterModel basis, PropertyRef target) => SetOverride(variant, basis, PropertyOverride.Cleared(target));

    public static string UniqueId(string basis, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.Ordinal);
        var id = basis;
        for (var index = 2; used.Contains(id); index++)
        {
            id = $"{basis}-{index}";
        }

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
            if (!Matrix3x2.Invert(frame, out var inverse))
            {
                throw new InvalidOperationException("Cannot add a bone beneath a collapsed parent transform.");
            }

            var local = Vector2.Transform(new(rest.X, rest.Y), inverse);
            control.Node = RigNodeKinds.Affine;
            control.Transform = new()
            {
                X = local.X,
                Y = local.Y
            };
        }
        model.Controls.Add(control);
        model.Validate();
        return control;
    }

    /// <summary>
    /// Adds a canonical bone node at a parent's tip, continuing its direction: bound to the tip when the parent is a bone with
    /// a length, else a local offset along the parent's +X. Its own local +X axis points toward its tip. Beneath a legacy
    /// affine node it adds an affine child so that rig stays editable by its adapter.
    /// </summary>
    /// <summary>
    /// Declares, changes or removes a bone node's angular joint limits (P4b). Null removes them. Validation refuses a point
    /// or affine node, a rest rotation outside the range and a node solved by a point constraint, each by name.
    /// </summary>
    public static void SetLimit(CharacterModel model, string id, RigLimit? limit)
    {
        var control = Control(model, id);
        if (limit is not null && control.Node != RigNodeKinds.Bone)
        {
            throw new InvalidOperationException($"'{id}' is a {control.Node} node; joint limits belong to bone nodes. Convert the run to a bone chain first.");
        }

        var before = control.Limit;
        control.Limit = limit is null ? null : limit with { };
        try
        {
            model.Validate();
        }
        catch (InvalidDataException)
        {
            control.Limit = before;
            throw;
        }
    }

    public static ModelControl AddBone(CharacterModel model, string? parent, string? id = null)
    {
        var basis = parent is null ? null : Control(model, parent);
        if (basis is { Node: RigNodeKinds.Affine })
        {
            var origin = basis.Rest.XYZ + Rotation2D.ApplyXY(new(MathF.Max(basis.Length, .3f), 0, 0), basis.RestAngle);
            var affine = AddControl(model, parent, origin, id ?? UniqueId("bone", Names(model)));
            affine.RestAngle = basis.RestAngle;
            affine.Length = .5f;
            affine.Transform = new() { X = MathF.Max(basis.Length, .3f) };
            SyncAffineRest(model);
            model.Validate();
            return affine;
        }
        var bone = new ModelControl { Id = id ?? UniqueId("bone", Names(model)), Parent = parent, Node = RigNodeKinds.Bone, Length = .5f };
        if (basis is { Length: > 0 })
        {
            bone.Link = new RigLink();
        }
        else if (basis is not null)
        {
            bone.Local = new RigSetup { X = .3f };
        }

        model.Controls.Add(bone);
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
        if (model.Controls.FirstOrDefault(c => c.Parent == id) is { } child)
        {
            throw new InvalidOperationException($"{owner}: '{id}' still has child '{child.Id}'.");
        }

        if (model.IkChains.FirstOrDefault(c => c.Root == id || c.Joint == id || c.End == id || c.Frame == id) is { } chain)
        {
            throw new InvalidOperationException($"{owner}: chain '{chain.Id}' uses '{id}'.");
        }

        if (model.Measures.FirstOrDefault(m => m.Path.Contains(id)) is { } measure)
        {
            throw new InvalidOperationException($"{owner}: measure '{measure.Id}' uses '{id}'.");
        }

        // A part's bindings and feature payloads answer for their own control references (P2c); the command names the owner.
        foreach (var part in model.Parts)
        {
            if (part.Dependency(id) is { } dependency)
            {
                throw new InvalidOperationException($"{owner}: part '{part.Id}' uses '{id}' as a {dependency}; detach it first.");
            }
        }

        if (model.Slots.FirstOrDefault(s => s.Bone == id) is { } slot)
        {
            throw new InvalidOperationException($"{owner}: slot '{slot.Id}' uses '{id}'. Move or remove the slot first.");
        }

        var parts = model.Parts.Where(p => p.AttachedTo(id)).ToList();
        model.Parts.RemoveAll(parts.Contains);
        model.Controls.RemoveAll(c => c.Id == id);
        foreach (var group in model.Groups)
        {
            group.Targets.Remove(id);
        }

        model.Validate();
        return parts;
    }

    public static void Reparent(CharacterModel model, string id, string? parent)
    {
        var control = Control(model, id);
        if (control.Node == RigNodeKinds.Bone)
        {
            // The bone keeps its character frame: its setup is re-expressed in the new parent's frame and any link is dropped.
            var nodes = RigGraph.Compile(model);
            var node = nodes[id];
            var target = parent is null ? null : nodes[parent];
            if (target is { Kind: RigNodeKinds.Affine })
            {
                throw new InvalidOperationException($"Cannot hang bone '{id}' from the affine node '{parent}'; the legacy affine adapter carries no canonical bones.");
            }

            var frame = target?.RestFrame ?? Matrix3x2.Identity;
            if (!new Frame2D(frame).TryToLocal(new(node.Rest.X, node.Rest.Y), out var local))
            {
                throw new InvalidOperationException("Cannot reparent a bone beneath a collapsed parent.");
            }

            var parentScale = target is null ? 1 : Vector2.TransformNormal(Vector2.UnitX, frame).Length();
            var ownScale = Vector2.TransformNormal(Vector2.UnitX, node.RestFrame).Length();
            var parentHand = target?.Handedness ?? 1;
            control.Link = null;
            control.Local = new RigSetup
            {
                X = local.X,
                Y = local.Y,
                Rotation = parentHand * (node.RestAngle - (target?.RestAngle ?? 0)),
                Scale = ownScale / parentScale,
                Reflect = node.Handedness != parentHand,
                Layer = node.RestLayer - (target?.RestLayer ?? 0),
            };
            control.Parent = parent;
            model.Validate();
            return;
        }
        if (control.Transform is not null)
        {
            var resolved = ResolvedModel.From(model);
            var frame = parent is null ? Matrix3x2.Identity : resolved.RestTransforms[parent];
            if (!Matrix3x2.Invert(frame, out var inverse))
            {
                throw new InvalidOperationException("Cannot reparent a bone beneath a collapsed parent transform.");
            }

            control.Transform = Affine2D.FromMatrix(resolved.RestTransforms[id] * inverse);
        }
        control.Parent = parent;
        model.Validate();
        if (control.Transform is not null)
        {
            SyncAffineRest(model);
        }
    }

    /// <summary>Converts a parent-to-child run of point controls into canonical bone nodes; see <see cref="RigConversion.ToBoneChain"/>.</summary>
    public static IReadOnlyList<string> ConvertToBoneChain(CharacterModel model, IReadOnlyList<string> controls) => RigConversion.ToBoneChain(model, controls);

    /// <summary>Makes <paramref name="end"/> the tip of a two-bone chain over its parent and grandparent.</summary>
    public static ModelChain AddChain(CharacterModel model, string end) => AddIk(model, end, bone: false);

    /// <summary>Adds a typed bone-frame IK constraint. Both segment origins must meet their parent's +X tip.</summary>
    public static ModelChain AddBoneIk(CharacterModel model, string end) => AddIk(model, end, bone: true);

    private static ModelChain AddIk(CharacterModel model, string end, bool bone)
    {
        var joint = Control(model, end).Parent ?? throw new InvalidOperationException($"'{end}' needs a parent and grandparent to end a chain.");
        var root = Control(model, joint).Parent ?? throw new InvalidOperationException($"'{joint}' needs a parent to root a chain.");
        var chain = new ModelChain { Id = UniqueId(end + "-chain", model.IkChains.Select(c => c.Id)), Root = root, Joint = joint, End = end, Solver = bone ? ModelChain.BoneSolver : ModelChain.PointSolver };
        model.Constraints.Add(chain);
        // A group that owned the newly solved controls now owns the chain, whole.
        foreach (var group in model.Groups.Where(g => g.Targets.RemoveAll(t => t == joint || t == end) > 0))
        {
            group.Targets.Add(chain.Id);
        }

        model.Validate();
        return chain;
    }

    public static void RemoveChain(CharacterModel model, string id)
    {
        if (model.Constraints.RemoveAll(c => c.Id == id) == 0)
        {
            throw new InvalidOperationException($"No chain '{id}'.");
        }

        foreach (var group in model.Groups)
        {
            group.Targets.Remove(id);
        }

        model.Validate();
    }

    /// <summary>A measure along a chain's two bones, which its channels can then scale with.</summary>
    public static ModelMeasure AddMeasure(CharacterModel model, string id, IReadOnlyList<string> path)
    {
        AuthoredAsset.RequireId(id, "measure id");
        var measure = new ModelMeasure { Id = id, Path = [.. path] };
        model.Measures.Add(measure);
        model.Validate();
        return measure;
    }

    public static void RemoveMeasure(CharacterModel model, string id)
    {
        if (model.Controls.Any(c => c.Scale == id) || model.IkChains.Any(c => c.Scale == id))
        {
            throw new InvalidOperationException($"Measure '{id}' is still used as a scale.");
        }

        model.Measures.RemoveAll(m => m.Id == id);
        model.Validate();
    }

    public static PuppetPart AddPart(CharacterModel model, IGeometryValue2D geometry, string a, string? b = null)
    {
        var part = new PuppetPart
        {
            Id = UniqueId(geometry.Kind, model.Parts.Select(p => p.Id)),
            Geometry = geometry,
            A = a,
            B = b,
            Material = geometry is ICurve2D ? new RenderMaterialDefinition2D()
                : new RenderMaterialDefinition2D { Fill = "#fff8e7", Outline = new() }
        };
        model.Parts.Add(part);
        model.Validate();
        return part;
    }

    /// <summary>Creates a drawing part centered on a bone and carried by its local XY frame.</summary>
    public static PuppetPart AddPartToBone(CharacterModel model, IShape2D geometry, string boneId)
    {
        var bone = Control(model, boneId);
        if (bone.Length <= 0)
        {
            throw new InvalidOperationException($"'{boneId}' has no bone length.");
        }

        var part = AddPart(model, geometry, boneId);
        part.Frame = boneId;
        part.OffsetX = bone.Length / 2;
        if (geometry is RoundedRectangle2D rounded)
        {
            part.Geometry = rounded.WithSize(new(bone.Length, .2f));
        }
        model.Validate();
        return part;
    }

    /// <summary>Replaces the drawing geometry while keeping its identity, placement and material.</summary>
    public static void SetPartGeometry(CharacterModel model, string id, IGeometryValue2D geometry)
    {
        var part = model.Parts.First(p => p.Id == id);
        if (part.Geometry is RoundedPolylineCurve2D)
        {
            // A rounded path owns its bindings; replacing the geometry drops the path.
            part.Bindings = null;
        }

        if (part.Bindings is { Count: > 0 })
        {
            throw new InvalidOperationException("Detach point bindings before replacing the geometry.");
        }

        if (geometry is ICurve2D && (part.B is null || part.B == part.A))
        {
            part.B = model.Controls.FirstOrDefault(control => control.Parent == part.A)?.Id
                ?? model.Controls.FirstOrDefault(control => control.Id != part.A)?.Id
                ?? throw new InvalidOperationException("A curve needs a second control.");
        }

        if (geometry is ICurve2D)
        {
            part.Frame = null;
        }

        part.Geometry = geometry;
        // Each feature payload decides whether the new geometry can carry it; the command knows no wave or face rule.
        part.DropUnsupportedPayloads();
        if (part.Geometry is IShape2D && part.Material is { Fill: null, Outline: null })
        {
            part.Material = new RenderMaterialDefinition2D { Fill = "#fff8e7", Outline = new() };
        }

        model.Validate();
    }

    /// <summary>Authors a drawing path over existing controls. It consumes the pose; it does not create or solve IK.</summary>
    public static void SetRoundedPath(PuppetPart part, IReadOnlyList<string> controls, float softness = 1)
    {
        if (controls.Count is < 2 or > 64 || controls.Any(string.IsNullOrWhiteSpace) || controls.Distinct().Count() != controls.Count)
        {
            throw new InvalidDataException("A drawing path needs 2 to 64 distinct controls.");
        }

        var points = Enumerable.Range(0, controls.Count).Select(i => new Vector2(0, i / (float)(controls.Count - 1)));
        part.A = controls[0];
        part.B = controls[^1];
        part.Frame = null;
        part.Geometry = RoundedPolylineCurve2D.Builder.Create(points, softness);
        part.Bindings = [.. controls.Select((id, i) => new PartPointBinding { Point = i, Control = id })];
    }

    public static void SetCutout(PuppetPart part, IReadOnlyList<Vector2> points)
    {
        CheckCutout(points);
        part.Geometry = new SimplePolygon2D { Vertices = [.. points] }.Validated();
    }

    public static void CheckCutout(IReadOnlyList<Vector2>? points)
    {
        if (points is not { Count: >= 3 and <= 64 })
        {
            throw new InvalidDataException("A cutout needs 3 to 64 perimeter points.");
        }

        foreach (var point in points)
        {
            new Limit(-10000, 10000).Check(point.X, "cutout.point.x");
            new Limit(-10000, 10000).Check(point.Y, "cutout.point.y");
        }
        try
        {
            PolygonTriangulation2D.Triangulate(points, 1e-8);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("Cutout must be a simple polygon with area: " + ex.Message, ex);
        }
    }

    /// <summary>Edits the perimeter and remaps attachments in the same transaction. New points inherit their preceding attachment.</summary>
    public static void InsertCutoutPoint(PuppetPart part, int after)
    {
        var points = new List<Vector2>(PartContours2D.CutoutPoints(part));
        points.Insert(after + 1, Vector2.Lerp(points[after], points[(after + 1) % points.Count], .5f));
        CheckCutout(points);
        var bindings = (part.Bindings ?? []).ConvertAll(b => b with { Point = b.Point > after ? b.Point + 1 : b.Point });
        if (part.Bindings?.FirstOrDefault(b => b.Point == after) is { } source)
        {
            bindings.Add(source with
            {
                Point = after + 1
            });
        }

        SetCutout(part, points);
        part.Bindings = bindings.Count > 0 ? bindings : null;
    }

    public static void RemoveCutoutPoint(PuppetPart part, int index)
    {
        var points = new List<Vector2>(PartContours2D.CutoutPoints(part));
        points.RemoveAt(index);
        CheckCutout(points);
        SetCutout(part, points);
        part.Bindings = part.Bindings?.Where(b => b.Point != index).Select(b => b with { Point = b.Point > index ? b.Point - 1 : b.Point }).ToList();
    }

    public static void RemovePart(CharacterModel model, string id)
    {
        if (model.Parts.RemoveAll(p => p.Id == id) == 0)
        {
            throw new InvalidOperationException($"No part '{id}'.");
        }

        foreach (var skin in model.Skins)
        {
            foreach (var entries in skin.Attachments.Values)
            {
                foreach (var key in entries.Where(p => p.Value == id).Select(p => p.Key).ToList())
                {
                    entries.Remove(key);
                }
            }
        }
    }

    /// <summary>
    /// Moves a control's rest position, and with <paramref name="children"/> its whole subtree by the same amount. A bone
    /// node moves through its local setup (its bound origin cannot move: change the parent's length or the link); point
    /// descendants keep or shift their character-frame rest by the flag, bone descendants follow their parent unless the
    /// flag is off, when their local setup is re-expressed so they stay in place.
    /// </summary>
    public static void MoveRest(CharacterModel model, string id, Vector3 position, bool children)
    {
        var moving = Control(model, id);
        if (moving.Node == RigNodeKinds.Bone)
        {
            if (moving.Link is not null)
            {
                throw new InvalidOperationException($"'{id}' is bound to '{moving.Parent}' at t = {moving.Link.T:0.##}; change the parent's length or the link instead of its origin.");
            }

            var nodes = RigGraph.Compile(model);
            var before = nodes[id].Rest;
            var parentNode = moving.Parent is null ? null : nodes[moving.Parent];
            if (!new Frame2D(parentNode?.RestFrame ?? Matrix3x2.Identity).TryToLocal(new(position.X, position.Y), out var local))
            {
                throw new InvalidOperationException("Cannot move a bone beneath a collapsed parent.");
            }

            var setup = moving.Local ?? new RigSetup();
            setup.X = local.X;
            setup.Y = local.Y;
            setup.Layer = position.Z - (parentNode?.RestLayer ?? 0);
            moving.Local = setup;
            CarryDescendants(model, id, position - before, children, nodes);
            model.Validate();
            return;
        }
        if (moving.Transform is not null)
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
                    if (!Matrix3x2.Invert(parent, out var inverse))
                    {
                        throw new InvalidOperationException("Cannot move a bone through a collapsed parent transform.");
                    }

                    var point = Vector2.Transform(new(desired.X, desired.Y), inverse);
                    local.X = point.X;
                    local.Y = point.Y;
                    frames[control.Id] = local.Matrix * parent;
                }
                else
                {
                    frames[control.Id] = resolved.RestTransforms[control.Id] with
                    {
                        M31 = desired.X,
                        M32 = desired.Y
                    };
                }

                control.Rest = PuppetPoint.From(desired);
            }
            return;
        }
        var rest = RigGraph.Compile(model);
        var delta = position - moving.Rest.XYZ;
        moving.Rest = PuppetPoint.From(position);
        CarryDescendants(model, id, delta, children, rest);
        model.Validate();
    }

    /// <summary>
    /// After a control moved by <paramref name="delta"/>: with <paramref name="children"/>, point descendants shift with it
    /// (bone descendants follow through the hierarchy); without, bone children re-express their local setup against the
    /// moved parent so every descendant keeps its character-frame rest. A bound child always follows its parent.
    /// </summary>
    private static void CarryDescendants(CharacterModel model, string id, Vector3 delta, bool children, IReadOnlyDictionary<string, RigNode> before)
    {
        if (children)
        {
            foreach (var control in Subtree(model.Controls, id).Where(c => c.Id != id && c.Node == RigNodeKinds.Point))
            {
                control.Rest = PuppetPoint.From(control.Rest.XYZ + delta);
            }

            return;
        }
        var after = RigGraph.Compile(model);
        foreach (var child in model.Controls.Where(c => c.Parent == id && c.Node == RigNodeKinds.Bone && c.Link is null))
        {
            var keep = before[child.Id].Rest;
            if (!new Frame2D(after[id].RestFrame).TryToLocal(new(keep.X, keep.Y), out var local))
            {
                throw new InvalidOperationException("Cannot keep a bone in place beneath a collapsed parent.");
            }

            var setup = child.Local ?? new RigSetup();
            setup.X = local.X;
            setup.Y = local.Y;
            setup.Layer = keep.Z - after[id].RestLayer;
            child.Local = setup;
        }
    }

    /// <summary>Turns a bone in the rest pose to a character-frame direction, carrying descendant origins and their rest directions.</summary>
    public static void RotateRestBone(CharacterModel model, string id, float angle)
    {
        var bone = Control(model, id);
        if (bone.Node == RigNodeKinds.Bone)
        {
            var nodes = RigGraph.Compile(model);
            var node = nodes[id];
            var parentNode = bone.Parent is null ? null : nodes[bone.Parent];
            var setup = bone.Local ?? new RigSetup();
            setup.Rotation = node.CharacterOriented ? angle : (parentNode?.Handedness ?? 1) * (angle - (parentNode?.RestAngle ?? 0));
            bone.Local = setup;
            // Point descendants are authored in the character frame, so they turn about the pivot explicitly; bone and affine
            // descendants are parent-local and follow.
            var about = new Vector2(node.Rest.X, node.Rest.Y);
            var turn = angle - node.RestAngle;
            foreach (var control in Subtree(model.Controls, id).Where(c => c.Id != id && c.Node == RigNodeKinds.Point))
            {
                control.Rest = PuppetPoint.From(Rotation2D.ApplyXYAround(control.Rest.XYZ, about, turn));
                control.RestAngle += turn;
            }

            model.Validate();
            return;
        }
        if (bone.Transform is { } affine)
        {
            var resolved = ResolvedModel.From(model);
            var parent = bone.Parent is null ? Matrix3x2.Identity : resolved.RestTransforms[bone.Parent];
            if (!Matrix3x2.Invert(parent, out var inverse))
            {
                throw new InvalidOperationException("Cannot rotate a bone through a collapsed parent transform.");
            }

            var axis = Vector2.TransformNormal(new(MathF.Cos(angle), MathF.Sin(angle)), inverse);
            affine.Rotation = MathF.Atan2(axis.Y, axis.X) - affine.ShearX - (affine.ScaleX < 0 ? MathF.PI : 0);
            SyncAffineRest(model);
            return;
        }
        var delta = angle - bone.RestAngle;
        var pivot = bone.Rest.XY;
        foreach (var control in Subtree(model.Controls, id).Where(c => c.Node == RigNodeKinds.Point))
        {
            if (control != bone)
            {
                control.Rest = PuppetPoint.From(Rotation2D.ApplyXYAround(control.Rest.XYZ, pivot, delta));
            }

            control.RestAngle += delta;
        }
        model.Validate();
    }

    /// <summary>
    /// Changes a segment's length. Children bound to the bone follow the new tip through their links without any frame
    /// stretching; for legacy point and affine bones the connected IK joint and its descendants are carried to the new tip.
    /// </summary>
    public static void ResizeBone(CharacterModel model, string id, float length)
    {
        new Limit(.01f, 100).Check(length, "bone length");
        var bone = Control(model, id);
        if (bone.Node == RigNodeKinds.Bone)
        {
            bone.Length = length;
            model.Validate();
            return;
        }
        var next = model.IkChains.Where(c => c.Solver == ModelChain.BoneSolver && (c.Root == id || c.Joint == id))
            .Select(c => c.Root == id ? c.Joint : c.End).Where(child => Control(model, child) is not { Node: RigNodeKinds.Bone, Link: not null }).Distinct().ToArray();
        if (next.Length == 0)
        {
            bone.Length = length;
            model.Validate();
            return;
        }
        var resolved = ResolvedModel.From(model);
        var delta = new Vector3(Vector2.TransformNormal(new(length - bone.Length, 0), resolved.RestTransforms[id]), 0);
        bone.Length = length;
        foreach (var child in next)
        {
            if (Control(model, child).Transform is { } local)
            {
                local.X = length;
                local.Y = 0;
            }
            foreach (var member in Subtree(model.Controls, child))
            {
                member.Rest = PuppetPoint.From(resolved.Rest[member.Id] + delta);
            }
        }
        SyncAffineRest(model);
        model.Validate();
    }

    /// <summary>
    /// Moves a control of a variant to a character-frame rest position by overriding its parent-local setup (<c>local.x</c>,
    /// <c>local.y</c>, <c>local.layer</c>; only the components that change), never by freezing an absolute position. With
    /// <paramref name="children"/> the subtree follows through the hierarchy and no descendant is touched; without, every
    /// unbound child gets its own local setup override so it stays in place, and a bound child follows its parent as it
    /// must. A bound origin and a legacy affine node are refused with the reason.
    /// </summary>
    public static void MoveRest(ResolvedModel resolved, ModelVariant variant, string id, Vector3 position, bool children)
    {
        var node = resolved.Nodes.GetValueOrDefault(id) ?? throw new InvalidOperationException($"'{resolved.Id}' has no control '{id}'.");
        if (node.Kind == RigNodeKinds.Affine)
        {
            throw new InvalidOperationException($"'{id}' is a legacy affine node; the adapter exposes no variant-overridable setup until P11. Edit the base model.");
        }

        if (node.Link is { } link)
        {
            throw new InvalidOperationException($"'{id}' is bound to '{node.Parent}' at t = {link.T:0.##}; override the parent's length or the link instead of its origin.");
        }

        var parent = node.Parent is null ? null : resolved.Nodes[node.Parent];
        var local = LocalTranslation(parent, new(position.X, position.Y));
        var changes = new List<PropertyOverride>();
        void Move(string control, RigSetup setup, Vector2 translation, float layer)
        {
            if (translation.X != setup.X) changes.Add(PropertyOverride.Of(PropertyRef.OfControl(control, RigProperties.LocalX), translation.X));
            if (translation.Y != setup.Y) changes.Add(PropertyOverride.Of(PropertyRef.OfControl(control, RigProperties.LocalY), translation.Y));
            if (layer != setup.Layer) changes.Add(PropertyOverride.Of(PropertyRef.OfControl(control, RigProperties.LocalLayer), layer));
        }
        Move(id, node.Setup, local, position.Z - (parent?.RestLayer ?? 0));
        if (!children)
        {
            // The moved node's new frame, from its new translation beneath the unchanged parent: unbound children re-express
            // their setup against it so their character rest stays; bound children have no independent position.
            var moved = node with { Setup = node.Setup with { X = local.X, Y = local.Y, Layer = position.Z - (parent?.RestLayer ?? 0) } };
            var frame = parent is null ? LayeredFrame2D.Local(moved.Setup.ScaleVector, moved.Setup.Rotation, moved.Setup.Translation, 0).Frame.Matrix
                : parent.Kind == RigNodeKinds.Affine
                    ? Matrix3x2.CreateRotation(moved.RestAngle) * Matrix3x2.CreateTranslation(position.X, position.Y)
                    : node.Kind == RigNodeKinds.Point
                        ? Matrix3x2.CreateRotation(moved.RestAngle) * Matrix3x2.CreateTranslation(position.X, position.Y)
                        : LayeredFrame2D.Local(moved.Setup.ScaleVector, moved.Setup.Rotation, moved.Setup.Translation, 0).Frame.Matrix * parent.RestFrame;
            var movedNode = moved with { RestFrame = frame, RestLayer = position.Z };
            foreach (var child in resolved.Base.Controls.Where(c => c.Parent == id && c.Node != RigNodeKinds.Affine))
            {
                var childNode = resolved.Nodes[child.Id];
                if (childNode.Link is not null)
                {
                    continue;
                }

                var keep = childNode.Rest;
                Move(child.Id, childNode.Setup, LocalTranslation(movedNode, new(keep.X, keep.Y)), keep.Z - position.Z);
            }
        }
        SetOverrides(variant, resolved.Base, changes);
    }

    /// <summary>A character-frame position in a parent node's local axes, by the compiler's rule for that parent kind.</summary>
    private static Vector2 LocalTranslation(RigNode? parent, Vector2 position)
    {
        if (parent is null)
        {
            return position;
        }

        if (parent.Kind == RigNodeKinds.Affine)
        {
            // The legacy affine adapter carries point children by rotation only; its scale and shear stay on its own frame.
            return Rotation2D.Apply(position - new Vector2(parent.RestFrame.M31, parent.RestFrame.M32), -parent.RestAngle);
        }

        if (!new Frame2D(parent.RestFrame).TryToLocal(position, out var local))
        {
            throw new InvalidOperationException($"Cannot place a node beneath the collapsed parent '{parent.Id}'.");
        }

        return local;
    }

    /// <summary>Overrides a variant bone node's length; a point control's legacy length is not a variant property.</summary>
    public static void ResizeBone(ResolvedModel resolved, ModelVariant variant, string id, float length)
    {
        var node = resolved.Nodes.GetValueOrDefault(id) ?? throw new InvalidOperationException($"'{resolved.Id}' has no control '{id}'.");
        if (node.Kind != RigNodeKinds.Bone)
        {
            throw new InvalidOperationException($"'{id}' is a {node.Kind} control; only a bone node's length is a variant property. Convert the run to a bone chain on the base model.");
        }

        new Limit(.01f, 100).Check(length, "bone length");
        SetOverride(variant, resolved.Base, PropertyOverride.Of(PropertyRef.OfControl(id, RigProperties.Length), length));
    }

    /// <summary>
    /// Turns and resizes a variant's bone so its tip lands on a character-frame position: overrides <c>local.rotation</c>
    /// (when the direction changes) and <c>length</c>. Bound and unbound bone nodes alike; a point or affine node is refused.
    /// </summary>
    public static void MoveBoneTip(ResolvedModel resolved, ModelVariant variant, string id, Vector2 position)
    {
        var node = resolved.Nodes.GetValueOrDefault(id) ?? throw new InvalidOperationException($"'{resolved.Id}' has no control '{id}'.");
        if (node.Kind != RigNodeKinds.Bone)
        {
            throw new InvalidOperationException($"'{id}' is a {node.Kind} control; a variant can only drag a bone node's tip. Convert the run to a bone chain on the base model.");
        }

        var origin = new Vector2(node.Rest.X, node.Rest.Y);
        var delta = position - origin;
        if (delta.LengthSquared() < .0001f)
        {
            return;
        }

        var parent = node.Parent is null ? null : resolved.Nodes[node.Parent];
        var angle = MathF.Atan2(delta.Y, delta.X);
        var rotation = node.CharacterOriented ? angle : (parent?.Handedness ?? 1) * (angle - (parent?.RestAngle ?? 0));
        // The frame's X axis carries the inherited uniform scale, so the tip lands exactly.
        var unit = Vector2.TransformNormal(Vector2.UnitX, node.RestFrame).Length();
        if (unit <= 1e-6f)
        {
            throw new InvalidOperationException($"'{id}' has a collapsed frame; its tip cannot be placed.");
        }

        var changes = new List<PropertyOverride>();
        if (MathF.Abs(rotation - node.Setup.Rotation) > 1e-6f)
        {
            changes.Add(PropertyOverride.Of(PropertyRef.OfControl(id, RigProperties.LocalRotation), rotation));
        }

        changes.Add(PropertyOverride.Of(PropertyRef.OfControl(id, RigProperties.Length), delta.Length() / unit));
        SetOverrides(variant, resolved.Base, changes);
    }

    /// <summary>
    /// Edits one variant's view of a part: the fields <paramref name="change"/> alters on the resolved part become typed
    /// overrides of this variant; untouched fields keep inheriting. A material edit merges the changed members into the
    /// existing material override, so a fill override survives a later outline edit.
    /// </summary>
    public static void EditPartOverride(ModelVariant variant, CharacterModel basis, PuppetPart resolved, Action<PuppetPart> change)
    {
        var edited = resolved with { };
        change(edited);
        // The property registry diffs the typed part properties (fit, face, visibility); this command knows no feature field.
        var changes = RigProperties.PartChanges(resolved, edited).ToList();
        PropertyRef Target(string property) => PropertyRef.OfPart(resolved.Id, property);
        var was = resolved.RenderMaterial;
        var now = edited.RenderMaterial;
        RenderMaterialDefinition2D? material = variant.Override(Target(RigProperties.Material))?.Material;
        if (now.Asset != was.Asset)
        {
            // Selecting another shared material restarts the override from the edited material as a whole.
            material = now;
        }
        else
        {
            if (now.Fill != was.Fill) material = (material ?? new()) with { Fill = now.Fill };
            if (now.StrokeWidth != was.StrokeWidth) material = (material ?? new()) with { StrokeWidth = now.StrokeWidth };
            if (now.Texture != was.Texture) material = (material ?? new()) with { Texture = now.Texture };
            if (now.Tint != was.Tint) material = (material ?? new()) with { Tint = now.Tint };
            if (now.Outline?.Width != was.Outline?.Width)
            {
                material = (material ?? new()) with { Outline = (material?.Outline ?? new RenderOutlineDefinition2D()) with { Width = now.Outline?.Width } };
            }

            if (now.Outline?.Color != was.Outline?.Color)
            {
                material = (material ?? new()) with { Outline = (material?.Outline ?? new RenderOutlineDefinition2D()) with { Color = now.Outline?.Color } };
            }

            if (!Equals(now.Paint, was.Paint)) material = (material ?? new()) with { Paint = now.Paint };
        }
        if (material is not null && !Equals(material, variant.Override(Target(RigProperties.Material))?.Material))
        {
            changes.Add(PropertyOverride.Of(Target(RigProperties.Material), material));
        }

        SetOverrides(variant, basis, changes);
    }

    /// <summary>Turns and resizes a rest bone to put its tip at a model-space position.</summary>
    public static void MoveBoneTip(CharacterModel model, string id, Vector2 position)
    {
        var origin = ResolvedModel.From(model).Rest[id];
        var delta = position - new Vector2(origin.X, origin.Y);
        if (delta.LengthSquared() < .0001f)
        {
            return;
        }

        RotateRestBone(model, id, MathF.Atan2(delta.Y, delta.X));
        // The frame's X axis carries the inherited uniform scale (and the point control's unit axes), so the tip lands exactly.
        var unit = Vector2.TransformNormal(Vector2.UnitX, RigGraph.Compile(model)[id].RestFrame).Length();
        if (unit > 1e-6f)
        {
            ResizeBone(model, id, delta.Length() / unit);
        }
    }

    private static IEnumerable<ModelControl> Subtree(IReadOnlyList<ModelControl> controls, string id)
    {
        var members = new HashSet<string>(StringComparer.Ordinal) { id };
        // Parents precede children after enough passes; the tree is small, so repeat until nothing is added.
        for (var added = true; added;)
        {
            added = false;
            foreach (var control in controls)
            {
                if (control.Parent is { } parent && members.Contains(parent) && members.Add(control.Id))
                {
                    added = true;
                }
            }
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
            var frame = resolved.RestTransforms[control.Id];
            control.RestAngle = MathF.Atan2(frame.M12, frame.M11);
        }
    }
}
