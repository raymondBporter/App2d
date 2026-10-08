namespace App2d.Core.Characters.Authored;

/// <summary>Painted torso materials, a solid hide wrap, and socket-mounted hair. No new rig or gameplay geometry.</summary>
public static class PersonWardrobe
{
    public const string HeadSocket = "head-art", BodySocket = "body-art";
    public const string ShortHair = "hair-short", ShortHairBack = "hair-short-back";

    public static IEnumerable<ModelSocket> Sockets() =>
    [
        // The current head/face drawing stays in screen axes even when the torso leans.
        new() { Id = HeadSocket, Control = "head", Frame = CharacterModel.Locomotion },
        new() { Id = BodySocket, Control = "hips", Frame = "hips", Toward = "chest" },
    ];

    public static IEnumerable<EquipmentBinding> Caveman(string build) =>
    [
        new() { Prop = build + "-hide-wrap", Socket = BodySocket },
        new() { Prop = build + "-hair", Socket = HeadSocket },
        new() { Prop = build + "-beard", Socket = HeadSocket },
    ];

    public static IEnumerable<PropAsset> Props()
    {
        var shortHair = Hair(ShortHair, "Short tousled hair", 1, "#654334", false);
        shortHair.BackView = ShortHairBack;
        yield return shortHair;
        var back = Art(ShortHairBack, "Short tousled hair / back");
        Patch(back, "#654334", -.15f,
            [(-.27f, .10f), (-.30f, .21f), (-.23f, .20f), (-.29f, .28f), (-.15f, .26f), (-.15f, .35f), (-.06f, .30f), (.035f, .37f), (.045f, .30f), (.19f, .33f), (.15f, .27f), (.28f, .26f), (.23f, .22f), (.29f, .16f), (.28f, -.08f), (.22f, -.19f), (.11f, -.25f), (-.12f, -.24f), (-.23f, -.17f), (-.28f, -.06f)]);
        yield return back;
        foreach (var (id, width, torso, head, hide, hair) in new[]
        {
            ("brute", 1.45f, 1.05f, .95f, "#d7a348", "#69402a"),
            ("cinder", 1.05f, .95f, 1.05f, "#b97549", "#843f27"),
        })
        {
            var person = PersonTemplate.Model();
            var build = new PersonBuild { Width = width };
            var layers = PersonWardrobeDepths.From(ResolvedModel.From(person, build.Apply(person, "wardrobe-fit", "Wardrobe fit")));
            var wrap = Art(id + "-hide-wrap", id == "brute" ? "Ochre ragged hide wrap" : "Russet ragged hide wrap");
            Patch(wrap, hide, layers.WrapCenter,
                [(-.18f, .08f), (.18f, .08f), (.205f, -.075f), (.125f, -.05f), (.075f, -.115f), (.01f, -.065f), (-.055f, -.11f), (-.115f, -.05f), (-.20f, -.075f)], width, torso, layers.WrapThickness);
            Patch(wrap, "#705039", layers.DetailCenter, [(-.185f, .085f), (.185f, .085f), (.19f, .025f), (-.19f, .025f)], width, torso, layers.DetailThickness);
            Spot(wrap, -.105f * width, -.015f * torso, .019f, "#765033", layers.DetailCenter, layers.DetailThickness);
            Spot(wrap, .09f * width, -.02f * torso, .022f, "#765033", layers.DetailCenter, layers.DetailThickness);
            yield return wrap;
            yield return Hair(id + "-hair", id + " / wild hair", head, hair, true);
            yield return Beard(id + "-beard", id + " / caveman beard", head, hair, id == "brute" ? 1.15f : .92f);
        }
    }

    private static PropAsset Art(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Ink = "#222b32",
        LineWidth = .045f,
        Tip = default,
        Usage = id.EndsWith("-hide-wrap", StringComparison.Ordinal) ? "clothing" : "hair",
        Attachment = id.EndsWith("-hide-wrap", StringComparison.Ordinal) ? BodySocket : HeadSocket,
    };

    private static void Patch(PropAsset art, string fill, float z, (float X, float Y)[] points, float sx = 1, float sy = 1, float thickness = .008f)
        => art.Solids.Add(PropGeometry.Extrude(points.Select(p => new PuppetPoint(p.X * sx, p.Y * sy, z)), thickness, fill));

    private static List<PartPaint> TunicPaint(string skin)
    {
        PartPaint SpotPaint(float x, float y, float rx, float ry) => new()
        {
            Material = new() { Fill = "#765033" },
            Points = [.. Enumerable.Range(0, 7).Select(i => new PuppetPoint(x + MathF.Cos(i * MathF.Tau / 7) * rx, y + MathF.Sin(i * MathF.Tau / 7) * ry))],
        };
        return
        [
            // Exposed shoulder/neckline, clipped to the same rounded torso contour as the fabric.
            new() { Material = new() { Fill = skin }, Points = [new(-.35f,.65f), new(.8f,.65f), new(.8f,-.04f)] },
            SpotPaint(-.25f, .20f, .085f, .055f),
            SpotPaint(.18f, -.07f, .10f, .065f),
            SpotPaint(-.23f, -.30f, .07f, .045f),
        ];
    }

