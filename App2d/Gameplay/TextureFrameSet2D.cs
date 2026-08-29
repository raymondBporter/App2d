using App2d.Engine.Rendering.Textures;

namespace App2d.Gameplay;

internal sealed class TextureFrameSet2D(TextureCache2D textures, string[] relativePaths)
{
    private readonly TextureCache2D _textures = ArgGuard.RequireNotNull(textures);
    private readonly string[] _relativePaths = ArgGuard.RequireNotNull(relativePaths);

    public int Count => _relativePaths.Length;

    public Texture2D this[int index] => _textures.Load(_relativePaths[index]);
}
