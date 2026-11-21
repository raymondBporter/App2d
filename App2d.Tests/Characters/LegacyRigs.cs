using App2d.Core.Animation;
using App2d.Core.Characters.Authored;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;

namespace App2d.Tests.Characters;

/// <summary>
/// Reads an authored rig as a <see cref="Skeleton2D"/> and compares the two, until the authored rigs are retired. Dies with
/// <see cref="LegacyClips"/>.
/// </summary>
internal static class LegacyRigs
{
    /// <summary>The model's joints at rest and its IK chains as limbs. Every chain end holds its own angle, as the rig's do.</summary>
    public static Skeleton2D Skeleton(ResolvedModel model)
    {
        var index = model.Order.Select((c, i) => (c.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        RigidTransform2D World(string id) => new(model.Nodes[id].Rest.XY, model.Nodes[id].RestAngle);
        var joints = model.Order.Select(c => new Joint2D(c.Id, c.Parent is null ? Skeleton2D.NoParent : index[c.Parent],
            c.Parent is null ? World(c.Id) : World(c.Id).Then(World(c.Parent).Inverse())) { Layer = model.Nodes[c.Id].Layer }).ToArray();
        foreach (var chain in model.IkConstraints)
        {
            Assert.True(model.Nodes[chain.End].CharacterOriented, $"{model.Id}: the end of chain '{chain.Id}' turns with its limb, which limbs do not do.");
        }
        Assert.DoesNotContain(model.Order, c => model.Nodes[c.Id].CharacterOriented && model.IkConstraints.All(k => k.End != c.Id));
        return new(joints)
        {
            Limbs = [.. model.IkConstraints.Select(k => new Limb2D(index[k.End], k.Bend, k.Frame == CharacterModel.Locomotion ? Skeleton2D.NoParent : index[k.Frame]))],
            Mounts = [.. model.Base.Sockets.Select(s => new Mount2D(s.Id, index[s.Control], s.Frame == CharacterModel.Locomotion ? Skeleton2D.NoParent : index[s.Frame ?? s.Control],
                s.Toward is null ? Skeleton2D.NoParent : index[s.Toward], s.Transform, s.Layer))],
        };
    }

    public static RigidTransform2D[] RestWorld(Skeleton2D skeleton)
    {
        var world = new RigidTransform2D[skeleton.Count];
        skeleton.ToWorld(skeleton.RestPose(), world);
        return world;
    }

    /// <summary>Same joints, parents, rest positions, rest angles and layers; same limbs; same mounts.</summary>
    public static void AssertRestMatches(ResolvedModel model, Skeleton2D skeleton)
    {
        var world = RestWorld(skeleton);
        Assert.Equal(model.Order.Count, skeleton.Count);
        foreach (var control in model.Order)
        {
            var joint = skeleton.IndexOf(control.Id);
            var node = model.Nodes[control.Id];
            var parent = skeleton.Joints[joint].Parent;
            Assert.Equal(control.Parent, parent == Skeleton2D.NoParent ? null : skeleton.Joints[parent].Name);
            Assert.True(Vector2f.Distance(node.Rest.XY, world[joint].Translation) < 1e-4f, $"{model.Id} {control.Id}: rig {node.Rest.XY}, skeleton {world[joint].Translation}.");
            Assert.True(MathF.Abs(Angles.WrapSigned(node.RestAngle - world[joint].Rotation)) < 1e-4f, $"{model.Id} {control.Id}: rig angle {node.RestAngle}, skeleton {world[joint].Rotation}.");
            Assert.True(MathF.Abs(node.Rest.Z - skeleton.RestLayers[joint]) < 1e-5f, $"{model.Id} {control.Id}: rig layer {node.Rest.Z}, skeleton {skeleton.RestLayers[joint]}.");
        }
        Assert.Equal(model.IkConstraints.Select(k => (k.End, k.Bend, Frame: k.Frame == CharacterModel.Locomotion ? null : k.Frame)),
            skeleton.Limbs.Select(l => (skeleton.Joints[l.End].Name, l.Bend, Frame: l.Frame == Skeleton2D.NoParent ? null : skeleton.Joints[l.Frame].Name)));
        Assert.Equal(model.Base.Sockets.Select(s => s.Id), skeleton.Mounts.Select(m => m.Name));
        foreach (var (socket, mount) in model.Base.Sockets.Zip(skeleton.Mounts)) Assert.Equal(socket.Layer, mount.Layer, 5);
    }

    /// <summary>
    /// Plays clips both ways on one build and returns the largest difference over all joints and sampled times. Travel must
    /// match outright. <paramref name="clipFor"/> finds the skeleton clip that stands for an authored one.
    /// </summary>
    public static (float Distance, float Angle, string Where) Compare(ResolvedModel model, Skeleton2D skeleton, IEnumerable<MotionClip> authoredClips,
        Func<MotionClip, SkeletonClip2D> clipFor, int steps)
    {
        var local = new RigidTransform2D[skeleton.Count];
        var world = new RigidTransform2D[skeleton.Count];
        var turns = new Vector3f[skeleton.Mounts.Length];
        float[] keyed = new float[skeleton.Count], layers = new float[skeleton.Count];
        var worst = (Distance: 0f, Angle: 0f, Where: "");
        foreach (var authored in authoredClips)
        {
            var clip = clipFor(authored);
            Assert.Equal(authored.Duration, clip.Duration);
            Assert.Equal(authored.Loop, clip.Loop);
            Assert.Equal(authored.Markers.Select(m => new ClipMarker2D(m.Id, m.Time)), clip.Markers);
            for (var step = 0; step <= steps; step++)
            {
                var time = authored.Duration * step / steps;
                var expected = PoseEvaluator.Sample(model, authored, time, repeat: false, new PoseInput { InPlace = true });
                clip.Pose(skeleton, time, local);
                skeleton.ToWorld(local, world);
                for (var joint = 0; joint < skeleton.Count; joint++)
                {
                    var name = skeleton.Joints[joint].Name;
                    var distance = Vector2f.Distance(expected.Points[name].XY, world[joint].Translation);
                    var angle = MathF.Abs(Angles.WrapSigned(expected.Angles[name] - world[joint].Rotation));
                    if (distance > worst.Distance || angle > worst.Angle)
                    {
                        worst = (MathF.Max(distance, worst.Distance), MathF.Max(angle, worst.Angle), $"{model.Id}, {authored.Id} at {time:0.###}s, {name}: evaluator {expected.Points[name].XY} / {expected.Angles[name]:0.####}, skeleton {world[joint].Translation} / {world[joint].Rotation:0.####}");
                    }
                }
                // Every mount sits and turns where the rig's socket of the same name does.
                var actor = new ActorPose(expected, Vector2f.Zero, 1);
                foreach (var mount in skeleton.Mounts)
                {
                    var socket = actor.SocketPlacement(model.Base.Sockets.Single(s => s.Id == mount.Name));
                    var placed = mount.World(world);
                    var distance = Vector2f.Distance(socket.Origin.XY, placed.Translation);
                    var angle = MathF.Abs(Angles.WrapSigned(socket.XAxis.AngleRadians - placed.Rotation));
                    if (distance > worst.Distance || angle > worst.Angle)
                    {
                        worst = (MathF.Max(distance, worst.Distance), MathF.Max(angle, worst.Angle), $"{model.Id}, {authored.Id} at {time:0.###}s, mount {mount.Name}: socket {socket.Origin.XY} / {socket.XAxis.AngleRadians:0.####}, mount {placed.Translation} / {placed.Rotation:0.####}");
                    }
                }
                // Every joint sits at the same layer in the drawing order, clips that key layers included.
                clip.SampleLayers(time, keyed);
                skeleton.ToLayers(keyed, layers);
                for (var joint = 0; joint < skeleton.Count; joint++)
                {
                    var name = skeleton.Joints[joint].Name;
                    Assert.True(MathF.Abs(expected.Points[name].Z - layers[joint]) < 1e-5f, $"{model.Id}, {authored.Id} at {time:0.###}s, {name}: evaluator layer {expected.Points[name].Z}, skeleton {layers[joint]}.");
                }
                // Every mount is turned out of the plane as far as the rig's socket of the same name is.
                clip.SampleMounts(time, turns);
                for (var mount = 0; mount < turns.Length; mount++)
                {
                    var socket = expected.SocketAngles.GetValueOrDefault(skeleton.Mounts[mount].Name);
                    var angle = (socket - turns[mount]).Length();
                    if (angle > worst.Angle) worst = (worst.Distance, angle, $"{model.Id}, {authored.Id} at {time:0.###}s, mount {skeleton.Mounts[mount].Name}: socket turned {socket}, mount {turns[mount]}");
                }
                var travel = PoseEvaluator.Sample(model, authored, time).Locomotion.XY;
                Assert.True(Vector2f.Distance(travel, clip.TravelAt(skeleton, time)) < 1e-4f, $"{model.Id}, {authored.Id} at {time:0.###}s: travel {travel}, clip {clip.TravelAt(skeleton, time)}.");
            }
        }
        return worst;
    }

    /// <summary>Vertex for vertex: the same triangles, colours and layers.</summary>
    public static void AssertSameMesh(ReadOnlySpan<LayeredVertex> expected, ReadOnlySpan<LayeredVertex> actual, string what)
    {
        Assert.True(expected.Length > 0, $"{what} drew nothing.");
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            var where = $"{what}, vertex {i} of {expected.Length}";
            Assert.True((expected[i].Position - actual[i].Position).Length() < 2e-3f, $"{where}: part {expected[i].Position}, attachment {actual[i].Position}.");
            // A shaded face's colour is rounded from its angle to the light, so a turned solid may land one step either side.
            Color4b want = expected[i].Color, got = actual[i].Color;
            Assert.True(Math.Abs(want.R - got.R) <= 2 && Math.Abs(want.G - got.G) <= 2 && Math.Abs(want.B - got.B) <= 2 && want.A == got.A, $"{where}: part {want}, attachment {got}.");
            Assert.True(MathF.Abs(expected[i].Layer - actual[i].Layer) < 1e-5f, $"{where}: part layer {expected[i].Layer}, attachment {actual[i].Layer}.");
        }
    }

    /// <summary>The repository's root, found from wherever the tests were built.</summary>
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "App2d.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }

    /// <summary>
    /// Checks one generated file against what the emitter writes now. With the environment variable APP2D_WRITE_CLIPS set to 1
    /// it rewrites the file first.
    /// </summary>
    public static bool UpToDate(string file, string source)
    {
        var path = Path.Combine(RepositoryRoot(), "App2d.Core", "Characters", "Skeletons", file);
        if (Environment.GetEnvironmentVariable("APP2D_WRITE_CLIPS") == "1") File.WriteAllText(path, source.Replace("\n", "\r\n"));
        return File.Exists(path) && source == File.ReadAllText(path).Replace("\r\n", "\n");
    }
}
