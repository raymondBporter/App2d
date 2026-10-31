using App2d.CharacterStudio.Authoring;
using System.Text.RegularExpressions;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Explicit, one-way conversion of a prototype <c>.puppet.json</c> into a base model and one clip per motion. IDs become
/// lowercase asset IDs; chains whose ends ever touch the ground are keyed in the locomotion frame and share one reach measure,
/// the others are keyed from their root. The puppet file is only read.
/// </summary>
public static partial class PuppetImport
{
    [GeneratedRegex("[^a-z0-9]+")] private static partial Regex NotId();

    public static string Slug(string text, string fallback = "asset")
    {
        var slug = NotId().Replace(text.ToLowerInvariant(), "-").Trim('-');
        slug = slug.Length == 0 ? fallback : slug[..Math.Min(slug.Length, 60)];
        return char.IsAsciiLetterOrDigit(slug[0]) ? slug : fallback;
    }

    public sealed record Result(CharacterModel Model, IReadOnlyList<MotionClip> Clips);

    /// <param name="taken">IDs already in use; new model and clip IDs avoid them.</param>
    public static Result Convert(PuppetDefinition puppet, string modelId, string modelName, string sourcePath, IEnumerable<string> taken)
    {
        puppet.Validate();
        var used = new HashSet<string>(taken, StringComparer.Ordinal);
        if (!used.Add(modelId))
        {
            throw new InvalidDataException($"The id '{modelId}' is already used.");
        }

        // Control IDs are model-local, so only clashes inside this model matter.
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var localIds = new HashSet<string>(StringComparer.Ordinal) { CharacterModel.Locomotion, CharacterModel.Unit };
        string Unique(string basis, HashSet<string> set)
        {
            var id = basis;
            for (var n = 2; !set.Add(id); n++)
            {
                id = $"{basis}-{n}";
            }

            return id;
        }
        foreach (var control in puppet.Controls)
        {
            ids[control.Id] = Unique(Slug(control.Id, "control"), localIds);
        }

        var parents = puppet.Bones.ToDictionary(b => ids[b.To], b => ids[b.From], StringComparer.Ordinal);
        var grounded = puppet.Motions.SelectMany(m => m.Contacts).Select(c => ids[c.End]).ToHashSet(StringComparer.Ordinal);
        var chainIds = new HashSet<string>(StringComparer.Ordinal);
        string ChainId(string end) => Unique(end.EndsWith("-hand", StringComparison.Ordinal) ? end[..^5] + "-arm" : end.EndsWith("-foot", StringComparison.Ordinal) ? end[..^5] + "-leg" : end + "-chain", chainIds);

        var chains = puppet.Chains.ConvertAll(c => new ModelChain { Id = ChainId(ids[c.End]), Root = ids[c.Root], Joint = ids[c.Joint], End = ids[c.End], Bend = c.Bend });
        var measures = chains.ConvertAll(c => new ModelMeasure { Id = c.Id, Path = [c.Root, c.Joint, c.End] });
        // Contacts need their chain's scale to equal the travel scale, so every grounded chain shares the first one's reach.
        var reach = chains.FirstOrDefault(c => grounded.Contains(c.End))?.Id;
        foreach (var chain in chains)
        {
            var onGround = grounded.Contains(chain.End);
            chain.Frame = onGround ? CharacterModel.Locomotion : chain.Root;
            chain.Scale = onGround ? reach! : chain.Id;
        }
        var partIds = new HashSet<string>(StringComparer.Ordinal);
        var model = new CharacterModel
        {
            Id = modelId,
            Name = modelName,
            Ink = puppet.Ink,
            LineWidth = puppet.LineWidth,
            Controls = [.. puppet.Controls.Select(c => new ModelControl { Id = ids[c.Id], Parent = parents.GetValueOrDefault(ids[c.Id]), Rest = c.Rest })],
            Constraints = [.. chains],
            Measures = measures,
            Parts = [.. puppet.Parts.Select(p => p with { Id = Unique(Slug(p.Id, "part"), partIds), A = ids[p.A], B = p.B is null ? null : ids[p.B] })],
            Source = new() { Kind = AssetSource.Puppet, File = sourcePath },
        };
        // The prototype's position-only chains become bone constraints (P11c): the run turns into bone nodes, the end keeps the
        // character axes, and a root that frames its own constraint (an arm) stays as the pivot of a new solved bone.
        foreach (var chain in chains)
        {
            RigConversion.ToBoneConstraint(model, chain.Id, []);
        }
        model.Validate();

        var converted = new PuppetDefinition
        {
            Name = puppet.Name,
            Ink = puppet.Ink,
            LineWidth = puppet.LineWidth,
            Motions = [.. puppet.Motions.Select(m => m with
            {
                Keys = [.. m.Keys.Select(k => k with { Points = k.Points.ToDictionary(p => ids[p.Key], p => p.Value, StringComparer.Ordinal) })],
                Contacts = [.. m.Contacts.Select(c => c with { End = ids[c.End] })],
            })],
        };
        var clips = new List<MotionClip>();
        foreach (var motion in converted.Motions)
        {
            var id = Unique(Slug($"{modelId}-{motion.Name}", modelId + "-motion"), used);
            var clip = PuppetMotionConverter.Convert(converted, motion, model, id, motion.Name, reach ?? CharacterModel.Unit);
            clip.Source = new()
            {
                Kind = AssetSource.Puppet,
                File = sourcePath,
                Motion = motion.Name
            };
            clips.Add(clip);
        }
        return new(model, clips);
    }
}
