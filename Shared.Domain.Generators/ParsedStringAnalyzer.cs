using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Shared.Domain.Generators;

/// <summary>
/// Reports why a <c>[ParsedString]</c> type gets no generated code. Kept out of the generator
/// because diagnostics carry locations, which would defeat the generator's incremental caching.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParsedStringAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ParsedStringRules.All;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(
            symbolContext =>
            {
                var type = (INamedTypeSymbol)symbolContext.Symbol;
                var attribute = type.GetAttributes()
                    .FirstOrDefault(a =>
                        a.AttributeClass?.ToDisplayString()
                        == ParsedStringRules.AttributeMetadataName
                    );
                if (attribute is null)
                    return;

                foreach (var diagnostic in ParsedStringRules.Violations(type, attribute))
                    symbolContext.ReportDiagnostic(diagnostic);
            },
            SymbolKind.NamedType
        );
    }
}
