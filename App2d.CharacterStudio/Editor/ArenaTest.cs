using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using App2d.Gameplay.Entities;
using App2d.Rendering.Characters;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// Test: plays the authored arena in the shared stage from an explicit snapshot of the current drafts. The first roster
/// entry is controlled (A/D or arrows move, Shift runs, Space/W jumps, J attacks); the others fight it. Restart takes a new
/// snapshot. Leaving Test returns to the same document, workspace and selection, which it never touches.
/// </summary>
internal sealed class ArenaTest(AuthoringWorkspace assets, GraphicsDevice device, ImGuiHost gui, PointCharacterRenderer renderer) : IDisposable
{
    private readonly ArenaDrawing _drawing = new(device, renderer);
    private readonly List<string> _log = [];
    private IReadOnlyList<ResolvedEntity> _available = [];
    private RenderTarget2D? _target;
    private nint _targetId;
    private double _accumulator;
    private int _logged;

    public bool Active { get; private set; }
    public bool Paused { get; set; }
    public bool Overlays { get; set; } = true;
    public List<string> Roster { get; } = [StarterContent.Player, StarterContent.SpearGuard, StarterContent.StalkerPest];
    public IReadOnlyList<string> Problems { get; private set; } = [];
    public AuthoredArena? Arena { get; private set; }

    /// <summary>Takes a snapshot of the drafts and starts the arena with the roster's entities that compiled.</summary>
    public void Start()
    {
        var (entities, problems) = assets.SnapshotEntities();
        _available = entities; var chosen = Roster.Select(id => entities.FirstOrDefault(e => e.Id == id)).OfType<ResolvedEntity>().ToList();
        var missing = Roster.Where(id => entities.All(e => e.Id != id)).Select(id => $"Entity '{id}' is not playable; see the problems above.");
        Problems = [.. problems, .. missing];
        Arena = chosen.Count > 0 ? new AuthoredArena(chosen) : null;
        _log.Clear(); _logged = 0; _accumulator = 0; Active = true;
    }

    public void Stop() { Active = false; Arena = null; }

    /// <summary>Advances whole fixed steps; leftover time carries to the next frame.</summary>
    public void Advance(float seconds, ArenaInput input)
    {
        if (Arena is null || Paused) return;
        _accumulator = Math.Min(_accumulator + seconds, .25);
        while (_accumulator >= AuthoredArena.StepSeconds) { Arena.Step(input); _accumulator -= AuthoredArena.StepSeconds; }
        Record();
    }

