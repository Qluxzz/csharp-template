using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Shared.Domain;

/// <summary>
/// An Auth0 user id, e.g. <c>auth0|507f1f77bcf86cd799439011</c>. Holding one means it has
/// already been parsed, so code receiving it never has to check the format again.
/// </summary>
[JsonConverter(typeof(Auth0IdJsonConverter))]
public sealed partial record Auth0Id : IParsable<Auth0Id>
{
    /// <summary>The format <see cref="TryParse"/> accepts, published in the OpenAPI schema.</summary>
    public const string Pattern = @"^auth0\|[0-9a-f]{24}$";

    /// <summary>An example value, published in the OpenAPI schema.</summary>
    public const string Example = "auth0|507f1f77bcf86cd799439011";

    private readonly string _value;

    private Auth0Id(string value)
    {
        _value = value;
    }

    /// <exception cref="FormatException"><paramref name="s"/> is not an Auth0 id.</exception>
    public static Auth0Id Parse(string s, IFormatProvider? provider = null) =>
        TryParse(s, provider, out var id)
            ? id
            : throw new FormatException($"Auth0 id should look like {Example}");

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        [NotNullWhen(true)] out Auth0Id? result
    )
    {
        // .NET's `$` also matches before a trailing newline, which the ECMAScript regex that
        // OpenAPI clients use does not, so require the match to cover the whole input.
        // PatternRegex has no match timeout, so RegexMatchTimeoutException cannot be thrown.
#pragma warning disable Ex0100
        result =
            s is not null
            && PatternRegex().Match(s) is { Success: true } match
            && match.Length == s.Length
                ? new Auth0Id(s)
                : null;
#pragma warning restore Ex0100
        return result is not null;
    }

    [GeneratedRegex(Pattern)]
    private static partial Regex PatternRegex();

    public override string ToString() => _value;
}

/// <summary>
/// Lets <see cref="Auth0Id"/> be read from and written to JSON bodies as a plain string,
/// including as a dictionary key.
/// </summary>
public sealed class Auth0IdJsonConverter : JsonConverter<Auth0Id>
{
    // Must be a JsonException: the model binder turns that into a 400, anything else is a 500.
    // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to#when-to-throw-which-exception-type
    // That's System.Text.Json's documented contract for converters even though the base
    // method has no <exception> tag, so the analyzer's override check is suppressed.
#pragma warning disable Ex0200
    /// <exception cref="JsonException">The value is not an Auth0 id.</exception>
    public override Auth0Id Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        Auth0Id.TryParse(reader.GetString(), null, out var id)
            ? id
            : throw new JsonException($"Auth0 id should look like {Auth0Id.Example}");

    public override void Write(
        Utf8JsonWriter writer,
        Auth0Id value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.ToString());

    /// <exception cref="JsonException">The key is not an Auth0 id.</exception>
    public override Auth0Id ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => Read(ref reader, typeToConvert, options);
#pragma warning restore Ex0200

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        [DisallowNull] Auth0Id value,
        JsonSerializerOptions options
    ) => writer.WritePropertyName(value.ToString());
}
