using App2d.Core.Characters;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

internal sealed class HoundAnatomy : ICharacterAnatomy
{
    private static readonly string[][] Chains = [["FrontUpperLeg", "FrontLowerLeg", "IKFrontLeg", "FF"], ["BackLeg", "BackUpperLeg", "BackLowerLeg", "IKBackLeg", "FFB"]];
    private static readonly string[] Spine = ["Back", "Torso", "Torso2", "Torso3", "Neck1", "Neck2", "Neck3", "Head"];
    private readonly Dictionary<string, int> _indices;
    private readonly Vector3[] _source, _drawing;
    private readonly HeadDrawing _headDrawing = new();
    private readonly HoundPose _pose;
    public HoundAnatomy(PointLibrary library)
    {
        _pose = new(library);
        _indices = library.PointNames.Select((name, i) => (name, i)).ToDictionary(p => p.name, p => p.i);
        _source = new Vector3[_indices.Count]; _drawing = new Vector3[_indices.Count];
    }
    private Vector3 At(string name, string end = "head") => _drawing[_indices[name + ":" + end]];
    public void Build(Vector3[] raw, PointClip clip, double time, CharacterAppearance look, CharacterDrawOptions options, CharacterMesh mesh, CharacterMesh face, CharacterMesh backdrop)
    {
        _pose.Transform(raw, look); _pose.CopyTo(_source, _drawing);
        var yaw = look.Yaw * MathF.PI / 180; var cos = MathF.Cos(yaw); var sin = MathF.Sin(yaw);
        var ink = CharacterJson.Color(look.Ink); var paper = CharacterJson.Color(look.Fill); var far = look.FarTint ? new Color(.43f, .49f, .48f) : ink;
        if (options.Mode != "skeleton")
        {
            var rear = At("Back") - new Vector3(0, .23f * look.Size, 0); var front = At("Torso3") - new Vector3(0, .20f * look.Size, 0); var center = (rear + front) / 2;
            var angle = MathF.Atan2(front.Y - rear.Y, front.X - rear.X); var length = new Vector2(front.X - rear.X, front.Y - rear.Y).Length(); var half = length * .5f + .10f * look.Size;
            Vector3 Local(float x, float y) => center + new Vector3(x * MathF.Cos(angle) - y * MathF.Sin(angle), x * MathF.Sin(angle) + y * MathF.Cos(angle), x / MathF.Max(length, 1e-8f) * (front.Z - rear.Z));
            var f = look.Body * look.Size;
            var body = CharacterCurves.Closed([Local(-half, 0), Local(-half * .80f, f * .78f), Local(-half * .1f, f * .91f), Local(half * .76f, f * .82f), Local(half, 0), Local(half * .72f, -f), Local(-half * .08f, -f * .85f), Local(-half * .82f, -f * .69f)]);
            var head = At("Head"); var snout = At("Head", "tail"); var radius = .30f * look.Head * look.Size; var headAngle = MathF.Atan2(snout.Y - head.Y, snout.X - head.X);
            var rx = radius * look.HeadWidth; var ry = radius * look.HeadHeight;
            Vector2[] profile = [new(1, 0), new(.64f, .64f), new(-.12f, 1), new(-.81f, .78f), new(-1, 0), new(-.72f, -.68f), new(.04f, -.75f), new(.77f, -.44f)];
            var contour = CharacterCurves.Closed(profile.Select((p, i) =>
            {
                var t = i / 8f * MathF.Tau; var x = rx * (p.X * (1 - look.HeadRoundness) + MathF.Cos(t) * look.HeadRoundness); var y = ry * (p.Y * (1 - look.HeadRoundness) + MathF.Sin(t) * look.HeadRoundness);
                if (look.Flip) y = -y;
                return head + new Vector3(x * MathF.Cos(headAngle) - y * MathF.Sin(headAngle), x * MathF.Sin(headAngle) + y * MathF.Cos(headAngle), 0);
            }).ToArray());
            var customRight = new Vector3(MathF.Cos(headAngle), MathF.Sin(headAngle), 0) * (radius / .6f);
            var customDown = new Vector3(MathF.Sin(headAngle), -MathF.Cos(headAngle), 0) * (radius / .6f) * (look.Flip ? -1 : 1);
            if (look.CustomHead is { } editedHead)
            {
                var local = editedHead.Contour();
                contour = local.Append(local[0]).Select(p => head + customRight * p.X + customDown * p.Y).ToList();
            }
            var filled = options.Mode != "sticks";
            foreach (var side in new[] { "R", "L" }) foreach (var chain in Chains)
            {
                var offset = side == "R" ? look.Spread : 0;
                var path = chain.Select(n => At(n + "." + side)).Append(At(chain[^1] + "." + side, "tail")).Select(p => p - new Vector3(offset * cos, 0, offset * sin)).ToList();
                if (filled)
                {
                    var root = (At(chain[0] + ".L") + At(chain[0] + ".R")) / 2;
                    path = CharacterCurves.Trim([Local(chain[0] == "BackLeg" ? -half * .65f : half * .65f, 0), root, .. path], body);
                }
                else path.Insert(0, At(chain[0] == "BackLeg" ? "Back" : "Torso3"));
                mesh.Path(path.Take(Math.Max(0, path.Count - 1)).ToArray(), side == "R" ? far : ink, .055f);
                if (path.Count > 1) mesh.Line(path[^2], path[^1], .073f, side == "R" ? far : ink);
            }
            List<Vector3> Curve(IEnumerable<Vector3> p) => look.Curves ? CharacterCurves.Smooth(p) : p.ToList();
            var tail = Curve(new[] { filled ? rear : At("Back") }.Concat(Enumerable.Range(1, 8).Select(i => At("Tail" + i))).Append(At("Tail8", "tail")));
            var neck = Curve(filled ? [front, At("Neck1"), At("Neck2"), At("Neck3"), head] : Spine.Select(n => At(n)));
            if (filled) { tail = CharacterCurves.Trim(tail, body); neck = CharacterCurves.Trim(neck, body); }
            neck.Reverse(); neck = CharacterCurves.Trim(neck, contour); neck.Reverse();
            mesh.Path(tail, ink, .055f, .025f); mesh.Path(neck, ink, .055f);
            if (filled) { mesh.Shell(center, body, look.Thickness * look.Size, paper); mesh.Path(body, ink, .055f); }
            if (look.CustomHead is { } customHead)
                _headDrawing.Build(mesh, face, customHead, head - new Vector3(0, 0, .27f * look.Head * look.Size), customRight, customDown, .055f, ink);
            else
            {
            mesh.Shell(head, contour, .27f * look.Head * look.Size, paper); mesh.Path(contour, ink, .055f);
            if (look.Face != "none")
            {
                foreach (var uv in new[] { new Vector2(0, 0), new(0, 1), new(1, 0), new(1, 0), new(0, 1), new(1, 1) })
                {
                    var x = (uv.X - .5f) * rx * 1.56f; var y = (.5f - uv.Y) * ry * 1.4f;
                    // Facing reflects the head basis; rotating by pi alone inverts the face.
                    if (look.Flip) y = -y;
                    face.Vertex(head + new Vector3(x * MathF.Cos(headAngle) - y * MathF.Sin(headAngle), x * MathF.Sin(headAngle) + y * MathF.Cos(headAngle), -.27f * look.Head * look.Size - .012f), Color.White, uv);
                }
            }
            }
        }
        if (options.Joints || options.Mode is "skeleton" or "overlay") foreach (var (name, index) in _indices.Where(p => p.Key.EndsWith(":head")))
        {
            var a = _source[index] * look.Size; var b = _source[_indices[name[..^5] + ":tail"]] * look.Size;
            if (look.Flip) { a.X *= -1; b.X *= -1; } a.Z = b.Z = -5;
            mesh.Line(a, b, .018f, new Color(.06f, .58f, .56f)); mesh.Disk(a, .035f, new Color(.06f, .58f, .56f));
        }
    }
}
