namespace App2d.Core.Characters.Authored;

/// <summary>
/// The resolution state between the authored rig and the final compile: every node's parent-local setup, length and link
/// (point controls' setups derived from their authored rest), the parts and the attachment instances. Parameter bindings and
/// explicit overrides write here in the order of plan section 3.7; <see cref="RigGraph.Compile(CharacterModel, IReadOnlyList{ModelControl}, RigLayout)"/>
/// then derives positions and frames from the final values.
/// </summary>
internal sealed class RigLayout
{
    private RigLayout(Dictionary<string, RigSetup> setups, Dictionary<string, float> lengths, Dictionary<string, RigLink?> links, Dictionary<string, RigLimit?> limits, List<PartGeometry> parts, List<PartInstance> attachments)
    {
        Setups = setups;
        Lengths = lengths;
        Links = links;
        Limits = limits;
        Parts = parts;
        Attachments = attachments;
    }

    public Dictionary<string, RigSetup> Setups { get; }
    public Dictionary<string, float> Lengths { get; }
    public Dictionary<string, RigLink?> Links { get; }
    /// <summary>Each bone node's joint limits (null where none are declared), written by limit bindings and overrides.</summary>
    public Dictionary<string, RigLimit?> Limits { get; }
    public List<PartGeometry> Parts { get; }
    /// <summary>The attachment instances with bound placement and fit, then the wearer's overrides, written.</summary>
    public List<PartInstance> Attachments { get; }
    /// <summary>Controls whose setup a binding or override changed. An untouched point control keeps its authored rest bit for bit.</summary>
    public HashSet<string> Moved { get; } = new(StringComparer.Ordinal);

