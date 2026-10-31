using App2d.CharacterStudio.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Tests;

/// <summary>The prototype evaluator retained only to verify imported motion against its source.</summary>
internal sealed class PuppetReferencePose(PuppetPose pose)
{
    public Dictionary<string, Vector3> Points => pose.Points;
    public Vector3 Position => pose.Position;
    public Vector3 World(string id) => Points[id] + Position;
    public List<Contact> Contacts { get; } = [];
    public sealed record Contact(string End, Vector3 Target, float Error);

    public static PuppetReferencePose Sample(PuppetDefinition definition, PuppetMotion motion, double seconds, bool repeat = false)
    {
        ArgGuard.NonNegative(seconds);
        var pose = PuppetPose.Rest(definition);
        var result = new PuppetReferencePose(pose);
        var cycles = repeat && motion.Loop ? Math.Floor(seconds / motion.Duration) : 0;
        var time = (float)(cycles > 0 ? seconds % motion.Duration : Math.Min(seconds, motion.Duration));
        Vector3 travel = default;
        if (motion.Keys.Count > 0)
        {
            var first = motion.Keys[0];
            var last = motion.Keys[^1];
            travel = (last.Position.XYZ - first.Position.XYZ) * (float)cycles;
            var a = first;
            var b = first;
            foreach (var key in motion.Keys)
            {
                b = key;
                if (key.Time >= time)
                {
                    break;
                }

                a = key;
            }
            var amount = b.Time <= a.Time ? 0 : Math.Clamp((time - a.Time) / (b.Time - a.Time), 0, 1);
            pose.Position = Vector3.Lerp(a.Position.XYZ, b.Position.XYZ, amount) + travel;
            foreach (var control in definition.Controls)
            {
                pose.Points[control.Id] = Vector3.Lerp(a.Points.GetValueOrDefault(control.Id, control.Rest).XYZ, b.Points.GetValueOrDefault(control.Id, control.Rest).XYZ, amount);
            }
        }
        foreach (var chain in definition.Chains)
        {
            pose.Solve(definition, chain, pose.Points[chain.End]);
        }

        foreach (var contact in motion.Contacts.Where(c => time >= c.Start && time <= c.Finish))
        {
            var target = contact.Target.XYZ + travel;
            pose.Solve(definition, definition.Chains.Single(c => c.End == contact.End), target - pose.Position);
            result.Contacts.Add(new(contact.End, target, Vector2.Distance(new(result.World(contact.End).X, result.World(contact.End).Y), new(target.X, target.Y))));
        }
        return result;
    }

}
