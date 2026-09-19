using App2d.Noodle.Projectors;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

internal sealed class JointShowcase2D
{
    private static readonly XnaColor GuideColor = new(174, 186, 211, 105);
    private static readonly XnaColor RequestedColor = new(255, 177, 90, 150);
    private static readonly XnaColor ConstrainedColor = new(78, 206, 255);
    private static readonly XnaColor LabelColor = new(207, 217, 235);

    private readonly PolarLinkProjector2D _revolute = new(78f, -0.8f, 0.8f);
    private readonly AxisProjector2D _prismatic = new(new Vector2(1f, 0.18f), -72f, 72f);
    private readonly DistanceProjector2D _distance = new(68f, 68f);

    public void Render(Renderer2D renderer, Camera2D camera, double totalSeconds)
    {
        DrawRevolute(renderer, camera, totalSeconds, new Vector2(400f, 155f));
        DrawPrismatic(renderer, camera, totalSeconds, new Vector2(400f, 0f));
        DrawDistance(renderer, camera, totalSeconds, new Vector2(400f, -165f));
    }

    private void DrawRevolute(Renderer2D renderer, Camera2D camera, double time, Vector2 anchor)
    {
        Span<Vector2> limitArc = stackalloc Vector2[25];
        for (var index = 0; index < limitArc.Length; index++)
        {
            var angle = float.Lerp(_revolute.MinimumAngle, _revolute.MaximumAngle, index / (limitArc.Length - 1f));
            limitArc[index] = anchor + 58f * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        renderer.DrawWorldPolyline(limitArc, GuideColor, 2f);

        var requestedAngle = MathF.Sin((float)time * 0.9f) * 1.45f;
        var requested = anchor + _revolute.Length * new Vector2(MathF.Cos(requestedAngle), MathF.Sin(requestedAngle));
        var constrained = _revolute.Project(anchor, requested);
        DrawRequestedAndConstrained(renderer, anchor, requested, constrained);
        DrawLabel(renderer, camera, PolarLinkProjector2D.DisplayName, PolarLinkProjector2D.DisplayEquation,
            anchor + new Vector2(-86f, 67f));
    }

    private void DrawPrismatic(Renderer2D renderer, Camera2D camera, double time, Vector2 anchor)
    {
        var trackStart = anchor + _prismatic.Axis * _prismatic.MinimumTranslation;
        var trackEnd = anchor + _prismatic.Axis * _prismatic.MaximumTranslation;
        Span<Vector2> track = [trackStart, trackEnd];
        renderer.DrawWorldPolyline(track, GuideColor, 5f);

        var perpendicular = new Vector2(-_prismatic.Axis.Y, _prismatic.Axis.X);
        var requested = anchor + _prismatic.Axis * (MathF.Sin((float)time * 1.15f) * 112f) + perpendicular * 34f;
        var constrained = _prismatic.Project(anchor, requested);
        renderer.DrawWorldCircle(requested, 5f, RequestedColor, 2f);
        Span<Vector2> projection = [requested, constrained];
        renderer.DrawWorldPolyline(projection, RequestedColor, 1.5f);

        var along = _prismatic.Axis * 13f;
        var across = perpendicular * 10f;
        Span<Vector2> carriage =
        [
            constrained - along - across,
            constrained + along - across,
            constrained + along + across,
            constrained - along + across
        ];
        renderer.DrawWorldConvexPolygon(carriage, ConstrainedColor);
        DrawLabel(renderer, camera, AxisProjector2D.DisplayName, AxisProjector2D.DisplayEquation,
            anchor + new Vector2(-86f, 64f));
    }

    private void DrawDistance(Renderer2D renderer, Camera2D camera, double time, Vector2 anchor)
    {
        renderer.DrawWorldCircle(anchor, _distance.MaximumDistance, GuideColor, 2f);
        var requested = anchor + new Vector2(
            MathF.Cos((float)time * 0.8f) * 108f,
            MathF.Sin((float)time * 1.25f) * 82f);
        var constrained = _distance.Project(anchor, requested);
        DrawRequestedAndConstrained(renderer, anchor, requested, constrained);
        DrawLabel(renderer, camera, _distance.Name, _distance.Equation, anchor + new Vector2(-86f, 93f));
    }

    private static void DrawRequestedAndConstrained(
        Renderer2D renderer,
        Vector2 anchor,
        Vector2 requested,
        Vector2 constrained)
    {
        Span<Vector2> requestedLink = [anchor, requested];
        Span<Vector2> constrainedLink = [anchor, constrained];
        renderer.DrawWorldPolyline(requestedLink, RequestedColor, 1.5f);
        renderer.DrawWorldCircle(requested, 5f, RequestedColor, 2f);
        renderer.DrawWorldPolyline(constrainedLink, ConstrainedColor, 5f);
        renderer.DrawWorldCircle(anchor, 7f, ConstrainedColor, 3f);
        renderer.DrawWorldCircle(constrained, 8f, ConstrainedColor, 3f);
    }

    private static void DrawLabel(
        Renderer2D renderer,
        Camera2D camera,
        string name,
        string equation,
        Vector2 worldPosition)
    {
        var screenPosition = camera.WorldToDevice(worldPosition);
        renderer.DrawScreenText(name, screenPosition, XnaColor.White);
        renderer.DrawScreenText(equation, screenPosition + new Vector2(0f, 22f), LabelColor);
    }
}
