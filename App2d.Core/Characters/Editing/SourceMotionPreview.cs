using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Core.Characters.Editing;

/// <summary>Maps an imported clip's source points into a preview model's space, caching the sampler and reference fit.</summary>
public sealed class SourceMotionPreview(SourceLibraries sources)
{
    private (MotionClip Clip, AssetSource Source, ResolvedModel Model, LibraryImport.Sampler Sampler, float Ratio, Vector3 Offset)? _cached;

    public IEnumerable<(string Control, Vector3 Point)> Sample(Subject subject, MotionClip clip, float time)
    {
        if (clip.Source is not { Kind: AssetSource.Library, Points: { } points } source) return [];
        if (_cached is not { } cached || cached.Clip != clip || cached.Source != source || cached.Model != subject.Model)
        {
            var library = sources.Get(source.File);
            var reference = new LibraryImport.Sampler(library, source.Rest ?? LibraryImport.DefaultRest(library, source.Motion!), points).At(0);
            var ratio = LibraryImport.Ratios(subject.Model, reference).Overall;
            var root = subject.Model.Order.FirstOrDefault(c => reference.ContainsKey(c.Id))?.Id;
            var offset = root is null ? Vector3.Zero : subject.Model.Rest[root] - reference[root] * ratio;
            _cached = cached = (clip, source, subject.Model, new(library, source.Motion!, points), ratio, offset);
        }
        return cached.Sampler.At(time).Select(p => (p.Key, p.Value * cached.Ratio + cached.Offset + subject.Pose.Locomotion));
    }
}
