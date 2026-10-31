using App2d.Core.Mathematics;
using App2d.CharacterStudio.Authoring;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using static App2d.Core.Characters.Authored.PersonLoadout;
using static App2d.Core.Characters.Humanoid;

namespace App2d.CharacterStudio.CharacterSeeds;

/// <summary>Humanoid hand/foot art, painted torso materials and socket-mounted wardrobe. Uses the existing rig.</summary>
public static class PersonWardrobe
{
    public static IEnumerable<ModelSocket> Sockets() =>
    [
        // The current head/face drawing stays in screen axes even when the torso leans.
        new() { Id = HeadSocket, Control = Head, Frame = CharacterModel.Locomotion },
        new() { Id = BodySocket, Control = Hips, Frame = Hips, Toward = Chest },
        new() { Id = LeftHandSocket, Control = LeftHand, Toward = LeftElbow },
        new() { Id = RightHandSocket, Control = RightHand, Toward = RightElbow },
        new() { Id = LeftFootSocket, Control = LeftFoot, Toward = LeftKnee },
        new() { Id = RightFootSocket, Control = RightFoot, Toward = RightKnee },
    ];

    /// <summary>Visible extremities on the existing controls. Adding artwork does not change animation structure.</summary>
    public static void AddBodyParts(CharacterModel model)
    {
        var head = model.Parts.FindIndex(p => p.Id == Head);
        var headMaterial = model.Parts[head].RenderMaterial;
        if (model.Materials.TryAdd("skin", new() { Fill = headMaterial.Resolve(model.Materials).Fill ?? "#fff8e7", Outline = new() }) && headMaterial.Asset is null)
        {
            model.Parts[head] = model.Parts[head] with
            {
                Material = model.Parts[head].RenderMaterial with { Asset = "skin", Fill = null }
            };
        }
        foreach (var (control, toward, hand) in new[]
        {
            (LeftHand, LeftElbow, true), (RightHand, RightElbow, true),
            (LeftFoot, LeftKnee, false), (RightFoot, RightKnee, false)
        })
        {
            if (model.Parts.Any(p => p.Id == control)) continue;
            model.Parts.Add(new()
            {
                Id = control,
                A = control,
                B = toward,
                Layer = -.004f,
                Geometry = new Ellipse2D
                {
                    Center = hand ? new(0, -.014f) : new(.044f, 0),
                    Radii = hand ? new(.043f, .055f) : new(.088f, .024f)
                }.Validated(),
                Material = new() { Asset = "skin", Outline = new() { Width = .012f } }
            });
        }
    }

    public static IEnumerable<EquipmentBinding> Caveman(string build) =>
    [
        new() { Prop = build + "-hide-wrap", Socket = BodySocket },
        new() { Prop = build + "-hair", Socket = HeadSocket },
        new() { Prop = build + "-beard", Socket = HeadSocket },
        .. Footwear(build == "brute" ? "boot" : "sandal"),
    ];

    public static IEnumerable<EquipmentBinding> Footwear(string style) =>
    [
        new() { Prop = style + "-left", Socket = LeftFootSocket },
        new() { Prop = style + "-right", Socket = RightFootSocket },
    ];

