using App2d.Core.Characters;
using App2d.Rendering.Characters;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.CharacterStudio;

internal static class StudioChecks
{
    public static void Run(string root)
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "catalog.json")));
        var libraries = JsonSerializer.Deserialize<LibraryEntry[]>(catalog.RootElement.GetProperty("libraries"), AuthoredJson.Tolerant)!;
        var clips = 0; var poses = 0; var maximumTriangles = 0;
        foreach (var entry in libraries)
        {
            var library = PointLibrary.Load(Path.Combine(root, entry.Path)); var geometry = new CharacterGeometry(library);
            var look = StudioDocument.Defaults(library); var sample = new Vector3[library.PointNames.Count];
            foreach (var clip in library.Clips.Values)
            {
                clips++;
                // Every stored endpoint and every interpolation interval, not just a few named animations.
                for (var i = 0; i < clip.SampleCount - 1; i++)
                {
                    clip.Sample(clip.Times[i], sample, true);
                    for (var p = 0; p < sample.Length; p++) Near(sample[p], clip.Read(i, p), clip.Id + " knot");
                    clip.Sample((clip.Times[i] + clip.Times[i + 1]) / 2, sample, true);
                    for (var p = 0; p < sample.Length; p++) Near(sample[p], (clip.Read(i, p) + clip.Read(i + 1, p)) / 2, clip.Id + " midpoint");
                }
                clip.Sample(clip.Duration, sample, true);
                for (var p = 0; p < sample.Length; p++) Near(sample[p], clip.Read(clip.SampleCount - 1, p), clip.Id + " held end");
                clip.Sample(clip.Duration, sample);
                for (var p = 0; p < sample.Length; p++) Near(sample[p], clip.Read(clip.Loop ? 0 : clip.SampleCount - 1, p), clip.Id + " wrap/clamp");
                foreach (var t in new[] { 0, clip.Duration * .37, clip.Duration })
                {
                    geometry.Build(clip, t, look, new(true), true); poses++;
                    if (geometry.Body.Count == 0 || geometry.Body.Count % 3 != 0) throw new InvalidDataException("Empty or partial mesh " + clip.Id);
                    maximumTriangles = Math.Max(maximumTriangles, geometry.Body.TriangleCount);
                    geometry.Build(clip, t, look with { Flip = true, Size = .7f, Head = look.Head * 1.2f, LegLength = .65f, NeckLength = .5f, TailLength = .4f }, new(false, false, false), true); poses++;
                }
            }
            VerifyPlayback(library);
        }
        VerifyDocument(root);
        VerifyReferences(root);
        VerifyHeads(root);
        VerifyEntities(root);
        VerifyWolfLook(root);
        var report = $"PASS {libraries.Length} libraries / {clips} clips / {poses} geometry cases; every stored sample and interval, held endpoints, playback, source pose/geometry references, weapons, head workshop, entity action/collision evaluation and document undo/save/load. Maximum exercised mesh: {maximumTriangles} triangles.";
        Console.WriteLine(report);
        // WinExe builds also leave a readable result for scripted verification.
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "character-studio-checks.txt"), report);
    }
    private static void Near(Vector3 a, Vector3 b, string context, float tolerance = .00015f)
    { if (Vector3.Distance(a, b) > tolerance) throw new InvalidDataException($"{context}: {a} != {b}"); }
    private static void VerifyWolfLook(string root)
    {
        var path = Path.Combine(root, "looks", "tomek-wolf.json");
        var document = new StudioDocument(PointLibrary.Load(Path.Combine(root, "quadruped", "library.json")));
        document.Apply(StudioDocument.ReadPreset(path), path);
        if (document.Bindings.Count != 9) throw new InvalidDataException("Missing wolf bindings.");
        foreach (var clipId in document.Bindings.Values)
        {
            var clip = document.Library.Clips[clipId];
            if (clip.Source != "tomek_wolf") throw new InvalidDataException("Wrong wolf binding source.");
            document.Geometry.Build(clip, clip.Duration, document.Appearance, new(false, false, false), true);
            var rightFace = document.Geometry.Face.Vertices.ToArray();
            document.Geometry.Build(clip, clip.Duration, document.Appearance with { Flip = true }, new(false, false, false), true);
            if (rightFace.Length != 6 || document.Geometry.Face.Count != rightFace.Length)
                throw new InvalidDataException("Missing wolf face.");
            for (var i = 0; i < rightFace.Length; i++)
            {
                var expected = rightFace[i].Position; expected.X *= -1;
                var actual = document.Geometry.Face.Vertices[i].Position;
                Near(new(actual.X, actual.Y, actual.Z), new(expected.X, expected.Y, expected.Z), "Wolf face reflection");
                if (document.Geometry.Face.Vertices[i].TextureCoordinate != rightFace[i].TextureCoordinate)
                    throw new InvalidDataException("Wolf reflection changed face UVs.");
            }
        }
    }
    private static void VerifyPlayback(PointLibrary library)
    {
        var first = library.Clips.Values.First(); var last = library.Clips.Values.Last(); var playback = new PointPlayback(library, first.Id);
        playback.Select(last.Id, true); playback.Advance(first.Duration + last.Duration * .3);
        if (playback.ClipId != last.Id || Math.Abs(playback.Time - last.Duration * .3) > 1e-6) throw new InvalidDataException("Queued carry was lost.");
        playback.StartSequence([first.Id, last.Id], false); playback.Advance(first.Duration + last.Duration + .1);
        if (playback.Playing || !playback.Finished || playback.ClipId != last.Id) throw new InvalidDataException("Sequence endpoint was lost.");
        playback.Seek(0); playback.Step(1); if (playback.Time != last.Times[1]) throw new InvalidDataException("Adaptive step failed.");
        playback.Step(-1); if (playback.Time != 0) throw new InvalidDataException("Reverse adaptive step failed.");
    }
    private static void VerifyReferences(string root)
    {
        var file = Path.Combine(root, "reference-checks.json");
        if (!File.Exists(file)) throw new FileNotFoundException("Missing source reference checks. Run capture-reference.cjs.", file);
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var test in document.RootElement.EnumerateArray())
        {
            var library = PointLibrary.Load(Path.Combine(root, test.GetProperty("library").GetString()!, "library.json"));
            var clip = library.Clips[test.GetProperty("clip").GetString()!]; var time = test.GetProperty("time").GetDouble();
            var pose = new Vector3[library.PointNames.Count]; clip.Sample(time, pose, true);
            var expected = test.GetProperty("pose");
            for (var i = 0; i < pose.Length; i++) Near(pose[i], new(expected[i * 3].GetSingle(), expected[i * 3 + 1].GetSingle(), expected[i * 3 + 2].GetSingle()), clip.Id + " source pose");
            var appearance = JsonSerializer.SerializeToNode(StudioDocument.Defaults(library), AuthoredJson.Tolerant)!.AsObject();
            foreach (var p in test.GetProperty("appearance").EnumerateObject()) appearance[p.Name] = JsonNode.Parse(p.Value.GetRawText());
            var look = appearance.Deserialize<CharacterAppearance>(AuthoredJson.Tolerant)!;
            // These fixtures verify the imported source artwork, including its original
            // face vertices. New expressive faces are exercised by the render sheets.
            if (library.Anatomy == "person" && look.Face is not ("none" or "cycle")) look = look with { Face = "source:" + look.Face };
            var geometry = new CharacterGeometry(library); geometry.Build(clip, time, look, new(false, false, false), true);
            var referenceCount = test.GetProperty("vertexCount").GetInt32();
            if (geometry.Body.Count != referenceCount) throw new InvalidDataException($"{library.Id}/{clip.Id}: {geometry.Body.Count} vertices != reference {referenceCount}");
            foreach (var v in test.GetProperty("vertices").EnumerateArray())
            {
                var p = geometry.Body.Vertices[v[0].GetInt32()].Position;
                Near(new(p.X, p.Y, p.Z), new(v[1].GetSingle(), v[2].GetSingle(), v[3].GetSingle()), library.Id + "/" + clip.Id + " source geometry", .001f);
            }
        }
    }
    private static void VerifyDocument(string root)
    {
        var document = new StudioDocument(PointLibrary.Load(Path.Combine(root, "person", "library.json")));
        var before = document.Appearance with { };
        document.Appearance.Head = 1.4f; document.RecordEdit(false); document.Undo();
        if (document.Appearance.Head != before.Head) throw new InvalidDataException("Appearance undo failed.");
        document.Redo(); if (document.Appearance.Head != 1.4f) throw new InvalidDataException("Appearance redo failed.");
        before = document.Appearance with { };
        document.Appearance.CustomHead = new HeadShape { Muzzle = 1.3f, FaceAngle = 30, Offsets = new HeadOffsets().With(2, new(.1f, -.15f)) };
        document.Appearance.Weapon = "hammer"; document.Appearance.WeaponHeadSize = 1.5f;
        var edited = document.Appearance with { };
        document.RecordEdit(false); document.Undo();
        if (document.Appearance.CustomHead is not null || document.Appearance.Weapon != "sword") throw new InvalidDataException("Head/weapon undo failed.");
        document.Redo(); if (document.Appearance != edited) throw new InvalidDataException("Head/weapon redo failed.");
        document.Bind("Idle", "idle");
        var file = Path.Combine(Path.GetTempPath(), "character-studio-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            document.Save(file); var preset = StudioDocument.ReadPreset(file);
            var restored = new StudioDocument(PointLibrary.Load(Path.Combine(root, "person", "library.json"))); restored.Apply(preset, file);
            if (restored.Appearance != document.Appearance || restored.Bindings["Idle"] != "idle" || restored.Dirty) throw new InvalidDataException("Preset roundtrip failed.");
        }
        finally { File.Delete(file); }
    }
    private static void VerifyHeads(string root)
    {
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "head-reference-checks.json")));
        var drawing = new HeadDrawing(); var body = new CharacterMesh(); var face = new CharacterMesh();
        foreach (var test in reference.RootElement.EnumerateArray())
        {
            var shape = test.GetProperty("shape").Deserialize<HeadShape>(AuthoredJson.Tolerant)!; shape.Validate();
            var points = shape.Points(); var expected = test.GetProperty("points");
            for (var i = 0; i < points.Length; i++) Near(new(points[i], 0), new(expected[i][0].GetSingle(), expected[i][1].GetSingle(), 0), "Source head control point");
            var curves = test.GetProperty("curve"); var contour = shape.Contour(); var index = 0; Vector2? previous = null;
            foreach (var curve in curves.EnumerateArray())
            {
                Vector2 Read(string name) => new(curve.GetProperty(name)[0].GetSingle(), curve.GetProperty(name)[1].GetSingle());
                for (var k = 0; k <= 8; k++)
                {
                    var t = k / 8f; var p = Read("entry") * ((1 - t) * (1 - t)) + Read("at") * (2 * t * (1 - t)) + Read("exit") * (t * t);
                    if (previous is { } prev && Vector2.DistanceSquared(prev, p) <= 1e-12f) continue;
                    Near(new(contour[index++], 0), new(p, 0), "Source head quadratic corner"); previous = p;
                }
            }
            if (index != contour.Length) throw new InvalidDataException("Head contour count mismatch.");
            var roundtrip = JsonSerializer.Deserialize<HeadShape>(JsonSerializer.Serialize(shape, AuthoredJson.Tolerant), AuthoredJson.Tolerant);
            if (shape != roundtrip) throw new InvalidDataException("Portable head roundtrip changed the shape.");
            body.Clear(); face.Clear(); drawing.Build(body, face, shape with { Face = "none" }, Vector3.Zero, Vector3.UnitX, -Vector3.UnitY, .035f, Microsoft.Xna.Framework.Color.Black);
            if (body.Count == 0) throw new InvalidDataException("Head workshop produced an empty mesh.");
            // Concave fills must cover the silhouette exactly, without the overfill from a triangle fan.
            var fillArea = 0f; var outlineArea = 0f;
            var fill = new Microsoft.Xna.Framework.Color(Convert.ToByte(shape.Color.Substring(1, 2), 16), Convert.ToByte(shape.Color.Substring(3, 2), 16), Convert.ToByte(shape.Color.Substring(5, 2), 16));
            for (var i = 0; i < body.Count; i += 3)
            {
                if (body.Vertices[i].Color != fill) continue;
                var a = body.Vertices[i].Position; var b = body.Vertices[i + 1].Position; var c = body.Vertices[i + 2].Position;
                fillArea += MathF.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) / 2;
            }
            for (var i = 0; i < contour.Length; i++) { var a = contour[i]; var b = contour[(i + 1) % contour.Length]; outlineArea += a.X * b.Y - b.X * a.Y; }
            if (MathF.Abs(fillArea - MathF.Abs(outlineArea / 2)) > .0001f) throw new InvalidDataException("Concave head fill escaped its outline.");
        }
        var crossed = new HeadShape { Offsets = new HeadOffsets().With(0, new(1, 1)) };
        if (crossed.IsSimple()) throw new InvalidDataException("Folded head outline was accepted.");
        foreach (var id in new[] { "person", "quadruped" })
        {
            var document = new StudioDocument(PointLibrary.Load(Path.Combine(root, id, "library.json")));
            foreach (var clip in document.Library.Clips.Values) foreach (var flipped in new[] { false, true })
            {
                var look = document.Appearance with { CustomHead = new HeadShape { FaceAngle = 35, FaceX = .3f }, Flip = flipped, NeckLength = .5f };
                document.Geometry.Build(clip, clip.Duration * .37, look, new(false, false, false));
                var withFace = document.Geometry.Body.Count;
                document.Geometry.Build(clip, clip.Duration * .37, look with { CustomHead = look.CustomHead! with { Face = "none" } }, new(false, false, false));
                if (withFace <= document.Geometry.Body.Count) throw new InvalidDataException("Animated custom head lost its vector face.");
            }
        }
    }
    private static void VerifyEntities(string root)
    {
        var libraries = new Dictionary<string, PointLibrary>();
        foreach (var path in Directory.GetFiles(Path.Combine(root, "entities"), "*.json"))
        {
            var entity = EntityTypeDefinition.Load(path);
            if (!libraries.TryGetValue(entity.Library, out var library)) libraries.Add(entity.Library, library = PointLibrary.Load(Path.Combine(root, entity.Library, "library.json")));
            entity.Validate(library); var document = new StudioDocument(library); document.SetEntity(entity, path);
            var original = System.Text.Json.JsonSerializer.Serialize(entity, EntityTypeDefinition.JsonOptions);
            entity.Actions["attack"].Damage++; entity.Regions["head"].Padding = .2f; document.RecordEdit(false); document.Undo();
            if (System.Text.Json.JsonSerializer.Serialize(document.Entity, EntityTypeDefinition.JsonOptions) != original) throw new InvalidDataException("Entity action/collision undo failed.");
            document.Redo();
            if (document.Entity!.Regions["head"].Padding != .2f) throw new InvalidDataException("Entity action/collision redo failed.");
            var file = Path.Combine(Path.GetTempPath(), "entity-type-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                document.Save(file); var restored = EntityTypeDefinition.Load(file); restored.Validate(library);
                if (System.Text.Json.JsonSerializer.Serialize(restored, EntityTypeDefinition.JsonOptions) != System.Text.Json.JsonSerializer.Serialize(document.Entity, EntityTypeDefinition.JsonOptions)) throw new InvalidDataException("Entity roundtrip failed.");
            }
            finally { File.Delete(file); }
            foreach (var action in entity.Actions.Values) foreach (var t in new[] { 0d, action.Duration * .4, action.Duration * .7, action.Duration }) foreach (var flip in new[] { false, true })
            {
                document.EntityPose!.Evaluate(entity, action, t, flip);
                var pose = document.EntityPose;
                foreach (var p in pose.Hurt.SelectMany(r => r.Points)) if (!float.IsFinite(p.X) || !float.IsFinite(p.Y)) throw new InvalidDataException("Nonfinite collision geometry.");
                document.Geometry.Build(library.Clips[action.Clip], pose.ClipTime, pose.Look, new(false, false, false), true);
            }
        }
    }
}
