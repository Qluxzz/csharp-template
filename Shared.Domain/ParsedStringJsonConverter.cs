using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Domain;

/// <summary>
/// Reads and writes any <see cref="IParsedString{TSelf}"/> as a plain JSON string, including as
/// a dictionary key. Attached to every <see cref="ParsedStringAttribute"/> type by the generator.
/// </summary>
public sealed class ParsedStringJsonConverter<T> : JsonConverter<T>
    where T : class, IParsedString<T>
{
    // Must be a JsonException: the model binder turns that into a 400, anything else is a 500.
    // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to#when-to-throw-which-exception-type
    // That's System.Text.Json's documented contract for converters even though the base
    // method has no <exception> tag, so the analyzer's override check is suppressed.
    // The rejected value is left out of the message, since it may be a token or other secret.
#pragma warning disable Ex0200
    /// <exception cref="JsonException">The value is not a valid <typeparamref name="T"/>.</exception>
    public override T Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        T.TryParse(reader.GetString(), null, out var result)
            ? result
            : throw new JsonException($"{typeof(T).Name} should look like \"{T.Example}\"");

    /// <exception cref="JsonException">The key is not a valid <typeparamref name="T"/>.</exception>
    public override T ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => Read(ref reader, typeToConvert, options);
#pragma warning restore Ex0200

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        [DisallowNull] T value,
        JsonSerializerOptions options
    ) =>
        writer.WritePropertyName(
            value.ToString()
                ?? throw new UnreachableException(
                    $"The generated {typeof(T).Name}.ToString() returns the parsed string, never null"
                )
        );
}
