using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.CharacterSeeds;
using App2d.CharacterStudio.Editing;
using App2d.CharacterStudio.Importing;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Shapes;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Presentation.Player;
using App2d.Presentation.Persons;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>Renders the real editor on a hidden WinForms surface without constructing a MonoGame Game.</summary>
internal static class EditorSurfaceSmoke
{
    public static void Run(string authoredRoot, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var window = new Form { ClientSize = new Size(1600, 940) };
        using var surface = new Control { Dock = DockStyle.Fill };
        window.Controls.Add(surface);
        using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
            new PresentationParameters
            {
                DeviceWindowHandle = surface.Handle,
                BackBufferWidth = 1600,
                BackBufferHeight = 940,
                DepthStencilFormat = DepthFormat.Depth24,
                PresentationInterval = PresentInterval.Immediate
            });
        // A second live host also exercises context selection while fonts and styles are initialized.
        using var otherSurface = new Control();
        using var otherGui = new ImGuiHost(device, new WinFormsImGuiPlatform(otherSurface));
        using var gui = new ImGuiHost(device, new WinFormsImGuiPlatform(surface));
        using var renderer = new PointCharacterRenderer(device);
        var assets = AuthoringWorkspace.Open(authoredRoot);
        var sources = SourceLibraries.Open(Path.GetDirectoryName(Path.GetFullPath(authoredRoot))!);
        var session = new EditorSession(assets, sources)
        {
            SeedAppearance = id => PersonWardrobe.Props().FirstOrDefault(p => p.Id == id)
        };
        var slotClip = CreateSlotPreview(assets);
        // In-memory fixtures only: exercise the manual primitive tools without adding shipped assets.
        var localGeometry = assets.Create(new GeometryAsset2D { Id = "surface-polygon", Name = "Local polygon", Geometry = GeometryJson2D.CreateDefault(ShapeKinds2D.SimplePolygon) });
        localGeometry.Replace(GeometryAssetEditing.InsertAfter(GeometryAssetEditing.InsertAfter(localGeometry.Asset, 0), 2));
        assets.Create(new RenderMaterialAsset2D { Id = "surface-material", Name = "Library surface" });
        assets.Create(new Piece { Id = "surface-piece", Name = "Library piece", Geometry = localGeometry.Id, Material = new() { Asset = "surface-material" }, Groups = ["Clothes/Armor"], Attachment = Humanoid.LeftFootSocket });
        using var shell = new EditorShell(session, new Viewport(device, gui, renderer), new ArenaTest(assets, device, gui, renderer), gui);
        session.Open("person-walk");
        session.Pin("tall-thin");
        session.Pin("short-broad");
        session.Seek(.3f);
        if (session.ActiveDocument is null)
        {
            throw new InvalidOperationException(session.Message);
        }