    public static IEnumerable<Part> Props()
    {
        foreach (var prop in FootwearProps()) yield return prop;
        var shortHair = Hair(ShortHair, "Short tousled hair", 1, "#654334", false);
        shortHair.BackView = ShortHairBack;
        yield return shortHair;
        var back = Art(ShortHairBack, "Short tousled hair / back");
        Patch(back, "#654334", -.15f,
        [
            (-.27f, .10f),
            (-.30f, .21f),
            (-.23f, .20f),
            (-.29f, .28f),
            (-.15f, .26f),
            (-.15f, .35f),
            (-.06f, .30f),
            (.035f, .37f),
            (.045f, .30f),
            (.19f, .33f),
            (.15f, .27f),
            (.28f, .26f),
            (.23f, .22f),
            (.29f, .16f),
            (.28f, -.08f),
            (.22f, -.19f),
            (.11f, -.25f),
            (-.12f, -.24f),
            (-.23f, -.17f),
            (-.28f, -.06f)
        ]);
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
            [
                (-.18f, .08f),
                (.18f, .08f),
                (.205f, -.075f),
                (.125f, -.05f),
                (.075f, -.115f),
                (.01f, -.065f),
                (-.055f, -.11f),
                (-.115f, -.05f),
                (-.20f, -.075f)
            ], width, torso, layers.WrapThickness);
            Patch(wrap, "#705039", layers.DetailCenter, [(-.185f, .085f), (.185f, .085f), (.19f, .025f), (-.19f, .025f)], width, torso, layers.DetailThickness);
            Spot(wrap, -.105f * width, -.015f * torso, .019f, "#765033", layers.DetailCenter, layers.DetailThickness);
            Spot(wrap, .09f * width, -.02f * torso, .022f, "#765033", layers.DetailCenter, layers.DetailThickness);
            yield return wrap;
            yield return Hair(id + "-hair", id + " / wild hair", head, hair, true);
            yield return Beard(id + "-beard", id + " / caveman beard", head, hair, id == "brute" ? 1.15f : .92f);
        }
    }

    private static IEnumerable<Part> FootwearProps()
    {
        foreach (var side in new[] { "left", "right" })
        {
            var socket = side == "left" ? LeftFootSocket : RightFootSocket;
            Part Shoe(string style, string name) => new()
            {
                Id = style + "-" + side,
                Name = name + " / " + side,
                Usage = "clothing",
                Attachment = socket,

                LineWidth = .045f
            };
            var sandal = Shoe("sandal", "Leather sandal");
            Patch(sandal, "#68432e", 0,
                [(-.053f, -.023f), (.135f, -.023f), (.15f, -.012f), (.146f, .004f), (-.05f, .004f)], thickness: .06f);
            // The bare foot remains visible between the two leather straps.
            Patch(sandal, "#bc8853", -.038f,
                [(-.037f, .003f), (-.015f, .003f), (.009f, .072f), (-.014f, .078f)], thickness: .009f);
            Patch(sandal, "#bc8853", -.038f,
                [(.054f, .003f), (.079f, .003f), (.06f, .038f), (.037f, .033f)], thickness: .009f);
            foreach (var solid in sandal.Geometry)
                solid.Material = solid.RenderMaterial with { Outline = new() { Width = .008f } };
            yield return sandal;

            var boot = Shoe("boot", "Short leather boot");
            Patch(boot, "#754c38", 0,
                [(-.058f, -.023f), (.145f, -.023f), (.153f, -.006f), (.145f, .027f),
                 (.11f, .045f), (.047f, .055f), (.043f, .165f), (-.05f, .165f)], thickness: .08f);
            Patch(boot, "#3d302b", -.048f,
                [(-.06f, -.024f), (.147f, -.024f), (.153f, -.009f), (-.06f, -.009f)], thickness: .008f);
            Patch(boot, "#b98a59", -.048f,
                [(-.052f, .135f), (.045f, .135f), (.045f, .163f), (-.052f, .163f)], thickness: .008f);
            foreach (var solid in boot.Geometry)
                solid.Material = solid.RenderMaterial with { Outline = new() { Width = .01f } };
            yield return boot;
        }
    }

    private static Part Art(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Ink = "#222b32",
        LineWidth = .045f,

        Usage = id.EndsWith("-hide-wrap", StringComparison.Ordinal) ? "clothing" : "hair",
        Attachment = id.EndsWith("-hide-wrap", StringComparison.Ordinal) ? BodySocket : HeadSocket,
    };

    private static void Patch(Part art, string fill, float z, (float X, float Y)[] points, float sx = 1, float sy = 1, float thickness = .008f)
    {
        var entry = PartGeometryBuilder.Extrude(points.Select(p => new Vector3f(p.X * sx, p.Y * sy, z)), thickness, fill);
        entry.Id = $"mesh-{art.Geometry.Count + 1}";
        art.Geometry.Add(entry);
    }

    private static void Spot(Part art, float x, float y, float radius, string fill, float z, float thickness)
    {
        Patch(art, fill, z, [.. Enumerable.Range(0, 7).Select(i =>
        {
            var angle = i * MathF.Tau / 7;
            return (x + MathF.Cos(angle) * radius * .8f, y + MathF.Sin(angle) * radius);
        })], thickness: thickness);
        art.Geometry[^1].Material = art.Geometry[^1].RenderMaterial with
        {
            Outline = null
        };
    }

    private static Part Hair(string id, string name, float scale, string color, bool wild)
    {
        var art = Art(id, name);
        // Behind the head: a short nape or the caveman's wider side tufts.
        if (wild)
        {
            Patch(art, color, -.095f, [(-.29f, .20f), (-.37f, .06f), (-.31f, .06f), (-.39f, -.09f), (-.31f, -.06f), (-.32f, -.23f), (-.21f, -.17f), (.23f, -.18f), (.34f, -.23f), (.31f, -.05f), (.38f, -.10f), (.32f, .13f), (.23f, .27f)], scale, scale);
        }
        else
        {
            Patch(art, color, -.095f, [(-.25f, .18f), (-.28f, .03f), (-.25f, -.14f), (-.19f, -.17f), (-.19f, .19f)], scale, scale);
        }
        // Front silhouette: unequal chunky tufts, with a high fringe to leave the brows clear.
        Patch(art, color, -.15f,
        [
            (-.27f, .10f),
            (-.30f, .24f),
            (-.23f, .23f),
            (-.29f, .32f),
            (-.15f, .30f),
            (-.15f, .40f),
            (-.06f, .34f),
            (.035f, .43f),
            (.045f, .34f),
            (.19f, .38f),
            (.15f, .31f),
            (.28f, .30f),
            (.23f, .25f),
            (.29f, .19f),
            (.17f, .18f),
            (.07f, .24f),
            (-.015f, .19f),
            (-.11f, .22f),
            (-.18f, .12f),
            (-.22f, .025f)
        ], scale, scale * (wild ? 1.13f : .87f));
        return art;
    }

    private static Part Beard(string id, string name, float head, string color, float length)
    {
        var art = Art(id, name);
        // The concave top leaves the animated mouth visible; the bottom is an irregular fan of locks.
        Patch(art, color, -.16f,
        [
            (-.255f, .015f),
            (-.18f, -.055f),
            (-.105f, -.10f),
            (-.08f, -.18f),
            (.20f, -.18f),
            (.22f, -.08f),
            (.27f, -.045f),
            (.32f, -.13f),
            (.28f, -.17f),
            (.34f, -.29f),
            (.24f, -.25f),
            (.24f, -.39f),
            (.15f, -.35f),
            (.08f, -.49f),
            (.015f, -.40f),
            (-.075f, -.46f),
            (-.105f, -.36f),
            (-.215f, -.38f),
            (-.20f, -.27f),
            (-.30f, -.30f),
            (-.26f, -.19f),
            (-.315f, -.18f)
        ], head, head * length);
        return art;
    }

    /// <summary>Seeds missing wardrobe art and updates its bindings. Existing art belongs to the editor unless replacement is explicit.</summary>
    public static void Write(string root, bool replaceExistingProps = false, SeedReport? report = null)
    {
        report ??= new(replaceExistingProps);
        var modelPath = Path.Combine(root, "models", "person.json");
        var model = CharacterModel.Load(modelPath);
        AddBodyParts(model);
        foreach (var socket in Sockets())
        {
            model.Sockets.RemoveAll(s => s.Id == socket.Id);
            model.Sockets.Add(socket);
        }
        report.Write(modelPath, model.ToJson());
        foreach (var prop in Props())
        {
            var path = Path.Combine(root, "parts", prop.Id + ".json");
            if (replaceExistingProps || !File.Exists(path))
            {
                report.Write(path, prop.ToJson());
            }
        }
        foreach (var id in new[] { "hero", "maul-brute", "cinder-gunner" })
        {
            var path = Path.Combine(root, "parts", id + ".json");
            var entity = Part.FromJson(File.ReadAllText(path));
            // The old floating shirt is now the body's material.
            entity.Equipment.RemoveAll(e => e.Prop is "brute-tunic" or "cinder-tunic");
            var clothes = id == "hero" ? [new EquipmentBinding { Prop = ShortHair, Socket = HeadSocket }] : Caveman(id == "maul-brute" ? "brute" : "cinder").ToArray();
            foreach (var item in clothes)
            {
                entity.Equipment.RemoveAll(e => e.Prop == item.Prop);
                entity.Equipment.Add(item);
            }
            report.Write(path, entity.ToJson());
        }
    }
}
