using App2d.Analyzers.CodeFixes;
using App2d.Core.Mathematics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using Xunit;

namespace App2d.Analyzers.Tests;

public sealed class VectorRuleTests
{
    private const string Prefix = "using System; using System.Numerics; using App2d.Core.Mathematics; " +
        "using App2d.Core.Mathematics; using Vector2d = App2d.Core.Mathematics.Vec2<double>; class Example { ";
    private const string Suffix = " }";

    private static readonly ImmutableArray<MetadataReference> References =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Vector2f).Assembly.Location).Distinct()
            .Select(path => MetadataReference.CreateFromFile(path))];

    // APP2D003

    [Theory]
    [InlineData("float First(IVector<Vector2f, float> value) => value[0];")]
    [InlineData("IVector<Vector2d, double>? Find() => null;")]
    [InlineData("IVector<Vector2d, double>? field;")]
    [InlineData("IVector<Vector2f, float>[] Items { get; } = [];")]
    [InlineData("void M() { IVector<Vector2f, float>? value = null; _ = value; }")]
    [InlineData("void M() { static void Local(IVector<Vector2f, float> value) { } }")]
    public async Task ReportsInterfaceTypedVectors(string member)
    {
        var diagnostic = Assert.Single(await AnalyzeAsync(member, new InterfaceTypedVectorAnalyzer()));
        Assert.Equal(InterfaceTypedVectorAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Theory]
    [InlineData("R Dot<T, R>(T value) where T : unmanaged, IVector<T, R> where R : unmanaged, IFloatingPointIeee754<R> => T.Dot(value, value);")]
    [InlineData("float Dot(Vector2f value) => Vector2f.Dot(value, value);")]
    [InlineData("Vector2f[] items = [];")]
    public async Task IgnoresConcreteAndConstrainedVectors(string member) =>
        Assert.Empty(await AnalyzeAsync(member, new InterfaceTypedVectorAnalyzer()));

    // APP2D004

    [Theory]
    [InlineData("Vector2 Copy(Vector2 v) => new Vector2(v.X, v.Y);", "Vector2 Copy(Vector2 v) => v;")]
    [InlineData("Vector3 Copy(Vector3 v) => new(v.X, v.Y, v.Z);", "Vector3 Copy(Vector3 v) => v;")]
    [InlineData("Vector2 Copy(Vector2 v) => new Vector2(y: v.Y, x: v.X);", "Vector2 Copy(Vector2 v) => v;")]
    [InlineData("Vector2f Wrap(Vector2 v) => new Vector2f(v.X, v.Y);", "Vector2f Wrap(Vector2 v) => new Vector2f(v);")]
    [InlineData("Vector2f Wrap(Vector2 v) => new(v.X, v.Y);", "Vector2f Wrap(Vector2 v) => new(v);")]
    [InlineData("Vector4f Wrap(Vector4 v) => new Vector4f(v.X, v.Y, v.Z, v.W);", "Vector4f Wrap(Vector4 v) => new Vector4f(v);")]
    [InlineData("Vector2d Widen(Vector2 v) => new Vector2d(v.X, v.Y);", "Vector2d Widen(Vector2 v) => new Vector2d(v);")]
    [InlineData("struct Body { public Vector2 Position; } Vector2 Read(Body body) => new Vector2(body.Position.X, body.Position.Y);",
        "struct Body { public Vector2 Position; } Vector2 Read(Body body) => body.Position;")]
    public Task FixesRebuiltVectors(string member, string expected) =>
        AssertFixAsync(member, expected, new VectorRebuildAnalyzer(), VectorRebuildAnalyzer.DiagnosticId);

    [Theory]
    [InlineData("Vector2 Swap(Vector2 v) => new Vector2(v.Y, v.X);")]
    [InlineData("Vector2 Mix(Vector2 a, Vector2 b) => new Vector2(a.X, b.Y);")]
    [InlineData("Vector2 Drop(Vector3 v) => new Vector2(v.X, v.Y);")]
    [InlineData("Vector3f Lift(Vector2 v) => new Vector3f(v.X, v.Y);")]
    [InlineData("Vector2 Narrow(Vector2d v) => new Vector2((float)v.X, (float)v.Y);")]
    [InlineData("Vector2 Call(Func<Vector2> next) => new Vector2(next().X, next().Y);")]
    [InlineData("Vector2 Set(Vector2 v) => new Vector2(v.X, v.Y) { X = 1 };")]
    public async Task IgnoresOtherVectorConstructions(string member) =>
        Assert.Empty(await AnalyzeAsync(member, new VectorRebuildAnalyzer()));

    // APP2D005

    [Theory]
    [InlineData("Vector2 Add(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);", "Vector2 Add(Vector2 a, Vector2 b) => a + b;")]
    [InlineData("Vector2 Sub(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);", "Vector2 Sub(Vector2 a, Vector2 b) => a - b;")]
    [InlineData("Vector3 Mul(Vector3 a, Vector3 b) => new Vector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);", "Vector3 Mul(Vector3 a, Vector3 b) => a * b;")]
    [InlineData("Vector2 Scale(Vector2 a, float s) => new Vector2(a.X * s, a.Y * s);", "Vector2 Scale(Vector2 a, float s) => a * s;")]
    [InlineData("Vector2 Scale(Vector2 a, float s) => new Vector2(s * a.X, s * a.Y);", "Vector2 Scale(Vector2 a, float s) => s * a;")]
    [InlineData("Vector2 Half(Vector2 a) => new Vector2(a.X / 2f, a.Y / 2f);", "Vector2 Half(Vector2 a) => a / 2f;")]
    [InlineData("Vector2 Third(Vector2 a) => new Vector2(a.X * (1f / 3f), a.Y * (1f / 3f));", "Vector2 Third(Vector2 a) => a * (1f / 3f);")]
    [InlineData("Vector2d Add(Vector2d a, Vector2d b) => new Vector2d(a.X + b.X, a.Y + b.Y);", "Vector2d Add(Vector2d a, Vector2d b) => a + b;")]
    [InlineData("Vector2f Mul(Vector2f a, Vector2f b) => new Vector2f(a.X * b.X, a.Y * b.Y);", "Vector2f Mul(Vector2f a, Vector2f b) => a * b;")]
    [InlineData("Vector2f Div(Vector2f a, Vector2f b) => new Vector2f(a.X / b.X, a.Y / b.Y);", "Vector2f Div(Vector2f a, Vector2f b) => a / b;")]
    [InlineData("Vector2f Scale(Vector2f a, float s) => new Vector2f(a.X * s, a.Y * s);", "Vector2f Scale(Vector2f a, float s) => a * s;")]
    [InlineData("float Gap(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y).Length();", "float Gap(Vector2 a, Vector2 b) => (a - b).Length();")]
    [InlineData("struct Body { public Vector2 Position; } Vector2 Move(Body body, Vector2 d) => new Vector2(body.Position.X + d.X, body.Position.Y + d.Y);",
        "struct Body { public Vector2 Position; } Vector2 Move(Body body, Vector2 d) => body.Position + d;")]
    public Task FixesComponentwiseArithmetic(string member, string expected) =>
        AssertFixAsync(member, expected, new ComponentwiseVectorMathAnalyzer(), ComponentwiseVectorMathAnalyzer.DiagnosticId);

    [Theory]
    [InlineData("Vector2 Mixed(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y - b.Y);")]
    [InlineData("Vector2 Cross(Vector2 a, Vector2 b) => new Vector2(a.X * b.Y, a.Y * b.X);")]
    [InlineData("Vector2 Swap(Vector2 a, Vector2 b) => new Vector2(a.Y + b.Y, a.X + b.X);")]
    [InlineData("Vector2 Shift(Vector2 a, float s) => new Vector2(a.X + s, a.Y + s);")] // No Vector2 + float operator.
    [InlineData("Vector2 Narrow(Vector2 a, Vector2d b) => new Vector2(a.X + (float)b.X, a.Y + (float)b.Y);")]
    [InlineData("Vector2 Call(Vector2 a, Func<Vector2> next) => new Vector2(a.X + next().X, a.Y + next().Y);")]
    [InlineData("Vector2i Add(Vector2i a, Vector2i b) => new Vector2i(a.X + b.X, a.Y + b.Y);")] // Integer operators are checked.
    public async Task IgnoresOtherArithmetic(string member) =>
        Assert.Empty(await AnalyzeAsync(member, new ComponentwiseVectorMathAnalyzer()));

    private static async Task AssertFixAsync(string member, string expected, DiagnosticAnalyzer analyzer, string id)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, Prefix + member + Suffix);
        var diagnostic = Assert.Single(await AnalyzeAsync(document, analyzer));
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

        var actions = new List<CodeAction>();
        await new VectorExpressionCodeFixProvider().RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        var operations = await Assert.Single(actions).GetOperationsAsync(CancellationToken.None);
        var fixedDocument = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution.GetDocument(document.Id)!;

        Assert.Equal(Prefix + expected + Suffix, (await fixedDocument.GetTextAsync()).ToString());
        Assert.Empty(await AnalyzeAsync(fixedDocument, analyzer));
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string member, DiagnosticAnalyzer analyzer)
    {
        using var workspace = new AdhocWorkspace();
        return await AnalyzeAsync(CreateDocument(workspace, Prefix + member + Suffix), analyzer);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Document document, DiagnosticAnalyzer analyzer)
    {
        var compilation = (await document.Project.GetCompilationAsync())!;
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        return diagnostics;
    }

    private static Document CreateDocument(AdhocWorkspace workspace, string source) =>
        workspace.AddProject("Example", LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Preview))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable))
            .WithMetadataReferences(References)
            .AddDocument("Example.cs", source);
}
