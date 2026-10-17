using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Rendering.Characters;
using App2d.Core.Shapes;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

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
            SeedModel = CharacterTemplates.Instantiate,
            SeedAppearance = id => PersonWardrobe.Props().FirstOrDefault(p => p.Id == id)
        };
        var slotClip = CreateSlotPreview(assets);
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
            void Capture(string name)
            {
                for (var frame = 0; frame < 4; frame++)
                {
                    gui.Begin(1f / 60);
                    shell.Draw();
                    using (device.UseRenderTarget(target, new Color(19, 23, 29)))
                    {
                        gui.Render();
                    }
                }
                target.SavePng(Path.Combine(outputDirectory, $"{name}-{width}x{height}.png"));
            }

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

            var cutoutModel = assets.Models.FirstOrDefault(m => m.Asset.Parts.Any(p => p.Geometry is SimplePolygon2D.Builder));
            if (cutoutModel is not null)
            {
                session.Open(cutoutModel.Id);
                session.Selection.Select(part: cutoutModel.Asset.Parts.First(p => p.Geometry is SimplePolygon2D.Builder).Id);
                Capture("model-cutout");
            }

            session.Open("person-walk");
            session.Selection.Select(control: Humanoid.Chest);
            session.Seek(0);
            Capture("animation-control");
            if (assets.Props.FirstOrDefault(p => p.Asset.Usage == "prop") is { } prop &&
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
            foreach (var (role, at) in new[] { ("smile", 1.5f), ("blink", 1f), ("wink", 1f), ("angry", 1f), ("worried", 1f), ("surprised", 1f), ("crossed-eyes", 1f), ("hair-wave", .375f) })
            {
                session.Open("curve-face-" + role);
                session.ShowRest = session.EditRig = false;
                session.SetMode(Workspace.Model);
                session.Selection.Clear();
                session.Seek(at);
                shell.Viewport.Fit(session.Scene().FirstOrDefault());
                Capture("curve-face-" + role);
            }
            session.Open("curve-face");
            session.Selection.Select(part: "hair");
            Capture("curve-face-hair-inspector");
        }
        Console.WriteLine("Animation settings, controls, weapon and slot inspectors, model views and curve facial studies rendered at two sizes and scales.");
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