        foreach (var (width, height, scale) in new[] { (1600, 940, 1f), (1200, 800, 1.25f) })
        {
            window.ClientSize = new Size(width, height);
            device.Reset(new PresentationParameters
            {
                DeviceWindowHandle = surface.Handle,
                BackBufferWidth = width,
                BackBufferHeight = height,
                DepthStencilFormat = DepthFormat.Depth24,
                PresentationInterval = PresentInterval.Immediate
            });
            gui.UserScale = scale;
            using var target = RenderCapture2D.CreateTarget(device, width, height, DepthFormat.None, 0);
            void Capture(string name, Action? draw = null)
            {
                for (var frame = 0; frame < 4; frame++)
                {
                    gui.Begin(1f / 60);
                    if (draw is null) shell.Draw(); else draw();
                    using (device.UseRenderTarget(target, StudioTheme.ApplicationBackground))
                    {
                        gui.Render();
                    }
                }
                target.SavePng(Path.Combine(outputDirectory, $"{name}-{width}x{height}.png"));
            }

            session.Open(localGeometry.Id);
            Capture("library-geometry");
            shell.LibraryInspector.ShowGeometryWindow(localGeometry.Id);
            Capture("library-geometry-window");
            shell.LibraryInspector.CloseGeometryWindow();
            session.Open("surface-material");
            Capture("library-render-material");
            session.Open("surface-piece");
            Capture("library-piece");
            var equipmentPiece = assets.Piece("surface-piece")!;
            equipmentPiece.Edit(() =>
            {
                equipmentPiece.Asset.Game = new() { Equipment = new() { Slot = PersonEquipmentSlots.Shins } };
                equipmentPiece.Asset.ScaleX = .1f;
                equipmentPiece.Asset.ScaleY = .22f;
                equipmentPiece.Asset.Y = .18f;
            });
            session.SetSubject("person");
            session.Compare.Clear();
            session.PreviewPartOnWearer = true;
            shell.LibraryInspector.FocusEquipment();
            Capture("equipment-overlay-preview");
            equipmentPiece.Edit(() => equipmentPiece.Asset.Game!.Equipment!.Replace = true);
            shell.LibraryInspector.FocusEquipment();
            Capture("equipment-replacement-preview");
            session.PreviewPartOnWearer = false;
            equipmentPiece.Edit(() =>
            {
                equipmentPiece.Asset.Game = null;
                equipmentPiece.Asset.ScaleX = equipmentPiece.Asset.ScaleY = 1;
                equipmentPiece.Asset.Y = 0;
            });
            var (wardrobe, doll) = EquipmentPreview(authoredRoot, outputDirectory);
            try
            {
                var cell = Enumerable.Range(0, wardrobe.Inventory.Count).Single(i => wardrobe.Inventory[i]?.Id == "surface-test-equipment");
                wardrobe.Equip(cell, WardrobeSlot2D.Shins);
                doll.Open();
                Capture("equipment-inventory-pair", () => doll.Draw(device, gui, new(1f / 60, 0, 0), canOpen: false));
                wardrobe.Unequip(WardrobeSlot2D.Shins);
                Capture("equipment-inventory-unequipped", () => doll.Draw(device, gui, new(1f / 60, 0, 0), canOpen: false));
            }
            finally { doll.ReleaseDevice(gui); }
            session.Pin("tall-thin");
            session.Pin("short-broad");

            session.Open("person-walk");
            session.EditRig = false;
            session.Seek(.3f);
            Capture("character-editor-surface");
            session.Open("person");
            session.Selection.Select(part: Humanoid.Body);
            Capture("model-base-part");
            session.EditRig = true;
            session.Selection.Select(control: Humanoid.Chest);
            Capture("model-base-rig");
            session.Open("tall-thin");
            session.EditRig = false;
            session.Selection.Select(part: Humanoid.Body);
            Capture("model-variant-part");
            // P6c: the variant's rig inspector shows each property's provenance, the length/tip override and the limits placeholder.
            session.EditRig = true;
            session.Selection.Select(control: Humanoid.LeftElbow);
            if (assets.Variant("tall-thin") is { } tall && assets.Resolve("tall-thin") is { } resolved)
            {
                session.Edit(tall, () => ModelAuthoring.ResizeBone(resolved, tall.Asset, Humanoid.LeftElbow, resolved.Nodes[Humanoid.LeftElbow].Length + .15f));
                Capture("model-variant-rig");
                session.Undo();
            }
            session.EditRig = false;
            // P7c: the Person's face instance on the base model's attachment inspector.
            session.Open("person");
            session.Selection.Select(attachment: PersonTemplate.FaceInstance);
            Capture("model-face-instance");
            if (assets.Part("boot") is { } boot)
            {
                // P5c: a library part on a wearer (preview before attaching), then the instance on the base model.
                session.Open("person");
                session.Open(boot.Id);
                session.PreviewSocket = Humanoid.LeftFootSocket;
                Capture("appearance-library-part");
                if (session.AttachPart(boot.Id, Humanoid.LeftFootSocket))
                {
                    session.Open("person");
                    session.Selection.Select(attachment: assets.Model("person")!.Asset.Attachments[^1].Id);
                    Capture("model-attachment");
                    session.Undo();
                }
            }

            var cutoutModel = assets.Models.FirstOrDefault(m => m.Asset.Parts.Any(p => p.Geometry is SimplePolygon2D));
            if (cutoutModel is not null)
            {
                session.Open(cutoutModel.Id);
                session.Selection.Select(part: cutoutModel.Asset.Parts.First(p => p.Geometry is SimplePolygon2D).Id);
                Capture("model-cutout");
            }

            session.Open("person-walk");
            session.Selection.Select(control: Humanoid.Chest);
            session.Seek(0);
            Capture("animation-control");
            if (assets.Parts.FirstOrDefault(p => p.Asset.Usage == "prop") is { } prop &&
                session.Scene().FirstOrDefault()?.Model.Base.Sockets.FirstOrDefault() is { } socket)
            {
                session.PreviewProp = prop.Id;
                session.PreviewSocket = socket.Id;
                session.PreviewWeapon = session.EditWeapon = true;
                session.Selection.Clear();
                Capture("animation-weapon");
            }
            session.PreviewWeapon = session.EditWeapon = false;
            session.Open(slotClip);
            session.Selection.Select(slot: "paint");
            session.Seek(0);
            shell.Viewport.Fit(session.Scene().FirstOrDefault());
            Capture("animation-slot");
            // P7a: an animated part (the tail) instanced on the Person, its exposed tip selected in the walk.
            if (assets.Part(PartSeeds.TailId) is null)
            {
                assets.Create(PartSeeds.Tail());
            }

            session.Open("person");
            if (assets.Model("person")!.Asset.Attachments.All(a => a.Asset != PartSeeds.TailId) && session.AttachPart(PartSeeds.TailId, Humanoid.BodySocket))
            {
                session.Open("person-walk");
                session.Seek(.3f);
                session.Selection.Select(control: "tail/tip");
                shell.Viewport.Fit(session.Scene().FirstOrDefault());
                Capture("animation-nested-part");
                session.Selection.Select(attachment: PartSeeds.TailId);
                Capture("animation-part-expression");
                session.Open("person");
                session.Selection.Select(attachment: assets.Model("person")!.Asset.Attachments.First(a => a.Asset == PartSeeds.TailId).Id);
                Capture("model-animated-attachment");
                session.Undo();
            }
            // P8c: the converted spear guard's colliders in the Entity inspector, and the overlay of the registered colliders inside the thrust window.
            if (assets.Entity("spear-guard") is { Asset.Behavior.Colliders.Count: > 0 })
            {
                session.Open("spear-guard");
                session.Selection.Collider = "reach";
                Capture("entity-colliders");
                session.PreviewActionOf("attack");
                if (session.Entity is null)
                {
                    Console.WriteLine("entity-collider-overlay skipped: " + session.EntityError);
                    return;
                }

                var thrust = session.Entity.Actions["attack"].Hits[0];
                session.Seek(thrust.Start + .02f);
                session.Selection.Collider = "spear-tip";
                shell.Viewport.Fit(session.Scene().FirstOrDefault());
                Capture("entity-collider-overlay");
                // P9c: the typed behavior inspector on the version-4 shield defender (strike, receiving damage, guard, properties).
                session.Open("shield-defender");
                session.Selection.Collider = null;
                shell.EntityInspector.ScrollTo = "Behaviors";
                Capture("entity-behaviors");
                shell.EntityInspector.ScrollTo = null;
            }
        }
        Console.WriteLine("Animation settings, controls, weapon and slot inspectors, model views and curve facial studies rendered at two sizes and scales.");
    }

    private static (PlayerWardrobe2D Wardrobe, PaperDollUi2D Doll) EquipmentPreview(string authoredRoot, string outputDirectory)
    {
        var root = Path.Combine(outputDirectory, "equipment-fixture");
        foreach (var path in Directory.EnumerateFiles(authoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(root, Path.GetRelativePath(authoredRoot, path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target, overwrite: true);
        }
        var fixture = new Piece
        {
            Id = "surface-test-equipment", Name = "QA shin guard",
            InlineGeometry = new() { Id = "inline", Name = "QA geometry", Geometry = Rectangle2D.Builder.FromSize(.09f, .22f, new(0, .18f)) },
            Material = new() { Fill = "#927044" },
            Game = new() { Equipment = new() { Slot = PersonEquipmentSlots.Shins } },
        };
        Directory.CreateDirectory(Path.Combine(root, "pieces"));
        AuthoredAsset.Write(Path.Combine(root, "pieces", fixture.Id + ".json"), fixture.ToJson());
        var wardrobe = new PlayerWardrobe2D(PersonMoves.From(AuthoredCatalog.Load(root)));
        return (wardrobe, new(wardrobe));
    }

    private static string CreateSlotPreview(AuthoringWorkspace assets)
    {
        var model = ModelAuthoring.Empty("surface-slots", "Slot preview");
        var bone = ModelAuthoring.AddBone(model, null, "root");
        ModelAuthoring.AddPartToBone(model, RoundedRectangle2D.Builder.FromSize(.4f, .4f, .05f), bone.Id);
        // An unattached slot exercises its inspector without requiring an external image fixture.
        model.Slots = [new() { Id = "paint", Bone = bone.Id }];
        assets.Create(model);
        var clip = ClipAuthoring.New(ResolvedModel.From(model), "surface-slot-clip", "Slot animation");
        ClipAuthoring.SetAttachmentKey(clip, "paint", 0, null);
        ClipAuthoring.SetColorKey(clip, "paint", SlotColorTrack2D.Rgba, 0, new(.3f, .6f, .8f, 1));
        ClipAuthoring.SetColorKey(clip, "paint", SlotColorTrack2D.Rgba, 1, Vector4.One);
        ClipAuthoring.SetColorCurve(clip, "paint", SlotColorTrack2D.Rgba, 0, 0, new()
        {
            Control1 = new(.2f, 0),
            Control2 = new(.8f, 1)
        });
        assets.Create(clip);
        return clip.Id;
    }
}
