using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace Shared.Host.Test;

public class SignalRTest
{
    [Fact]
    public async Task SignalRHubShouldWorkAsExpected()
    {
        var host = new SharedHostBuilder().WithSignalR().Build();

        host.MapHub<TestHub>(ITestHub.Pattern);

        await host.StartAsync(CancellationToken.None);

        var url = $"http://localhost:8080{ITestHub.Pattern}";

        var connection = new HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build();

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.StopAsync();

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SignalRHubWithBearerTokenShouldWorkAsExpected()
    {
        var (key, token) = Test();

        var host = new SharedHostBuilder()
            .WithSignalR()
            .WithBearerToken(new("test-api", "test", key))
            .Build();

        host.MapHub<TestHub>(ITestHub.Pattern);

        await host.StartAsync(CancellationToken.None);

        var url = $"http://localhost:8080{ITestHub.Pattern}";

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(url)
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);

        var connection = new HubConnectionBuilder()
            .WithUrl(
                url,
                options =>
                {
                    options.SkipNegotiation = true;
                    options.Transports = HttpTransportType.WebSockets;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            )
            .WithAutomaticReconnect()
            .Build();

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.StopAsync();

        await host.StopAsync(CancellationToken.None);
    }

    private interface ITestHub
    {
        public const string Pattern = "/test-hub";
    }

    private class TestHub : Hub<ITestHub> { }

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

        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test-api",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        return (key, new JwtSecurityTokenHandler().WriteToken(token));
    }
}
