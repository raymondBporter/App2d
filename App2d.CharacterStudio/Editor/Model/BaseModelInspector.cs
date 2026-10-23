using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Mathematics.Transforms;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Edits a base model, its selected rig element or part, and its motion-set assignments.</summary>
internal sealed class BaseModelInspector(EditorSession session, ModelStructuralEdits structural, ModelPartInspector parts)
{
    private string _newSet = "";
    private readonly ParameterInspector _parameters = new(session);
    private readonly AttachmentInspector _attachments = new(session);
    private ResolvedModel? Resolved => session.Assets.Resolve(session.SubjectId);

    public void Draw(AssetDocument<CharacterModel> document)
    {
        var model = document.Asset;
        Ui.Header("Base model");
        var name = model.Name;
        if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0)
        {
            session.Change(document, () => document.Asset.Name = name);
        }

        var ink = model.Ink;
        if (Ui.ColorHex("Ink", ref ink))
        {
            session.Change(document, () => document.Asset.Ink = ink);
        }

        var line = model.LineWidth;
        if (Ui.Slider("Outline width", ref line, .005f, .12f))
        {
            session.Change(document, () => document.Asset.LineWidth = line);
        }

        _parameters.Draw(document, Resolved);
        Dependents(document);
        MotionSets(document);
        GroupsAndLooks(model);

        if (session.Selection.Control is { } id && model.Controls.FirstOrDefault(c => c.Id == id) is { } control)
        {
            Ui.Header((control.Length > 0 ? "Bone " : "Control ") + control.Id);
            ControlSetup(document, control, id);
            var children = session.MoveChildren;
            if (ImGui.Checkbox("Move children with it", ref children))
            {
                session.MoveChildren = children;
            }

            var parents = model.Controls.Select(c => c.Id).Where(c => c != id).Prepend("(locomotion)");

            if (Ui.Combo("Parent", control.Parent ?? "(locomotion)", parents) is { } parent)
            {
                structural.Request(document, $"reparent '{id}' to '{parent}'",
                    () => ModelAuthoring.Reparent(document.Asset, id, parent == "(locomotion)" ? null : parent));
            }

            if (Ui.Combo("Translation scales with", control.Scale, model.Measures.Select(m => m.Id).Prepend(CharacterModel.Unit)) is { } scale)
            {
                structural.Request(document, $"scale '{id}' by '{scale}'",
                    () => document.Asset.Controls.First(c => c.Id == id).Scale = scale);
            }

            if (ImGui.Button("Delete control"))
            {
                structural.Request(document, $"delete '{id}'",
                    () =>
                    {
                        ModelAuthoring.RemoveControl(document.Asset, id);
                        session.Selection.Clear();
                    });
            }

            Ui.Help("Deleting removes shapes attached to it. Children, chains and measures must be changed first.");
        }
        if (session.Selection.Attachment is { } attachmentId)
        {
            _attachments.DrawBase(document, attachmentId);
        }

