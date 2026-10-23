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
    private int _piece;
    private int _drag = -1;
    private string? _asset;
    public Workspace Mode => Workspace.Appearance;
    public float TimelineHeight => 80 * Ui.Scale;
    public void Timeline() => TransportBar.Draw(session, true);

    public void Outline()
    {
        if (session.PartDocument is { } part)
        {
            PartOutline(part);
            return;
        }

        Ui.Header("Hair and clothing");
        Ui.Help("New > Appearance creates a reusable asset. Right-click an asset to duplicate it.");
        if (Ui.Combo("Appearance", session.PreviewProp, session.Assets.Props.Where(p => p.Asset.Usage != "prop").Select(p => p.Id)) is { } id)
        {
            session.Open(id);
        }

        if (session.AppearanceDocument is not { } doc || doc.Asset.Usage == "prop")
        {
            return;
        }

        if (_asset != doc.Id)
        {
            _asset = doc.Id;
            _piece = 0;
            _drag = -1;
        }

        for (var i = 0; i < doc.Asset.Solids.Count; i++)
        {
            if (ImGui.Selectable($"Piece {i + 1}", i == _piece))
            {
                _piece = i;
            }
        }

        if (ImGui.Button("Add piece"))
        {
            session.Edit(doc, () =>
            {
                doc.Asset.Solids.Add(PropGeometry.Extrude([new(-.1f, 0), new(.1f, 0), new(.1f, .2f), new(-.1f, .2f)], .02f, "#8c4d2c"));
                _piece = doc.Asset.Solids.Count - 1;
            });
        }
    }

    public void Inspector()
    {
        if (session.PartDocument is { } part)
        {
            PartInspector(part);
            return;
        }

        if (session.AppearanceDocument is not { } doc || doc.Asset.Usage == "prop")
        {
            Ui.Help("Create or open hair or clothing, or a library part, from the asset browser.");
            return;
        }
        var art = doc.Asset;
        Ui.Header("Appearance asset");
        var name = art.Name;
        if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0)
        {
            session.Change(doc, () => doc.Asset.Name = name);
        }

        if (Ui.Combo("Type", art.Usage, ["hair", "clothing"]) is { } usage)
        {
            session.Edit(doc, () => doc.Asset.Usage = usage);
        }

        if (Ui.Combo("Wearer", session.EntityId ?? "", session.Assets.Entities.Select(e => e.Id)) is { } wearer)
        {
            session.SetEntity(wearer);
        }

        var model = session.Assets.Resolve(session.SubjectId);
        if (model is not null)
        {
            if (Ui.Combo("Socket", session.PreviewSocket, model.Base.Sockets.Select(s => s.Id)) is { } socket)
            {
                session.PreviewSocket = socket;
                session.Edit(doc, () => doc.Asset.Attachment = socket);
            }
            if (Ui.Button("Equip on wearer", session.EntityId is not null))
            {
                session.EquipAppearance(session.EntityId!, doc.Id, session.PreviewSocket);
            }
        }
        Ui.Help("Equipping adds this piece. Remove unwanted hair or clothes in Entity > Equipment. Shared edits affect all wearers.");
        var rest = session.ShowRest;
        if (ImGui.Checkbox("Rest pose", ref rest))
        {
            session.ShowRest = rest;
        }

        var size = art.Scale;
        if (Ui.Drag("Size", ref size, .005f, .001f, 100))
        {
            session.Change(doc, () => doc.Asset.Scale = Math.Clamp(size, .001f, 100));
        }

        var grip = art.Grip.Layered;
        if (CharacterUi.LayeredPoint("Attachment origin", ref grip))
        {
            session.Change(doc, () => doc.Asset.Grip = PuppetPoint.From(grip));
        }

        var ink = art.Ink;
        if (Ui.ColorHex("Ink", ref ink))
        {
            session.Change(doc, () => doc.Asset.Ink = ink);
        }

        var line = art.LineWidth;
        if (Ui.Drag("Line width", ref line, .001f, .001f, .2f))
        {
            session.Change(doc, () => doc.Asset.LineWidth = Math.Clamp(line, .001f, .2f));
        }

        if (model is not null && ImGui.Button("Match body ink"))
        {
            session.Edit(doc, () =>
            {
                doc.Asset.Ink = model.Base.Ink;
                doc.Asset.LineWidth = model.Base.LineWidth;
            });
        }

        if (Ui.Combo("Back view", art.BackView ?? "", session.Assets.Props.Where(p => p.Id != doc.Id && p.Asset.Usage != "prop").Select(p => p.Id).Prepend(""), id => id.Length == 0 ? "Same artwork" : id) is { } back)
        {
            session.Edit(doc, () => doc.Asset.BackView = back.Length == 0 ? null : back);
        }

        Ui.Help("Back view follows the player's view-back animation markers.");
        _piece = Math.Clamp(_piece, 0, Math.Max(0, art.Solids.Count - 1));
        if (art.Solids.Count > 0)
        {
            Piece(doc);
        }

        if (ImGui.Button("Save all"))
        {
            session.SaveAll();
        }

        foreach (var problem in session.Assets.Problems(doc))
        {
            Ui.Problem(problem);
        }
    }

    // ---- Library parts (P5c) -------------------------------------------------------------------------------------------

    private int _partPiece;
    private string _partAsset = "";
    private string _newLocation = "";

    private void PartOutline(AssetDocument<PartAsset> doc)
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
        }

        Ui.Header("Pieces");
        var asset = doc.Asset;
        for (var i = 0; i < asset.Parts.Count; i++)
        {
            if (ImGui.Selectable($"{asset.Parts[i].Id}  ({asset.Parts[i].Geometry?.Kind ?? "?"} on {asset.Parts[i].A})", _partPiece == i))
            {
                _partPiece = i;
            }
        }

        for (var i = 0; i < asset.Solids.Count; i++)
        {
            if (ImGui.Selectable($"solid {i + 1}  ({asset.Solids[i].Outline?.Count.ToString() ?? "mesh"} points)", _partPiece == asset.Parts.Count + i))
            {
                _partPiece = asset.Parts.Count + i;
            }
        }

        if (ImGui.SmallButton("Add shape"))
        {
            session.Edit(doc, () =>
            {
                doc.Asset.Parts.Add(new() { Id = ModelAuthoring.UniqueId("shape", doc.Asset.Parts.Select(p => p.Id)), A = PartLocation.Origin, Geometry = RoundedRectangle2D.Builder.FromSize(.12f, .1f, .02f), Material = new() { Fill = "#b97549", Outline = new() } });
                doc.Asset.Validate();
                _partPiece = doc.Asset.Parts.Count - 1;
            });
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Add curve"))
        {
            session.Edit(doc, () =>
            {
                if (doc.Asset.Locations.Count == 0)
                {
                    doc.Asset.Locations.Add(new() { Id = "end", Y = .2f });
                }

                doc.Asset.Parts.Add(new() { Id = ModelAuthoring.UniqueId("curve", doc.Asset.Parts.Select(p => p.Id)), A = PartLocation.Origin, B = doc.Asset.Locations[0].Id, Geometry = new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(), Material = new() { StrokeWidth = .03f } });
                doc.Asset.Validate();
                _partPiece = doc.Asset.Parts.Count - 1;
            });
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Add solid"))
        {
            session.Edit(doc, () =>
            {
                doc.Asset.Solids.Add(PropGeometry.Extrude([new(-.08f, 0), new(.08f, 0), new(.08f, .12f), new(-.08f, .12f)], .02f, "#8c4d2c"));
                doc.Asset.Validate();
                _partPiece = doc.Asset.Parts.Count + doc.Asset.Solids.Count - 1;
            });
        }

        Ui.Header("Locations");
        ImGui.TextDisabled("origin  (the socket)");
        foreach (var location in asset.Locations.ToArray())
        {
            ImGui.PushID(location.Id);
            var xy = new Vector2(location.X, location.Y);
            ImGui.TextUnformatted(location.Id);
            ImGui.SameLine(90 * Ui.Scale);
            ImGui.SetNextItemWidth(130 * Ui.Scale);
            if (ImGui.DragFloat2("##xy", ref xy, .005f, -100, 100))
            {
                session.Change(doc, () => { var live = doc.Asset.Locations.First(l => l.Id == location.Id); live.X = xy.X; live.Y = xy.Y; doc.Asset.Validate(); });
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("×"))
            {
                session.Edit(doc, () => { doc.Asset.Locations.RemoveAll(l => l.Id == location.Id); doc.Asset.Validate(); });
            }

            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(90 * Ui.Scale);
        ImGui.InputTextWithHint("##new-location", "location id", ref _newLocation, 40);
        ImGui.SameLine();
        if (Ui.Button("Add location", _newLocation.Trim().Length > 0))
        {
            var id2 = _newLocation.Trim();
            if (session.Edit(doc, () => { doc.Asset.Locations.Add(new() { Id = id2 }); doc.Asset.Validate(); }))
            {
                _newLocation = "";
            }
        }
    }

    private void PartInspector(AssetDocument<PartAsset> doc)
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

        Ui.Header("Preview on wearer");
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

            var basis = session.SubjectModel?.Id ?? session.SubjectVariant?.Asset.Base;
            if (Ui.Button($"Attach to {basis}", basis is not null && model.Base.Sockets.Count > 0))
            {
                session.AttachPart(doc.Id, session.PreviewSocket, session.PreviewMirror);
            }

            Ui.Help(worn.Length == 0 ? "The preview shows the part at the chosen socket until it is attached. Attaching creates an instance on the base model; variants override it per instance."
                : "Attaching again adds another instance (the other hand or foot, mirrored).");
        }

        var rest = session.ShowRest;
        if (ImGui.Checkbox("Rest pose", ref rest))
        {
            session.ShowRest = rest;
        }

        _partPiece = Math.Clamp(_partPiece, 0, Math.Max(0, asset.Parts.Count + asset.Solids.Count - 1));
        if (_partPiece < asset.Parts.Count)
        {
            PartPiece(doc, _partPiece);
        }
        else if (_partPiece - asset.Parts.Count < asset.Solids.Count)
        {
            SolidPiece(doc, _partPiece - asset.Parts.Count);
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

    private void PartPiece(AssetDocument<PartAsset> doc, int index)
    {
        var piece = doc.Asset.Parts[index];
        Ui.Header($"Piece {piece.Id} ({piece.Geometry?.Kind})");
        void Change(Action<PuppetPart> set) => session.Change(doc, () => { set(doc.Asset.Parts[index]); doc.Asset.Validate(); });
        void Edit(Action<PuppetPart> set) => session.Edit(doc, () => { set(doc.Asset.Parts[index]); doc.Asset.Validate(); });
        var locations = doc.Asset.Locations.Select(l => l.Id).Prepend(PartLocation.Origin).ToArray();
        if (Ui.Combo("Attached at", piece.A, locations) is { } a)
        {
            Edit(p => p.A = a);
        }

        if (piece.Geometry is ICurve2D && Ui.Combo("Toward", piece.B ?? "", locations) is { } b)
        {
            Edit(p => p.B = b);
        }

        var fill = piece.RenderMaterial.Fill ?? (piece.Geometry is ICurve2D ? doc.Asset.Ink : "#fff8e7");
        if (Ui.ColorHex("Fill", ref fill))
        {
            Change(p => p.Material = p.RenderMaterial with { Fill = fill });
        }

        if (piece.Geometry is ICurve2D)
        {
            var stroke = piece.RenderMaterial.StrokeWidth ?? doc.Asset.LineWidth;
            if (Ui.Drag("Stroke width", ref stroke, .001f, .001f, 2))
            {
                Change(p => p.Material = p.RenderMaterial with { StrokeWidth = stroke });
            }
        }
        else
        {
            var outlined = piece.RenderMaterial.Outline is not null;
            if (ImGui.Checkbox("Outline", ref outlined))
            {
                Edit(p => p.Material = p.RenderMaterial with { Outline = outlined ? new RenderOutlineDefinition2D() : null });
            }
        }
        var scale = new Vector2(piece.ScaleX, piece.ScaleY);
        if (Ui.Drag2("Scale X / Y", ref scale, .01f, -4, 4))
        {
            Change(p => { p.ScaleX = scale.X; p.ScaleY = scale.Y; });
        }

        var offset = new Vector2(piece.OffsetX, piece.OffsetY);
        if (Ui.Drag2("Offset X / Y", ref offset, .005f, -2, 2))
        {
            Change(p => { p.OffsetX = offset.X; p.OffsetY = offset.Y; });
        }

        var angle = float.RadiansToDegrees(piece.Angle);
        if (Ui.Drag("Angle (degrees)", ref angle, 1f, -720, 720))
        {
            Change(p => p.Angle = float.DegreesToRadians(angle));
        }

        var depth = piece.Depth;
        if (Ui.Drag("Layer offset", ref depth, .005f, -16, 16))
        {
            Change(p => p.Depth = depth);
        }

        var hidden = piece.Hidden;
        if (ImGui.Checkbox("Hidden", ref hidden))
        {
            Edit(p => p.Hidden = hidden);
        }

        if (Ui.Button("Remove piece", doc.Asset.Parts.Count + doc.Asset.Solids.Count > 1))
        {
            session.Edit(doc, () => { doc.Asset.Parts.RemoveAt(index); doc.Asset.Validate(); });
            _partPiece = 0;
        }

        Ui.Help("Pieces sit in the part's own frame; the instance's placement, mirror and fit on each wearer come from Model > Library parts.");
    }

    private void SolidPiece(AssetDocument<PartAsset> doc, int index)
    {
        var solid = doc.Asset.Solids[index];
        Ui.Header($"Solid {index + 1}");
        var fill = solid.RenderMaterial.Fill ?? "#8c4d2c";
        if (Ui.ColorHex("Fill", ref fill))
        {
            session.Change(doc, () => { var s = doc.Asset.Solids[index]; s.Material = s.RenderMaterial with { Fill = fill }; doc.Asset.Validate(); });
        }

        var outlined = solid.RenderMaterial.Outline is not null;
        if (ImGui.Checkbox("Outline", ref outlined))
        {
            session.Edit(doc, () => { var s = doc.Asset.Solids[index]; s.Material = s.RenderMaterial with { Outline = outlined ? s.RenderMaterial.Outline ?? new RenderOutlineDefinition2D() : null }; doc.Asset.Validate(); });
        }

        if (solid.Outline is { } points)
        {
            var thickness = solid.Thickness;
            if (Ui.Drag("Thickness", ref thickness, .001f, .0001f, 2))
            {
                session.Change(doc, () => { doc.Asset.Solids[index].Thickness = Math.Clamp(thickness, .0001f, 2); doc.Asset.RebuildSolids(); doc.Asset.Validate(); });
            }

            var layer = points.Average(p => p.Z);
            if (Ui.Drag("Layer", ref layer, .005f, -2, 2))
            {
                var delta = layer - points.Average(p => p.Z);
                session.Change(doc, () => { var s = doc.Asset.Solids[index]; s.Outline = s.Outline!.ConvertAll(p => p with { Z = p.Z + delta }); doc.Asset.RebuildSolids(); doc.Asset.Validate(); });
            }

            Ui.Help("Outline points are edited as coordinates for now; dragging library-part outlines in the viewport arrives with the cutout tools in P7.");
        }
        else
        {
            Ui.Help("This mesh has no editable outline.");
        }

        if (Ui.Button("Remove solid", doc.Asset.Parts.Count + doc.Asset.Solids.Count > 1))
        {
            session.Edit(doc, () => { doc.Asset.Solids.RemoveAt(index); doc.Asset.Validate(); });
            _partPiece = 0;
        }
    }

    private void Piece(AssetDocument<PropAsset> doc)
    {
        var piece = doc.Asset.Solids[_piece];
        Ui.Header($"Piece {_piece + 1}");
        var fill = piece.RenderMaterial.Fill!;
        if (Ui.ColorHex("Fill", ref fill))
        {
            session.Change(doc, () =>
        {
            var solid = doc.Asset.Solids[_piece];
            solid.Material = solid.RenderMaterial with
            {
                Fill = fill
            };
        });
        }

        var outlined = piece.RenderMaterial.Outline is not null;
        if (ImGui.Checkbox("Outline", ref outlined))
        {
            session.Edit(doc, () =>
        {
            var solid = doc.Asset.Solids[_piece];
            solid.Material = solid.RenderMaterial with
            {
                Outline = outlined ? solid.RenderMaterial.Outline ?? new RenderOutlineDefinition2D() : null
            };
        });
        }

        if (outlined)
        {
            var outlineColor = piece.RenderMaterial.Outline?.Color ?? doc.Asset.Ink;
            if (Ui.ColorHex("Outline color", ref outlineColor))
            {
                session.Change(doc, () =>
            {
                var solid = doc.Asset.Solids[_piece];
                solid.Material = solid.RenderMaterial with
                {
                    Outline = solid.RenderMaterial.Outline! with
                    {
                        Color = outlineColor
                    }
                };
            });
            }

            if (piece.RenderMaterial.Outline?.Color is not null && ImGui.SmallButton("Use prop ink"))
            {
                session.Change(doc, () =>
            {
                var solid = doc.Asset.Solids[_piece];
                solid.Material = solid.RenderMaterial with
                {
                    Outline = solid.RenderMaterial.Outline! with
                    {
                        Color = null
                    }
                };
            });
            }
        }
        if (Ui.Button("Remove piece", doc.Asset.Solids.Count > 1))
        {
            session.Edit(doc, () => doc.Asset.Solids.RemoveAt(_piece));
            _piece = 0;
            return;
        }
        if (piece.Outline is not { } points)
        {
            Ui.Help("This imported mesh has no editable cutout outline.");
            return;
        }
        var thickness = piece.Thickness;
        if (Ui.Drag("Thickness", ref thickness, .001f, .0001f, 2))
        {
            Cut(doc, points, Math.Clamp(thickness, .0001f, 2));
        }

        var layer = new CharacterLayer2D(points.Average(p => p.Z));
        if (CharacterUi.CharacterLayer("Piece layer", ref layer, -2, 2))
        {
            var delta = layer.Order - points.Average(p => p.Z);
            Cut(doc, points.Select(p => p with { Z = p.Z + delta }), thickness);
        }
        Ui.Help("Drag the gold points in the viewport, or edit coordinates below. Clothing can span both legs in layer order; keep the near arm in front of the belt.");
        if (session.PreviewSocket == Humanoid.BodySocket && session.Assets.Resolve(session.SubjectId) is { } model && model.Rest.ContainsKey(Humanoid.LeftHip) && model.Rest.ContainsKey(Humanoid.RightHip))
        {
            try
            {
                var layers = PersonWardrobeDepths.From(model);
                Ui.Help($"Layer guide: near arm {layers.NearArm:F3}, belt {layers.FrontDetail:F3}, wrap front {layers.WrapFront:F3}, near leg {layers.NearLeg:F3}, far leg {layers.FarLeg:F3}, wrap back {layers.WrapBack:F3}.");
            }
            catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException)
            {
                Ui.Help("This rig has custom layer spacing; place the garment using its layer and thickness.");
            }
        }
        if (ImGui.CollapsingHeader("Outline points"))
        {
            for (var i = 0; i < points.Count; i++)
            {
                ImGui.PushID(i);
                var p = points[i].XY;
                if (Ui.Drag2("XY", ref p))
                {
                    var changed = points.ToArray();
                    changed[i] = new(p.X, p.Y, points[i].Z);
                    Cut(doc, changed, thickness);
                }
                if (ImGui.SmallButton("Insert after"))
                {
                    var changed = points.ToList();
                    changed.Insert(i + 1, PuppetPoint.From((points[i].XYZ + points[(i + 1) % points.Count].XYZ) / 2));
                    Cut(doc, changed, thickness);
                }
                ImGui.SameLine();
                if (Ui.Button("Remove", points.Count > 3))
                {
                    var changed = points.ToList();
                    changed.RemoveAt(i);
                    Cut(doc, changed, thickness);
                }
                ImGui.PopID();
            }
        }
    }

    private void Cut(AssetDocument<PropAsset> doc, IEnumerable<PuppetPoint> points, float thickness)
    {
        var copy = points.ToArray();
        session.Change(doc, () => AppearanceAuthoring.Cutout(doc.Asset, _piece, copy, thickness));
    }

    public void Overlay(ViewportFrame frame)
    {
        if (frame.Primary is not { } primary || session.AppearanceDocument is not { } doc ||
            session.WeaponFor(primary.Subject.Model) is not { } worn || _piece >= doc.Asset.Solids.Count ||
            doc.Asset.Solids[_piece].Outline is not { } points)
        {
            return;
        }

        var socket = new ActorPose(primary.Subject.Pose, Vector2.Zero, 1).Socket(worn.Socket);
        var gold = StudioTheme.AppearanceSelection.PackedValue;
        for (var i = 0; i < points.Count; i++)
        {
            var p = frame.Screen(PropPlacement3D.PropPoint(socket, doc.Asset, points[i]));
            var next = frame.Screen(PropPlacement3D.PropPoint(socket, doc.Asset, points[(i + 1) % points.Count]));
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

        var coordinates = PropPlacement3D.PropPlane(socket, doc.Asset, points[_drag]);
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
        Cut(doc, edited, doc.Asset.Solids[_piece].Thickness);
    }
}
