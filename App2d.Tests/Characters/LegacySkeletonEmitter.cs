using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using System.Text;

namespace App2d.Tests.Characters;

/// <summary>Writes skeletons read from authored rigs out as C# source. Dies with <see cref="LegacyRigs"/>.</summary>
internal static class LegacySkeletonEmitter
{
    /// <summary>
    /// One class of joint and limb index constants, and one skeleton property per build. Every build must have the same joints
    /// and limbs in the same order; only their rest transforms differ.
    /// </summary>
    public static string Emit(string className, string summary, string regenerateWith, IReadOnlyList<string> limbs,
        IReadOnlyList<(string Property, string Summary, Skeleton2D Skeleton)> builds)
    {
        var first = builds[0].Skeleton;
        string Joint(int joint) => LegacyClipEmitter.Pascal(first.Joints[joint].Name);
        var source = new StringBuilder();
        source.Append("// Generated from the authored JSON rigs by LegacySkeletonEmitter in App2d.Tests. While those rigs exist, change them and\n");
        source.Append($"// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named {regenerateWith}) instead of editing this file.\n");
        source.Append("using App2d.Core.Kinematics;\n\nnamespace App2d.Core.Characters;\n\n");
        source.Append($"/// <summary>{summary}</summary>\npublic static class {className}\n{{\n");
        source.Append(LegacyClipEmitter.Wrap("public const int ", "", [.. Enumerable.Range(0, first.Count).Select(i => $"{Joint(i)} = {i}")], 4, ";", "")).Append('\n');
        if (limbs.Count > 0)
        {
            source.Append("    /// <summary>Indices into the skeleton's limbs.</summary>\n");
            source.Append(LegacyClipEmitter.Wrap("public const int ", "", [.. limbs.Select((l, i) => $"{l} = {i}")], 4, ";", "")).Append('\n');
        }
        string[] mounts = [.. first.Mounts.Select(m => LegacyClipEmitter.Pascal(m.Name))];
        if (mounts.Length > 0)
        {
            source.Append("    /// <summary>Indices into the skeleton's mounts.</summary>\n");
            source.Append(LegacyClipEmitter.Wrap("public const int ", "", [.. mounts.Select((m, i) => $"{m} = {i}")], 4, ";", "")).Append('\n');
        }
        string[] names = [.. Enumerable.Range(0, first.Count).Select(Joint), .. limbs, .. mounts];
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        foreach (var (property, text, skeleton) in builds)
        {
            Assert.Equal(first.Joints.Select(j => (j.Name, j.Parent)), skeleton.Joints.Select(j => (j.Name, j.Parent)));
            Assert.Equal(first.Limbs.Select(l => (l.End, l.Bend, l.Frame)), skeleton.Limbs.Select(l => (l.End, l.Bend, l.Frame)));
            source.Append($"\n    /// <summary>{text}</summary>\n    public static Skeleton2D {property} {{ get; }} = new(\n");
            for (var i = 0; i < skeleton.Count; i++)
            {
                var joint = skeleton.Joints[i];
                Assert.True(joint.Limits == JointLimits2D.Free && joint.MoveScale == 1, "Limits and move scales are not written.");
                var parent = joint.Parent == Skeleton2D.NoParent ? "Skeleton2D.NoParent" : Joint(joint.Parent);
                var layer = LegacyClipEmitter.Number(joint.Layer) == "0f" ? "" : $" {{ Layer = {LegacyClipEmitter.Number(joint.Layer)} }}";
                source.Append($"        new(\"{joint.Name}\", {parent}, {Rest(joint.Rest)}){layer}{(i == skeleton.Count - 1 ? ")" : ",")}\n");
            }
            var members = new List<string>();
            if (skeleton.Limbs.Length > 0)
            {
                var entries = skeleton.Limbs.Select(l => $"new({Joint(l.End)}, {l.Bend}{(l.Frame == Skeleton2D.NoParent ? "" : ", " + Joint(l.Frame))})").ToList();
                members.Add(LegacyClipEmitter.Wrap("Limbs = ", "[", entries, 8, ",", "]"));
            }
            if (skeleton.Mounts.Length > 0)
            {
                Assert.Equal(first.Mounts.Select(m => (m.Name, m.At, m.Turn, m.Toward)), skeleton.Mounts.Select(m => (m.Name, m.At, m.Turn, m.Toward)));
                members.Add(LegacyClipEmitter.Wrap("Mounts = ", "[", [.. skeleton.Mounts.Select(m => Mount(m, Joint))], 8, ",", "]"));
            }
            if (members.Count == 0)
            {
                source.Length--;
                source.Append(";\n");
                continue;
            }
            source.Append("    {\n").Append(string.Join("\n", members)).Append("\n    };\n");
        }
        return source.Append("}\n").ToString();
    }

    private static string Mount(Mount2D mount, Func<int, string> joint)
    {
        var local = (Rest(mount.Local) == "default" ? "" : ", " + Rest(mount.Local))
            + (LegacyClipEmitter.Number(mount.Layer) == "0f" ? "" : $", layer: {LegacyClipEmitter.Number(mount.Layer)}");
        if (mount.Toward != Skeleton2D.NoParent)
        {
            Assert.True(mount.Turn == mount.At, $"Mount '{mount.Name}' aims at a joint but falls back to a different joint's turn, which is not written.");
            return $"Mount2D.Aimed(\"{mount.Name}\", {joint(mount.At)}, {joint(mount.Toward)}{local})";
        }
        if (mount.Turn == Skeleton2D.NoParent) return $"Mount2D.Upright(\"{mount.Name}\", {joint(mount.At)}{local})";
        return mount.Turn == mount.At ? $"Mount2D.On(\"{mount.Name}\", {joint(mount.At)}{local})" : $"Mount2D.Turning(\"{mount.Name}\", {joint(mount.At)}, {joint(mount.Turn)}{local})";
    }

    private static string Rest(RigidTransform2D rest)
    {
        var turn = LegacyClipEmitter.Number(rest.Rotation);
        var move = LegacyClipEmitter.Vector(rest.Translation);
        if (move == "new(0f, 0f)") return turn == "0f" ? "default" : $"new(default, {turn})";
        return turn == "0f" ? $"new({move})" : $"new({move}, {turn})";
    }
}
