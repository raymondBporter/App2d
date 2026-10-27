using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>Reusable cutout art, edited on its wearer with the same sockets and character layers as the game.</summary>
internal sealed class AppearanceView(EditorSession session) : IWorkspaceView
{
    private int _drag = -1;
    public Workspace Mode => Workspace.Appearance;
    public float TimelineHeight => 80 * Ui.Scale;
    public void Timeline() => TransportBar.Draw(session, true);

    public void Outline()
    {
        if (session.PartDocument is { } part) PartOutline(part);
        else Ui.Help("Open or create a library part.");
    }

    public void Inspector()
    {
        if (session.PartDocument is { } part) PartInspector(part);
    }

    // ---- Library parts (P5c) -------------------------------------------------------------------------------------------

    private readonly Model.ModelPartInspector _geometryInspector = new(session, new(session));
    private int _partPiece;
    private string _partAsset = "";
    private readonly LibraryPartFields _partFields = new(session);

    private void PartOutline(AssetDocument<Part> doc)
    {
        Ui.Header("Library parts");
        Ui.Help("Shared assets: every character and variant that instances one shows the same artwork. Attach from here or from Model > Library parts.");
        if (Ui.Combo("Library part", doc.Id, session.Assets.Parts.Select(p => p.Id).Order(StringComparer.Ordinal), id => session.Assets.Part(id)?.Name ?? id) is { } id)
        {
            session.Open(id);
        }

        if (_partAsset != doc.Id)
        {
            _partAsset = doc.Id;
            _partPiece = 0;
            _drag = -1;
        }

        Ui.Header("Pieces");
        var asset = doc.Asset;
        var origin = asset.IsAnimated ? asset.Controls[0].Id : PartLocation.Origin;
        for (var i = 0; i < asset.Geometry.Count; i++)
        {
            if (ImGui.Selectable($"{asset.Geometry[i].Id}  ({asset.Geometry[i].Kind} on {asset.Geometry[i].A})", _partPiece == i))
            {
                _partPiece = i;
            }
        }

        if (ImGui.SmallButton("Add shape"))
        {
            session.Edit(doc, () =>
            {
                doc.Asset.Geometry.Add(new() { Id = ModelAuthoring.UniqueId("shape", doc.Asset.Geometry.Select(p => p.Id)), A = origin, Frame = origin, Geometry = RoundedRectangle2D.Builder.FromSize(.12f, .1f, .02f), Material = new() { Fill = "#b97549", Outline = new() } });
                doc.Asset.Validate();
                _partPiece = doc.Asset.Geometry.Count - 1;
            });
        }

        ImGui.SameLine();
        if (Ui.Button("Add curve", !asset.IsAnimated || asset.Controls.Count > 1))
        {
            session.Edit(doc, () =>
            {
                if (!doc.Asset.IsAnimated && doc.Asset.Locations.Count == 0)
                {
                    doc.Asset.Locations.Add(new() { Id = "end", Y = .2f });
                }

                doc.Asset.Geometry.Add(new() { Id = ModelAuthoring.UniqueId("curve", doc.Asset.Geometry.Select(p => p.Id)), A = origin, B = doc.Asset.IsAnimated ? doc.Asset.Controls[1].Id : doc.Asset.Locations[0].Id, Geometry = new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(), Material = new() { StrokeWidth = .03f } });
                doc.Asset.Validate();
                _partPiece = doc.Asset.Geometry.Count - 1;
            });
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Add mesh"))
        {
            session.Edit(doc, () =>
            {
                doc.Asset.Geometry.Add(new() { Id = ModelAuthoring.UniqueId("mesh", doc.Asset.Geometry.Select(p => p.Id)), A = origin, Frame = origin, Material = new() { Fill = "#8c4d2c", Outline = new() }, Mesh = new() { Outline = [new(-.08f, 0), new(.08f, 0), new(.08f, .12f), new(-.08f, .12f)], Thickness = .02f } });
                doc.Asset.Validate();
                _partPiece = doc.Asset.Geometry.Count - 1;
            });
        }

        _partFields.Locations(doc);
    }

    private void PartInspector(AssetDocument<Part> doc)
    {
        var asset = doc.Asset;
        Ui.Header("Shared library part");
        var wearers = session.Assets.Wearers(doc.Id);
        ImGui.TextDisabled(wearers.Count == 0 ? "Not worn yet. Edits here change the shared asset." : $"Shared by {wearers.Count} wearer(s): {string.Join(", ", wearers)}. Edits here reach all of them.");
        var name = asset.Name;
        if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0)
        {
            session.Change(doc, () => doc.Asset.Name = name);
        }

        var usage = asset.Usage;
        if (Ui.Text("Category", ref usage, 40) && usage.Trim().Length > 0)
        {
            session.Change(doc, () => { doc.Asset.Usage = usage.Trim(); doc.Asset.Validate(); });
        }

        _partFields.Placement(doc);
        _partFields.Import(doc);
        _partFields.Motion(doc);

        var ink = asset.Ink;
        if (Ui.ColorHex("Ink", ref ink))
        {
            session.Change(doc, () => doc.Asset.Ink = ink);
        }

        var line = asset.LineWidth;
        if (Ui.Drag("Line width", ref line, .001f, .001f, .2f))
        {
            session.Change(doc, () => doc.Asset.LineWidth = Math.Clamp(line, .001f, .2f));
        }

        Ui.Header(asset.IsModule ? "Whole rig module preview" : "Part preview");
        var onWearer = session.PreviewPartOnWearer;
        if (ImGui.Checkbox("Preview on wearer (temporary)", ref onWearer)) session.PreviewPartOnWearer = onWearer;
        Ui.Help(onWearer ? "This is a temporary fitting preview. Only Attach writes an instance to the model." : "Showing this part alone. The wearer and its saved attachments are unchanged.");
        var subjects = session.Assets.Models.Select(m => m.Id).Concat(session.Assets.Variants.Select(v => v.Id)).Order(StringComparer.Ordinal);
        if (Ui.Combo("Wearer", session.SubjectId ?? "", subjects, id => session.Assets.Find(id)?.Name is { } n ? $"{n} ({id})" : id) is { } wearer)
        {
            session.SetSubject(wearer);
        }

        var model = session.Assets.Resolve(session.SubjectId);
        if (model is not null)
        {
            var worn = model.Attachments.Where(a => a.Asset.Id == doc.Id).Select(a => a.Id).ToArray();
            if (worn.Length > 0)
            {
                ImGui.TextDisabled($"This wearer instances it as {string.Join(", ", worn)} (edit those in Model > Library parts).");
            }

            if (Ui.Combo("Socket", session.PreviewSocket, model.Base.Sockets.Select(s => s.Id)) is { } socket)
            {
                session.PreviewSocket = socket;
            }

            var mirror = session.PreviewMirror;
            if (ImGui.Checkbox("Mirror across the socket", ref mirror))
            {
                session.PreviewMirror = mirror;
            }

            if (session.EntityId is { } entityId && Ui.Button("Equip on " + entityId, model.Base.Sockets.Count > 0))
                session.EquipAppearance(entityId, doc.Id, session.PreviewSocket);
            var basis = session.SubjectModel?.Id ?? session.SubjectVariant?.Asset.Base;
            if (Ui.Button($"Attach to {basis}", basis is not null && model.Base.Sockets.Count > 0))
            {
                session.AttachPart(doc.Id, session.PreviewSocket, session.PreviewMirror);
            }

            Ui.Help(worn.Length == 0 ? "Attach creates a saved instance at the chosen socket on the base model; variants can override it per instance."
                : "Attaching again adds another instance (the other hand or foot, mirrored).");
        }

        var rest = session.ShowRest;
        if (ImGui.Checkbox("Rest pose", ref rest))
        {
            session.ShowRest = rest;
        }

        if (asset.IsAnimated)
        {
            AnimatedPayload(doc);
        }

        _partPiece = Math.Clamp(_partPiece, 0, Math.Max(0, asset.Geometry.Count - 1));
        if (_partPiece < asset.Geometry.Count)
        {
            PartPiece(doc, _partPiece);
        }
        if (ImGui.Button("Save all"))
        {
            session.SaveAll();
        }

        foreach (var problem in session.Assets.Problems(doc))
        {
            Ui.Problem(problem);
        }

        References.Draw(session, doc.Id);
    }

