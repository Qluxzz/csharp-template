using Shared.Domain;

namespace Shared.Host;

/// <summary>
/// The <c>aud</c> claim a bearer token must carry to be accepted, e.g. <c>https://api.example.com</c>.
/// </summary>
[ParsedString(@"\S", "https://api.example.com")]
public sealed partial record JwtAudience
{
    private static JwtAudience? TryParse(string value) =>
        StringOrUri.IsValid(value) ? new JwtAudience(value) : null;
}
