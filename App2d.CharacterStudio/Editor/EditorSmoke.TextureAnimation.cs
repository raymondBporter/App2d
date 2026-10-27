using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Characters.Editing;
using App2d.Core.Geometry.Curves;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

internal sealed partial class EditorSmoke
{
    private TextureAnimation? _textureProof;

    private void BuildTextureAnimation(EditorShell shell)
    {
        var s = shell.Session;
        const string folder = "images/texture-proof";
        Directory.CreateDirectory(Path.Combine(s.Assets.Root, folder));
        foreach (var (name, width, height, color) in new[] { ("small", 32, 16, Color.Coral), ("wide", 128, 32, Color.Turquoise) })
        {
            using var image = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.Clear(color);
                graphics.FillRectangle(Brushes.White, width / 2, 0, width / 8, height);
                graphics.FillRectangle(Brushes.DarkSlateGray, width * 7 / 8, 0, width / 8, height);
            }
            image.Save(Path.Combine(s.Assets.Root, folder, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        _textureProof = new() { Frames = [new() { Texture = folder + "/small.png", Seconds = .25f },
            new() { Texture = folder + "/wide.png", Seconds = .75f, Source = new() { X = .5f, Width = .5f } }] };
        Check(s.NewModel("texture-proof", "Texture on a moving bone", "empty"), s);
        Check(s.Edit(s.SubjectModel!, () =>
        {
            var model = s.SubjectModel!.Asset;
            ModelAuthoring.AddBone(model, null, "carrier");
            model.Parts.Add(new() { Id = "panel", A = "carrier", Frame = "carrier", Geometry = Rectangle2D.Builder.FromSize(1.5f, .8f),
                Material = new() { TextureAnimation = _textureProof, Alpha = .9f } });
        }), s);
        Check(s.NewClip("texture-proof-motion", "Sway and fade", 1), s);
        Check(s.Edit(s.ClipDocument!, () =>
        {
            var clip = s.ClipDocument!.Asset;
            ClipAuthoring.SetKey(clip, new(MotionClip.RotateKind, "carrier"), 0, default, -.3f);
            ClipAuthoring.SetKey(clip, new(MotionClip.RotateKind, "carrier"), 1, default, .6f);
            ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "carrier"), 0, Vector3.Zero);
            ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "carrier"), 1, new(.3f, .2f, 0));
            ClipAuthoring.SetKey(clip, new(MotionClip.AlphaKind, "panel"), 0, Vector3.UnitX);
            ClipAuthoring.SetKey(clip, new(MotionClip.AlphaKind, "panel"), 1, new(.25f, 0, 0));
            ClipAuthoring.SetKey(clip, new(MotionClip.TintKind, "panel"), 0, Vector3.One);
            ClipAuthoring.SetKey(clip, new(MotionClip.TintKind, "panel"), 1, new(.5f, .9f, 1));
        }), s);
        s.ShowRest = s.EditRig = false;
        s.SetMode(Workspace.Animate);
        s.Selection.Select(part: "panel");
        s.Seek(.1f);
        shell.Viewport.Fit(s.Scene().First());
        CheckTextureAnimation(s, folder + "/small.png");
    }

    private void CheckTextureAnimation(EditorSession s, string texture)
    {
        var subject = s.Scene().First();
        var drawing = new PuppetDrawing(); drawing.Build(subject.Model, subject.Pose);
        Record(drawing.Batches.Single().Texture == texture, "texture sequence samples " + texture);
        Record(drawing.Mesh.Vertices.ToArray().All(v => v.Color.A is > 0 and < 230), "material alpha multiplies the keyed fade");
        Record(!s.Transport.Playing && subject.Pose.Angles["carrier"] != 0, "seeking poses the carrier and texture without advancing playback");
    }

    private void SaveTextureAnimation(EditorShell shell)
    {
        var s = shell.Session;
        var document = s.ClipDocument!;
        var before = document.Asset.ToJson();
        Check(s.Edit(document, () => ClipAuthoring.SetKey(document.Asset, new(MotionClip.AlphaKind, "panel"), .5f, new(.1f, 0, 0))), s);
        document.Undo();
        Record(document.Asset.ToJson() == before, "texture/material animation edit undoes on the existing clip document");
        s.SaveAll();
        var reopened = AuthoringWorkspace.Open(s.Assets.Root);
        Record(reopened.Clip(document.Id)!.Asset.ToJson() == before && reopened.Model("texture-proof")!.Asset.Parts[0].Material!.TextureAnimation!.Frames.Count == 2,
            "texture frames and ordinary material tracks survive save/reopen");
        s.Seek(.5f);
        CheckTextureAnimation(s, "images/texture-proof/wide.png");
    }

    private void PreviewTexturePath(EditorShell shell)
    {
        var s = shell.Session;
        Check(s.NewPart("texture-path", "Texture on a path"), s);
        var doc = s.PartDocument!;
        Check(s.Edit(doc, () =>
        {
            doc.Asset.Geometry = [new() { Id = "panel", A = PartLocation.Origin, Geometry = Rectangle2D.Builder.FromSize(1.5f, .8f), Material = new() { TextureAnimation = _textureProof } }];
            doc.Asset.Motion = new() { Curve = CubicBezier2D.Builder.Create(Vector2.Zero, new(0, 1), new(1, 1), new(1, 0)), PeriodSeconds = 2 };
        }), s);
        s.PreviewPartOnWearer = false;
        s.ShowRest = false;
        s.Seek(.5f);
        var subject = s.Scene().Single();
        var drawing = new PuppetDrawing(); drawing.Build(subject.Model, subject.Pose);
        Record(drawing.Batches.Single().Texture == "images/texture-proof/wide.png" && subject.Model.Attachments[0].Socket.OffsetX > 0,
            "one standalone transport previews a static Part's texture and path motion");
        Check(s.Save(doc), s);
        shell.Viewport.Fit(subject);
    }
}