    /// <summary>
    /// An animated payload's own section (P7a): its rig, clips, layers and expression bindings as the asset declares them,
    /// the fitting policy, and which controls and pieces a wearer's clip may key (exposure toggles edit the shared asset).
    /// </summary>
    private void AnimatedPayload(AssetDocument<Part> doc)
    {
        var asset = doc.Asset;
        Ui.Header("Animated payload");
        ImGui.TextDisabled($"Rig: {asset.Controls.Count} control(s), {asset.Constraints.Count} constraint(s), {asset.Groups.Count} group(s). Clips: {string.Join(", ", asset.Clips.Select(c => $"{c.Id} ({c.Duration:0.##} s{(c.Loop ? ", loops" : "")})"))}.");
        if (Ui.Combo("Fitting", asset.Fitting, PartFittings.All, f => f == PartFittings.Stretch ? "stretch: the fit deforms the whole artwork" : "layout: the fit repositions the controls") is { } fitting)
        {
            session.Edit(doc, () => { doc.Asset.Fitting = fitting; doc.Asset.Validate(); });
        }

        foreach (var layer in asset.Layers)
        {
            ImGui.TextDisabled($"  layer '{layer.Id}': {(layer.Mask is null ? "everything" : "mask '" + layer.Mask + "'")}, clock {layer.Clock}{(layer.Clock == PartClocks.Mapped ? $" (x{layer.Rate:0.##} + {layer.Offset:0.##} s)" : "")}, plays {(layer.Clip ?? "nothing")} by default");
        }

        var words = asset.Expressions.Keys.Append(Part.None).Prepend("").ToArray();
        if (Ui.Combo("Default expression", asset.Expression ?? "", words, w => w.Length == 0 ? "nothing (rest and the layers' default clips)" : w) is { } expression)
        {
            session.Edit(doc, () => { doc.Asset.Expression = expression.Length == 0 ? null : expression; doc.Asset.Validate(); });
        }

        foreach (var (name, binding) in asset.Expressions)
        {
            ImGui.TextDisabled($"  '{name}': {(binding.Hidden ? "hides the instance" : binding.Clip is null ? "rests the " + (binding.Layer ?? asset.BaseLayer?.Id) + " layer" : $"{(binding.Time is { } t ? $"holds {binding.Clip} at {t:0.##} s" : "plays " + binding.Clip)} on '{binding.Layer ?? asset.BaseLayer?.Id}'")}{(binding.Mutes.Count > 0 ? ", mutes " + string.Join(", ", binding.Mutes) : "")}");
        }

        ImGui.TextDisabled("Exposed to a wearer's clips (namespaced tracks):");
        foreach (var target in asset.Controls.Select(c => c.Id).Concat(asset.Geometry.Select(p => p.Id)))
        {
            var exposed = asset.Exposed.Contains(target, StringComparer.Ordinal);
            if (ImGui.Checkbox($"{target}{(asset.Geometry.Any(p => p.Id == target) ? " (piece)" : "")}##expose", ref exposed))
            {
                session.Edit(doc, () =>
                {
                    doc.Asset.Exposed.RemoveAll(e => e == target);
                    if (exposed)
                    {
                        doc.Asset.Exposed.Add(target);
                    }

                    doc.Asset.Validate();
                }, $"{(exposed ? "Exposed" : "Unexposed")} '{target}'; clips keying it on a wearer are checked when they validate.");
            }
        }

        Ui.Help("The rig, clips and bindings are authored as data (conversion or seeds); a wearer's clip selects expressions and keys the exposed targets through Animate.");
    }

    private void PartPiece(AssetDocument<Part> doc, int index)
    {
        var piece = doc.Asset.Geometry[index];
        Ui.Header($"Piece {piece.Id} ({piece.Kind})");
        void Change(Action<PartGeometry> set) => session.Change(doc, () => { set(doc.Asset.Geometry[index]); doc.Asset.Validate(); });
        void Edit(Action<PartGeometry> set) => session.Edit(doc, () => { set(doc.Asset.Geometry[index]); doc.Asset.Validate(); });
        var locations = doc.Asset.IsAnimated ? doc.Asset.Controls.Select(c => c.Id).ToArray() : doc.Asset.Locations.Select(l => l.Id).Prepend(PartLocation.Origin).ToArray();
        if (Ui.Combo("Attached at", piece.A, locations) is { } a)
        {
            Edit(p => { p.A = a; if (p.Frame is not null) p.Frame = a; });
        }

        if (piece.Geometry is ICurve2D && piece.Frame is null && Ui.Combo("Toward", piece.B ?? "", locations) is { } b)
        {
            Edit(p => p.B = b);
        }

        _geometryInspector.Draw(piece, null, Change, doc.Asset);

        if (Ui.Button("Remove piece", doc.Asset.Geometry.Count > 1))
        {
            session.Edit(doc, () => { doc.Asset.Geometry.RemoveAt(index); doc.Asset.Validate(); });
            _partPiece = 0;
            _drag = -1;
        }

        Ui.Help("Pieces sit in the part's own frame; the instance's placement, mirror and fit on each wearer come from Model > Library parts.");
    }

    public void Overlay(ViewportFrame frame)
    {
        if (frame.Primary is not { } primary || session.PartDocument is not { } doc ||
            _partPiece >= doc.Asset.Geometry.Count || doc.Asset.Geometry[_partPiece].Mesh?.Outline is not { } points) return;
        var attachment = primary.Subject.Model.Attachments.FirstOrDefault(a => a.Asset.Id == doc.Id)
            ?? session.PreviewAttachmentOn(primary.Subject.Model);
        if (attachment is null || attachment.IsAnimated) return;
        var piece = doc.Asset.Geometry[_partPiece];
        var (world, localFrame) = attachment.Lookups(LayeredFrame2D.Identity);
        var socket = PartPlacement3D.GeometryFrame(PartAttachment2D.FrameOf(piece, world, localFrame),
            PartPlacement3D.Placement(attachment, new ActorPose(primary.Subject.Pose, Vector2.Zero, 1).Socket(attachment.Socket)),
            doc.Asset.Point(piece.A)?.Z ?? 0);
        var gold = StudioTheme.AppearanceSelection.PackedValue;
        for (var i = 0; i < points.Count; i++)
        {
            var p = frame.Screen(socket.TransformPoint(points[i].XYZ));
            var next = frame.Screen(socket.TransformPoint(points[(i + 1) % points.Count].XYZ));
            frame.Draw.AddLine(p, next, gold, 1);
            frame.Draw.AddCircleFilled(p, 4 * Ui.Scale, gold);
            if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Vector2.Distance(p, ViewportFrame.Mouse) < 8 * Ui.Scale)
            {
                _drag = i;
                session.Transport.Pause();
                session.BeginDrag();
            }
        }
        if (_drag < 0)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _drag = -1;
            session.EndDrag();
            return;
        }
        if (_drag >= points.Count || ImGui.GetIO().MouseDelta == Vector2.Zero)
        {
            return;
        }

        var coordinates = new Frame2D(socket.TransformPoint(points[_drag].XYZ).XY, socket.XAxis.XY, socket.YAxis.XY);
        if (MathF.Abs(coordinates.Matrix.GetDeterminant()) < 1e-6f ||
            !coordinates.TryToLocal(frame.World(ViewportFrame.Mouse).XY, out var delta))
        {
            return;
        }

        var edited = points.ToArray();
        edited[_drag] = points[_drag] with
        {
            X = points[_drag].X + delta.X,
            Y = points[_drag].Y + delta.Y
        };
        session.Change(doc, () => { doc.Asset.Geometry[_partPiece].Mesh!.Outline = [.. edited]; doc.Asset.Validate(); });
    }
}
