using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Shared.Host.Test;

public class TimeProviderTest
{
    private static readonly string[] _noArgs = [];

    [Fact]
    public async Task EndpointsCanInjectTheClockWithoutRegisteringOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = new SharedHostBuilder().Build(_noArgs, (_, services) =>
        {
            // This is what .UseTestServer does
            services.AddSingleton<IHostLifetime, NoopHostLifetime>();
            services.AddSingleton<IServer, TestServer>();
        });

        host.MapGet("/now", ([FromServices] TimeProvider clock) =>
            TypedResults.Ok(clock.GetUtcNow())
        );

        await host.StartAsync(cancellationToken);

        var response = await host.GetTestClient().GetAsync("/now", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ATimeProviderRegisteredByTheCallerReplacesTheSystemClock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = new SharedHostBuilder().Build(_noArgs, (_, services) =>
        {
            // This is what .UseTestServer does
            services.AddSingleton<IHostLifetime, NoopHostLifetime>();
            services.AddSingleton<IServer, TestServer>();

            services.AddSingleton<TimeProvider>(
                new FixedTimeProvider(new DateTimeOffset(2000, 1, 2, 3, 4, 5, TimeSpan.Zero))
            );
        });

        host.MapGet("/now", ([FromServices] TimeProvider clock) =>
            TypedResults.Ok(clock.GetUtcNow())
        );

        await host.StartAsync(cancellationToken);

        var response = await host.GetTestClient().GetStringAsync("/now", cancellationToken);

        Assert.Equal(@"""2000-01-02T03:04:05+00:00""", response);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
