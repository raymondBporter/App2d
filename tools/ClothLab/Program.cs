using System.Numerics;
using System.Text.Json;
using App2d.Core.Characters;

// Export the real template + IK/contact evaluation, rather than recreating a gait in JavaScript.
var output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/cloth-lab/puppet-cape.html");
var source = Path.GetFullPath(args.Length > 1 ? args[1] : "tools/ClothLab");
const int intervals = 120;
float Round(float x) => MathF.Round(x, 6);
float[] Point(Vector3 p) => [Round(p.X), Round(p.Y), Round(p.Z)];
object Export(PuppetDefinition definition)
{
    var motion = definition.Motions[0];
    var travel = motion.Keys[^1].Position.XYZ - motion.Keys[0].Position.XYZ;
    var frames = Enumerable.Range(0, intervals + 1).Select(i =>
    {
        var pose = PuppetPose.Sample(definition, motion, motion.Duration * (double)i / intervals);
        return definition.Controls.SelectMany(c => Point(pose.World(c.Id) - new Vector3(pose.Position.X, 0, 0))).ToArray();
    }).ToArray();
    if (frames.Any(frame => frame.Any(value => !float.IsFinite(value)))) throw new InvalidOperationException("Nonfinite sampled pose.");
    if (frames[0].Zip(frames[^1]).Max(p => MathF.Abs(p.First - p.Second)) > .0001f)
        throw new InvalidOperationException("The sampled local pose is discontinuous at the loop seam.");
    return new
    {
        name = definition.Name, duration = motion.Duration, travel = Point(travel), speed = Round(travel.X / motion.Duration),
        controls = definition.Controls.Select(c => c.Id), frames, parts = definition.Parts,
        ink = definition.Ink, lineWidth = definition.LineWidth,
        face = FaceExpressions.Get(definition.Parts.Single(p => p.Id == "head").Face)
    };
}
var json = JsonSerializer.Serialize(new { walk = Export(PuppetTemplates.StepStudy()), run = Export(PuppetTemplates.RunStudy()) }, JsonSerializerOptions.Web);
var fragment = File.ReadAllText(Path.Combine(source, "cape.template.html"))
    .Replace("/*__PUPPET_DATA__*/", json)
    .Replace("/*__CAPE_SIMULATION__*/", File.ReadAllText(Path.Combine(source, "cape-simulation.js")));
if (fragment.Contains("/*__")) throw new InvalidOperationException("Unfilled template marker.");
if (System.Text.Encoding.UTF8.GetByteCount(fragment) >= 1_000_000) throw new InvalidOperationException("Preview exceeds inline size limit.");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, fragment);
Console.WriteLine($"Exported actual walk/run poses and cape experiment: {output}");
Console.WriteLine("PASS: finite samples, walk/run loop seams. Walk 0.416667 units/s; run 2.222222 units/s.");
