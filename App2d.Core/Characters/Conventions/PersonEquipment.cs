using App2d.Core.Characters.Drawing;
using App2d.Core.Geometry.Curves;

namespace App2d.Core.Characters.Authored;

/// <summary>The standard Person equipment contract; character evaluation remains anatomy independent.</summary>
public static class PersonEquipment
{
    public static (string Left, string Right) Sockets(string slot) => slot switch
    {
        PersonEquipmentSlots.Shoes => (Humanoid.LeftFootSocket, Humanoid.RightFootSocket),
        PersonEquipmentSlots.Gloves => (Humanoid.LeftHandSocket, Humanoid.RightHandSocket),
        PersonEquipmentSlots.Shins => (Humanoid.LeftShinSocket, Humanoid.RightShinSocket),
        _ => throw new InvalidDataException($"Unknown Person equipment slot '{slot}'."),
    };

    /// <summary>Shin artwork uses the ankle as origin, +Y toward the knee, just like existing foot-fitted artwork.</summary>
    public static IEnumerable<ModelSocket> ShinSockets() =>
    [
        new() { Id = Humanoid.LeftShinSocket, Control = Humanoid.LeftFoot, Toward = Humanoid.LeftKnee, Layer = -.01f },
        new() { Id = Humanoid.RightShinSocket, Control = Humanoid.RightFoot, Toward = Humanoid.RightKnee, Layer = -.01f },
    ];

    public static IReadOnlyList<ResolvedAttachment> Placements(Part asset, EquipmentSettings settings, IReadOnlyDictionary<string, ModelSocket> sockets)
    {
        settings.Validate();
        var (left, right) = Sockets(settings.Slot);
        ResolvedAttachment Place(string socket, bool mirror, string side) => ResolvedAttachment.Resolve(
            new() { Id = "equipment-" + settings.Slot + "-" + side, Asset = asset.Id, Socket = socket, Mirror = mirror },
            id => id == asset.Id ? asset : null, sockets, $"Equipment '{asset.Id}'");
        return [Place(left, false, "left"), Place(right, settings.MirrorRight, "right")];
    }

    public static CharacterArtworkOverrides Replacements(ResolvedModel model, IEnumerable<EquipmentSettings> equipped)
    {
        var result = new CharacterArtworkOverrides();
        foreach (var settings in equipped.Where(e => e.Replace))
        {
            var (left, right) = Sockets(settings.Slot);
            result.HiddenSockets.UnionWith([left, right]);
            switch (settings.Slot)
            {
                case PersonEquipmentSlots.Shoes:
                    result.Parts[Humanoid.LeftFoot] = result.Parts[Humanoid.RightFoot] = null;
                    break;
                case PersonEquipmentSlots.Gloves:
                    result.Parts[Humanoid.LeftHand] = result.Parts[Humanoid.RightHand] = null;
                    break;
                case PersonEquipmentSlots.Shins:
                    result.Parts[Humanoid.LeftShin] = result.Parts[Humanoid.RightShin] = null;
                    KeepThigh(Humanoid.LeftLeg, Humanoid.LeftKnee);
                    KeepThigh(Humanoid.RightLeg, Humanoid.RightKnee);
                    break;
            }
        }
        return result;

        void KeepThigh(string id, string knee)
        {
            var part = model.Parts.FirstOrDefault(p => p.Id == id);
            if (part?.Geometry is not RoundedPolylineCurve2D path || part.Bindings is not { } bindings) return;
            var joint = bindings.FirstOrDefault(b => b.Control == knee)?.Point;
            if (joint is not > 0 || joint >= path.Points.Length - 1) return;
            var count = joint.Value + 1;
            result.Parts[id] = part with
            {
                B = knee,
                Geometry = path with { Points = path.Points[..count] },
                Bindings = [.. bindings.Where(b => b.Point < count)],
            };
        }
    }
}
