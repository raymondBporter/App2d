using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using App2d.EditorUi;
using ImGuiNET;

namespace App2d.Demos;

/// <summary>
/// The person as a skeleton, clips and attachments: pick a clip, lay another over the upper body, and change the build.
/// When the authored JSON rig is found it is drawn alongside, at the standard build, for comparison.
/// </summary>
internal sealed class PersonDemo : IDemo
{
    private static readonly string[] Clips = [.. PersonClips.All.Select(c => c.Name)];
    private static readonly string[] Layers = ["(none)", .. Clips];

    private readonly LayeredMesh _mesh = new();
    private int _clip = Math.Max(0, Array.IndexOf(Clips, "person-walk"));
    private int _layer;
    private float _seconds, _weight = 1, _legs = 1, _torso = 1, _arms = 1, _width = 1, _head = 1;
    private bool _paused;

    public string Name => "Person";
    public void Draw() => DemoCanvas.Layout(DrawCanvas, DrawControls);

    public void Update(float seconds)
    {
        if (!_paused) _seconds += seconds;
    }

    /// <summary>The authored characters folder, looked for upward from where the demo runs. Null when it is not there.</summary>

    private void DrawControls()
    {
        ImGui.TextUnformatted("Clip");
        ImGui.SetNextItemWidth(-1);
        ImGui.Combo("##clip", ref _clip, Clips, Clips.Length);
        ImGui.TextUnformatted("Over the upper body");
        ImGui.SetNextItemWidth(-1);
        ImGui.Combo("##layer", ref _layer, Layers, Layers.Length);
        ImGui.SetNextItemWidth(-1);
        ImGui.SliderFloat("##weight", ref _weight, 0f, 1f, "weight %.2f");
        ImGui.Checkbox("Pause", ref _paused);
        ImGui.Separator();
        ImGui.TextUnformatted("Build");
        Slider("legs", ref _legs);
        Slider("torso", ref _torso);
        Slider("arms", ref _arms);
        Slider("width", ref _width);
        Slider("head", ref _head);
        if (ImGui.Button("Standard build")) _legs = _torso = _arms = _width = _head = 1;
    }

    private static void Slider(string label, ref float value)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.SliderFloat("##" + label, ref value, .6f, 1.5f, label + " %.2f");
    }

    private void DrawCanvas()
    {
        var canvas = DemoCanvas.Begin("person", new(0, 1), new(4.6f, 2.6f));
        canvas.DrawGrid();
        var clip = PersonClips.All[_clip];
        var over = _layer == 0 ? null : PersonClips.All[_layer - 1];
        // Every clip repeats here, so one that plays once can be watched again.
        float time = _seconds % clip.Duration, overTime = over is null ? 0 : _seconds % over.Duration;

        var skeleton = PersonSkeleton.Build(_legs, _torso, _arms, _width);
        Span<RigidTransform2D> local = stackalloc RigidTransform2D[skeleton.Count], world = stackalloc RigidTransform2D[skeleton.Count];
        Span<LimbSample2D> limbs = stackalloc LimbSample2D[skeleton.Limbs.Length];
        Span<float> keyed = stackalloc float[skeleton.Count], layers = stackalloc float[skeleton.Count];
        Span<Vector3f> turns = stackalloc Vector3f[skeleton.Mounts.Length];
        clip.Sample(skeleton, time, local, limbs);
        clip.SampleLayers(time, keyed);
        clip.SampleMounts(time, turns);
        if (over is not null)
        {
            Span<RigidTransform2D> overLocal = stackalloc RigidTransform2D[skeleton.Count];
            Span<LimbSample2D> overLimbs = stackalloc LimbSample2D[skeleton.Limbs.Length];
            Span<float> overKeyed = stackalloc float[skeleton.Count];
            Span<Vector3f> overTurns = stackalloc Vector3f[skeleton.Mounts.Length];
            over.Sample(skeleton, overTime, overLocal, overLimbs);
            over.SampleLayers(overTime, overKeyed);
            over.SampleMounts(overTime, overTurns);
            SkeletonClip2D.Overlay(local, limbs, overLocal, overLimbs, PersonSkeleton.UpperBody, _weight);
            SkeletonClip2D.OverlayLayers(keyed, overKeyed, PersonSkeleton.UpperBody, _weight);
            SkeletonClip2D.OverlayMounts(turns, overTurns, PersonSkeleton.UpperBody, _weight);
        }
        SkeletonClip2D.Place(skeleton, local, limbs);
        skeleton.ToWorld(local, world);
        skeleton.ToLayers(keyed, layers);
        _mesh.Clear();
        AttachmentPainter.Paint(_mesh, new(skeleton, world, layers, turns), PersonBody.Build(_torso, _width, _head));
        DrawMesh(canvas, _mesh, Vector2f.Zero);
    }

    private static void Label(DemoCanvas canvas, Vector2f at, string text) =>
        canvas.Draw.AddText(canvas.ScreenAt(at + new Vector2f(-.6f, -.12f)), Ui.Color(160, 175, 195), text);

    /// <summary>Draws a mesh's triangles through the canvas, far to near; a larger layer is farther away.</summary>
    private static void DrawMesh(DemoCanvas canvas, LayeredMesh mesh, Vector2f offset)
    {
        var vertices = mesh.Vertices;
        var order = new (float Layer, int First)[vertices.Length / 3];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = ((vertices[i * 3].Layer + vertices[i * 3 + 1].Layer + vertices[i * 3 + 2].Layer) / 3, i * 3);
        }
        foreach (var (_, first) in order.OrderByDescending(t => t.Layer))
        {
            var color = mesh.Vertices[first].Color;
            canvas.Draw.AddTriangleFilled(At(canvas, mesh.Vertices[first], offset), At(canvas, mesh.Vertices[first + 1], offset),
                At(canvas, mesh.Vertices[first + 2], offset), Ui.Color(color.R, color.G, color.B, color.A));
        }
    }

    private static System.Numerics.Vector2 At(DemoCanvas canvas, LayeredVertex vertex, Vector2f offset) =>
        canvas.ScreenAt(new Vector2f(vertex.Position.X, vertex.Position.Y) + offset);
}
