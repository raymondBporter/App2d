using App2d.Core.Characters.Authored;
using App2d.Core.Rendering.Characters;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.CharacterStudio.AngeliaGallery;

/// <summary>
/// A grid of person figures, each looping one pose animation ported from the AngeliA engine. Poses are built directly from
/// forward kinematics (no clips, no IK) and drawn through the normal puppet path. Launch with <c>--angelia-gallery</c>;
/// <c>--angelia-gallery output-directory</c> saves a few frames as PNG and exits.
/// </summary>
internal sealed class AngeliaGalleryApp : Game
{
    private const int Columns = 5;
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetRoot;
    private readonly string? _snapshotDir; // when set: render a few frames to PNG and exit
    private static readonly int[] SnapshotFrames = [0, 5, 10, 20, 30];
    private int _snapshotIndex;
    private RenderTarget2D? _snapshotTarget;
    private PointCharacterRenderer _renderer = null!;
    private ImGuiHost _gui = null!;
    private ResolvedModel _model = null!;
    private AngeliaRig _rig = null!;
    private readonly PuppetDrawing[] _drawings = [.. AngeliaPoses.All.Select(_ => new PuppetDrawing())];
    private readonly CharacterMesh _ground = new(256);

    public AngeliaGalleryApp(string assetRoot, string? snapshotDir = null)
    {
        _assetRoot = assetRoot; _snapshotDir = snapshotDir;
        _graphics = new(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferredDepthStencilFormat = DepthFormat.Depth24,
            PreferMultiSampling = true,
            PreferredBackBufferWidth = 1500,
            PreferredBackBufferHeight = 900
        };
        Window.Title = "AngeliA poses on App2d figures"; Window.AllowUserResizing = true; IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _renderer = new(GraphicsDevice); _gui = new(this);
        var catalog = AuthoredCatalog.Load(Path.Combine(_assetRoot, "authored"));
        if (catalog.Errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, catalog.Errors));
        _model = catalog.Resolve("person");
        _rig = new(PoseEvaluator.Rest(_model).Points);
    }

    protected override void Draw(GameTime time)
    {
        _gui.Begin((float)time.ElapsedGameTime.TotalSeconds);
        var frame = _snapshotDir is null ? (int)(time.TotalGameTime.TotalSeconds * 60) : SnapshotFrames[_snapshotIndex];
        var screen = GraphicsDevice.PresentationParameters;
        if (_snapshotDir is not null)
        {
            _snapshotTarget ??= new(GraphicsDevice, screen.BackBufferWidth, screen.BackBufferHeight, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
            GraphicsDevice.SetRenderTarget(_snapshotTarget);
        }
        int rows = (AngeliaPoses.All.Length + Columns - 1) / Columns, cellW = screen.BackBufferWidth / Columns, cellH = screen.BackBufferHeight / rows;
        var ppu = Math.Min(cellH / 3.2f, cellW / 2.6f);
        var labels = ImGui.GetForegroundDrawList();

        GraphicsDevice.Viewport = new(0, 0, screen.BackBufferWidth, screen.BackBufferHeight);
        GraphicsDevice.Clear(new Color(237, 238, 226));
        for (var i = 0; i < AngeliaPoses.All.Length; i++)
        {
            var (name, poseAt) = AngeliaPoses.All[i];
            var pose = new EvaluatedPose();
            _rig.Apply(poseAt(frame), pose.Points);
            pose.Expressions["head"] = "relaxed";
            _drawings[i].Build(_model, pose);

            int x = i % Columns * cellW, y = i / Columns * cellH;
            GraphicsDevice.Viewport = new(x, y, cellW, cellH);
            var projection = PointCharacterRenderer.Projection(cellW, cellH, new(cellW * .5f, cellH * .8f), ppu);
            _ground.Clear(); _ground.Line(new(-1.2f, 0, 1), new(1.2f, 0, 1), 1.5f / ppu, new Color(154, 169, 158));
            _renderer.Draw(_ground, projection, Matrix.Identity, writeDepth: false);
            _renderer.Draw(_drawings[i].Mesh, projection, Matrix.Identity);
            labels.AddText(new System.Numerics.Vector2(x + 12, y + 8), 0xFF3A3A3A, name);
        }
        GraphicsDevice.Viewport = new(0, 0, screen.BackBufferWidth, screen.BackBufferHeight);
        _gui.Render();
        if (_snapshotTarget is not null)
        {
            GraphicsDevice.SetRenderTarget(null);
            Directory.CreateDirectory(_snapshotDir!);
            using (var stream = File.Create(Path.Combine(_snapshotDir!, $"gallery-{frame:D3}.png"))) _snapshotTarget.SaveAsPng(stream, _snapshotTarget.Width, _snapshotTarget.Height);
            if (++_snapshotIndex >= SnapshotFrames.Length) { Exit(); return; }
        }
        base.Draw(time);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _snapshotTarget?.Dispose(); _renderer?.Dispose(); _gui?.Dispose(); }
        base.Dispose(disposing);
    }
}
