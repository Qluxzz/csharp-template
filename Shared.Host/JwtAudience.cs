using System.Diagnostics.CodeAnalysis;

namespace Shared.Host;

/// <summary>
/// The <c>aud</c> claim a bearer token must carry to be accepted, e.g. <c>https://api.example.com</c>.
/// </summary>
public sealed record JwtAudience : IParsable<JwtAudience>
{
    private readonly string _value;

    private JwtAudience(string value)
    {
        _value = value;
    }

    /// <exception cref="FormatException"><paramref name="s"/> is not a valid audience.</exception>
    public static JwtAudience Parse(string s, IFormatProvider? provider = null) =>
        TryParse(s, provider, out var audience)
            ? audience
            : throw new FormatException(
                "JWT audience should be a non-blank string, or a URI if it contains ':'"
            );

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        [NotNullWhen(true)] out JwtAudience? result
    )
    {
        result = s is not null && StringOrUri.IsValid(s) ? new JwtAudience(s) : null;
        return result is not null;
    }

    public override string ToString() => _value;
}
