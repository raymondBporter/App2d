using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace App2d.Analyzers;

/// <summary>Recognizes the vector types and component access patterns shared by the vector rules.</summary>
internal sealed class VectorSymbols
{
    public const string FixKindProperty = "VectorFixKind";
    public const string ExpressionProperty = "VectorExpression";

    /// <summary>Replace the whole object creation with the expression.</summary>
    public const string ReplaceFix = "Replace";

    /// <summary>Keep the object creation and pass the expression as its only argument.</summary>
    public const string ArgumentFix = "Argument";

    public static readonly string[] ComponentNames = ["X", "Y", "Z", "W"];

    private static readonly string[] KnownMetadataNames =
    [
        "System.Numerics.Vector2", "System.Numerics.Vector3", "System.Numerics.Vector4",
        "App2d.Core.Mathematics.Vector2i"
    ];

    private readonly ImmutableHashSet<ISymbol> _known;

    private VectorSymbols(INamedTypeSymbol? vectorInterface, ImmutableHashSet<ISymbol> known)
    {
        Interface = vectorInterface;
        _known = known;
    }

    /// <summary>App2d.Core.Mathematics.IVector`2, when the compilation references it.</summary>
    public INamedTypeSymbol? Interface { get; }

    public static VectorSymbols? Create(Compilation compilation)
    {
        var vectorInterface = compilation.GetTypeByMetadataName("App2d.Core.Mathematics.IVector`2");
        var known = ImmutableHashSet.CreateBuilder<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var name in KnownMetadataNames)
        {
            if (compilation.GetTypeByMetadataName(name) is { } type)
                known.Add(type);
        }

        return vectorInterface is null && known.Count == 0 ? null : new VectorSymbols(vectorInterface, known.ToImmutable());
    }

    /// <summary>A concrete vector struct: a known System.Numerics/App2d vector, or a struct implementing IVector.</summary>
    public bool IsVector(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Struct } named &&
        (_known.Contains(named.OriginalDefinition) || Interface is not null && named.AllInterfaces.Any(IsInterfaceDefinition));

    /// <summary>IVector itself, or an interface deriving from it.</summary>
    public bool IsVectorInterface(ITypeSymbol type) =>
        Interface is not null && type is INamedTypeSymbol { TypeKind: TypeKind.Interface } named &&
        (IsInterfaceDefinition(named) || named.AllInterfaces.Any(IsInterfaceDefinition));

    private bool IsInterfaceDefinition(INamedTypeSymbol type) =>
        SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, Interface);

    /// <summary>The number of leading X, Y, Z, W instance components the type exposes.</summary>
    public static int Dimension(ITypeSymbol type)
    {
        var dimension = 0;
        while (dimension < ComponentNames.Length && ComponentType(type, ComponentNames[dimension]) is not null)
            dimension++;
        return dimension;
    }

    public static ITypeSymbol? ComponentType(ITypeSymbol type, string name)
    {
        foreach (var member in type.GetMembers(name))
        {
            switch (member)
            {
                case IFieldSymbol { IsStatic: false } field:
                    return field.Type;
                case IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod: not null } property:
                    return property.Type;
            }
        }

        return null;
    }

    /// <summary>
    /// Maps constructor arguments to component positions by parameter name (x, y, z, w), so named
    /// arguments in any order still line up. Fails for initializers, defaults, params, or other parameters.
    /// </summary>
    public static IOperation[]? ComponentArguments(IObjectCreationOperation creation, int dimension)
    {
        if (dimension < 2 || creation.Initializer is not null || creation.Constructor is null ||
            creation.Arguments.Length != dimension)
        {
            return null;
        }

        var values = new IOperation?[dimension];
        foreach (var argument in creation.Arguments)
        {
            if (argument.ArgumentKind != ArgumentKind.Explicit || argument.Parameter is null) return null;
            var index = Array.FindIndex(ComponentNames,
                name => string.Equals(name, argument.Parameter.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index >= dimension || values[index] is not null) return null;
            values[index] = argument.Value;
        }

        return values!;
    }

    /// <summary>Reads <c>instance.Name</c> from a field or a parameterless property.</summary>
    public static bool TryGetComponent(IOperation operation, out IOperation instance, out string name)
    {
        switch (operation)
        {
            case IFieldReferenceOperation { Instance: { } fieldInstance } field:
                instance = fieldInstance;
                name = field.Field.Name;
                return true;
            case IPropertyReferenceOperation { Instance: { } propertyInstance, Arguments.IsEmpty: true } property:
                instance = propertyInstance;
                name = property.Property.Name;
                return true;
            default:
                instance = operation;
                name = "";
                return false;
        }
    }

    /// <summary>
    /// Identifies a value that can be read once instead of once per component: a local, parameter,
    /// <c>this</c>, a constant, or a chain of fields and parameterless properties over one of those.
    /// Returns null for invocations, object creations, and other expressions with possible side effects.
    /// </summary>
    public static string? StableKey(IOperation operation) => operation switch
    {
        ILocalReferenceOperation local => "local " + local.Local.Name,
        IParameterReferenceOperation parameter => "parameter " + parameter.Parameter.Name,
        IInstanceReferenceOperation => "this",
        { ConstantValue: { HasValue: true } constant } => "constant " + operation.Type?.ToDisplayString() + " " + constant.Value,
        IFieldReferenceOperation { Instance: null } field => "static " + field.Field.ToDisplayString(),
        IFieldReferenceOperation { Instance: { } instance } field => Member(StableKey(instance), field.Field.Name),
        IPropertyReferenceOperation { Arguments.IsEmpty: true, Instance: null } property => "static " + property.Property.ToDisplayString(),
        IPropertyReferenceOperation { Arguments.IsEmpty: true, Instance: { } instance } property => Member(StableKey(instance), property.Property.Name),
        _ => null
    };

    private static string? Member(string? instance, string name) => instance is null ? null : instance + "." + name;

    /// <summary>Source text for an operand; an implicit receiver is written as <c>this</c>.</summary>
    public static string Text(IOperation operation) =>
        operation is IInstanceReferenceOperation { IsImplicit: true } ? "this" : operation.Syntax.ToString();

    /// <summary>
    /// Rules that suggest a vector operator must not fire inside the vector type itself, where that
    /// operator is usually implemented with exactly the component-wise code being reported.
    /// </summary>
    public static bool IsInside(ISymbol containingSymbol, ITypeSymbol type)
    {
        for (var current = containingSymbol.ContainingType; current is not null; current = current.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, type.OriginalDefinition))
                return true;
        }

        return false;
    }
}