    private void Record()
    {
        if (Arena is null) return;
        foreach (var e in Arena.Events.Skip(_logged))
            if (e.Event.Sound is not null || e.Event.Kind == AnimationEvent.EventKind)
                _log.Add($"{e.Tick,6}  {Arena.Actors[e.Actor].Entity.Name}: {e.Event.Id}{(e.Event.Sound is { } s ? $" (sound {s})" : "")}");
        _logged = Arena.Events.Count;
        if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);
    }

    private static ArenaInput ReadInput()
    {
        var io = ImGui.GetIO();
        if (io.WantTextInput) return default;
        var move = (ImGui.IsKeyDown(ImGuiKey.D) || ImGui.IsKeyDown(ImGuiKey.RightArrow) ? 1 : 0) - (ImGui.IsKeyDown(ImGuiKey.A) || ImGui.IsKeyDown(ImGuiKey.LeftArrow) ? 1 : 0);
        return new(move, io.KeyShift, ImGui.IsKeyDown(ImGuiKey.Space) || ImGui.IsKeyDown(ImGuiKey.W) || ImGui.IsKeyDown(ImGuiKey.UpArrow), ImGui.IsKeyDown(ImGuiKey.J));
    }

    public void Stage(Vector2 available, bool live)
    {
        if (live) Advance(ImGui.GetIO().DeltaTime, ReadInput());
        if (ImGui.SmallButton(Paused ? "Resume" : "Pause")) Paused = !Paused;
        ImGui.SameLine(); if (ImGui.SmallButton("Restart")) Start();
        ImGui.SameLine(); var overlays = Overlays; if (ImGui.Checkbox("Collision", ref overlays)) Overlays = overlays;
        ImGui.SameLine(); ImGui.TextDisabled("A/D move  Shift run  Space jump  J attack");
        var size = ImGui.GetContentRegionAvail(); var width = Math.Max(1, (int)size.X); var height = Math.Max(1, (int)size.Y);
        if (_target is null || _target.Width != width || _target.Height != height)
        {
            if (_target is not null) { gui.Unregister(_targetId); _target.Dispose(); }
            _target = new(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
            _targetId = gui.Register(_target);
        }
        device.SetRenderTarget(_target); device.Clear(new Color(237, 238, 226));
        if (Arena is not null)
        {
            var ppu = MathF.Min(height / 4.2f, width / 11f); var centerX = Arena.Player.Position.X;
            var projection = PointCharacterRenderer.Projection(width, height, new(width / 2f - centerX * ppu, height * .86f), ppu);
            _drawing.Draw(ArenaActorFrame.Capture(Arena), projection, ppu, centerX, Overlays);
        }
        device.SetRenderTarget(null);
        var start = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("arena", new(width, height));
        ImGui.GetWindowDrawList().AddImage(_targetId, start, start + new Vector2(width, height));
        if (Arena is null) ImGui.GetWindowDrawList().AddText(start + new Vector2(20, 20) * Ui.Scale, Ui.Color(160, 70, 40), "Nothing to play: no roster entity compiled.");
    }

    /// <summary>The outline while testing: the roster, first entry controlled.</summary>
    public void RosterPanel()
    {
        Ui.Header("Roster");
        Ui.Help("The first entity is yours; the others fight it. Changes apply on Restart.");
        for (var i = 0; i < Roster.Count; i++)
        {
            ImGui.PushID(i);
            var chosen = Ui.Combo(i == 0 ? "Controlled" : $"Opponent {i}", Roster[i], _available.Select(e => e.Id));
            if (chosen is not null) Roster[i] = chosen;
            if (i > 0 && ImGui.SmallButton("Remove")) { Roster.RemoveAt(i); ImGui.PopID(); break; }
            ImGui.PopID();
        }
        if (_available.Count > 0 && ImGui.SmallButton("Add opponent")) Roster.Add(_available[0].Id);
    }

    public void Inspector()
    {
        foreach (var problem in Problems) Ui.Problem(problem);
        if (Arena is null) return;
        foreach (var actor in Arena.Actors)
        {
            Ui.Header(actor.Entity.Name);
            var animator = actor.Animator;
            ImGui.TextUnformatted($"Health {actor.Health}/{actor.Entity.Asset.Health}   {(actor.Grounded ? "grounded" : "airborne")}");
            ImGui.TextUnformatted(animator.Action is { } action ? $"Action {action} {animator.ActionTime:F2}s ({actor.Entity.Actions[action].Source})"
                : $"Role {animator.Role} ({actor.Entity.Roles[animator.Role].Clip.Id}, {actor.Entity.Roles[animator.Role].Source})");
            ImGui.TextDisabled($"{actor.Entity.Controller.Id}: {string.Join(", ", actor.Entity.Actions.Keys)}");
            ImGui.TextDisabled($"Contacts held: {animator.Anchors.Count}");
        }
    }

    public void Timeline()
    {
        Ui.Header("Events");
        if (Arena is not null) ImGui.TextDisabled($"Tick {Arena.Tick}   hits {Arena.Hits.Count}");
        for (var i = _log.Count - 1; i >= 0 && i >= _log.Count - 40; i--) ImGui.TextUnformatted(_log[i]);
    }

    public void Dispose() => _target?.Dispose();
}
