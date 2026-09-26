using App2d.Core.Characters;
using App2d.Gameplay.Entities;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.CharacterStudio;

/// <summary>
/// Phase-three visual gate: the authored arena rendered from each actor's final pose with its collision overlays. Thrust
/// anticipation, active hit geometry and recovery in both facings; the player's jump; the three-legged stalker walking
/// and lunging. The bottom band repeats each frame at game scale.
/// </summary>
internal sealed partial class ProofRenders
{
    private const int EntityWidth = 1600, EntityHeight = 860, EntityPanelHeight = 640;
    private const float EntityPpu = 120, EntityGamePpu = 40;
    private List<(string Name, float CenterX, IReadOnlyList<ArenaActorFrame> Actors)>? _entityFrames;
    private readonly List<string> _entityReport = [];
    private ArenaDrawing? _arenaDrawing;

    private bool PrepareEntityProof()
    {
        _entityFrames ??= BuildEntityFrames();
        if (_smokeIndex >= _entityFrames.Count) { File.WriteAllLines(Path.Combine(_smokePath!, "entity-proof.txt"), _entityReport); return false; }
        _proofTarget ??= new(GraphicsDevice, EntityWidth, EntityHeight, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        _arenaDrawing ??= new(GraphicsDevice, _renderer);
        var (name, centerX, actors) = _entityFrames[_smokeIndex];
        GraphicsDevice.SetRenderTarget(_proofTarget); GraphicsDevice.Clear(new Color(237, 238, 226));
        foreach (var (viewport, ppu, anchorY) in new[] { (new Viewport(0, 0, EntityWidth, EntityPanelHeight), EntityPpu, EntityPanelHeight * .9f), (new Viewport(0, EntityPanelHeight, EntityWidth, EntityHeight - EntityPanelHeight), EntityGamePpu, (EntityHeight - EntityPanelHeight) * .85f) })
        {
            GraphicsDevice.Viewport = viewport;
            var projection = PointCharacterRenderer.Projection(viewport.Width, viewport.Height, new(viewport.Width * .5f - centerX * ppu, anchorY), ppu);
            _arenaDrawing.Draw(actors, projection, ppu, centerX, ppu == EntityPpu);
        }
        GraphicsDevice.SetRenderTarget(null);
        using var stream = File.Create(Path.Combine(_smokePath!, name + ".png"));
        _proofTarget.SaveAsPng(stream, EntityWidth, EntityHeight);
        return true;
    }

    private List<(string, float, IReadOnlyList<ArenaActorFrame>)> BuildEntityFrames()
    {
        var catalog = ProofCatalog();
        var frames = new List<(string, float, IReadOnlyList<ArenaActorFrame>)>();
        void Capture(string name, AuthoredArena arena, float centerX, string note)
        {
            frames.Add((name, centerX, ArenaActorFrame.Capture(arena)));
            _entityReport.Add($"{name}: tick {arena.Tick}; {note}; " + string.Join("; ", arena.Actors.Select(a =>
                $"{a.Entity.Id} {(a.Animator.Action is { } action ? $"{action} {a.Animator.ActionTime:F3}s" : $"{a.Animator.Role} {a.Animator.RoleTime:F3}")} at ({a.Position.X:F2}, {a.Position.Y:F2}) facing {a.Facing} health {a.Health}")));
        }
        static void Until(AuthoredArena arena, Func<AuthoredArena, bool> done, ArenaInput input = default, int limit = 2400)
        { for (var i = 0; i < limit && !done(arena); i++) arena.Step(input); }

        foreach (var side in new[] { 1, -1 })
        {
            var label = side > 0 ? "guard-faces-left" : "guard-faces-right";
            var arena = new AuthoredArena([catalog.Entities["player"], catalog.Entities["spear-guard"]]);
            arena.Teleport(0, new(0, 0)); arena.Teleport(1, new(2.05f * side, 0));
            var guard = arena.Actors[1]; var hit = guard.Entity.Actions["attack"].Hits[0];
            Until(arena, a => guard.Animator.Action == "attack" && guard.Animator.ActionTime >= .24);
            Capture($"{label}-1-anticipation", arena, side, "thrust anticipation: no attack region");
            Until(arena, a => guard.Animator.ActionTime >= hit.Start + .03);
            Capture($"{label}-2-active", arena, side, $"active window {hit.Start:F2}-{hit.Finish:F2}s: attack region at the spear tip; hits so far {arena.Hits.Count}");
            Until(arena, a => guard.Animator.ActionTime >= .75 || guard.Animator.Action is null);
            Capture($"{label}-3-recovery", arena, side, "recovery: no attack region");
            _entityReport.Add($"{label}: hits {string.Join(", ", arena.Hits.Select(h => $"tick {h.Tick} {h.Window} -> actor {h.Target}"))}");
        }

        var jump = new AuthoredArena([catalog.Entities["player"]]); jump.Teleport(0, new(0, 0));
        var player = jump.Player;
        jump.Step(new(Jump: true));
        Until(jump, a => player.Animator.ActionTime >= .12);
        Capture("player-jump-1-crouch", jump, .5f, "crouch with planted feet before launch");
        Until(jump, a => player.Position.Y > .6f, new(Move: .6f));
        Capture("player-jump-2-rising", jump, .5f, "after the launch event");
        Until(jump, a => player.Velocity.Y <= 0, new(Move: .6f));
        Capture("player-jump-3-apex", jump, .5f, "apex, tucked");
        Until(jump, a => player.Grounded, new(Move: .6f));
        jump.Step(new(Move: .6f));
        Capture("player-jump-4-landed", jump, .5f, "landed back into locomotion with fresh contacts");
        var guardArena = new AuthoredArena([catalog.Entities["spear-guard"]]);
        for (var i = 0; i < 60; i++) guardArena.Step(new(Jump: true));
        _entityReport.Add($"spear guard asked to jump for 60 steps: height {guardArena.Player.Position.Y:F2}, action {guardArena.Player.Animator.Action ?? "none"} (rejected: walker controller)");

        var stalker = new AuthoredArena([catalog.Entities["player"], catalog.Entities["stalker-pest"]]);
        stalker.Teleport(0, new(-2.5f, 0)); stalker.Teleport(1, new(1.5f, 0));
        var pest = stalker.Actors[1];
        Until(stalker, a => pest.Animator.Role == "walk" && pest.Animator.RoleTime > .3);
        Capture("stalker-1-walk", stalker, -.5f, "walking: outer legs held on world anchors");
        Until(stalker, a => pest.Animator.RoleTime > .8);
        Capture("stalker-2-walk", stalker, -.5f, "walking: middle leg held");
        var sting = pest.Entity.Actions["attack"].Hits[0];
        Until(stalker, a => pest.Animator.Action == "attack" && pest.Animator.ActionTime >= sting.Start + .02);
        Capture("stalker-3-lunge", stalker, -1, "lunge active: sting region on the stinger socket");
        return frames;
    }
}
