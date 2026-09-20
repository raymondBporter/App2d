using App2d.Gameplay.Entities;
using App2d.Rendering.Characters;
using ImGuiNET;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

internal sealed partial class StudioGame
{
    private EntityPlaytest? _arena;
    private bool _arenaOpen, _arenaPaused, _arenaCollision, _arenaSound = true, _arenaAi = true, _arenaJump;
    private string _arenaPlayer = "player";
    private double _arenaAccumulator;
    private RenderTarget2D? _arenaTarget;
    private nint _arenaTargetId;
    private readonly Dictionary<string, CharacterGeometry> _arenaGeometry = [];
    private readonly Dictionary<string, SoundEffect> _cueSounds = [];
    private readonly CharacterMesh _arenaBackdrop = new(4096);
    private bool _audioUnavailable;
    private void PlayCue(string id)
    {
        if (id == "none" || _audioUnavailable || _smokePath is not null) return;
        try
        {
            if (!_cueSounds.TryGetValue(id, out var sound))
            {
                var frequency = id switch { "shot" => 700f, "heavy" => 90f, "bite" => 260f, "swing" => 420f, _ => 160f };
                const int rate = 22050; const int samples = 3307; var bytes = new byte[samples * 2];
                for (var i = 0; i < samples; i++)
                {
                    var t = i / (float)rate; var envelope = MathF.Min(1, i / 100f) * (1 - i / (float)samples);
                    var value = (short)(MathF.Sin(MathF.Tau * frequency * t * (1 - t * 2)) * envelope * 6500);
                    bytes[i * 2] = (byte)value; bytes[i * 2 + 1] = (byte)(value >> 8);
                }
                _cueSounds[id] = sound = new(bytes, rate, AudioChannels.Mono);
            }
            sound.Play(.35f, 0, 0);
        }
        catch (NoAudioHardwareException ex) { _audioUnavailable = true; _error = ex.Message; }
    }
    private void StartArena()
    {
        var documents = _entityDocuments.Values.ToArray();
        var controlled = documents.FirstOrDefault(d => d.Entity!.Id == _arenaPlayer) ?? documents[0];
        _arena = new(new[] { controlled }.Concat(documents.Where(d => d != controlled)).Select(d => (d.Entity!, d.Library)));
        _arenaGeometry.Clear();
        foreach (var doc in documents) if (!_arenaGeometry.ContainsKey(doc.Library.Id)) _arenaGeometry.Add(doc.Library.Id, new(doc.Library));
        _arenaOpen = true; _arenaPaused = false; _arenaAccumulator = 0; _arenaJump = false;
    }
    private void DrawArena()
    {
        if (!_arenaOpen || _arena is null) return;
        var display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowSize(display * .94f, ImGuiCond.FirstUseEver); ImGui.SetNextWindowPos(display * .03f, ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Entity playtest", ref _arenaOpen)) { ImGui.End(); return; }
        if (ImGui.Button("Restart / apply edits")) Attempt(StartArena);
        ImGui.SameLine(); ImGui.Checkbox("Pause", ref _arenaPaused); ImGui.SameLine(); ImGui.Checkbox("Enemy AI", ref _arenaAi); ImGui.SameLine(); ImGui.Checkbox("Collision", ref _arenaCollision); ImGui.SameLine(); ImGui.Checkbox("Sound", ref _arenaSound);
        ImGui.SameLine(); ImGui.SetNextItemWidth(180 * Scale);
        if (ImGui.BeginCombo("Play as", _arenaPlayer)) { foreach (var doc in _entityDocuments.Values) if (ImGui.Selectable(doc.Entity!.Name, _arenaPlayer == doc.Entity.Id)) { _arenaPlayer = doc.Entity.Id; Attempt(StartArena); } ImGui.EndCombo(); }
        ImGui.TextWrapped("A/D or arrows: move   Space: jump   J: attack   K: shoot (if available)   R: restart. Click this window to take control.");
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.GetIO().WantTextInput;
        if (focused && ImGui.IsKeyPressed(ImGuiKey.R)) Attempt(StartArena);
        if (focused && ImGui.IsKeyPressed(ImGuiKey.Space)) _arenaJump = true;
        if (!_arenaPaused && IsActive && _smokePath is null)
        {
            _arenaAccumulator += Math.Min(.1f, ImGui.GetIO().DeltaTime); _arena.EnemiesEnabled = _arenaAi;
            var move = focused ? (ImGui.IsKeyDown(ImGuiKey.D) || ImGui.IsKeyDown(ImGuiKey.RightArrow) ? 1 : 0) - (ImGui.IsKeyDown(ImGuiKey.A) || ImGui.IsKeyDown(ImGuiKey.LeftArrow) ? 1 : 0) : 0;
            while (_arenaAccumulator >= EntityPlaytest.StepSeconds)
            {
                _arena.Step(new(move, focused && _arenaJump, focused && ImGui.IsKeyDown(ImGuiKey.J), focused && ImGui.IsKeyDown(ImGuiKey.K))); _arenaJump = false;
                if (_arenaSound) foreach (var cue in _arena.Cues) PlayCue(cue.Sound);
                _arenaAccumulator -= EntityPlaytest.StepSeconds;
            }
        }
        else _arenaAccumulator = 0;
        var size = ImGui.GetContentRegionAvail(); RenderArena(new(size.X, MathF.Max(120 * Scale, size.Y - 70 * Scale)));
        foreach (var actor in _arena.Actors) { ImGui.TextUnformatted($"{actor.Type.Name}: {actor.Health}/{actor.Type.Health}  {actor.ActionId}"); ImGui.SameLine(); }
        ImGui.NewLine();
        ImGui.TextDisabled("Playtest uses a snapshot of the open types. Restart applies unsaved edits. Colored regions use the same CPU pose as drawing.");
        ImGui.End();
    }
    private void RenderArena(Vector2 size)
    {
        var width = Math.Max(1, (int)size.X); var height = Math.Max(1, (int)size.Y);
        if (_arenaTarget is null || _arenaTarget.Width != width || _arenaTarget.Height != height)
        {
            if (_arenaTarget is not null) { _gui.Unregister(_arenaTargetId); _arenaTarget.Dispose(); }
            _arenaTarget = new(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents); _arenaTargetId = _gui.Register(_arenaTarget);
        }
        GraphicsDevice.SetRenderTarget(_arenaTarget); GraphicsDevice.Clear(new Color(230, 234, 227));
        var ppu = MathF.Min(width / 26f, height / 8f); var projection = PointCharacterRenderer.Projection(width, height, new(width / 2f, height * .83f), ppu);
        _arenaBackdrop.Clear(); _arenaBackdrop.Line(new(-12, 0, 0), new(12, 0, 0), .04f, new Color(100, 120, 130));
        _arenaBackdrop.Line(new(-12, 0, 0), new(-12, 4, 0), .04f, new Color(100, 120, 130)); _arenaBackdrop.Line(new(12, 0, 0), new(12, 4, 0), .04f, new Color(100, 120, 130));
        _renderer.Draw(_arenaBackdrop, projection, Matrix.Identity, writeDepth: false);
        foreach (var actor in _arena!.Actors)
        {
            var pose = actor.Pose; var geometry = _arenaGeometry[actor.Library.Id];
            geometry.Build(actor.Library.Clips[actor.Action.Clip], pose.ClipTime, pose.Look, new(false, false, true), true);
            var transform = Matrix.CreateTranslation(actor.Position.X + pose.Offset.X, actor.Position.Y + pose.Offset.Y, 0);
            GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
            _renderer.Draw(geometry.Backdrop, projection, transform, writeDepth: false);
            _renderer.Draw(geometry.Body, projection, transform);
            if (_faces.TryGetValue(pose.Look.CustomHead?.Face ?? pose.Look.Face, out var face)) _renderer.Draw(geometry.Face, projection, transform, face, false);
            if (_arenaCollision)
            {
                BuildCollisionOverlay(pose, actor.Action.Active(actor.ActionTime), actor.Action);
                _renderer.Draw(_collisionOverlay, projection, Matrix.CreateTranslation(actor.Position.X, actor.Position.Y, 0), writeDepth: false);
            }
        }
        _arenaBackdrop.Clear();
        foreach (var bolt in _arena.Bolts)
            _arenaBackdrop.Polygon(App2d.Core.Characters.EntityRegion.Box("bolt", bolt.Position, bolt.Size).Points.Select(p => new Vector3(p, -10)).ToArray(), new Color(225, 100, 35), null, 0);
        GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0); _renderer.Draw(_arenaBackdrop, projection, Matrix.Identity, writeDepth: false);
        GraphicsDevice.SetRenderTarget(null); ImGui.Image(_arenaTargetId, new(width, height));
    }
}
