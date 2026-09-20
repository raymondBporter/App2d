using App2d.Core.Characters;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

/// <summary>Bold vector features that work on both the original dome and edited heads.</summary>
public static class FaceDrawing
{
    public static void Build(CharacterMesh mesh, FacePose pose, Func<Vector2, Vector3> at, float width, Color ink)
    {
        void Line(Vector2 a, Vector2 b, float weight = 1) => mesh.Line(at(a), at(b), width * weight, ink);
        void Curve(Vector2 a, Vector2 control, Vector2 b, float weight = 1)
        {
            var previous = a;
            for (var i = 1; i <= 12; i++)
            {
                var t = i / 12f; var next = a * ((1 - t) * (1 - t)) + control * (2 * t * (1 - t)) + b * (t * t);
                Line(previous, next, weight: weight); previous = next;
            }
        }
        void Oval(Vector2 center, float rx, float ry, Color? fill = null, float depth = 0)
        {
            var offset = new Vector3(0, 0, depth);
            Vector3 Point(int i) => at(center + new Vector2(MathF.Cos(i * MathF.Tau / 24) * rx, MathF.Sin(i * MathF.Tau / 24) * ry)) - offset;
            for (var i = 0; i < 24; i++)
            {
                mesh.Triangle(at(center) - offset, Point(i), Point(i + 1), fill ?? ink);
            }
        }
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * .205f; var y = -.12f + side * pose.Asymmetry * .025f;
            var open = MathF.Max(0, pose.Eyes + side * pose.Asymmetry * .15f);
            if (pose.CrossEyes)
            {
                Line(new(x - .06f, y - .06f), new(x + .06f, y + .06f));
                Line(new(x - .06f, y + .06f), new(x + .06f, y - .06f));
            }
            else if (open < .18f)
                Curve(new(x - .08f, y), new(x, y + (pose.Smile > .5f ? -.09f : .045f)), new(x + .08f, y));
            else
            {
                // White eyes emerge only for exceptional reactions, then return to dots.
                var wide = Math.Clamp(pose.WideEyes, 0, 1);
                var center = new Vector2(x + pose.Gaze * .025f, y);
                var rx = float.Lerp(.045f, .088f, wide);
                var ry = float.Lerp(.065f, .105f, wide) * open;
                Oval(center, rx, ry);
                if (wide > .01f)
                {
                    Oval(center, rx * .77f, ry * .84f, Color.Lerp(ink, Color.White, wide), .002f);
                    var pupil = MathF.Min(.035f / MathF.Max(1, open), ry * .55f);
                    Oval(center + new Vector2(pose.Gaze * .015f, 0), pupil, pupil, ink, .004f);
                }
            }
            var brows = Math.Clamp(pose.Brows, 0, 1);
            if (brows > .02f)
            {
                var browY = -.29f - pose.BrowLift * .06f + side * pose.Asymmetry * .045f;
                var halfBrow = .095f * brows; var tilt = side * pose.BrowTilt * .065f * brows;
                Line(new(x - halfBrow, browY + tilt), new(x + halfBrow, browY - tilt), weight: 1.2f * brows);
            }
        }
        var half = .2f * pose.MouthWidth;
        if (pose.MouthOpen > .08f)
            Oval(new(0, .21f), half, .025f + pose.MouthOpen * .13f);
        else Curve(new(-half, .2f + pose.Asymmetry * .035f), new(0, .2f + pose.Smile * .19f), new(half, .2f - pose.Asymmetry * .035f), 1.15f);
    }
}
