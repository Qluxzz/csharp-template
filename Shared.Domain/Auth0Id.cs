namespace Shared.Domain;

/// <summary>
/// An Auth0 user id, e.g. <c>auth0|507f1f77bcf86cd799439011</c>. Holding one means it has
/// already been parsed, so code receiving it never has to check the format again.
/// </summary>
[ParsedString(@"^auth0\|.+$", "auth0|507f1f77bcf86cd799439011")]
public sealed partial record Auth0Id
{
    private const string _prefix = "auth0|";

    private static Auth0Id? TryParse(string value) =>
        value.StartsWith(_prefix, StringComparison.Ordinal) && value.Length > _prefix.Length
            ? new Auth0Id(value)
            : null;
}
