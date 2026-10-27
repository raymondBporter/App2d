using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Publishes an anatomy template as resources (character-system migration, P10a): the model with the dimensions its seed
/// declares (parameters, presets, measures, groups) and the version-2 template that publishes slots, dimensions, presets and
/// the default animation library. Transactional on a scratch output tree with a report (plan rules 3, 5, 6): the declared
/// model must resolve to the same rest geometry, parts and attachments as the source (every control within
/// <c>tolerance</c>, every part and instance equal), every clip of the model must still validate and sample identically at
/// five instants, every published preset must resolve, and the template's contract must hold; otherwise nothing is written
/// and the differences are reported. A rerun reports an output already holding the publication as "unchanged" and leaves an
/// output edited afterwards as "kept". The source tree is never written.
/// </summary>
public static class TemplatePublication
{
    public sealed record Result(string TemplateId, string ModelId, string? Output, string Status, IReadOnlyList<string> Report, IReadOnlyList<string> Differences)
    {
        public bool Published => Output is not null && Differences.Count == 0;
    }

    public static Result Publish(string root, ModelTemplate template, Action<CharacterModel>? declare, string output, float tolerance = 1e-6f)
    {
        template.Validate();
        var catalog = AuthoredCatalog.Load(root);
        var source = catalog.Models.GetValueOrDefault(template.Model) ?? throw new InvalidDataException($"Template '{template.Id}': no model '{template.Model}' under '{root}'.");
        var sourceText = File.ReadAllText(catalog.PathOf(template.Model));
        var sourceVersion = FileVersion(sourceText);
        var report = new List<string> { $"template '{template.Id}' publishes model '{template.Model}' (file at version {sourceVersion?.ToString() ?? "?"}, read at {source.Version})" };
        var differences = new List<string>();
        Func<string, Part?> partOf = catalog.Parts.GetValueOrDefault;
        var before = ResolvedModel.From(source, null, partOf);
        var declared = CharacterModel.FromJson(source.ToJson());
        var parameters = declared.Parameters.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var presets = declared.Presets.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var measures = declared.Measures.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var groups = declared.Groups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        declare?.Invoke(declared);
        declared.Validate();
        foreach (var parameter in declared.Parameters.Where(p => !parameters.Contains(p.Id)))
        {
            report.Add($"parameter '{parameter.Id}' declared: {parameter.Units} {parameter.Default:0.###} in {parameter.Min:0.###}..{parameter.Max:0.###}, {parameter.Bindings.Count} binding(s)");
        }
        foreach (var preset in declared.Presets.Where(p => !presets.Contains(p.Id)))
        {
            report.Add($"preset '{preset.Id}' declared: {string.Join(", ", preset.Values.Select(v => $"{v.Key} {v.Value:0.###}"))}");
        }
        foreach (var measure in declared.Measures.Where(m => !measures.Contains(m.Id)))
        {
            report.Add($"measure '{measure.Id}' declared along {string.Join(" > ", measure.Path)}");
        }
        foreach (var group in declared.Groups.Where(g => !groups.Contains(g.Id)))
        {
            report.Add($"group '{group.Id}' declared ({group.Targets.Count} targets)");
        }

        // The declaration changes nothing at rest: the published model is the source with its dimensions named.
        var after = ResolvedModel.From(declared, null, partOf);
        foreach (var control in before.Order)
        {
            if (Vector3.Distance(before.Rest[control.Id], after.Rest[control.Id]) > tolerance)
            {
                differences.Add($"control '{control.Id}' rest moved from {before.Rest[control.Id]} to {after.Rest[control.Id]}");
            }
        }
        for (var i = 0; i < before.Parts.Count; i++)
        {
            if (i >= after.Parts.Count || Json(before.Parts[i]) != Json(after.Parts[i]))
            {
                differences.Add($"part '{before.Parts[i].Id}' differs from the source");
            }
        }
        for (var i = 0; i < before.Attachments.Count; i++)
        {
            if (i >= after.Attachments.Count || Json(before.Attachments[i].Instance) != Json(after.Attachments[i].Instance))
            {
                differences.Add($"attachment '{before.Attachments[i].Id}' differs from the source");
            }
        }
        var clips = catalog.Animations.Values.Where(c => c.Model == template.Model).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        foreach (var clip in clips)
        {
            try
            {
                clip.Validate(after, exactRevision: false);
            }
            catch (InvalidDataException ex)
            {
                differences.Add($"clip '{clip.Id}' no longer validates: {ex.Message}");
                continue;
            }

            foreach (var phase in new[] { 0f, .17f, .5f, .83f, 1f })
            {
                var was = PoseEvaluator.Sample(before, clip, clip.Duration * phase);
                var now = PoseEvaluator.Sample(after, clip, clip.Duration * phase);
                foreach (var (id, point) in was.Points)
                {
                    if (Vector3.Distance(point, now.Points[id]) > tolerance)
                    {
                        differences.Add($"clip '{clip.Id}' at {clip.Duration * phase:0.##} s: '{id}' moved from {point} to {now.Points[id]}");
                        break;
                    }
                }
            }
        }
        report.Add($"{clips.Count} clip(s) validate and sample identically on the published model");
        foreach (var preset in template.Presets)
        {
            if (declared.Presets.FirstOrDefault(p => p.Id == preset) is not { } values)
            {
                continue;
            }

            try
            {
                var variant = new ModelVariant { Id = "preset-" + preset, Name = preset, Base = declared.Id };
                ModelAuthoring.ApplyPreset(declared, variant, preset);
                var built = ResolvedModel.From(declared, variant, partOf);
                report.Add($"preset '{preset}' resolves: {string.Join(", ", built.Measures.Select(m => $"{m.Key} {m.Value:0.###}"))}");
            }
            catch (InvalidDataException ex)
            {
                differences.Add($"preset '{preset}' does not resolve: {ex.Message}");
            }
        }
        foreach (var problem in template.ContractProblems(declared, partOf))
        {
            differences.Add("contract: " + problem);
        }
        var templateJson = template.ToJson();
        if (differences.Count > 0)
        {
            return new(template.Id, template.Model, null, "failed", report, differences);
        }

        Directory.CreateDirectory(Path.Combine(output, "models"));
        Directory.CreateDirectory(Path.Combine(output, "templates"));
        var modelPath = Path.Combine(output, "models", declared.Id + ".json");
        var templatePath = Path.Combine(output, "templates", template.Id + ".json");
        var modelJson = declared.ToJson();
        var status = Write(modelPath, modelJson, "model", report) + ", template " + Write(templatePath, templateJson, "template", report);
        report.Add($"model '{declared.Id}' at version {declared.Version} (source file at {sourceVersion?.ToString() ?? "?"}): {status}");
        File.WriteAllText(Path.Combine(output, $"template-publication-{template.Id}.md"), "# Template publication: " + template.Id + "\n\n" + string.Join("\n", report.Select(r => "- " + r)) + "\n");
        return new(template.Id, template.Model, output, status, report, differences);
    }

    /// <summary>Parts and instances carry geometry, paint and binding lists, so equality is by their serialized form.</summary>
    private static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value, AuthoredJson.Options);

    private static int? FileVersion(string json)
    {
        try
        {
            return System.Text.Json.Nodes.JsonNode.Parse(json) is System.Text.Json.Nodes.JsonObject root && root["version"] is System.Text.Json.Nodes.JsonValue value && value.TryGetValue(out int version) ? version : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string Write(string path, string json, string kind, List<string> report)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path);
            if (existing == json)
            {
                report.Add($"{kind} '{Path.GetFileName(path)}' unchanged");
                return "unchanged";
            }

            report.Add($"{kind} '{Path.GetFileName(path)}' kept (edited after an earlier publication; not overwritten)");
            return "kept";
        }

        File.WriteAllText(path, json);
        report.Add($"{kind} '{Path.GetFileName(path)}' written");
        return "published";
    }
}
