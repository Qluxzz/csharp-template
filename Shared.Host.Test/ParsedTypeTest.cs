using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Shared.Domain;

namespace Shared.Host.Test;

public class ParsedTypeTest
{
    private static readonly string[] _noArgs = [];

    private const string ValidId = "auth0|507f1f77bcf86cd799439011";

    private static async Task<SharedHost> StartHost(CancellationToken cancellationToken)
    {
        var host = new SharedHostBuilder()
            .WithSwagger()
            .Build(_noArgs, (_, services) =>
            {
                // This is what .UseTestServer does
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                services.AddSingleton<IServer, TestServer>();

                services.AddEndpointsApiExplorer();
            });

        host.MapGet("/users/{id}", (Auth0Id id) => TypedResults.Ok(new UserResponse(id)));

        await host.StartAsync(cancellationToken);
        return host;
    }

    [Fact]
    public async Task ValidRouteValueIsParsedAndReturnedAsAPlainString()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = await StartHost(cancellationToken);

        var response = await host.GetTestClient()
            .GetStringAsync($"/users/{Uri.EscapeDataString(ValidId)}", cancellationToken);

        Assert.Equal($$"""{"id":"{{ValidId}}"}""", response);
    }

    [Theory]
    [InlineData("507f1f77bcf86cd799439011")]
    [InlineData("google-oauth2|507f1f77bcf86cd799439011")]
    [InlineData("auth0|")]
    [InlineData("auth0|507f1f77bcf86cd79943901")]
    [InlineData("auth0|507f1f77bcf86cd7994390111")]
    [InlineData("auth0|507F1F77BCF86CD799439011")]
    [InlineData("auth0|507f1f77bcf86cd79943901g")]
    [InlineData("auth0|legacy-user-42")]
    [InlineData("auth0|507f1f77bcf86cd799439011\n")]
    [InlineData("auth0|507f\n1f77bcf86cd799439011")]
    public async Task InvalidRouteValueIsRejectedBeforeTheHandlerRuns(string id)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = await StartHost(cancellationToken);

        var response = await host.GetTestClient()
            .GetAsync($"/users/{Uri.EscapeDataString(id)}", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void InvalidJsonValueFailsAsJsonExceptionSoTheModelBinderReturns400()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<UserResponse>("""{"Id":"not-an-auth0-id"}""")
        );
    }

    [Fact]
    public void PublishedExampleIsAccepted()
    {
        // The example is shown in Swagger and in every format error, so it must match the pattern.
        Assert.True(Auth0Id.TryParse(Auth0Id.Example, null, out _));
    }

    [Fact]
    public void CanBeUsedAsADictionaryKey()
    {
        var id = Auth0Id.Parse(ValidId);

        var json = JsonSerializer.Serialize(new Dictionary<Auth0Id, int> { [id] = 1 });

        Assert.Equal($$"""{"{{ValidId}}":1}""", json);
        Assert.Equal(
            new Dictionary<Auth0Id, int> { [id] = 1 },
            JsonSerializer.Deserialize<Dictionary<Auth0Id, int>>(json)
        );
    }

    [Fact]
    public async Task IsDocumentedAsAStringInSwagger()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = await StartHost(cancellationToken);

        var response = await host.GetTestClient()
            .GetByteArrayAsync("/swagger/v1/swagger.json", cancellationToken);

        using var stream = new MemoryStream(response);

        var (document, diagnostic) = await new OpenApiJsonReader().ReadAsync(
            stream,
            new Uri("http://localhost"),
            new(),
            cancellationToken
        );

        Assert.NotNull(diagnostic);
        Assert.Empty(diagnostic.Warnings);

        var parameter = Assert.Single(
            document?.Paths["/users/{id}"].Operations?[HttpMethod.Get].Parameters ?? []
        );
        Assert.Equal(JsonSchemaType.String, parameter.Schema?.Type);
        Assert.Equal(Auth0Id.Pattern, parameter.Schema?.Pattern);
        // Swagger is published as OpenAPI 3.0, which only has the singular `example`.
#pragma warning disable CS0618
        Assert.Equal(Auth0Id.Example, parameter.Schema?.Example?.GetValue<string>());
#pragma warning restore CS0618

        var responseSchema = Assert.Contains(
            nameof(UserResponse),
            document?.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>()
        );
        Assert.Equal(JsonSchemaType.String, responseSchema.Properties?["id"].Type);
    }

    public record UserResponse(Auth0Id Id);
}
