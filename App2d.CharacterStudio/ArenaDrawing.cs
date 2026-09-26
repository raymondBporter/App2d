using App2d.Core.Characters;
using App2d.Gameplay.Entities;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

/// <summary>One actor as the arena left it after a step: its final pose and every region derived from that pose.</summary>
internal sealed record ArenaActorFrame(ResolvedEntity Entity, ActorPose Pose, EntityRegion Movement, IReadOnlyList<EntityRegion> Hurt,
    IReadOnlyList<EntityRegion> Attacks, IReadOnlyDictionary<string, Vector3> Anchors, bool Alive)
{
    public static IReadOnlyList<ArenaActorFrame> Capture(AuthoredArena arena) => [.. arena.Actors.Select(a => new ArenaActorFrame(
        a.Entity, a.Pose, a.Movement, a.Hurt, [.. a.Attacks.Select(x => x.Region)], a.Animator.Anchors.ToDictionary(), a.Alive))];
}

/// <summary>
/// Draws arena actors from their final poses, with collision overlays: movement boxes grey, hurt regions blue, active attack
/// regions red, prop tips yellow and held contact anchors green. Shared by the entity smoke and the editor's Test mode.
/// </summary>
internal sealed class ArenaDrawing(GraphicsDevice device, PointCharacterRenderer renderer)
{
    private readonly PuppetDrawing _drawing = new();
    private readonly CharacterMesh _overlay = new(16384);

    public void Draw(IReadOnlyList<ArenaActorFrame> actors, Matrix projection, float ppu, float centerX, bool overlays)
    {
        BuildOverlay(actors, 1 / ppu, centerX, overlays);
        renderer.Draw(_overlay, projection, Matrix.Identity, writeDepth: false);
        foreach (var actor in actors)
        {
            _drawing.Build(actor.Entity, actor.Pose.Local);
            device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
            renderer.Draw(_drawing.Mesh, projection, Matrix.CreateScale(actor.Pose.Facing, 1, 1) * Matrix.CreateTranslation(actor.Pose.Position.X, actor.Pose.Position.Y, 0));
        }
        if (!overlays) return;
        // Overlays again in front, so hit geometry reads over the figures.
        device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
        renderer.Draw(_overlay, projection, Matrix.Identity, writeDepth: false);
    }

    private void BuildOverlay(IReadOnlyList<ArenaActorFrame> actors, float pixel, float centerX, bool detail)
    {
        var mesh = _overlay; mesh.Clear();
        var ground = new Color(154, 169, 158);
        mesh.Line(new(centerX - 40, 0, 7), new(centerX + 40, 0, 7), pixel * 2, ground);
        for (var x = (int)MathF.Floor(centerX - 14); x <= centerX + 14; x++) mesh.Line(new(x, 0, 7), new(x, -8 * pixel, 7), pixel, ground);
        void Outline(EntityRegion region, Color color, float width)
        {
            var points = region.Points.Select(p => new Vector3(p, -6)).ToList();
            for (var i = 0; i < points.Count; i++) mesh.Line(points[i], points[(i + 1) % points.Count], width * pixel, color);
        }
        foreach (var actor in actors)
        {
            Outline(actor.Movement, new Color(120, 120, 120), 1.5f);
            if (!detail || !actor.Alive) continue;
            foreach (var region in actor.Hurt) Outline(region, new Color(60, 120, 220), 2);
            foreach (var region in actor.Attacks)
                mesh.Polygon([.. region.Points.Select(q => new Vector3(q, -6.1f))], new Color(235, 60, 50, 90), new Color(220, 40, 30), 3 * pixel);
            foreach (var equipment in actor.Entity.Equipment)
                mesh.Disk(ActorPose.PropPoint(actor.Pose.Socket(equipment.Socket), equipment.Prop, equipment.Prop.Tip) with { Z = -6.2f }, 5 * pixel, new Color(240, 200, 40));
            foreach (var anchor in actor.Anchors.Values)
            {
                var p = anchor with { Z = -6.2f }; var green = new Color(36, 140, 104);
                mesh.Line(p - new Vector3(9 * pixel, 0, 0), p + new Vector3(9 * pixel, 0, 0), 2 * pixel, green);
                mesh.Line(p - new Vector3(0, 9 * pixel, 0), p + new Vector3(0, 9 * pixel, 0), 2 * pixel, green);
            }
        }
    }
}
