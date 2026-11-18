using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shared.Domain;
using Shared.Domain.Generators;

namespace Shared.Host.Test;

public class ParsedStringGeneratorTest
{
    [Fact]
    public void TryParseCanNormalizeTheValue()
    {
        var email = Email.Parse("  Jane@Example.COM ");

        Assert.Equal("jane@example.com", email.ToString());
        Assert.Equal(Email.Parse("jane@example.com"), email);
    }

    [Fact]
    public void ParseErrorShowsTheExampleButNotTheRejectedValue()
    {
        var exception = Assert.Throws<FormatException>(() => Email.Parse("secret-token"));

        Assert.Equal("Email should look like \"jane@example.com\"", exception.Message);
    }

    [Fact]
    public void JsonUsesTheSameParsingAndErrorMessage()
    {
        Assert.Equal(
            new EmailBody(Email.Parse("jane@example.com")),
            JsonSerializer.Deserialize<EmailBody>("""{"Email":" JANE@example.com"}""")
        );

        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<EmailBody>("""{"Email":"secret-token"}""")
        );
        Assert.StartsWith("Email should look like \"jane@example.com\"", exception.Message);
    }

    [Fact]
    public void NullIsRejectedWithoutReachingTheTypesTryParse()
    {
        Assert.False(Email.TryParse(null, null, out _));
    }

    [Theory]
    [InlineData(
        "PS0001",
        """
            [ParsedString("^.+$", "a")]
            public partial class NotARecord
            {
                private NotARecord(string value) { }
                private static NotARecord? TryParse(string value) => null;
            }
            """
    )]
    [InlineData(
        "PS0001",
        """
            [ParsedString("^.+$", "a")]
            public sealed record NotPartial
            {
                private static NotPartial? TryParse(string value) => null;
            }
            """
    )]
    [InlineData(
        "PS0001",
        """
            [ParsedString("^.+$", "a")]
            public partial record NotSealed
            {
                private static NotSealed? TryParse(string value) => null;
            }
            """
    )]
    [InlineData(
        "PS0002",
        """
            [ParsedString("^.+$", "a")]
            public sealed partial record MissingTryParse;
            """
    )]
    [InlineData(
        "PS0002",
        """
            [ParsedString("^.+$", "a")]
            public sealed partial record PublicTryParse
            {
                public static PublicTryParse? TryParse(string value) => null;
            }
            """
    )]
    [InlineData(
        "PS0003",
        """
            [ParsedString("^.+$", "a")]
            public sealed partial record OwnConstructor
            {
                public OwnConstructor(int value) { }
                private static OwnConstructor? TryParse(string value) => null;
            }
            """
    )]
    [InlineData(
        "PS0003",
        """
            [ParsedString("^.+$", "a")]
            public sealed partial record PrimaryConstructor(string Value)
            {
                private static PrimaryConstructor? TryParse(string value) => null;
            }
            """
    )]
    [InlineData(
        "PS0004",
        """
            [ParsedString("", "a")]
            public sealed partial record EmptyPattern
            {
                private static EmptyPattern? TryParse(string value) => null;
            }
            """
    )]
    public async Task MisshapenTypesAreACompileError(string expectedId, string source)
    {
        var diagnostics = await Compile($"using Shared.Domain;\n{source}");

        Assert.Contains(expectedId, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task AWellShapedTypeCompilesWithoutDiagnostics()
    {
        var diagnostics = await Compile(
            """
            using Shared.Domain;

            [ParsedString("^.+$", "a")]
            public sealed partial record Fine
            {
                private static Fine? TryParse(string value) => new Fine(value);
            }
            """
        );

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    private static async Task<ImmutableArray<Diagnostic>> Compile(string source)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator)
            .Append(typeof(ParsedStringAttribute).Assembly.Location)
            .Select(path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "ParsedStringGeneratorTest",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        CSharpGeneratorDriver
            .Create(new ParsedStringGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _);

        return await generated
            .WithAnalyzers([new ParsedStringAnalyzer()])
            .GetAllDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    public record EmailBody(Email Email);
}

[ParsedString(@"^\S+@\S+$", "jane@example.com")]
public sealed partial record Email
{
    private static Email? TryParse(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Contains('@') ? new Email(normalized) : null;
    }
}
