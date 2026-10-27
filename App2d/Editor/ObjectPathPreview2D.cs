using App2d.Core.Characters.Authored;
using App2d.Core.Objects;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Editor;

/// <summary>Samples an object's path from a placement origin for drawing; empty when the object does not move.</summary>
internal static class ObjectPathPreview2D
{
    public static Vector2[] Sample(Part asset, Vector2 origin, int count = 32)
    {
        ArgGuard.ThrowIfNull(asset);
        ArgGuard.Positive(count);
        if (asset.Motion is not { } definition)
        {
            return [];
        }

        var motion = definition.CreateMotion(origin);
        var points = new Vector2[count + 1];
        for (var i = 0; i <= count; i++)
        {
            points[i] = motion.Position(definition.PeriodSeconds * i / count);
        }

        return points;
    }
}
