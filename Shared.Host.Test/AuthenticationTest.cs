using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Host.Test;

public class AuthenticationTest
{
    private static readonly string[] _noArgs = [];

    [Fact]
    public async Task UnauthorizedAccessIsAllowedIfUseBearerTokenHasntBeenCalled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = new SharedHostBuilder().Build(_noArgs, (_, services) =>
        {
            // This is what .UseTestServer does
            services.AddSingleton<IHostLifetime, NoopHostLifetime>();
            services.AddSingleton<IServer, TestServer>();

            services.AddEndpointsApiExplorer();
        });

        var response = "You should see this without passing a bearer token!";

        host.MapGet("/", () => response);

        await host.StartAsync(cancellationToken);

        var test = await host.GetTestClient().GetAsync("/", cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, test.StatusCode);
        Assert.Equal(response, await test.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task UnauthorizedAccessIsNotAllowedIfUseBearerTokenBeenCalled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (key, token) = Test();

        var host = new SharedHostBuilder()
            .WithBearerToken(new(JwtAudience.Parse("test-api"), JwtIssuer.Parse("test"), key))
            .Build(_noArgs, (_, services) =>
            {
                // This is what .UseTestServer does
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                services.AddSingleton<IServer, TestServer>();

                services.AddEndpointsApiExplorer();
            });

        var response = "You should not see this without passing a bearer token!";

        host.MapGet("/test", () => response);

        await host.StartAsync(cancellationToken);

        var testClient = host.GetTestClient();

        var test = await testClient.GetAsync("/test", cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, test.StatusCode);

        testClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        test = await testClient.GetAsync("/test", cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, test.StatusCode);
        Assert.Equal(response, await test.Content.ReadAsStringAsync(cancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":my-api")]
    [InlineData("https://")]
    [InlineData("1http://api.example.com")]
    public void BearerTokenIssuerAndAudienceRejectValuesOutsideStringOrUri(string? value)
    {
        // RFC 7519 §2: any string, but one containing ':' must be a URI. Anything else can
        // never match a token, which would only surface as 401s at runtime.
        Assert.False(JwtIssuer.TryParse(value, null, out _));
        Assert.False(JwtAudience.TryParse(value, null, out _));
    }

    [Theory]
    [InlineData("https://api.example.com")] // Auth0 API identifier
    [InlineData("6e74172b-be56-4843-9ff4-e66a39bb12e3")] // Entra ID v2 client id
    [InlineData("api://default")] // Okta default authorization server
    [InlineData("urn:example:api")]
    [InlineData("1234-abc.apps.googleusercontent.com")] // Google client id
    [InlineData("sts.amazonaws.com")]
    [InlineData("test-api")]
    public void BearerTokenIssuerAndAudienceAcceptWhatRealProvidersUse(string value)
    {
        Assert.True(JwtIssuer.TryParse(value, null, out _));
        Assert.True(JwtAudience.TryParse(value, null, out _));
    }

    private static (SymmetricSecurityKey SigningKey, string Token) Test()
    {
        var secret = "G2/Mtrmo1zRVI1zbDJplNh2p3X9hysFUiFRVZzW+D3s=";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "genericUser"),
            new Claim("role", "admin"),
        };

#pragma warning disable RS0030 // Do not use banned APIs, required by contract
        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test-api",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );
#pragma warning restore RS0030 // Do not use banned APIs

        return (key, new JwtSecurityTokenHandler().WriteToken(token));
    }
}
