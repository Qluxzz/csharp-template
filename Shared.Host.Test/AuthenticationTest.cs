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
        var host = new SharedHostBuilder().Build(
            _noArgs,
            services =>
            {
                // This is what .UseTestServer does
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                services.AddSingleton<IServer, TestServer>();

                services.AddEndpointsApiExplorer();
            }
        );

        var response = "You should see this without passing a bearer token!";

        host.MapGet("/", () => response);

        await host.StartAsync(CancellationToken.None);

        var test = await host.GetTestClient().GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, test.StatusCode);
        Assert.Equal(response, await test.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnauthorizedAccessIsNotAllowedIfUseBearerTokenBeenCalled()
    {
        var (key, token) = Test();

        var host = new SharedHostBuilder()
            .WithBearerToken(new("test-api", "test", key))
            .Build(
                _noArgs,
                services =>
                {
                    // This is what .UseTestServer does
                    services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                    services.AddSingleton<IServer, TestServer>();

                    services.AddEndpointsApiExplorer();
                }
            );

        var response = "You should not see this without passing a bearer token!";

        host.MapGet("/test", () => response);

        await host.StartAsync(CancellationToken.None);

        var testClient = host.GetTestClient();

        var test = await testClient.GetAsync("/test");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, test.StatusCode);

        testClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        test = await testClient.GetAsync("/test");
        Assert.Equal(System.Net.HttpStatusCode.OK, test.StatusCode);
        Assert.Equal(response, await test.Content.ReadAsStringAsync());
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
