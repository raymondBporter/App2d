using App2d.Core.Characters;
using App2d.Gameplay.Entities;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

/// <summary>
/// Phase-three visual gate: the authored arena rendered from each actor's final pose with its collision overlays. Thrust
/// anticipation, active hit geometry and recovery in both facings; the player's jump; the three-legged stalker walking
/// and lunging. Movement boxes are grey, hurt regions blue, active attack regions red, the spear tip a yellow dot and
/// held contact anchors green crosses. The bottom band repeats the frame at game scale.
/// </summary>
internal sealed partial class StudioGame
{
    private const int EntityWidth = 1600, EntityHeight = 860, EntityPanelHeight = 640;
    private const float EntityPpu = 120, EntityGamePpu = 40;
    private readonly bool _entitySmoke;
    private List<(string Name, Vector2 Center, List<AuthoredArena.Actor> Actors, List<(ActorPose Pose, List<EntityRegion> Hurt, List<EntityRegion> Attacks, EntityRegion Movement, IReadOnlyDictionary<string, Vector3> Anchors)> Snapshot)>? _entityFrames;
    private readonly List<string> _entityReport = [];
    private readonly PuppetDrawing _entityDrawing = new();
    private readonly CharacterMesh _entityOverlay = new(16384);

    private bool PrepareEntityProof()
    {
        _entityFrames ??= BuildEntityFrames();
        if (_smokeIndex >= _entityFrames.Count) { File.WriteAllLines(Path.Combine(_smokePath!, "entity-proof.txt"), _entityReport); return false; }
        _proofTarget ??= new(GraphicsDevice, EntityWidth, EntityHeight, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var (name, center, actors, snapshot) = _entityFrames[_smokeIndex];
        GraphicsDevice.SetRenderTarget(_proofTarget); GraphicsDevice.Clear(new Color(237, 238, 226));
        foreach (var (viewport, ppu, anchorY) in new[] { (new Viewport(0, 0, EntityWidth, EntityPanelHeight), EntityPpu, EntityPanelHeight * .9f), (new Viewport(0, EntityPanelHeight, EntityWidth, EntityHeight - EntityPanelHeight), EntityGamePpu, (EntityHeight - EntityPanelHeight) * .85f) })
        {
            GraphicsDevice.Viewport = viewport;
            var projection = PointCharacterRenderer.Projection(viewport.Width, viewport.Height, new(viewport.Width * .5f - center.X * ppu, anchorY), ppu);
            BuildEntityOverlay(center, snapshot.Select(s => (s.Hurt, s.Attacks, s.Movement, s.Anchors)).ToList(), actors, snapshot.Select(s => s.Pose).ToList(), 1 / ppu, ppu == EntityPpu);
            _renderer.Draw(_entityOverlay, projection, Matrix.Identity, writeDepth: false);
            for (var i = 0; i < actors.Count; i++)
            {
                var pose = snapshot[i].Pose;
                _entityDrawing.Build(actors[i].Entity, pose.Local);
                GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                _renderer.Draw(_entityDrawing.Mesh, projection, Matrix.CreateScale(pose.Facing, 1, 1) * Matrix.CreateTranslation(pose.Position.X, pose.Position.Y, 0));
            }
            if (ppu == EntityPpu)
            {
                // Overlays again in front, so hit geometry reads over the figures in the close-up.
                GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                _renderer.Draw(_entityOverlay, projection, Matrix.Identity, writeDepth: false);
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        using var stream = File.Create(Path.Combine(_smokePath!, name + ".png"));
        _proofTarget.SaveAsPng(stream, EntityWidth, EntityHeight);
        return true;
    }

    private void BuildEntityOverlay(Vector2 center, List<(List<EntityRegion> Hurt, List<EntityRegion> Attacks, EntityRegion Movement, IReadOnlyDictionary<string, Vector3> Anchors)> regions,
        List<AuthoredArena.Actor> actors, List<ActorPose> poses, float pixel, bool detail)
    {
        var mesh = _entityOverlay; mesh.Clear();
        var ground = new Color(154, 169, 158);
        mesh.Line(new(center.X - 30, 0, 7), new(center.X + 30, 0, 7), pixel * 2, ground);
        for (var x = (int)MathF.Floor(center.X - 8); x <= center.X + 8; x++) mesh.Line(new(x, 0, 7), new(x, -8 * pixel, 7), pixel, ground);
        void Outline(EntityRegion region, Color color, float width)
        {
            var points = region.Points.Select(p => new Vector3(p, -6)).ToList();
            for (var i = 0; i < points.Count; i++) mesh.Line(points[i], points[(i + 1) % points.Count], width * pixel, color);
        }
        for (var i = 0; i < regions.Count; i++)
        {
            var (hurt, attacks, movement, anchors) = regions[i];
            Outline(movement, new Color(120, 120, 120), 1.5f);
            if (!detail) continue;
            foreach (var region in hurt) Outline(region, new Color(60, 120, 220), 2);
            foreach (var region in attacks)
            {
                var p = region.Points.Select(q => new Vector3(q, -6.1f)).ToList();
                mesh.Polygon(p, new Color(235, 60, 50, 90), new Color(220, 40, 30), 3 * pixel);
            }
            foreach (var equipment in actors[i].Entity.Equipment)
            {
                var tip = ActorPose.PropPoint(poses[i].Socket(equipment.Socket), equipment.Prop, equipment.Prop.Tip);
                mesh.Disk(tip with { Z = -6.2f }, 5 * pixel, new Color(240, 200, 40));
            }
            foreach (var anchor in anchors.Values)
            {
                var p = anchor with { Z = -6.2f }; var green = new Color(36, 140, 104);
                mesh.Line(p - new Vector3(9 * pixel, 0, 0), p + new Vector3(9 * pixel, 0, 0), 2 * pixel, green);
                mesh.Line(p - new Vector3(0, 9 * pixel, 0), p + new Vector3(0, 9 * pixel, 0), 2 * pixel, green);
            }
        }
    }

    private List<(string, Vector2, List<AuthoredArena.Actor>, List<(ActorPose, List<EntityRegion>, List<EntityRegion>, EntityRegion, IReadOnlyDictionary<string, Vector3>)>)> BuildEntityFrames()
    {
        var catalog = ProofCatalog();
        var frames = new List<(string, Vector2, List<AuthoredArena.Actor>, List<(ActorPose, List<EntityRegion>, List<EntityRegion>, EntityRegion, IReadOnlyDictionary<string, Vector3>)>)>();
        void Capture(string name, AuthoredArena arena, Vector2 center, string note)
        {
            frames.Add((name, center, arena.Actors.ToList(), arena.Actors.Select(a => (a.Pose, a.Hurt, a.Attacks.Select(x => x.Region).ToList(), a.Movement,
                (IReadOnlyDictionary<string, Vector3>)a.Animator.Anchors.ToDictionary())).ToList()));
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
            Capture($"{label}-1-anticipation", arena, new(side, 0), "thrust anticipation: no attack region");
            Until(arena, a => guard.Animator.ActionTime >= hit.Start + .03);
            Capture($"{label}-2-active", arena, new(side, 0), $"active window {hit.Start:F2}-{hit.Finish:F2}s: attack region at the spear tip; player hits so far {arena.Hits.Count}");
            Until(arena, a => guard.Animator.ActionTime >= .75 || guard.Animator.Action is null);
            Capture($"{label}-3-recovery", arena, new(side, 0), "recovery: no attack region");
            _entityReport.Add($"{label}: hits {string.Join(", ", arena.Hits.Select(h => $"tick {h.Tick} {h.Window} -> actor {h.Target}"))}");
        }

        var jump = new AuthoredArena([catalog.Entities["player"]]); jump.Teleport(0, new(0, 0));
        var player = jump.Player;
        jump.Step(new(Jump: true));
        Until(jump, a => player.Animator.ActionTime >= .12);
        Capture("player-jump-1-crouch", jump, new(.5f, 0), "crouch with planted feet before launch");
        Until(jump, a => player.Position.Y > .6f, new(Move: .6f));
        Capture("player-jump-2-rising", jump, new(.5f, 0), "after the launch event");
        Until(jump, a => player.Velocity.Y <= 0, new(Move: .6f));
        Capture("player-jump-3-apex", jump, new(.5f, 0), "apex, tucked");
        Until(jump, a => player.Grounded, new(Move: .6f));
        jump.Step(new(Move: .6f));
        Capture("player-jump-4-landed", jump, new(.5f, 0), "landed back into locomotion with fresh contacts");
        var guardArena = new AuthoredArena([catalog.Entities["spear-guard"]]);
        for (var i = 0; i < 60; i++) guardArena.Step(new(Jump: true));
        _entityReport.Add($"spear guard asked to jump for 60 steps: height {guardArena.Player.Position.Y:F2}, action {guardArena.Player.Animator.Action ?? "none"} (rejected: walker controller)");

        var stalker = new AuthoredArena([catalog.Entities["player"], catalog.Entities["stalker-pest"]]);
        stalker.Teleport(0, new(-2.5f, 0)); stalker.Teleport(1, new(1.5f, 0));
        var pest = stalker.Actors[1];
        Until(stalker, a => pest.Animator.Role == "walk" && pest.Animator.RoleTime > .3);
        Capture("stalker-1-walk", stalker, new(-.5f, 0), "walking: outer legs held on world anchors");
        Until(stalker, a => pest.Animator.RoleTime > .8);
        Capture("stalker-2-walk", stalker, new(-.5f, 0), "walking: middle leg held");
        var sting = pest.Entity.Actions["attack"].Hits[0];
        Until(stalker, a => pest.Animator.Action == "attack" && pest.Animator.ActionTime >= sting.Start + .02);
        Capture("stalker-3-lunge", stalker, new(-1, 0), "lunge active: sting region on the stinger socket");
        return frames;
    }
}
