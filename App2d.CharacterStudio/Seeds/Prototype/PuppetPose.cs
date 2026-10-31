using App2d.Core.Characters;
using App2d.Core.Kinematics;
using System.Numerics;

namespace App2d.CharacterStudio.CharacterSeeds;

/// <summary>A pose used to author the prototype puppet studies before importing their motion.</summary>
public sealed class PuppetPose
{
    public Dictionary<string, Vector3> Points { get; } = [];
    public Vector3 Position { get; set; }

    public static PuppetPose Rest(PuppetDefinition definition)
    {
        var pose = new PuppetPose();
        foreach (var control in definition.Controls)
        {
            pose.Points.Add(control.Id, control.Rest.XYZ);
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
            foreach (var bone in definition.Bones.Where(b => b.From == id))
            {
                MoveBranch(bone.To, delta);
            }
        }
        var joint = new Vector3(solved.Joint, Points[chain.Joint].Z);
        var end = new Vector3(solved.End, target.Z);
        var jointDelta = joint - Points[chain.Joint];
        foreach (var bone in definition.Bones.Where(b => b.From == chain.Joint && b.To != chain.End))
        {
            MoveBranch(bone.To, jointDelta);
        }

        MoveBranch(chain.End, end - Points[chain.End]);
        Points[chain.Joint] = joint;
    }

    public PuppetKey Key(float time) => new()
    {
        Time = time,
        Position = PuppetPoint.From(Position),
        Points = Points.ToDictionary(p => p.Key, p => PuppetPoint.From(p.Value))
    };
}