    public static RigLayout From(CharacterModel model, IReadOnlyDictionary<string, RigNode> baseNodes) => new(
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Setup with { }, StringComparer.Ordinal),
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Length, StringComparer.Ordinal),
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Link is { } link ? link with { } : null, StringComparer.Ordinal),
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Limit is { } limit ? limit with { } : null, StringComparer.Ordinal),
        model.Parts.ConvertAll(p => p with { }),
        model.Attachments.ConvertAll(a => a with { }));

    public float Get(PropertyRef property)
    {
        if (property.Attachment is { } attachment)
        {
            var a = Attachments[AttachmentIndex(attachment)];
            if (property.Property.StartsWith(RigProperties.Input, StringComparison.Ordinal))
            {
                return a.Inputs.TryGetValue(property.Property[RigProperties.Input.Length..], out var input) ? input : throw Unknown(property);
            }

            return property.Property switch
            {
                RigProperties.X => a.X,
                RigProperties.Y => a.Y,
                RigProperties.Angle => a.Angle,
                RigProperties.Scale => a.Scale,
                RigProperties.Layer => a.Layer,
                RigProperties.FitX => a.FitX,
                RigProperties.FitY => a.FitY,
                _ => throw Unknown(property),
            };
        }

        if (property.Part is { } part)
        {
            var p = Parts[PartIndex(part)];
            return property.Property switch
            {
                RigProperties.ScaleX => p.ScaleX,
                RigProperties.ScaleY => p.ScaleY,
                RigProperties.OffsetX => p.OffsetX,
                RigProperties.OffsetY => p.OffsetY,
                _ => throw Unknown(property),
            };
        }

        var id = property.Control ?? throw Unknown(property);
        return property.Property switch
        {
            RigProperties.Length => Lengths[id],
            RigProperties.LocalX => Setups[id].X,
            RigProperties.LocalY => Setups[id].Y,
            RigProperties.LocalRotation => Setups[id].Rotation,
            RigProperties.LocalLayer => Setups[id].Layer,
            RigProperties.LinkT => Links[id]!.T,
            RigProperties.LinkOffsetX => Links[id]!.OffsetX,
            RigProperties.LinkOffsetY => Links[id]!.OffsetY,
            RigProperties.LimitMin => Limit(id, property).Min,
            RigProperties.LimitMax => Limit(id, property).Max,
            _ => throw Unknown(property),
        };
    }

    /// <summary>The joint limits a limit property writes to; a node without declared limits exposes none.</summary>
    private RigLimit Limit(string id, PropertyRef property) =>
        Limits[id] ?? throw new InvalidDataException($"{property}: '{id}' declares no joint limits; add them on the base model.");

    public void Set(PropertyRef property, float value)
    {
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException($"{property}: the bound value is not finite.");
        }

        // A binding at its identity (a multiplier of 1) leaves the authored value, and the node, untouched.
        if (value == Get(property))
        {
            return;
        }

        if (property.Attachment is { } attachment)
        {
            var index = AttachmentIndex(attachment);
            if (property.Property.StartsWith(RigProperties.Input, StringComparison.Ordinal))
            {
                var inputs = new Dictionary<string, float>(Attachments[index].Inputs, StringComparer.Ordinal);
                var input = property.Property[RigProperties.Input.Length..];
                if (!inputs.ContainsKey(input))
                {
                    throw Unknown(property);
                }

                inputs[input] = value;
                Attachments[index] = Attachments[index] with { Inputs = inputs };
                return;
            }

            Attachments[index] = property.Property switch
            {
                RigProperties.X => Attachments[index] with { X = value },
                RigProperties.Y => Attachments[index] with { Y = value },
                RigProperties.Angle => Attachments[index] with { Angle = value },
                RigProperties.Scale => Attachments[index] with { Scale = value },
                RigProperties.Layer => Attachments[index] with { Layer = value },
                RigProperties.FitX => Attachments[index] with { FitX = value },
                RigProperties.FitY => Attachments[index] with { FitY = value },
                _ => throw Unknown(property),
            };
            Attachments[index].Validate($"{property}");
            return;
        }

        if (property.Part is { } part)
        {
            var index = PartIndex(part);
            Parts[index] = property.Property switch
            {
                RigProperties.ScaleX => Parts[index] with { ScaleX = value },
                RigProperties.ScaleY => Parts[index] with { ScaleY = value },
                RigProperties.OffsetX => Parts[index] with { OffsetX = value },
                RigProperties.OffsetY => Parts[index] with { OffsetY = value },
                _ => throw Unknown(property),
            };
            return;
        }

        var id = property.Control ?? throw Unknown(property);
        switch (property.Property)
        {
            case RigProperties.Length:
                new Limit(0, 100).Check(value, $"{property}");
                Lengths[id] = value;
                break;
            case RigProperties.LocalX: Setups[id].X = value; break;
            case RigProperties.LocalY: Setups[id].Y = value; break;
            case RigProperties.LocalRotation: Setups[id].Rotation = value; break;
            case RigProperties.LocalLayer: Setups[id].Layer = value; break;
            case RigProperties.LinkT: Links[id]!.T = value; break;
            case RigProperties.LinkOffsetX: Links[id]!.OffsetX = value; break;
            case RigProperties.LinkOffsetY: Links[id]!.OffsetY = value; break;
            // A limit bounds the rotation without moving the rest: the node stays where it was.
            case RigProperties.LimitMin: Limit(id, property).Min = value; return;
            case RigProperties.LimitMax: Limit(id, property).Max = value; return;
            default: throw Unknown(property);
        }
        Moved.Add(id);
    }

    /// <summary>
    /// Writes one explicit override after the bindings (plan section 3.7 step 3): a number pins the value (a clear pins the
    /// authored value read from <paramref name="authored"/>, bypassing the binding); a flag, selection or material replaces
    /// the base's, a clear restoring the property's empty value.
    /// </summary>
    public void Apply(PropertyOverride change, PropertyDeclaration declaration, RigLayout authored)
    {
        var target = change.Target;
        if (declaration.Kind == PropertyKinds.Number)
        {
            Set(target, change.Clear ? authored.Get(target) : change.Value!.Value);
            return;
        }

        if (target.Part is { } part)
        {
            var index = PartIndex(part);
            var current = Parts[index];
            Parts[index] = declaration.Name switch
            {
                RigProperties.Material => current with { Material = current.RenderMaterial.WithOverride(change.Material) },
                RigProperties.Hidden => current with { Hidden = !change.Clear && change.Flag!.Value },
                _ => throw Unknown(target),
            };
            return;
        }

        if (target.Attachment is { } attachment)
        {
            var index = AttachmentIndex(attachment);
            var current = Attachments[index];
            Attachments[index] = declaration.Name switch
            {
                RigProperties.Asset => current with { Asset = change.Selection! },
                RigProperties.Mirror => current with { Mirror = !change.Clear && change.Flag!.Value },
                RigProperties.Material => current with { Material = change.Clear ? null : current.Material is null ? change.Material : current.Material.WithOverride(change.Material) },
                RigProperties.Hidden => current with { Hidden = !change.Clear && change.Flag!.Value },
                RigProperties.Expression => current with { Expression = change.Clear ? null : change.Selection },
                _ => throw Unknown(target),
            };
            return;
        }

        throw Unknown(target);
    }

    private int PartIndex(string part)
    {
        var index = Parts.FindIndex(p => p.Id == part);
        return index >= 0 ? index : throw new InvalidDataException($"No part '{part}'.");
    }

    private int AttachmentIndex(string attachment)
    {
        var index = Attachments.FindIndex(a => a.Id == attachment);
        return index >= 0 ? index : throw new InvalidDataException($"No attachment '{attachment}'.");
    }

    private static InvalidDataException Unknown(PropertyRef property) => new($"{property}: no such property.");
}
