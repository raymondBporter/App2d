using App2d.Core.Characters;
using App2d.Core.Characters.Authored;

namespace App2d.CharacterSeeds;

/// <summary>
/// The Person's five proportion parameters as typed values, and the seed that declares them on a Person-structured model.
/// Since P4a the proportions are data: <see cref="Declare"/> writes the parameter declarations and their bindings (bone
/// lengths, parent-local layout, part fitting) into the model, and resolution applies them through the generic catalog. This
/// class is a Seeds convenience for writing those values, not an engine rule: Core has no humanoid proportion code.
/// </summary>
public sealed record PersonBuild
{
    public static readonly Limit Range = new(.5f, 2);
    public float Legs { get; init; } = 1;
    public float Torso { get; init; } = 1;
    public float Arms { get; init; } = 1;
    public float Width { get; init; } = 1;
    public float Head { get; init; } = 1;

    public static PersonBuild TallThin { get; } = new() { Legs = 1.2f, Torso = 1.12f, Arms = 1.15f, Width = .8f, Head = .92f };
    public static PersonBuild ShortBroad { get; } = new() { Legs = .82f, Torso = .9f, Arms = .88f, Width = 1.3f, Head = 1.1f };
    /// <summary>
    /// Deliberately extreme builds at the hard range's ends (P4c): every leg, torso and arm dimension doubled or halved while
    /// the shared clips play. They are proof fixtures for the proportion gate, not shipped presets: `models/person.json`
    /// stays unchanged (plan rule 3) until the Person bundle converts in P11, when these join its preset list.
    /// </summary>
    public static PersonBuild Stilted { get; } = new() { Legs = 2, Torso = .5f, Arms = 2, Width = .6f, Head = .5f };
    public static PersonBuild Squat { get; } = new() { Legs = .5f, Torso = 2, Arms = .5f, Width = 1.6f, Head = 2 };

    public const string LegsId = "legs", TorsoId = "torso", ArmsId = "arms", WidthId = "width", HeadId = "head";
    public static readonly string[] Ids = [LegsId, TorsoId, ArmsId, WidthId, HeadId];

    public Dictionary<string, float> Values() => new(StringComparer.Ordinal) { [LegsId] = Legs, [TorsoId] = Torso, [ArmsId] = Arms, [WidthId] = Width, [HeadId] = Head };
    public static PersonBuild From(IReadOnlyDictionary<string, float> values)
    {
        float Get(string id) => values.TryGetValue(id, out var value) ? value : 1;
        return new()
        {
            Legs = Get(LegsId),
            Torso = Get(TorsoId),
            Arms = Get(ArmsId),
            Width = Get(WidthId),
            Head = Get(HeadId)
        };
    }

