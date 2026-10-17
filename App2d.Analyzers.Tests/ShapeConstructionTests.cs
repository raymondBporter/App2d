using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Geometry.Curves;
using App2d.Analyzers.CodeFixes;
using App2d.Core.Shapes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using Xunit;

namespace App2d.Analyzers.Tests;

public sealed class ShapeConstructionTests
{
    private const string Imports = "using System.Numerics; using App2d.Core.Shapes; using App2d.Core.Geometry.Curves; ";

    private static readonly ImmutableArray<MetadataReference> References =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Circle2D).Assembly.Location).Distinct()
            .Select(path => MetadataReference.CreateFromFile(path))];

    public static TheoryData<string> Constructors =>
    [
        "new Circle2D(2)",
        "new Ellipse2D(new Vector2(2, 1))",
        "new Capsule2D(Vector2.Zero, Vector2.One, 2)",
        "new Rectangle2D(Vector2.Zero, Vector2.One)",
        "new AxisAlignedRectangle2D(Vector2.Zero, Vector2.One)",
        "new RoundedRectangle2D(Vector2.Zero, new Vector2(2), 0.5f)",
        "new Triangle2D(Vector2.Zero, Vector2.UnitX, Vector2.UnitY)",
        "new ConvexPolygon2D([Vector2.Zero, Vector2.UnitX, Vector2.UnitY])",
        "new SimplePolygon2D([Vector2.Zero, Vector2.UnitX, Vector2.UnitY])",
        "new HalfSpace2D(Vector2.UnitY, 2)",
        "new CompositeShape2D([Circle2D.Builder.Create(2)])",
        "new LineSegmentCurve2D(Vector2.Zero, Vector2.One)",
        "new PolylineCurve2D([Vector2.Zero, Vector2.One])",
        "new Arc2D(Vector2.Zero, 2, 0, 1)",
        "new QuadraticBezier2D(Vector2.Zero, Vector2.UnitY, Vector2.One)",
        "new CubicBezier2D(Vector2.Zero, Vector2.UnitY, Vector2.UnitX, Vector2.One)",
        "new BSpline2D([Vector2.Zero, Vector2.UnitY, Vector2.UnitX, Vector2.One])"
    ];

    [Theory]
    [MemberData(nameof(Constructors))]
    public async Task EveryBuiltInConstructorHasACompilingBuilderFix(string expression)
    {
        var source = Imports + "class Example { object Make() => " + expression + "; }";
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, source);
        var diagnostics = await AnalyzeAsync(document);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ShapeConstructionAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(expression, source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));

        var actions = await GetFixesAsync(document, diagnostic);
        var fixedDocument = await ApplyAsync(Assert.Single(actions), document.Id);
        Assert.Contains(".Builder.Create(", (await fixedDocument.GetTextAsync()).ToString());
        Assert.Empty(await AnalyzeAsync(fixedDocument));
    }

    [Theory]
    [InlineData("Circle2D Make() => new(2);", "Circle2D Make() => Circle2D.Builder.Create(2);")]
    [InlineData("Circle2D Make() => new Circle2D(center: Next(), radius: Radius());", "Circle2D Make() => Circle2D.Builder.Create(center: Next(), radius: Radius());")]
    [InlineData("Circle2D Make() => new Circle2D(/* radius */ 2, new(1, 2));", "Circle2D Make() => Circle2D.Builder.Create(/* radius */ 2, new(1, 2));")]
    [InlineData("Circle2D Make() => new global::App2d.Core.Shapes.Circle2D(2);", "Circle2D Make() => Circle2D.Builder.Create(2);")]
    [InlineData("Circle2D Make() => new Disk(2);", "Circle2D Make() => Circle2D.Builder.Create(2);")]
    [InlineData("HalfSpace2D Make() => new HalfSpace2D(offset: 2, outwardNormal: Vector2.UnitY);", "HalfSpace2D Make() => HalfSpace2D.Builder.Create(offset: 2, outwardNormal: Vector2.UnitY);")]
    [InlineData("Arc2D Make() => new(Vector2.Zero, 2, 0, 1);", "Arc2D Make() => Arc2D.Builder.Create(Vector2.Zero, 2, 0, 1);")]
    [InlineData("BSpline2D Make() => new BSpline2D(degree: 1, controlPoints: new[] { Next(), Vector2.One });", "BSpline2D Make() => BSpline2D.Builder.Create(degree: 1, controlPoints: new[] { Next(), Vector2.One });")]
    public async Task FixPreservesArgumentsAndSupportsTargetTypingAndAliases(string member, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, Imports + "using Disk = App2d.Core.Shapes.Circle2D; class Example { " + member +
            " Vector2 Next() => Vector2.Zero; float Radius() => 2; }");
        var diagnostic = Assert.Single(await AnalyzeAsync(document));
        var actions = await GetFixesAsync(document, diagnostic);
        var fixedDocument = await ApplyAsync(Assert.Single(actions), document.Id);
        var root = (await fixedDocument.GetSyntaxRootAsync())!;
        var actual = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "Make")
            .NormalizeWhitespace().ToFullString().Replace("Disk", "Circle2D");
        Assert.Equal(SyntaxFactory.ParseMemberDeclaration(expected)!.NormalizeWhitespace().ToFullString(), actual);
        Assert.Empty(await AnalyzeAsync(fixedDocument));
    }

    [Theory]
    [InlineData("new Circle2D()")]
    public async Task ReportsButDoesNotOfferAnInvalidFactoryReplacement(string expression)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, Imports + "class Example { object Make() => " + expression + "; }");
        var diagnostic = Assert.Single(await AnalyzeAsync(document));
        Assert.Empty(await GetFixesAsync(document, diagnostic));
    }

    [Theory]
    [InlineData("new Circle2D { Center = Vector2.Zero, Radius = 2 }")]
    [InlineData("new Circle2D(2) { }")]
    public async Task InitializerFixValidatesTheGeometryWithoutIntroducingBuilderState(string expression)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, Imports + "class Example { object Make() => " + expression + "; }");
        var diagnostic = Assert.Single(await AnalyzeAsync(document));
        var fixedDocument = await ApplyAsync(Assert.Single(await GetFixesAsync(document, diagnostic)), document.Id);
        Assert.Contains("Validated(", (await fixedDocument.GetTextAsync()).ToString());
        Assert.Empty(await AnalyzeAsync(fixedDocument));
    }

    [Fact]
    public async Task ExplicitValidationOfAValueIsAValidConstructionBoundary()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, Imports + "using App2d.Core.Geometry; class Example { object Make() => new Circle2D(2).Validated(); }");
        Assert.Empty(await AnalyzeAsync(document));
    }

    [Theory]
    [InlineData("", "new global::App2d.Core.Shapes.Circle2D(31f)")]
    [InlineData("using App2d.Core.Shapes;", "new Circle2D(31f)")]
    [InlineData("using App2d.Core.Shapes;", "new global::App2d.Core.Shapes.Circle2D(31f)")]
    public async Task FixUsesShortNameAndAddsExactlyOneNamespaceImport(string imports, string expression)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, imports + "\nclass Example { object Make() => " + expression + "; }");
        var diagnostic = Assert.Single(await AnalyzeAsync(document));
        var fixedDocument = await ApplyAsync(Assert.Single(await GetFixesAsync(document, diagnostic)), document.Id);
        var root = (CompilationUnitSyntax)(await fixedDocument.GetSyntaxRootAsync())!;
        var call = Assert.Single(root.DescendantNodes().OfType<InvocationExpressionSyntax>());
        Assert.Equal("Circle2D.Builder.Create(31f)", call.ToString());
        Assert.Single(root.Usings, directive => directive.Name?.ToString() == "App2d.Core.Shapes");
        Assert.Empty(await AnalyzeAsync(fixedDocument));
    }

    [Fact]
    public async Task FixUsesExistingGlobalImportWithoutAddingADuplicate()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, "class Example { object Make() => new global::App2d.Core.Shapes.Circle2D(31f); }");
        document = document.Project.AddDocument("GlobalUsings.cs", "global using App2d.Core.Shapes;").Project.GetDocument(document.Id)!;
        var diagnostic = Assert.Single(await AnalyzeAsync(document));
        var fixedDocument = await ApplyAsync(Assert.Single(await GetFixesAsync(document, diagnostic)), document.Id);
        var root = (CompilationUnitSyntax)(await fixedDocument.GetSyntaxRootAsync())!;
        Assert.Empty(root.Usings);
        Assert.Equal("Circle2D.Builder.Create(31f)", Assert.Single(root.DescendantNodes().OfType<InvocationExpressionSyntax>()).ToString());
        Assert.Empty(await AnalyzeAsync(fixedDocument));
    }

    [Fact]
    public async Task FixAddsImportWithAFileScopedNamespaceAndTargetTypedNew()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, """
            // Keep the file header.
            namespace Examples;
            class Example
            {
                global::App2d.Core.Shapes.Circle2D Make() => new(31f);
            }
            """);
        var diagnostic = Assert.Single(await AnalyzeAsync(document));
        var fixedDocument = await ApplyAsync(Assert.Single(await GetFixesAsync(document, diagnostic)), document.Id);
        var root = (CompilationUnitSyntax)(await fixedDocument.GetSyntaxRootAsync())!;
        Assert.Contains("// Keep the file header.", root.ToFullString());
        Assert.Single(root.Usings, directive => directive.Name?.ToString() == "App2d.Core.Shapes");
        Assert.Single(root.Members.OfType<FileScopedNamespaceDeclarationSyntax>());
        Assert.Equal("Circle2D.Builder.Create(31f)", Assert.Single(root.DescendantNodes().OfType<InvocationExpressionSyntax>()).ToString());
        Assert.Empty(await AnalyzeAsync(fixedDocument));
    }

    [Fact]
    public async Task FixKeepsGlobalQualificationWhenShorterNamesWouldBindToOtherTypes()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, """
            class Example
            {
                class App2d { }
                class Circle2D { }
                object Make() => new global::App2d.Core.Shapes.Circle2D(31f);
            }
            """);
        var diagnostic = Assert.Single(await AnalyzeAsync(document));
        var fixedDocument = await ApplyAsync(Assert.Single(await GetFixesAsync(document, diagnostic)), document.Id);
        var root = (await fixedDocument.GetSyntaxRootAsync())!;
        Assert.Equal("global::App2d.Core.Shapes.Circle2D.Builder.Create(31f)",
            Assert.Single(root.DescendantNodes().OfType<InvocationExpressionSyntax>()).ToString());
        Assert.Empty(await AnalyzeAsync(fixedDocument));
    }

    [Fact]
    public async Task ImportDoesNotMakeExistingTypeNamesAmbiguous()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, """
            using Other;
            class Example
            {
                Circle2D Existing;
                object Make() => new global::App2d.Core.Shapes.Circle2D(31f);
            }
            namespace Other { public class Circle2D { } }
            """);
        var diagnostic = Assert.Single(await AnalyzeAsync(document));
        var fixedDocument = await ApplyAsync(Assert.Single(await GetFixesAsync(document, diagnostic)), document.Id);
        Assert.Empty(await AnalyzeAsync(fixedDocument));
        var root = (CompilationUnitSyntax)(await fixedDocument.GetSyntaxRootAsync())!;
        Assert.DoesNotContain(root.Usings, directive => directive.Name?.ToString() == "App2d.Core.Shapes");
        var model = (await fixedDocument.GetSemanticModelAsync())!;
        var existing = root.DescendantNodes().OfType<FieldDeclarationSyntax>().Single().Declaration.Type;
        Assert.Equal("Other.Circle2D", model.GetTypeInfo(existing).Type!.ToDisplayString());
    }

    [Fact]
    public async Task IgnoresBuildersAndUnrelatedTypes()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, Imports + """
            class Example
            {
                object Make() => Circle2D.Builder.Create(2);
                object Data() => Circle2D.Builder.Create(2, Vector2.Zero);
                object CurveData() => Arc2D.Builder.Create(Vector2.Zero, 2, 0, 1);
                object Other() => new Other.Circle2D();
            }
            namespace Other { public struct Circle2D { } }
            """);
        Assert.Empty(await AnalyzeAsync(document));
    }

    [Fact]
    public async Task OnlyTheOwningShapeImplementationCanUseItsConstructor()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, """
            namespace App2d.Core.Shapes
            {
                public readonly record struct Circle2D(float Radius)
                {
                    public static class Builder
                    {
                        public static Circle2D Create(float radius) => new(radius);
                    }
                }
            }
            class Builder { object Make() => new App2d.Core.Shapes.Circle2D(2); }
            """);
        Assert.Single(await AnalyzeAsync(document));
    }

    [Fact]
    public async Task IgnoresGeneratedCodeAndHonorsPragmaSuppression()
    {
        using var workspace = new AdhocWorkspace();
        var generated = CreateDocument(workspace, "// <auto-generated/>\n" + Imports + "class Example { object Make() => new Circle2D(2); }");
        Assert.Empty(await AnalyzeAsync(generated));
        var suppressed = CreateDocument(workspace, Imports + "\n#pragma warning disable APP2D001\nclass Example { object Make() => new Circle2D(2); }");
        Assert.Empty(await AnalyzeAsync(suppressed));
    }

    [Fact]
    public async Task FixAllReplacesExplicitAndTargetTypedConstruction()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, Imports + "class Example { Circle2D A = new(2); Circle2D B = new Circle2D(3); }");
        var diagnostics = await AnalyzeAsync(document);
        Assert.Equal(2, diagnostics.Length);
        var provider = new UseShapeBuilderCodeFixProvider();
        var context = new FixAllContext(document, provider, FixAllScope.Document,
            ShapeConstructionAnalyzer.DiagnosticId, provider.FixableDiagnosticIds,
            new DocumentDiagnostics(diagnostics), CancellationToken.None);
        var action = await provider.GetFixAllProvider().GetFixAsync(context);
        var fixedDocument = await ApplyAsync(Assert.IsType<CodeAction>(action, false), document.Id);
        Assert.Empty(await AnalyzeAsync(fixedDocument));
        Assert.Equal(2, (await fixedDocument.GetTextAsync()).ToString().Split(".Builder.Create(").Length - 1);
    }

    [Fact]
    public async Task FixAllAddsOneImportForMultipleReplacements()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, """
            class Example
            {
                object A = new global::App2d.Core.Shapes.Circle2D(2);
                object B = new global::App2d.Core.Shapes.Circle2D(3);
            }
            """);
        var diagnostics = await AnalyzeAsync(document);
        Assert.Equal(2, diagnostics.Length);
        var provider = new UseShapeBuilderCodeFixProvider();
        var context = new FixAllContext(document, provider, FixAllScope.Document,
            ShapeConstructionAnalyzer.DiagnosticId, provider.FixableDiagnosticIds,
            new DocumentDiagnostics(diagnostics), CancellationToken.None);
        var action = await provider.GetFixAllProvider().GetFixAsync(context);
        var fixedDocument = await ApplyAsync(Assert.IsType<CodeAction>(action, false), document.Id);
        var root = (CompilationUnitSyntax)(await fixedDocument.GetSyntaxRootAsync())!;
        Assert.Single(root.Usings, directive => directive.Name?.ToString() == "App2d.Core.Shapes");
        Assert.All(root.DescendantNodes().OfType<InvocationExpressionSyntax>(), call => Assert.Equal("Circle2D.Builder.Create", call.Expression.ToString()));
        Assert.Empty(await AnalyzeAsync(fixedDocument));
    }

    private static Document CreateDocument(AdhocWorkspace workspace, string source)
    {
        var project = workspace.AddProject("Example", LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Preview))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(References);
        return project.AddDocument("Example.cs", source);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Document document)
    {
        var compilation = (await document.Project.GetCompilationAsync())!;
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var diagnostics = await compilation.WithAnalyzers([new ShapeConstructionAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        return diagnostics;
    }

    private static async Task<List<CodeAction>> GetFixesAsync(Document document, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        await new UseShapeBuilderCodeFixProvider().RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        return actions;
    }

    private static async Task<Document> ApplyAsync(CodeAction action, DocumentId documentId)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        return Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution.GetDocument(documentId)!;
    }

    private sealed class DocumentDiagnostics(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
    {
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());
        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
