using App2d.Rendering.Textures;

namespace App2d.Gameplay.Assets;

public static class LadderAssets2D
{
    /// <summary>Tilesets may supply their own ladder; otherwise use Kenney's.</summary>
    public static string ResolvePath(TextureCache2D textures, string tilesetId, bool isTop)
    {
        var file = isTop ? "top.png" : "middle.png";
        var path = Path.Combine("environments", "tilesets", tilesetId, "ladder", file);
        return File.Exists(Path.Combine(textures.ContentRoot, path))
            ? path
            : Path.Combine("environments", "tilesets", "kenney-grassland", "ladder", file);
    }
}
