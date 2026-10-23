namespace App2d.Beamwright.Optics;

/// <summary>Reference results produced by the upstream JavaScript tracer, checked without running JavaScript.</summary>
public sealed class ReferenceTrace
{
    public int Level { get; init; }
    public bool Solved { get; init; }
    public bool Win { get; init; }
    public double[] Lm { get; init; } = [];
    public double[] Quality { get; init; } = [];
    public bool[] Lit { get; init; } = [];
    public ReferenceProfile?[] Profiles { get; init; } = [];
}

public sealed class ReferenceProfile
{
    public double[] Bins { get; init; } = [];
    public double Accepted { get; init; }
    public double[] SeamPower { get; init; } = [];
}

public static class CampaignVerification
{
    public static IReadOnlyList<string> Check(BeamwrightContent content, ReferenceTrace reference)
    {
        var errors = new List<string>();
        var level = content.Levels[reference.Level];
        var result = new BeamTracer(content).Trace(level, reference.Solved ? level.Solution : []);

        if (result.Win != reference.Win)
            errors.Add($"win: expected {reference.Win}, got {result.Win}");

        if (result.Truncated)
            errors.Add("trace exceeded its segment budget");

        for (var i = 0; i < reference.Lm.Length; i++)
        {
            if (Math.Abs(result.Lumens[i] - reference.Lm[i]) > .05)
                errors.Add($"cell {i}: expected {reference.Lm[i]:F4} lm, got {result.Lumens[i]:F4}");

            if (result.Lit[i] != reference.Lit[i])
                errors.Add($"cell {i}: lit state differs");

            if (Math.Abs(result.Quality[i] - reference.Quality[i]) > .002)
                errors.Add($"cell {i}: quality differs");

            if (reference.Profiles[i] is not { } expected || result.Profiles[i] is not { } actual)
                continue;

            if (Math.Abs(expected.Accepted - actual.Accepted) > .05)
                errors.Add($"cell {i}: accepted power differs");

            for (var bin = 0; bin < expected.Bins.Length; bin++)
            {
                // Core uses float coordinates; upstream uses doubles. Only reference rays exactly on a strip
                // seam may move to the neighboring strip. Total power, quality and completion still must agree.
                var seamPower = expected.SeamPower.Length == expected.Bins.Length + 1
                    ? expected.SeamPower[bin] + expected.SeamPower[bin + 1]
                    : 0;
                if (Math.Abs(expected.Bins[bin] - actual.Bins[bin]) > .05 + seamPower) errors.Add($"cell {i}: sensor bin {bin} differs");
            }
        }
        return errors;
    }

    public static int Run(TextWriter output)
    {
        var content = BeamwrightContent.Default;
        var references = BeamwrightContent.ReadResource<ReferenceTrace[]>("reference-traces.json");
        var failed = 0;
        foreach (var reference in references)
        {
            var errors = Check(content, reference);
            if (errors.Count > 0) failed++;
            output.WriteLine($"{(errors.Count == 0 ? "PASS" : "FAIL")} {reference.Level + 1:00} {content.Levels[reference.Level].Name} ({(reference.Solved ? "solution" : "empty")}): {string.Join("; ", errors)}");
        }
        output.WriteLine($"{references.Length - failed}/{references.Length} upstream reference traces passed.");
        return failed == 0 ? 0 : 1;
    }
}
