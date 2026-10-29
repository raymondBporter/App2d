using App2d.Core.Characters;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Model;

internal static class MaterialAnimationFields
{
    public static void Draw(PartGeometry part, Action<Action<PartGeometry>> change)
    {
        var material = part.RenderMaterial;
        var alpha = material.Alpha ?? 1;
        if (Ui.Slider("Material alpha", ref alpha, 0, 1)) change(p => p.Material = p.RenderMaterial with { Alpha = alpha });
        var tint = ColorExtensions.FromHexRgba(material.Tint ?? "ffffffff").ToNumericsVector4();
        var rgb = new Vector3(tint.X, tint.Y, tint.Z);
        if (ImGui.ColorEdit3("Material tint", ref rgb))
        {
            var color = ColorExtensions.FromVector4(new(rgb, tint.W));
            change(p => p.Material = p.RenderMaterial with { Tint = $"{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}" });
        }
        if (part.Geometry is not Rectangle2D) return;
        Ui.Header("Texture");
        var texture = material.Texture ?? "";
        if (ImGui.InputText("Image path (Enter)", ref texture, 512, ImGuiInputTextFlags.EnterReturnsTrue))
            change(p => p.Material = p.RenderMaterial with { Texture = string.IsNullOrWhiteSpace(texture) ? null : texture.Trim() });
        Region("Image region", material.Source, region => change(p => p.Material = p.RenderMaterial with { Source = region }));
        if (material.TextureAnimation is not { Frames.Count: > 0 } animation)
        {
            if (Ui.Button("Animate texture", material.Texture is not null))
                change(p => p.Material = p.RenderMaterial with { TextureAnimation = new() { Frames = [new() { Texture = material.Texture! }] } });
            return;
        }
        var loop = animation.Loop;
        if (ImGui.Checkbox("Loop texture frames", ref loop))
            change(p => p.Material = p.RenderMaterial with { TextureAnimation = animation with { Loop = loop } });
        for (var i = 0; i < animation.Frames.Count; i++)
        {
            var index = i;
            var frame = animation.Frames[i];
            void Set(TextureFrame value) => change(p => p.Material = p.RenderMaterial with
            { TextureAnimation = animation with { Frames = [.. animation.Frames.Select((f, n) => n == index ? value : f)] } });
            ImGui.PushID(i);
            var path = frame.Texture;
            if (ImGui.InputText("Frame image (Enter)", ref path, 512, ImGuiInputTextFlags.EnterReturnsTrue)) Set(frame with { Texture = path.Trim() });
            var seconds = frame.Seconds;
            if (Ui.Drag("Seconds", ref seconds, .01f, .001f, 600)) Set(frame with { Seconds = seconds });
            Region("Frame region", frame.Source, source => Set(frame with { Source = source }));
            if (Ui.Button("Remove frame", animation.Frames.Count > 1))
                change(p => p.Material = p.RenderMaterial with { TextureAnimation = animation with { Frames = [.. animation.Frames.Where((_, n) => n != index)] } });
            ImGui.PopID();
        }
        if (Ui.Button("Add frame", animation.Frames.Count < 512))
            change(p => p.Material = p.RenderMaterial with { TextureAnimation = animation with { Frames = [.. animation.Frames, animation.Frames[^1]] } });
        if (ImGui.SmallButton("Use still image"))
            change(p => p.Material = p.RenderMaterial with { Texture = material.Texture ?? animation.Frames[0].Texture, TextureAnimation = new() });
        Ui.Help("Frames use the pose time; image dimensions do not change this rectangle's size or anchor. Animate alpha and tint in its piece channels.");
    }

    private static void Region(string label, TextureRegion? region, Action<TextureRegion?> set)
    {
        var crop = region is not null && region != new TextureRegion();
        if (ImGui.Checkbox(label, ref crop)) set(crop ? new() { Width = .5f } : new());
        if (!crop || region is null) return;
        var xy = new Vector2(region.X, region.Y);
        if (Ui.Drag2(label + " X / Y", ref xy, .005f, 0, 1)) set(region with { X = xy.X, Y = xy.Y });
        var size = new Vector2(region.Width, region.Height);
        if (Ui.Drag2(label + " width / height", ref size, .005f, .00001f, 1)) set(region with { Width = size.X, Height = size.Y });
    }
}