        if (session.Selection.Chain is { } chainId && model.IkChains.FirstOrDefault(c => c.Id == chainId) is { } chain)
        {
            Ui.Header("IK constraint " + chain.Id);
            ImGui.TextUnformatted($"{chain.Root} > {chain.Joint} > {chain.End}");
            ImGui.TextDisabled($"Solver: {chain.Solver}");
            if (ImGui.Button("Flip bend"))
            {
                session.Edit(document, () => document.Asset.IkChains.First(c => c.Id == chainId).Bend *= -1);
            }

            bool FreeFrame(ModelControl c)
            {
                for (ModelControl? node = c; node is not null; node = model.Controls.FirstOrDefault(p => p.Id == node.Parent))
                {
                    if (model.IkChains.Any(k => k.Joint == node.Id || k.End == node.Id || k.Solver == ModelChain.BoneSolver && k.Root == node.Id))
                    {
                        return false;
                    }
                }
                return true;
            }
            var frames = model.Controls.Where(FreeFrame).Select(c => c.Id).Prepend(CharacterModel.Locomotion);

            if (Ui.Combo("Target frame", chain.Frame, frames) is { } frame)
            {
                structural.Request(document, $"key '{chainId}' in '{frame}'",
                    () => document.Asset.IkChains.First(c => c.Id == chainId).Frame = frame);
            }

            if (Ui.Combo("Target scales with", chain.Scale, model.Measures.Select(m => m.Id).Prepend(CharacterModel.Unit)) is { } scale)
            {
                structural.Request(document, $"scale '{chainId}' by '{scale}'",
                    () => document.Asset.IkChains.First(c => c.Id == chainId).Scale = scale);
            }

            if (!model.Measures.Any(m => m.Path.SequenceEqual([chain.Root, chain.Joint, chain.End])) && ImGui.Button("Add reach measure"))
            {
                structural.Request(document, $"add measure '{chainId}-reach'",
                    () => ModelAuthoring.AddMeasure(document.Asset, ModelAuthoring.UniqueId(chainId + "-reach", document.Asset.Measures.Select(m => m.Id)), [chain.Root, chain.Joint, chain.End]));
            }

            if (ImGui.Button("Remove chain"))
            {
                structural.Request(document, $"remove chain '{chainId}'",
                    () =>
                    {
                        ModelAuthoring.RemoveChain(document.Asset, chainId);
                        session.Selection.Clear();
                    });
            }

            Ui.Help("The locomotion frame supports contact pins. A free control frame makes the target follow that control.");

            if (chain.Solver == ModelChain.BoneSolver)
            {
                Ui.Help("Bone IK turns both segments and their attachments. Uniform scale and reflection are supported; shear and nonuniform segment frames are not.");
            }
        }
        if (session.Selection.Part is { } partId && model.Parts.FirstOrDefault(p => p.Id == partId) is { } part)
        {
            Ui.Header($"Part {part.Id} ({part.Geometry!.Kind})");

            var controls = model.Controls.Select(c => c.Id).ToArray();

            if (Ui.Combo("Geometry", part.Geometry.Kind, GeometryJson2D.DrawableKinds.Where(kind => controls.Length > 1 || !CurveKinds2D.All.Contains(kind))) is { } kind)
            {
                session.Edit(document, () =>
                {
                    var geometry = GeometryJson2D.CreateDefault(kind);
                    ModelAuthoring.SetPartGeometry(document.Asset, partId, geometry);
                });
                return;
            }

            if (part.Geometry is RoundedPolylineCurve2D rounded && part.Bindings?.Count == rounded.Points.Length)
            {
                RoundedPath(document, part);
            }
            else
            {
            if (Ui.Combo("Attach to", part.A, controls) is { } a)
            {
                session.Edit(document, () =>
                {
                    Part(document, partId).A = a;
                    document.Asset.Validate();
                });
            }

            if (part.Geometry is not ICurve2D && Ui.Combo("Bone frame", part.Frame ?? "(none)", controls.Where(id => model.Controls.Any(c => c.Id == id && (c.Length > 0 || c.Transform is not null))).Prepend("(none)")) is { } bone)
            {
                session.Edit(document, () =>
                {
                    var edited = Part(document, partId);
                    edited.Frame = bone == "(none)" ? null : bone;
                    if (edited.Frame is not null)
                    {
                        edited.B = null;
                    }

                    document.Asset.Validate();
                });
            }

            if (Ui.Combo(part.Geometry is ICurve2D ? "End" : "Point toward", part.B ?? "(none)", part.Geometry is ICurve2D ? controls : controls.Prepend("(none)")) is { } b)
            {
                session.Edit(document, () =>
                {
                    var edited = Part(document, partId);
                    edited.B = b == "(none)" ? null : b;
                    if (edited.B is not null)
                    {
                        edited.Frame = null;
                    }

                    document.Asset.Validate();
                });
            }

                if (part.Geometry is ICurve2D && part.Bindings is not { Count: > 0 } && ImGui.Button("Make rounded control path"))
                {
                    session.Edit(document, () =>
                    {
                        ModelAuthoring.SetRoundedPath(Part(document, partId), [part.A, part.B!]);
                        document.Asset.Validate();
                    });
                }
            }

            if (part.Frame is not null)
            {
                var degrees = float.RadiansToDegrees(part.Angle);
                if (Ui.Drag("Shape angle (degrees)", ref degrees, 1f, -360, 360))
                {
                    session.Change(document, () => Part(document, partId).Angle = float.DegreesToRadians(degrees));
                }
                if (part.Material?.Texture is { } texture)
                {
                    ImGui.TextDisabled("Image: " + texture);
                    var scaleX = part.ScaleX;
                    var scaleY = part.ScaleY;

                    if (Ui.Drag("Attachment scale X", ref scaleX, .01f, -100, 100))
                    {
                        session.Change(document, () => Part(document, partId).ScaleX = scaleX);
                    }

                    if (Ui.Drag("Attachment scale Y", ref scaleY, .01f, -100, 100))
                    {
                        session.Change(document, () => Part(document, partId).ScaleY = scaleY);
                    }
                }
            }
            parts.Draw(part, null, change => session.Change(document, () => change(Part(document, partId))));
            if (ImGui.Button("Delete part"))
            {
                session.Edit(document, () =>
                {
                    ModelAuthoring.RemovePart(document.Asset, partId);
                    session.Selection.Clear();
                });
            }
        }
    }

    /// <summary>
    /// The selected control's setup, by its declared node kind: a point control's character-frame rest; a bone node's origin
    /// (derived, moved through its local setup or fixed by its link), local similarity, length, link and direction; the legacy
    /// affine adapter's channels. Every field says where the value lives, and an edit a kind cannot take is refused with its reason.
    /// </summary>
    private void ControlSetup(AssetDocument<CharacterModel> document, ModelControl control, string id)
    {
        var resolved = Resolved;
        var node = resolved?.Nodes.GetValueOrDefault(id);
        ModelControl Live() => document.Asset.Controls.First(c => c.Id == id);
        switch (control.Node)
        {
            case RigNodeKinds.Bone:
            {
                ImGui.TextDisabled(control.Link is { } link
                    ? $"Bone node: origin bound to '{control.Parent}' at t = {link.T:0.##}" + (link.OffsetX != 0 || link.OffsetY != 0 ? $" + ({link.OffsetX:0.###}, {link.OffsetY:0.###})" : "") + (link.Orientation == RigLinkOrientations.Character ? ", character axes" : "")
                    : "Bone node: parent-local setup; rest is derived");
                if (node is not null)
                {
                    var origin = LayeredPoint2D.From(node.Rest);
                    if (control.Link is null)
                    {
                        if (CharacterUi.LayeredPoint("Origin (character)", ref origin))
                        {
                            session.Change(document, () => ModelAuthoring.MoveRest(document.Asset, id, origin.ToVector3(), session.MoveChildren));
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled($"Origin (character): {node.Rest.X:0.###}, {node.Rest.Y:0.###}, layer {node.Rest.Z:0.###}; change the parent's length or the link.");
                    }

                    var direction = float.RadiansToDegrees(node.RestAngle);
                    if (Ui.Drag("Direction (degrees, character)", ref direction, 1f, -720, 720))
                    {
                        session.Change(document, () => ModelAuthoring.RotateRestBone(document.Asset, id, float.DegreesToRadians(direction)));
                    }
                }
                var length = control.Length;
                if (Ui.Drag("Bone length", ref length, .01f, 0, 100))
                {
                    session.Change(document, () =>
                    {
                        if (length < .01f)
                        {
                            Live().Length = 0;
                            document.Asset.Validate();
                        }
                        else
                        {
                            ModelAuthoring.ResizeBone(document.Asset, id, length);
                        }
                    });
                }

                ParameterInspector.BoundNote(resolved, PropertyRef.OfControl(id, RigProperties.Length));

                Ui.Help(control.Length > 0 ? "Children bound along this bone follow its length; their frames keep their own scale." : "Zero length: an ordinary control, still a handle, socket or target.");
                Ui.Header("Local setup");
                var local = control.Local ?? new RigSetup();
                void Local(Action<RigSetup> set) => session.Change(document, () =>
                {
                    var live = Live();
                    live.Local ??= new RigSetup();
                    set(live.Local);
                    document.Asset.Validate();
                });
                if (control.Link is null)
                {
                    var xy = new Vector2(local.X, local.Y);
                    if (Ui.Drag2("Local X / Y", ref xy, .005f, -10000, 10000))
                    {
                        Local(l => { l.X = xy.X; l.Y = xy.Y; });
                    }

                    ParameterInspector.BoundNote(resolved, PropertyRef.OfControl(id, RigProperties.LocalX));
                    ParameterInspector.BoundNote(resolved, PropertyRef.OfControl(id, RigProperties.LocalY));
                }
                var rotation = float.RadiansToDegrees(local.Rotation);
                if (Ui.Drag("Local rotation (degrees)", ref rotation, 1f, -720, 720))
                {
                    Local(l => l.Rotation = float.DegreesToRadians(rotation));
                }

                var scale = local.Scale;
                if (Ui.Drag("Uniform scale", ref scale, .01f, .001f, 1000))
                {
                    Local(l => l.Scale = scale);
                }

                var reflect = local.Reflect;
                if (ImGui.Checkbox("Reflect (mirror local Y)", ref reflect))
                {
                    Local(l => l.Reflect = reflect);
                }

                var layer = local.Layer;
                if (Ui.Drag("Layer offset from parent", ref layer, .005f, -32, 32))
                {
                    Local(l => l.Layer = layer);
                }

                ParameterInspector.BoundNote(resolved, PropertyRef.OfControl(id, RigProperties.LocalLayer));

                if (control.Parent is { } parentId)
                {
                    Ui.Header("Link");
                    var parent = document.Asset.Controls.First(c => c.Id == parentId);
                    if (control.Link is { } bound)
                    {
                        var t = bound.T;
                        if (Ui.Drag("Position along parent (t)", ref t, .01f, -10, 10))
                        {
                            session.Change(document, () => { Live().Link!.T = t; document.Asset.Validate(); });
                        }

                        ParameterInspector.BoundNote(resolved, PropertyRef.OfControl(id, RigProperties.LinkT));

                        var offset = new Vector2(bound.OffsetX, bound.OffsetY);
                        if (Ui.Drag2("Offset in parent axes", ref offset, .005f, -100, 100))
                        {
                            session.Change(document, () => { Live().Link!.OffsetX = offset.X; Live().Link!.OffsetY = offset.Y; document.Asset.Validate(); });
                        }

                        ParameterInspector.BoundNote(resolved, PropertyRef.OfControl(id, RigProperties.LinkOffsetX));
                        ParameterInspector.BoundNote(resolved, PropertyRef.OfControl(id, RigProperties.LinkOffsetY));

                        if (Ui.Combo("Orientation", bound.Orientation, RigLinkOrientations.All) is { } orientation)
                        {
                            session.Edit(document, () => { Live().Link!.Orientation = orientation; document.Asset.Validate(); });
                        }

                        if (ImGui.Button("Unbind (keep place as a local offset)"))
                        {
                            session.Edit(document, () =>
                            {
                                var live = Live();
                                var rest = (Resolved ?? throw new InvalidDataException("The model does not resolve; repair it first.")).Nodes[id].Rest;
                                live.Link = null;
                                ModelAuthoring.MoveRest(document.Asset, id, rest, children: true);
                            });
                        }

                        Ui.Help(parent.Length > 0 ? "t = 0 is the parent's origin, 1 its tip; the parent's final length places it." : "The parent has no length: t has nothing to measure along until it gets one.");
                    }
                    else if (parent.Node != RigNodeKinds.Affine && ImGui.Button("Bind to parent tip"))
                    {
                        session.Edit(document, () =>
                        {
                            var live = Live();
                            live.Link = new RigLink();
                            if (live.Local is not null)
                            {
                                live.Local.X = live.Local.Y = 0;
                            }

                            document.Asset.Validate();
                        });
                    }
                }
                break;
            }
            case RigNodeKinds.Affine:
            {
                ImGui.TextDisabled("Legacy affine node (adapter until P11): a full parent-local affine setup; scale and shear are not similarities.");
                var rest = node is null ? control.Rest.Layered : LayeredPoint2D.From(resolved!.Rest[id]);
                if (CharacterUi.LayeredPoint("Origin (character)", ref rest))
                {
                    session.Change(document, () => ModelAuthoring.MoveRest(document.Asset, id, rest.ToVector3(), session.MoveChildren));
                }

                if (control.Length > 0)
                {
                    var length = control.Length;
                    if (Ui.Drag("Bone length", ref length, .01f, .01f, 100))
                    {
                        session.Change(document, () => ModelAuthoring.ResizeBone(document.Asset, id, length));
                    }

                    var degrees = float.RadiansToDegrees(control.RestAngle);
                    if (Ui.Drag("Rest angle (degrees)", ref degrees, 1f, -360, 360))
                    {
                        session.Change(document, () => ModelAuthoring.RotateRestBone(document.Asset, id, float.DegreesToRadians(degrees)));
                    }
                }
                var transform = control.Transform!;
                Ui.Header("Legacy affine setup");
                void Number(string label, float value, Action<Affine2D, float> set, float min = -100, float max = 100)
                {
                    if (Ui.Drag(label, ref value, .01f, min, max))
                    {
                        session.Change(document, () =>
                        {
                            set(Live().Transform!, value);
                            ModelAuthoring.SyncAffineRest(document.Asset);
                        });
                    }
                }
                Number("Local X", transform.X, (t, v) => t.X = v);
                Number("Local Y", transform.Y, (t, v) => t.Y = v);
                Number("Local rotation (degrees)", float.RadiansToDegrees(transform.Rotation), (t, v) => t.Rotation = float.DegreesToRadians(v), -720, 720);
                Number("Scale X", transform.ScaleX, (t, v) => t.ScaleX = v);
                Number("Scale Y", transform.ScaleY, (t, v) => t.ScaleY = v);
                Number("Shear X (degrees)", float.RadiansToDegrees(transform.ShearX), (t, v) => t.ShearX = float.DegreesToRadians(v), -720, 720);
                Number("Shear Y (degrees)", float.RadiansToDegrees(transform.ShearY), (t, v) => t.ShearY = float.DegreesToRadians(v), -720, 720);
                break;
            }
            default:
            {
                ImGui.TextDisabled("Point control: character-frame rest; its parent-local setup is derived when the model resolves.");
                var rest = control.Rest.Layered;
                if (CharacterUi.LayeredPoint("Rest (character)", ref rest))
                {
                    session.Change(document, () => ModelAuthoring.MoveRest(document.Asset, id, rest.ToVector3(), session.MoveChildren));
                }

                if (control.Length > 0)
                {
                    var length = control.Length;
                    if (Ui.Drag("Bone length", ref length, .01f, .01f, 100))
                    {
                        session.Change(document, () => ModelAuthoring.ResizeBone(document.Asset, id, length));
                    }

                    var degrees = float.RadiansToDegrees(control.RestAngle);
                    if (Ui.Drag("Rest angle (degrees)", ref degrees, 1f, -360, 360))
                    {
                        session.Change(document, () => ModelAuthoring.RotateRestBone(document.Asset, id, float.DegreesToRadians(degrees)));
                    }

                    Ui.Help("The bone starts at this control and points along its local +X axis. Its children and attached shapes follow rotation.");
                }
                // A run of single children below this control can become a canonical bone chain.
                var run = new List<string> { id };
                for (var next = document.Asset.Controls.Where(c => c.Parent == run[^1]).ToArray(); next.Length == 1 && next[0].Node == RigNodeKinds.Point; next = document.Asset.Controls.Where(c => c.Parent == run[^1]).ToArray())
                {
                    run.Add(next[0].Id);
                }

                if (run.Count >= 2 && control.RestAngle == 0 && ImGui.Button($"Convert to bone chain ({string.Join(" > ", run)})"))
                {
                    structural.Request(document, $"convert '{string.Join("', '", run)}' to bone nodes", () => ModelAuthoring.ConvertToBoneChain(document.Asset, run));
                }

                if (run.Count >= 2)
                {
                    Ui.Help("Bone nodes carry a length and bind their children to it; rest positions and point IK are unchanged, their frames turn along the bones.");
                }

                break;
            }
        }
    }

    private static PuppetPart Part(AssetDocument<CharacterModel> document, string id) => document.Asset.Parts.First(p => p.Id == id);

    /// <summary>The ordered controls a rounded path draws through. Rounding follows the pose; it adds no IK or motion.</summary>
    private void RoundedPath(AssetDocument<CharacterModel> document, PuppetPart part)
    {
        var rounded = (RoundedPolylineCurve2D)part.Geometry!;
        var path = part.Bindings!.OrderBy(b => b.Point).Select(b => b.Control).ToList();
        var controls = document.Asset.Controls.Select(c => c.Id).ToArray();
        void Edit(Action<List<string>> edit) => session.Edit(document, () =>
        {
            var next = new List<string>(path);
            edit(next);
            ModelAuthoring.SetRoundedPath(Part(document, part.Id), next, rounded.Softness);
            document.Asset.Validate();
        });
        Ui.Help("Ordered drawing controls. Rounding follows the pose without changing IK or adding motion.");
        for (var i = 0; i < path.Count; i++)
        {
            var index = i;
            ImGui.PushID("path-point-" + i);
            if (Ui.Combo("Control " + (i + 1), path[i], controls.Where(c => c == path[index] || !path.Contains(c))) is { } control)
            {
                Edit(next => next[index] = control);
            }

            if (path.Count < 64 && Ui.Combo("Insert after", "(choose control)", controls.Where(c => !path.Contains(c))) is { } added)
            {
                Edit(next => next.Insert(index + 1, added));
            }

            if (path.Count > 2 && ImGui.Button("Remove"))
            {
                Edit(next => next.RemoveAt(index));
            }

            ImGui.PopID();
        }
        if (ImGui.Button("Convert to straight stroke"))
        {
            session.Edit(document, () =>
            {
                var edited = Part(document, part.Id);
                edited.Bindings = null;
                edited.Geometry = GeometryJson2D.CreateDefault(CurveKinds2D.Line);
                document.Asset.Validate();
            });
        }
    }

    /// <summary>Role-to-clip tables on the base. A new set copies another's assignments; there is no set inheritance.</summary>
    private void MotionSets(AssetDocument<CharacterModel> document)
    {
        var model = document.Asset;
        Ui.Header("Motion sets");
        var clips = session.Assets.ClipsFor(model.Id).OrderBy(c => c.Name).ToArray();
        foreach (var set in model.MotionSets.ToArray())
        {
            ImGui.PushID(set.Id);
            var users = session.Assets.EntitiesOn(model.Id).Where(e => e.Asset.MotionSet == set.Id).ToArray();
            if (ImGui.TreeNodeEx($"{set.Name} ({set.Id})", ImGuiTreeNodeFlags.None, $"{set.Name}  ({set.Roles.Count} roles, {users.Length} entities)"))
            {
                var name = set.Name;
                if (Ui.Text("Set name", ref name, 60) && name.Trim().Length > 0)
                {
                    session.Change(document, () => document.Asset.MotionSets.First(s => s.Id == set.Id).Name = name);
                }

                foreach (var role in EntityAuthoring.CommonRoles.Concat(set.Roles.Keys).Distinct())
                {
                    ImGui.PushID(role);
                    var assigned = set.Roles.GetValueOrDefault(role);
                    ImGui.TextUnformatted(role);
                    ImGui.SameLine(90 * Ui.Scale);
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.BeginCombo("##clip", assigned is null ? "(unassigned)" : session.Assets.Clip(assigned)?.Name ?? assigned + " (missing)"))
                    {
                        if (ImGui.Selectable("(unassigned)", assigned is null))
                        {
                            session.AssignRole(model.Id, set.Id, role, null);
                        }

                        foreach (var clip in clips)
                        {
                            if (ImGui.Selectable(clip.Name + "##" + clip.Id, clip.Id == assigned))
                            {
                                session.AssignRole(model.Id, set.Id, role, clip.Id);
                            }
                        }

                        ImGui.EndCombo();
                    }
                    ImGui.PopID();
                }
                if (users.Length > 0)
                {
                    ImGui.TextDisabled("Selected by " + string.Join(", ", users.Select(u => u.Name)));
                }

                if (ImGui.SmallButton("Copy to new set"))
                {
                    var id = ModelAuthoring.UniqueId(set.Id + "-copy", model.MotionSets.Select(s => s.Id));
                    session.NewMotionSet(model.Id, id, set.Name + " copy", set.Id);
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove set"))
                {
                    session.RemoveMotionSet(model.Id, set.Id);
                }

                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(140 * Ui.Scale);
        ImGui.InputTextWithHint("##new-set", "new set name", ref _newSet, 60);
        ImGui.SameLine();
        if (Ui.Button("Add empty set", _newSet.Trim().Length > 0))
        {
            var id = ModelAuthoring.UniqueId(new string([.. _newSet.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]).Trim('-') is { Length: > 0 } slug ? slug : "set", model.MotionSets.Select(s => s.Id));
            if (session.NewMotionSet(model.Id, id, _newSet.Trim(), null))
            {
                _newSet = "";
            }
        }
        Ui.Help("Sets describe an artistic choice such as Heavy; any build can use any set. Missing roles stay visibly unassigned.");
    }

    private static void GroupsAndLooks(CharacterModel model)
    {
        if (model.Groups.Count > 0)
        {
            Ui.Header("Control groups");
            foreach (var group in model.Groups)
            {
                ImGui.TextWrapped($"{group.Id}: {string.Join(", ", group.Targets)}");
            }

            Ui.Help("A masked action owns a group's channels over locomotion.");
        }
        if (model.Looks.Count > 0)
        {
            Ui.Header("Looks");
            ImGui.TextWrapped(string.Join(", ", model.Looks.Select(l => l.Name)));
            Ui.Help("Variants apply looks as overrides. Save a variant's look from its inspector.");
        }
    }

    private void Dependents(AssetDocument<CharacterModel> document)
    {
        var (variants, clips) = session.Assets.Dependents(document.Id);
        ImGui.TextDisabled($"Used by {variants.Count} variant(s) and {clips.Count} animation(s); revision {document.Asset.StructureRevision}.");
        if (!document.IsNew && session.Assets.StructureChanged(document))
        {
            Ui.Problem($"Structure changed since saving: saving raises the revision to {document.Asset.StructureRevision + 1} and moves compatible animations with it.");
        }
    }
}