    /// <summary>A variant of <paramref name="person"/> holding these parameter values; values equal to the default are not stored.</summary>
    public ModelVariant Apply(CharacterModel person, string id, string name, string? bodyFill = null)
    {
        foreach (var (value, amount) in Values())
        {
            Range.Check(amount, value);
        }

        var missing = Ids.Where(p => person.Parameters.All(d => d.Id != p)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException($"Person build values apply to models declaring the parameters {string.Join(", ", Ids)}; '{person.Id}' lacks {string.Join(", ", missing)}.");
        }

        var variant = new ModelVariant { Id = id, Name = name, Base = person.Id };
        foreach (var (value, amount) in Values())
        {
            if (amount != 1)
            {
                variant.Parameters[value] = amount;
            }
        }

        if (bodyFill is not null)
        {
            variant.Set(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Body, RigProperties.Material), new RenderMaterialDefinition2D { Fill = bodyFill }));
        }

        variant.Validate();
        return variant;
    }

    /// <summary>
    /// Declares the Person proportion parameters and their bindings on a model with the humanoid controls and parts, replacing
    /// the retired code rule with equivalent data. Feet stay on the ground: <c>legs</c> scales the hips' height about the
    /// ground line (the left foot's rest height) and every leg segment; <c>torso</c> scales the hips-to-chest and chest-to-head
    /// offsets, the shoulders' height on the chest and the body part's vertical fit; <c>arms</c> scales every arm segment
    /// (lengths on the bone chain, local offsets on point controls); <c>width</c> spreads the hips and shoulders in X and layer
    /// and widens the body; <c>head</c> scales the head part. Presets: standard, tall-thin, short-broad.
    /// </summary>
    public static void Declare(CharacterModel model)
    {
        var controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        // The ground line is the left foot's authored rest, derived through the leg bones alone (a tip-bound bone node authors no
        // rest of its own, P11a): resolved without any declared parameter, so re-declaring a declared model is a fixed point.
        var pristine = CharacterModel.FromJson(model.ToJson());
        pristine.Parameters = [];
        pristine.Presets = [];
        var ground = ResolvedModel.Rig(pristine).Rest[Humanoid.LeftFoot].Y;
        ParameterBinding Scale(string control, string property, float about = 0) => new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfControl(control, property), About = about };
        ParameterBinding Fit(string part, string property) => new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfPart(part, property) };
        // A segment's size lives on its length when the limb is a bone chain and on its parent-local offset when it is a point run.
        IEnumerable<ParameterBinding> Segment(string control) => controls[control].Node == RigNodeKinds.Bone
            ? [Scale(control, RigProperties.Length)]
            : [Scale(control, RigProperties.LocalX), Scale(control, RigProperties.LocalY), Scale(control, RigProperties.LocalLayer)];
        // A limb on the bone solver scales its two solved bones' lengths (the root bone sits beneath the shoulder pivot of an
        // arm, or is the hip itself); a point run scales each segment's parent-local offset.
        IEnumerable<ParameterBinding> Limb(string start, string joint, string end) => model.IkChains.FirstOrDefault(c => c.End == end) is { Solver: ModelChain.BoneSolver } chain
            ? [Scale(chain.Root, RigProperties.Length), Scale(chain.Joint, RigProperties.Length)]
            : controls[start].Node == RigNodeKinds.Bone
            ? [Scale(start, RigProperties.Length), Scale(joint, RigProperties.Length)]
            : Segment(joint).Concat(Segment(end));
        ModelParameter Parameter(string id, string name, (float Min, float Max) soft, IEnumerable<ParameterBinding> bindings) => new()
        {
            Id = id,
            Name = name,
            Units = ParameterUnits.Factor,
            Default = 1,
            Min = Range.Min,
            Max = Range.Max,
            SoftMin = soft.Min,
            SoftMax = soft.Max,
            Bindings = [.. bindings],
        };
        model.Parameters =
        [
            Parameter(LegsId, "Leg length", (.7f, 1.4f),
            [
                Scale(Humanoid.Hips, RigProperties.LocalY, ground),
                Scale(Humanoid.LeftHip, RigProperties.LocalY), Scale(Humanoid.RightHip, RigProperties.LocalY),
                .. Limb(Humanoid.LeftHip, Humanoid.LeftKnee, Humanoid.LeftFoot), .. Limb(Humanoid.RightHip, Humanoid.RightKnee, Humanoid.RightFoot),
            ]),
            Parameter(TorsoId, "Torso length", (.7f, 1.4f),
            [
                .. Segment(Humanoid.Chest), .. Segment(Humanoid.Head),
                Scale(Humanoid.LeftShoulder, RigProperties.LocalY), Scale(Humanoid.RightShoulder, RigProperties.LocalY),
                Fit(Humanoid.Body, RigProperties.ScaleY), Fit(Humanoid.Body, RigProperties.OffsetY),
            ]),
            Parameter(ArmsId, "Arm length", (.7f, 1.4f),
            [
                .. Limb(Humanoid.LeftShoulder, Humanoid.LeftElbow, Humanoid.LeftHand), .. Limb(Humanoid.RightShoulder, Humanoid.RightElbow, Humanoid.RightHand),
            ]),
            Parameter(WidthId, "Width", (.6f, 1.6f),
            [
                Scale(Humanoid.LeftHip, RigProperties.LocalX), Scale(Humanoid.LeftHip, RigProperties.LocalLayer),
                Scale(Humanoid.RightHip, RigProperties.LocalX), Scale(Humanoid.RightHip, RigProperties.LocalLayer),
                Scale(Humanoid.LeftShoulder, RigProperties.LocalX), Scale(Humanoid.LeftShoulder, RigProperties.LocalLayer),
                Scale(Humanoid.RightShoulder, RigProperties.LocalX), Scale(Humanoid.RightShoulder, RigProperties.LocalLayer),
                Fit(Humanoid.Body, RigProperties.ScaleX),
            ]),
            Parameter(HeadId, "Head size", (.7f, 1.4f), [Fit(Humanoid.Head, RigProperties.ScaleX), Fit(Humanoid.Head, RigProperties.ScaleY)]),
        ];
        model.Presets =
        [
            new() { Id = "standard", Name = "Standard", Values = [] },
            new() { Id = "tall-thin", Name = "Tall / thin", Values = TallThin.Values() },
            new() { Id = "short-broad", Name = "Short / broad", Values = ShortBroad.Values() },
        ];
    }
}
