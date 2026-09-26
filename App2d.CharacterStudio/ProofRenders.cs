using App2d.Rendering.Characters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.CharacterStudio;

/// <summary>
/// Headless review renders of the authored assets through the real drawing path: the shared-motion proof, the entity arena
/// proof and the player move review. Each mode writes its frames and report to an output directory, then exits.
/// </summary>
internal sealed partial class ProofRenders : Game
{
    public enum Mode { Motion, Entities, MoveReview }

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetRoot, _smokePath;
    private readonly Mode _mode;
    private PointCharacterRenderer _renderer = null!;
    private int _smokeIndex;

    /// <param name="assetRoot">The characters folder holding <c>authored</c>.</param>
    public ProofRenders(string assetRoot, string output, Mode mode)
    {
        _assetRoot = assetRoot; _smokePath = output; _mode = mode;
        _graphics = new(this) { GraphicsProfile = GraphicsProfile.HiDef, PreferredDepthStencilFormat = DepthFormat.Depth24, PreferMultiSampling = true,
            PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 360 };
        Window.Title = "Character proof renders | App2d";
    }

    protected override void LoadContent() { _renderer = new(GraphicsDevice); Directory.CreateDirectory(_smokePath); }

    protected override void Draw(GameTime time)
    {
        var more = _mode switch { Mode.Motion => PrepareMotionProof(), Mode.Entities => PrepareEntityProof(), _ => RenderMoveReview() };
        _smokeIndex++;
        GraphicsDevice.SetRenderTarget(null);
        if (!more) { Exit(); return; }
        base.Draw(time);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _proofTarget?.Dispose(); _renderer?.Dispose(); }
        base.Dispose(disposing);
    }
}
