using App2d.Core.Json;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Mathematics.Serialization;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Mathematics;

public sealed class InterpolationJsonTests
{
    private sealed record Reading(float Amount, string Label);

    private static Interpolator<TValue, TReal> RoundTrip<TValue, TReal>(Interpolator<TValue, TReal> rule)
        where TReal : IFloatingPointIeee754<TReal> => JsonSerializer.Deserialize<Interpolator<TValue, TReal>>(
            JsonSerializer.Serialize(rule, JsonProfiles.Mathematics), JsonProfiles.Mathematics);

    [Fact]
    public void CoreConvertersWorkWithoutCharacterSerializerOptions()
    {
        var rule = Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth)
            .With(Vector2f.Channels.X, Easingf.Power(2));
        var restored = JsonSerializer.Deserialize<Interpolator<Vector2f, float>>(JsonSerializer.Serialize(rule));
        Assert.Equal(rule.Evaluate(Vector2f.Zero, new(8f, 16f), .37f), restored.Evaluate(Vector2f.Zero, new(8f, 16f), .37f));
    }

    [Fact]
    public void LoadedChannelRecipesDoNotRebuildOrAllocateDuringSampling()
    {
        var rule = JsonSerializer.Deserialize<Interpolator<Vector3f, float>>("""
            {"kind":"default","operations":[{"kind":"progress","map":"smooth"},
              {"kind":"channel","channel":"x","interpolator":{"kind":"linear","progress":{"kind":"power","degree":2}}}]}
            """, JsonProfiles.Mathematics);
        var end = new Vector3f(8f, 16f, 32f);
        for (var index = 0; index < 64; index++) rule.Evaluate(Vector3f.Zero, end, .5f);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0f;
        for (var index = 0; index < 1000; index++) sum += rule.Evaluate(Vector3f.Zero, end, .5f).X;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(2000f, sum);
        Assert.Equal(0L, allocated);
    }

    [Fact]
    public void EveryFloatPresetAndDirectionUsesTheSameRecipeAsDouble()
    {
        foreach (var curve in Enum.GetValues<EasingCurve>())
        {
            foreach (var direction in Enum.GetValues<EasingDirection>())
            {
                var map = Easingf.FromCurve(curve, direction);
                var restored = JsonSerializer.Deserialize<ProgressMapf>(JsonSerializer.Serialize(map, JsonProfiles.Mathematics), JsonProfiles.Mathematics)!;
                foreach (var time in new[] { 0f, .37f, .5f, .63f, 1f })
                {
                    Assert.Equal(map.Sample(time), restored.Sample(time));
                    Assert.Equal(map.Derivative(time), restored.Derivative(time));
                }
            }
        }
    }

    [Fact]
    public void EveryCoreValueDefaultAndHoldCanRoundTrip()
    {
        Check(Interpolation.Default<Vector2f, float>(), new(1f, 2f), new(8f, -4f), .37f);
        Check(Interpolation.Default<Vector3f, float>(), new(1f, 2f, 3f), new(8f, -4f, 9f), .37f);
        Check(Interpolation.Default<Vector4f, float>(), new(1f, 2f, 3f, 4f), new(8f, -4f, 9f, 3f), .37f);
        Check(Interpolation.Default<Vector2d, double>(), new(1d, 2d), new(8d, -4d), .37d);
        Check(Interpolation.Default<Vector3d, double>(), new(1d, 2d, 3d), new(8d, -4d, 9d), .37d);
        Check(Interpolation.Default<Vector4d, double>(), new(1d, 2d, 3d, 4d), new(8d, -4d, 9d, 3d), .37d);
        Check(Interpolation.Default<RigidTransform2D, float>(), new(new(1f, 2f), 0f), new(new(8f, -4f), 3f * MathF.Tau), .37f);
        Check(Interpolation.Default<Polar<float>, float>(), new(1f, 2f), new(8f, -4f), .37f);
        Check(Interpolation.Default<Polar<double>, double>(), new(1d, 2d), new(8d, -4d), .37d);
        Check(Interpolation.Linear<float>(), 1f, 8f, .37f);
        Check(Interpolation.Linear<double>(), 1d, 1.0000000001d, .37d);
        Check(Interpolation.Hold<string, float>(), "start", "end", .37f);
        Check(Interpolation.ShortestAngle<double>(), 7d * Math.PI / 4d, Math.PI / 4d, .37d);

        static void Check<TValue, TReal>(Interpolator<TValue, TReal> rule, TValue start, TValue end, TReal time)
            where TReal : IFloatingPointIeee754<TReal> => Assert.Equal(rule.Evaluate(start, end, time), RoundTrip(rule).Evaluate(start, end, time));
    }

    [Fact]
    public void OrderedChannelAndProgressCompositionRetainsItsOrderAndOriginalEndpoints()
    {
        var basis = Interpolation.Default<Vector3d, double>().WithProgress(Easingd.Smooth);
        var rule = basis.With(Vector3d.Channels.X, Easingd.Power(2))
            .With(Vector3d.Channels.V, Interpolation.Default<Vector3d, double>().WithProgress(Easingd.Sine))
            .With(Vector3d.Channels.Y, Easingd.Power(3)).WithProgress(ProgressMapd.Reverse);
        var restored = RoundTrip(rule);
        for (var index = -10; index <= 110; index++)
        {
            var time = index / 100d;
            Assert.Equal(rule.Evaluate(new(1d, 2d, 3d), new(10d, -4d, 8d), time), restored.Evaluate(new(1d, 2d, 3d), new(10d, -4d, 8d), time));
        }
        var json = JsonSerializer.Serialize(rule, JsonProfiles.Mathematics);
        Assert.Contains("operations", json, StringComparison.Ordinal);
        Assert.Equal(json, JsonSerializer.Serialize(restored, JsonProfiles.Mathematics));
    }

    [Fact]
    public void RigidTranslationRgbAndAlphaSupportNestedStrategies()
    {
        var rigid = Interpolation.Default<RigidTransform2D, float>()
            .With(RigidTransform2D.Channels.Translation, Interpolation.Polar<Vector2f, float>(radius: Interpolation.Linear<float>().WithProgress(Easingf.Smooth)))
            .With(RigidTransform2D.Channels.Rotation, Easingf.Power(2));
        var a = new RigidTransform2D(new(1f, 0f), 0f);
        var b = new RigidTransform2D(new(0f, 2f), 2f * MathF.Tau);
        Assert.Equal(rigid.Evaluate(a, b, .37f), RoundTrip(rigid).Evaluate(a, b, .37f));

        var color = Interpolation.Hold<Vector4d, double>()
            .With(Vector4d.Channels.RGB, Interpolation.Default<Vector3d, double>().With(Vector3d.Channels.X, Easingd.Smooth))
            .With(Vector4d.Channels.A, Interpolation.Bezier(1d / 3d, 2d, 2d / 3d, 2d, absolute: true));
        var from = new Vector4d(1d, 2d, 3d, .5d);
        var to = new Vector4d(1.0000000001d, 8d, -4d, .5d);
        Assert.Equal(color.Evaluate(from, to, .37d), RoundTrip(color).Evaluate(from, to, .37d));
        var alias = JsonSerializer.Deserialize<Interpolator<Vector4f, float>>("""
            {"kind":"hold","operations":[{"kind":"channel","channel":"r","interpolator":{"kind":"linear","progress":"smooth"}}]}
            """, JsonProfiles.Mathematics);
        Assert.Equal(new Vector4f(.5f, 0f, 0f, 0f), alias.Evaluate(Vector4f.Zero, Vector4f.One, .5f));
    }

    [Fact]
    public void BezierAndPolarParametersPreserveNativePrecisionAndExcursions()
    {
        var absolute = Interpolation.Bezier(1d / 3d, 1.0000000001d, 2d / 3d, 1.0000000001d, absolute: true);
        Assert.Equal(1.000000000075d, RoundTrip(absolute).Evaluate(1d, 1d, .5d), 15);
        var relative = Interpolation.Bezier(0f, 1f / 3f, 0f, 2f / 3f);
        Assert.Equal(5f, RoundTrip(relative).Evaluate(0f, 10f, .125f), 5);
        var polar = Interpolation.Polar<Vector2d, double>(angle: Interpolation.Linear<double>().WithProgress(Easingd.Smooth));
        Assert.Equal(polar.Evaluate(Vector2d.UnitX, new(0d, 3d), .37d), RoundTrip(polar).Evaluate(Vector2d.UnitX, new(0d, 3d), .37d));
    }

    [Fact]
    public void ProgressFactoriesRoundTripValuesDerivativesInversesAndInputSpans()
    {
        ProgressMapd[] maps = [ProgressMapd.Forward, ProgressMapd.Reverse, ProgressMapd.PingPong, ProgressMapd.ReversePingPong,
            Easingd.Smooth, Easingd.Smoother, Easingd.Smoothest, Easingd.Step, Easingd.Sine, Easingd.Power(7),
            ProgressMapd.Constant(7d), ProgressMapd.Constant(7d).FromRange(4d, 4d), ProgressMapd.Affine(-2d, 3d),
            ProgressMapd.Normalize(6d, 2d), Easingd.Sine.WithDirection(EasingDirection.InOut).FromRange(6d, 2d).ToRange(10d, -4d),
            ProgressMapd.PingPong.Then(Easingd.Smooth), Easingd.Smooth.ToRange(2d, 2d),
            Easingd.Bezier(10d, 20d, 20d, 10d), Easingd.TimedBezier(.1d, -.3d, .8d, 1.3d)];
        foreach (var map in maps)
        {
            var json = JsonSerializer.Serialize(map, JsonProfiles.Mathematics);
            var restored = JsonSerializer.Deserialize<ProgressMapd>(json, JsonProfiles.Mathematics)!;
            Assert.Equal(json, JsonSerializer.Serialize(restored, JsonProfiles.Mathematics));
            Assert.Equal(map.InputBegin, restored.InputBegin);
            Assert.Equal(map.InputEnd, restored.InputEnd);
            Assert.Equal(map.IsContinuous, restored.IsContinuous);
            Assert.Equal(map.Inverse is null, restored.Inverse is null);
            foreach (var time in new[] { -.25d, 0d, .37d, .5d, 1d, 1.25d, 3d })
            {
                Assert.Equal(map.Sample(time), restored.Sample(time));
                Assert.Equal(map.Derivative(time), restored.Derivative(time));
            }
            if (map.Inverse is { } inverse && restored.Inverse is { } restoredInverse)
                Assert.Equal(inverse(map.Sample(.37d)), restoredInverse(map.Sample(.37d)));
        }
    }

    [Fact]
    public void ChannelAndCoordinateCompositionKeepClampedInputSpansAfterLoading()
    {
        var rule = Interpolation.Default<Vector2d, double>().WithProgress(Easingd.Smooth.FromRange(6d, 2d))
            .With(Vector2d.Channels.X, Easingd.Linear.FromRange(6d, 2d));
        var restored = RoundTrip(rule);
        Assert.Equal(6d, restored.InputBegin);
        Assert.Equal(2d, restored.InputEnd);
        Assert.Equal(rule.EvaluateClamped(Vector2d.Zero, new(4d, 8d), 20d), restored.EvaluateClamped(Vector2d.Zero, new(4d, 8d), 20d));
    }

    [Fact]
    public void ApplicationTypesChannelsCoordinateConversionsAndFunctionsCanHaveLocalRegistrations()
    {
        var amount = new InterpolationChannel<Reading, float>(static value => value.Amount, static (value, x) => value with { Amount = x });
        Polar<double> to(Vector2d value) => value.ToPolar();
        Vector2d from(Polar<double> value) => value.ToCartesian();
        var named = new Interpolator<float, float>(static (a, b, time) => a + (b - a) * time * time * time);
        var customProgress = ProgressMapd.FromFunction(static time => time * time * time);
        var factory = new InterpolatorJsonConverterFactory();
        var readingBindings = factory.For<Reading, float>().Default(Interpolation.Hold<Reading, float>()).Channel("amount", amount);
        factory.For<Vector2d, double>().Coordinates("polar", to, from);
        factory.For<float, float>().Named("cubic-code", named);
        var progressFactory = new ProgressMapJsonConverterFactory().Register("cubic-code", customProgress);
        var options = new JsonSerializerOptions(JsonProfiles.Mathematics);
        options.Converters.Insert(0, factory);
        options.Converters.Insert(0, progressFactory);

        var rule = Interpolation.Hold<Reading, float>().With(amount, named);
        var restored = JsonSerializer.Deserialize<Interpolator<Reading, float>>(JsonSerializer.Serialize(rule, options), options);
        Assert.Equal(new Reading(1f, "a"), restored.Evaluate(new(0f, "a"), new(8f, "b"), .5f));
        var coordinates = Interpolation.Default<Polar<double>, double>().InCoordinates(to, from).WithProgress(customProgress);
        var loadedCoordinates = JsonSerializer.Deserialize<Interpolator<Vector2d, double>>(JsonSerializer.Serialize(coordinates, options), options);
        Assert.Equal(coordinates.Evaluate(Vector2d.UnitX, new(0d, 2d), .37d), loadedCoordinates.Evaluate(Vector2d.UnitX, new(0d, 2d), .37d));
        Assert.Throws<InvalidOperationException>(() => factory.For<Reading, float>());
        Assert.Throws<InvalidOperationException>(() => readingBindings.Channel("other", amount));
    }

    [Fact]
    public void ARegisteredWholeRuleCanIncludeItsOwnProgressWithoutApplyingItTwice()
    {
        var rule = new Interpolator<float, float>(static (a, b, t) => a + (b - a) * t * t)
            .WithProgress(Easingf.Smooth);
        var factory = new InterpolatorJsonConverterFactory();
        factory.For<float, float>().Named("curved-code", rule);
        var options = new JsonSerializerOptions(JsonProfiles.Mathematics);
        options.Converters.Insert(0, factory);
        var json = JsonSerializer.Serialize(rule, options);
        var restored = JsonSerializer.Deserialize<Interpolator<float, float>>(json, options);
        Assert.Equal(rule.Evaluate(0f, 8f, .25f), restored.Evaluate(0f, 8f, .25f));
        Assert.Equal(json, JsonSerializer.Serialize(restored, options));
    }

    [Fact]
    public void CustomDefaultBindingsCannotSilentlyChangeTheConstructedRule()
    {
        var factory = new InterpolatorJsonConverterFactory();
        factory.For<Vector2f, float>().Default(Interpolation.Hold<Vector2f, float>());
        var options = new JsonSerializerOptions(JsonProfiles.Mathematics);
        options.Converters.Insert(0, factory);
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(Interpolation.Default<Vector2f, float>(), options));

        var curved = Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth);
        var otherFactory = new InterpolatorJsonConverterFactory();
        otherFactory.For<Vector2f, float>().Default(curved);
        var otherOptions = new JsonSerializerOptions(JsonProfiles.Mathematics);
        otherOptions.Converters.Insert(0, otherFactory);
        var restored = JsonSerializer.Deserialize<Interpolator<Vector2f, float>>(JsonSerializer.Serialize(curved, otherOptions), otherOptions);
        Assert.Equal(curved.Evaluate(Vector2f.Zero, Vector2f.One, .25f), restored.Evaluate(Vector2f.Zero, Vector2f.One, .25f));
    }

    [Theory]
    [InlineData("{\"kind\":\"unknown\"}")]
    [InlineData("{\"kind\":\"default\",\"typo\":1}")]
    [InlineData("{\"kind\":\"default\",\"operations\":[{\"kind\":\"channel\",\"channel\":\"missing\",\"interpolator\":\"hold\"}]}")]
    [InlineData("{\"kind\":\"default\",\"progress\":\"smooth\",\"operations\":[]}")]
    [InlineData("{\"kind\":\"default\",\"progress\":{\"kind\":\"power\",\"degree\":0}}")]
    public void InvalidRecipesFailAtTheJsonBoundary(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Interpolator<Vector3f, float>>(json, JsonProfiles.Mathematics));

    [Fact]
    public void UnnamedFunctionsChannelsAndCoordinateConversionsAreNotSilentlySaved()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new Interpolator<float, float>(static (a, b, t) => a + b + t), JsonProfiles.Mathematics));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(ProgressMapd.FromFunction(static time => time * time), JsonProfiles.Mathematics));
        var channel = new InterpolationChannel<Vector2f, float>(static value => value.X, static (value, x) => value with { X = x });
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(Interpolation.Default<Vector2f, float>().With(channel, Easingf.Smooth), JsonProfiles.Mathematics));
        var coordinates = Interpolation.Default<Polar<double>, double>().InCoordinates(static value => value.ToPolar(), static value => value.ToCartesian());
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(coordinates, JsonProfiles.Mathematics));
    }
}
