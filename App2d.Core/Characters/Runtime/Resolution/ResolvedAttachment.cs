using App2d.Core.Characters.Geometry;
using App2d.Core.Json;
using App2d.Core.Mathematics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// An attachment instance resolved for one wearer: the merged instance, its asset, the socket it hangs from, and the payload
/// with materials resolved against the asset's shared materials and the instance's override. An animated asset's rig is
/// compiled here too (<see cref="Rig"/>), over the resolved pieces. The placement is derived per pose from the socket's
/// evaluated placement; nothing here is a copy of the asset.
/// </summary>
public sealed record ResolvedAttachment(PartInstance Instance, Part Asset, ModelSocket Socket, IReadOnlyList<PartGeometry> Parts, ResolvedModel? Rig = null)
{
    public string Id => Instance.Id;
    public bool IsAnimated => Rig is not null;
    /// <summary>Whether the instance's fit stretches the artwork in the placement (else it repositions the asset's layout).</summary>
    public bool Stretches => Asset.Fitting == PartFittings.Stretch;
    /// <summary>The fit the asset's layout repositions by: the instance fit under the layout policy, none under stretch.</summary>
    public Vector2 LayoutFit => Stretches ? Vector2.One : new(Instance.FitX, Instance.FitY);

    /// <summary>The instance's placement in the frame its socket placement is given in (actor-local or world).</summary>
    public LayeredFrame2D Placement(LayeredFrame2D socketPlacement) => Instance.Placement(Stretches).Then(socketPlacement);

    /// <summary>A payload location's placement for a given instance placement: what a static payload's pieces attach to.</summary>
    public LayeredFrame2D LocationPlacement(string location, LayeredFrame2D placement)
    {
        var fit = LayoutFit;
        var frame = Asset.LocationFrame(location);
        return fit == Vector2.One ? frame.Then(placement) : frame.WithOrigin(new(frame.Origin.X * fit.X, frame.Origin.Y * fit.Y, frame.Origin.Z)).Then(placement);
    }

    /// <summary>The static payload's drawing inputs for an instance placement: each location's origin and frame, as the shared part drawing expects.</summary>
    public (Func<string, Vector3> World, Func<string, LayeredFrame2D> Frame) Lookups(LayeredFrame2D placement) =>
        (location => LocationPlacement(location, placement).Origin, location => LocationPlacement(location, placement));

    /// <summary>The expression this instance shows before any clip or request: its own, else the asset's default.</summary>
    public string? DefaultExpression => Instance.Expression ?? Asset.Expression;

    /// <summary>Resolves an instance for a wearer: the asset must exist and the socket must be the wearer's.</summary>
    public static ResolvedAttachment Resolve(PartInstance instance, Func<string, Part?> partOf, IReadOnlyDictionary<string, ModelSocket> sockets, string owner)
    {
        var asset = partOf(instance.Asset) ?? throw new InvalidDataException($"{owner} attachment '{instance.Id}': no part asset '{instance.Asset}'.");
        if (!sockets.TryGetValue(instance.Socket, out var socket))
        {
            throw new InvalidDataException($"{owner} attachment '{instance.Id}': the wearer has no socket '{instance.Socket}'.");
        }

        // The slot contract (P10b): a socket that declares a usage takes only parts of that usage; a reference path names the binding.
        if (socket.Usage is { } usage && asset.Usage != usage)
        {
            throw new InvalidDataException($"{owner} attachment '{instance.Id}': socket '{socket.Id}' takes '{usage}', part '{asset.Id}' is a '{asset.Usage}' (path: {owner} > attachments > {instance.Id} > socket {socket.Id} > usage).");
        }

        foreach (var input in instance.Inputs.Keys)
        {
            if (asset.Parameters.All(p => p.Id != input))
            {
                throw new InvalidDataException($"{owner} attachment '{instance.Id}': part '{asset.Id}' declares no input '{input}'{(asset.Parameters.Count == 0 ? "" : $" (one of {string.Join(", ", asset.Parameters.Select(p => p.Id))})")} (path: {owner} > attachments > {instance.Id} > inputs > {input}).");
            }
        }

        var parts = asset.Geometry.ConvertAll(p => p.ResolveMaterial(asset.Materials, instance.Material));
        ResolvedModel? rig = null;
        if (asset.IsAnimated)
        {
            try
            {
                // The instance's inputs are the module rig's parameter values (plan section 3.7 step 2: module selections and parameters before bindings).
                var inputs = instance.Inputs.Count == 0 ? null : new ModelVariant { Id = instance.Id, Name = instance.Id, Base = asset.Id, Parameters = new(instance.Inputs, StringComparer.Ordinal) };
                rig = ResolvedModel.From(asset.RigModel(parts), inputs);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"{owner} attachment '{instance.Id}' (part '{asset.Id}'): {ex.Message}", ex);
            }
        }

        if (instance.Expression is { } expression && !asset.Binds(expression))
        {
            throw new InvalidDataException($"{owner} attachment '{instance.Id}': part '{asset.Id}' binds no expression '{expression}'" + (asset.IsAnimated ? $" (one of {string.Join(", ", asset.Expressions.Keys.Append(Part.None))})." : "; a static part shows no expression."));
        }

        return new(instance, asset, socket, parts, rig);
    }
}