    private static void Spot(PropAsset art, float x, float y, float radius, string fill, float z, float thickness)
    {
        Patch(art, fill, z, [.. Enumerable.Range(0, 7).Select(i =>
        {
            var angle = i * MathF.Tau / 7; return (x + MathF.Cos(angle) * radius * .8f, y + MathF.Sin(angle) * radius);
        })], thickness: thickness);
        art.Solids[^1].Material = art.Solids[^1].RenderMaterial with { Outline = null };
    }

    private static PropAsset Hair(string id, string name, float scale, string color, bool wild)
    {
        var art = Art(id, name);
        // Behind the head: a short nape or the caveman's wider side tufts.
        if (wild)
            Patch(art, color, -.095f, [(-.29f, .20f), (-.37f, .06f), (-.31f, .06f), (-.39f, -.09f), (-.31f, -.06f), (-.32f, -.23f), (-.21f, -.17f), (.23f, -.18f), (.34f, -.23f), (.31f, -.05f), (.38f, -.10f), (.32f, .13f), (.23f, .27f)], scale, scale);
        else
            Patch(art, color, -.095f, [(-.25f, .18f), (-.28f, .03f), (-.25f, -.14f), (-.19f, -.17f), (-.19f, .19f)], scale, scale);
        // Front silhouette: unequal chunky tufts, with a high fringe to leave the brows clear.
        Patch(art, color, -.15f,
            [(-.27f, .10f), (-.30f, .24f), (-.23f, .23f), (-.29f, .32f), (-.15f, .30f), (-.15f, .40f), (-.06f, .34f), (.035f, .43f), (.045f, .34f), (.19f, .38f), (.15f, .31f), (.28f, .30f), (.23f, .25f), (.29f, .19f), (.17f, .18f), (.07f, .24f), (-.015f, .19f), (-.11f, .22f), (-.18f, .12f), (-.22f, .025f)], scale, scale * (wild ? 1.13f : .87f));
        return art;
    }

    private static PropAsset Beard(string id, string name, float head, string color, float length)
    {
        var art = Art(id, name);
        // The concave top leaves the animated mouth visible; the bottom is an irregular fan of locks.
        Patch(art, color, -.16f,
            [(-.255f, .015f), (-.18f, -.055f), (-.105f, -.10f), (-.08f, -.18f), (.20f, -.18f), (.22f, -.08f), (.27f, -.045f), (.32f, -.13f), (.28f, -.17f), (.34f, -.29f), (.24f, -.25f), (.24f, -.39f), (.15f, -.35f), (.08f, -.49f), (.015f, -.40f), (-.075f, -.46f), (-.105f, -.36f), (-.215f, -.38f), (-.20f, -.27f), (-.30f, -.30f), (-.26f, -.19f), (-.315f, -.18f)], head, head * length);
        return art;
    }

    /// <summary>Seeds missing wardrobe art and updates its bindings. Existing art belongs to the editor unless replacement is explicit.</summary>
    public static void Write(string root, bool replaceExistingProps = false)
    {
        var modelPath = Path.Combine(root, "models", "person.json");
        var model = CharacterModel.Load(modelPath);
        foreach (var socket in Sockets()) { model.Sockets.RemoveAll(s => s.Id == socket.Id); model.Sockets.Add(socket); }
        model.Save(modelPath);
        foreach (var prop in Props())
        {
            var path = Path.Combine(root, "props", prop.Id + ".json");
            if (replaceExistingProps || !File.Exists(path)) prop.Save(path);
        }
        foreach (var id in new[] { "hero", "maul-brute", "cinder-gunner" })
        {
            var path = Path.Combine(root, "entities", id + ".json");
            var entity = EntityAsset.FromJson(File.ReadAllText(path));
            // The old floating shirt is now the body's material.
            entity.Equipment.RemoveAll(e => e.Prop is "brute-tunic" or "cinder-tunic");
            var clothes = id == "hero" ? [new EquipmentBinding { Prop = ShortHair, Socket = HeadSocket }] : Caveman(id == "maul-brute" ? "brute" : "cinder").ToArray();
            foreach (var item in clothes) { entity.Equipment.RemoveAll(e => e.Prop == item.Prop); entity.Equipment.Add(item); }
            entity.Save(path);
        }
        foreach (var id in new[] { "brute", "cinder" })
        {
            var path = Path.Combine(root, "variants", id + ".json");
            var variant = ModelVariant.FromJson(File.ReadAllText(path));
            variant.Parts["body"] = variant.Parts["body"] with
            {
                Material = new() { Fill = id == "brute" ? variant.Parts["head"].Material?.Fill : "#b97549" },
                Paint = id == "brute" ? [] : TunicPaint(variant.Parts["head"].Material!.Fill!),
            };
            variant.Save(path);
        }
    }
}
