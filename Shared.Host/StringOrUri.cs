namespace Shared.Host;

internal static class StringOrUri
{
    /// <summary>
    /// RFC 7519 §2: any string is allowed, but one containing a ':' must be a URI. So
    /// <c>my-api</c>, a client id and <c>api://default</c> are valid, <c>:my-api</c> is not.
    /// </summary>
    public static bool IsValid(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && (!value.Contains(':') || Uri.TryCreate(value, UriKind.Absolute, out _));
}
