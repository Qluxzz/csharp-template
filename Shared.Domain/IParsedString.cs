namespace Shared.Domain;

/// <summary>
/// A type that is parsed from, and serialized as, a plain string. The type owns how that string
/// is described to API consumers, and Shared.Host turns it into the OpenAPI schema, so this
/// project doesn't have to depend on OpenAPI.
/// </summary>
public interface IParsedString<TSelf> : IParsable<TSelf>
    where TSelf : IParsedString<TSelf>
{
    /// <summary>A regular expression every valid value matches, shown to API consumers.</summary>
    static abstract string Pattern { get; }

    /// <summary>A valid value, used as the example in Swagger UI instead of <c>"string"</c>.</summary>
    static abstract string Example { get; }
}
