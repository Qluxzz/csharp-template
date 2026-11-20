using System.Diagnostics.CodeAnalysis;

namespace Shared.Host;

/// <summary>
/// The <c>iss</c> claim a bearer token must carry to be accepted, e.g. <c>https://example.eu.auth0.com/</c>.
/// </summary>
public sealed record JwtIssuer : IParsable<JwtIssuer>
{
    private readonly string _value;

    private JwtIssuer(string value)
    {
        _value = value;
    }

    /// <exception cref="FormatException"><paramref name="s"/> is not a valid issuer.</exception>
    public static JwtIssuer Parse(string s, IFormatProvider? provider = null) =>
        TryParse(s, provider, out var issuer)
            ? issuer
            : throw new FormatException(
                "JWT issuer should be a non-blank string, or a URI if it contains ':'"
            );

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        [NotNullWhen(true)] out JwtIssuer? result
    )
    {
        result = s is not null && StringOrUri.IsValid(s) ? new JwtIssuer(s) : null;
        return result is not null;
    }

    public override string ToString() => _value;
}
