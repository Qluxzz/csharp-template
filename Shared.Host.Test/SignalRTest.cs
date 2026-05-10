using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Host.Test;

public class SignalRTest
{
    private static readonly string[] _noArgs = [];

    [Fact]
    public async Task SignalRHubShouldWorkAsExpected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = new SharedHostBuilder().WithSignalR().Build(_noArgs);

        host.MapHub<TestHub>(ITestHub.Pattern);

        await host.StartAsync(cancellationToken);

        var url = $"http://localhost:5000{ITestHub.Pattern}";

        var connection = new HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build();

        await connection.StartAsync(cancellationToken);

        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.StopAsync(cancellationToken);

        await host.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task SignalRHubWithBearerTokenShouldConnectWhenSupplied()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (key, token) = Test();

        var host = new SharedHostBuilder()
            .WithSignalR()
            .WithBearerToken(new("test-api", "test", key))
            .Build(_noArgs);

        host.MapHub<TestHub>(ITestHub.Pattern);

        await host.StartAsync(cancellationToken);

        var url = $"http://localhost:5000{ITestHub.Pattern}";

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

        await connection.StartAsync(cancellationToken);

        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.StopAsync(cancellationToken);

        await host.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task SignalRHubWithBearerTokenShouldReturnUnauthorizedWhenNotSupplied()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (key, token) = Test();

        var host = new SharedHostBuilder()
            .WithSignalR()
            .WithBearerToken(new("test-api", "test", key))
            .Build(_noArgs);

        host.MapHub<TestHub>(ITestHub.Pattern);

        await host.StartAsync(cancellationToken);

        var url = $"http://localhost:5000{ITestHub.Pattern}";

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            // Did not provide a required bearer token
            var connection = new HubConnectionBuilder()
                .WithUrl(url)
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync(cancellationToken);
        });
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);

        await host.StopAsync(cancellationToken);
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
