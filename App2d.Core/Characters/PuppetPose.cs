using System.Numerics;
using App2d.Core.Kinematics;

namespace App2d.Core.Characters;

public sealed record PuppetContactResult(string End, Vector3 Target, float Error);

/// <summary>The same evaluated pose can feed drawing, attachments and future gameplay consumers.</summary>
public sealed class PuppetPose
{
    public Dictionary<string, Vector3> Points { get; } = [];
    public Vector3 Position { get; set; }
    public List<PuppetContactResult> Contacts { get; } = [];
    public Vector3 World(string id) => Points[id] + Position;

    public static PuppetPose Rest(PuppetDefinition definition)
    {
        var pose = new PuppetPose();
        foreach (var control in definition.Controls) pose.Points.Add(control.Id, control.Rest.XYZ);
        return pose;
    }

    public static PuppetPose Sample(PuppetDefinition definition, PuppetMotion motion, double seconds, bool repeat = false)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var pose = Rest(definition);
        var cycles = repeat && motion.Loop ? Math.Floor(seconds / motion.Duration) : 0;
        var time = (float)(cycles > 0 ? seconds % motion.Duration : Math.Min(seconds, motion.Duration));
        Vector3 travel = default;
        if (motion.Keys.Count > 0)
        {
            var first = motion.Keys[0]; var last = motion.Keys[^1];
            travel = (last.Position.XYZ - first.Position.XYZ) * (float)cycles;
            var a = first; var b = first;
            foreach (var key in motion.Keys) { b = key; if (key.Time >= time) break; a = key; }
            var amount = b.Time <= a.Time ? 0 : Math.Clamp((time - a.Time) / (b.Time - a.Time), 0, 1);
            pose.Position = Vector3.Lerp(a.Position.XYZ, b.Position.XYZ, amount) + travel;
            foreach (var control in definition.Controls)
                pose.Points[control.Id] = Vector3.Lerp(a.Points.GetValueOrDefault(control.Id, control.Rest).XYZ, b.Points.GetValueOrDefault(control.Id, control.Rest).XYZ, amount);
        }
        foreach (var chain in definition.Chains) pose.Solve(definition, chain, pose.Points[chain.End]);
        foreach (var contact in motion.Contacts.Where(c => time >= c.Start && time <= c.Finish))
        {
            var target = contact.Target.XYZ + travel;
            pose.Solve(definition, definition.Chains.Single(c => c.End == contact.End), target - pose.Position);
            pose.Contacts.Add(new(contact.End, target, Vector2.Distance(new(pose.World(contact.End).X, pose.World(contact.End).Y), new(target.X, target.Y))));
        }
        return pose;
    }

    public void Solve(PuppetDefinition definition, PuppetChain chain, Vector3 target)
    {
        Vector2 Rest(string id) => definition.Controls.Single(c => c.Id == id).Rest.XY;
        var root = Points[chain.Root];
        var solved = TwoBoneIk2D.Solve(new(root.X, root.Y), new(target.X, target.Y),
            Vector2.Distance(Rest(chain.Root), Rest(chain.Joint)), Vector2.Distance(Rest(chain.Joint), Rest(chain.End)), chain.Bend);
        void MoveBranch(string id, Vector3 delta)
        {
            Points[id] += delta;
            foreach (var bone in definition.Bones.Where(b => b.From == id)) MoveBranch(bone.To, delta);
        }
        var joint = new Vector3(solved.Joint, Points[chain.Joint].Z);
        var end = new Vector3(solved.End, target.Z);
        var jointDelta = joint - Points[chain.Joint];
        foreach (var bone in definition.Bones.Where(b => b.From == chain.Joint && b.To != chain.End)) MoveBranch(bone.To, jointDelta);
        MoveBranch(chain.End, end - Points[chain.End]);
        Points[chain.Joint] = joint;
    }

    public void Move(PuppetDefinition definition, string id, Vector3 target)
    {
        if (definition.Chains.Any(c => c.Joint == id)) throw new InvalidOperationException("This bend is solved by IK. Move the tip, or change the chain's bend direction.");
        var chain = definition.Chains.FirstOrDefault(c => c.End == id);
        if (chain is not null) { Solve(definition, chain, target); return; }
        var delta = target - Points[id];
        void Translate(string point)
        {
            Points[point] += delta;
            foreach (var bone in definition.Bones.Where(b => b.From == point)) Translate(bone.To);
        }
        Translate(id);
    }

    public PuppetKey Key(float time) => new()
    { Time = time, Position = PuppetPoint.From(Position), Points = Points.ToDictionary(p => p.Key, p => PuppetPoint.From(p.Value)) };
}
