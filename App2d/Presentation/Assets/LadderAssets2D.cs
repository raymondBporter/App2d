using App2d.Core.Rendering.Textures;

namespace App2d.Presentation.Assets;

public static class LadderAssets2D
{
    /// <summary>Tilesets may supply their own ladder; otherwise use the medieval ink ladder.</summary>
    public static string ResolvePath(TextureCache2D textures, string tilesetId, bool isTop)
    {
        var file = isTop ? "top.png" : "middle.png";
        var path = Path.Combine("environments", "tilesets", tilesetId, "ladder", file);
        return File.Exists(Path.Combine(textures.ContentRoot, path))
            ? path
            : Path.Combine("environments", "tilesets", "ink-medieval-ground", "ladder", file);
    }
}
