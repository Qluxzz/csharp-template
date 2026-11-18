using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Shared.Domain.Generators;

/// <summary>
/// The shape a <c>[ParsedString]</c> type must have for the generated code to compile and to
/// keep the guarantee that every instance went through its TryParse. Shared by the generator,
/// which skips types that break a rule, and the analyzer, which reports why.
/// </summary>
internal static class ParsedStringRules
{
    public const string AttributeMetadataName = "Shared.Domain.ParsedStringAttribute";

    private const string _category = "ParsedString";

    public static readonly DiagnosticDescriptor MustBeSealedPartialRecord = new(
        "PS0001",
        "Parsed string type must be a sealed partial record",
        "'{0}' must be a top-level, non-generic 'sealed partial record' to use [ParsedString]",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MustDeclareTryParse = new(
        "PS0002",
        "Parsed string type must declare TryParse",
        "'{0}' must declare 'private static {0}? TryParse(string value)', returning null for invalid input",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MustNotDeclareConstructor = new(
        "PS0003",
        "Parsed string type must not declare a constructor",
        "'{0}' must not declare a constructor or primary constructor, the private one is generated so instances can only come from TryParse",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MustHavePatternAndExample = new(
        "PS0004",
        "Parsed string type must have a pattern and an example",
        "'{0}' must pass a non-empty pattern and example to [ParsedString]",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static ImmutableArray<DiagnosticDescriptor> All =>
        [
            MustBeSealedPartialRecord,
            MustDeclareTryParse,
            MustNotDeclareConstructor,
            MustHavePatternAndExample,
        ];

    public static IEnumerable<Diagnostic> Violations(INamedTypeSymbol type, AttributeData attribute)
    {
        var location = type.Locations.FirstOrDefault();
        var declarations = type
            .DeclaringSyntaxReferences.Select(r => r.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .ToList();

        if (
            !type.IsRecord
            || type.TypeKind != TypeKind.Class
            || !type.IsSealed
            || type.IsGenericType
            || type.ContainingType is not null
            || declarations.Count == 0
            || declarations.Any(d => !d.Modifiers.Any(SyntaxKind.PartialKeyword))
        )
            yield return Diagnostic.Create(MustBeSealedPartialRecord, location, type.Name);

        var hasTryParse = type.GetMembers("TryParse")
            .OfType<IMethodSymbol>()
            .Any(m =>
                m.IsStatic
                && m.DeclaredAccessibility == Accessibility.Private
                && m.Parameters.Length == 1
                && m.Parameters[0].Type.SpecialType == SpecialType.System_String
                && m.Parameters[0].NullableAnnotation != NullableAnnotation.Annotated
                && SymbolEqualityComparer.Default.Equals(m.ReturnType, type)
                && m.ReturnType.NullableAnnotation == NullableAnnotation.Annotated
            );
        if (!hasTryParse)
            yield return Diagnostic.Create(MustDeclareTryParse, location, type.Name);

        // The analyzer sees the compilation after generation, so the generated private
        // (string) constructor is skipped. A hand-written one with that signature still fails
        // to compile as a duplicate of the generated one.
        if (
            type.InstanceConstructors.Any(c =>
                !c.IsImplicitlyDeclared
                && !(
                    c.DeclaredAccessibility == Accessibility.Private
                    && c.Parameters.Length == 1
                    && c.Parameters[0].Type.SpecialType == SpecialType.System_String
                )
            ) || declarations.Any(d => d.ParameterList is not null)
        )
            yield return Diagnostic.Create(MustNotDeclareConstructor, location, type.Name);

        var (pattern, example) = PatternAndExample(attribute);
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(example))
            yield return Diagnostic.Create(MustHavePatternAndExample, location, type.Name);
    }

    public static (string? Pattern, string? Example) PatternAndExample(AttributeData attribute) =>
        attribute.ConstructorArguments.Length == 2
            ? (
                attribute.ConstructorArguments[0].Value as string,
                attribute.ConstructorArguments[1].Value as string
            )
            : (null, null);
}
