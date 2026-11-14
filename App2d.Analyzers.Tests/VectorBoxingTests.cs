using App2d.Core.Mathematics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using Xunit;

namespace App2d.Analyzers.Tests;

public sealed class VectorBoxingTests
{
    private static readonly ImmutableArray<MetadataReference> References =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Vector2f).Assembly.Location).Distinct()
            .Select(path => MetadataReference.CreateFromFile(path))];

    [Theory]
    [InlineData("object Box(Vector2f value) => value;", 1)]
    [InlineData("object Box(Vector3d value) => (object)value;", 1)]
    [InlineData("IEquatable<Vector2f> Box(Vector2f value) => value;", 1)]
    [InlineData("object Box(Vector2i value) => value;", 1)]
    [InlineData("object Box(Vector2 value) => value;", 1)]
    [InlineData("object Box(Vector3 value) => value;", 1)]
    [InlineData("object Box(Vector4 value) => value;", 1)]
    [InlineData("object Box(Vector<float> value) => value;", 1)]
    [InlineData("object? Box(Vector2f? value) => value;", 1)]
    [InlineData("bool Equal(Vector2f a, Vector2f b) => object.Equals(a, b);", 2)]
    [InlineData("object[] Pack(Vector2f value) => Args(value); object[] Args(params object[] values) => values;", 1)]
    [InlineData("object Box<T, R>(T value) where T : unmanaged, IVector<T, R> where R : unmanaged, IFloatingPointIeee754<R> => value;", 1)]
    public async Task ReportsBoxingConversions(string member, int expectedCount)
    {
        var diagnostics = await AnalyzeAsync(member);

        Assert.Equal(expectedCount, diagnostics.Length);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal(VectorBoxingAnalyzer.DiagnosticId, diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        });
    }

    [Theory]
    [InlineData("Vector2f Create() => new(1, 2);")]
    [InlineData("Vector2 Convert(Vector2f value) => value.ToVector2();")]
    [InlineData("object Allocate() => new object();")]
    [InlineData("object Box(int value) => value;")]
    [InlineData("struct Vector2 { } object Box(Vector2 value) => value;")]
    [InlineData("object? BoxEmpty() => default(Vector2f?);")]
    [InlineData("object? BoxEmpty() => new Nullable<Vector2f>();")]
    [InlineData("object? BoxEmpty() => (Vector2f?)null;")]
    [InlineData("string Format(Vector2f value) => $\"{value}\";")]
    [InlineData("R Dot<T, R>(T value) where T : unmanaged, IVector<T, R> where R : unmanaged, IFloatingPointIeee754<R> => T.Dot(value, value);")]
    public async Task IgnoresOtherOperations(string member) => Assert.Empty(await AnalyzeAsync(member));

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string member)
    {
        var source = "using System; using System.Numerics; using App2d.Core.Mathematics; " +
            "using Vector2f = App2d.Core.Mathematics.Vec2<float>; class Example { " + member + " }";
        var compilation = CSharpCompilation.Create("Example", [CSharpSyntaxTree.ParseText(source)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return await compilation.WithAnalyzers([new VectorBoxingAnalyzer()]).GetAnalyzerDiagnosticsAsync();
    }
}
