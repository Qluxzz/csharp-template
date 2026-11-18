using Shared.Domain;

namespace Shared.Host;

/// <summary>
/// The <c>iss</c> claim a bearer token must carry to be accepted, e.g. <c>https://example.eu.auth0.com/</c>.
/// </summary>
[ParsedString(@"\S", "https://example.eu.auth0.com/")]
public sealed partial record JwtIssuer
{
    private static JwtIssuer? TryParse(string value) =>
        StringOrUri.IsValid(value) ? new JwtIssuer(value) : null;
}
