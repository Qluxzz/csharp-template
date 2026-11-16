using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Domain;

/// <summary>
/// An Auth0 user id, e.g. <c>auth0|507f1f77bcf86cd799439011</c>. Holding one means it has
/// already been parsed, so code receiving it never has to check the format again.
/// </summary>
[JsonConverter(typeof(Auth0IdJsonConverter))]
public sealed record Auth0Id : IParsedString<Auth0Id>
{
    public const string Prefix = "auth0|";

    public static string Pattern => @"^auth0\|.+$";

    public static string Example => "auth0|507f1f77bcf86cd799439011";

    private readonly string _value;

    private Auth0Id(string value)
    {
        _value = value;
    }

    /// <exception cref="FormatException"><paramref name="s"/> is not an Auth0 id.</exception>
    public static Auth0Id Parse(string s, IFormatProvider? provider = null) =>
        TryParse(s, provider, out var id)
            ? id
            : throw new FormatException($"Auth0 id should have the format {Prefix}XXXXXXX");

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        [NotNullWhen(true)] out Auth0Id? result
    )
    {
        result =
            s is not null
            && s.StartsWith(Prefix, StringComparison.Ordinal)
            && s.Length > Prefix.Length
                ? new Auth0Id(s)
                : null;
        return result is not null;
    }

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
            : throw new JsonException($"Auth0 id should have the format {Auth0Id.Prefix}XXXXXXX");

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
