using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Any;

namespace Shared.Host.Test;

public class UnitTest1
{
    [Fact]
    public async Task EnumsShouldBeReturnedAsStrings()
    {
        var host = new SharedHostBuilder()
            .WithSwagger()
            .UseRouting()
            .Build(services =>
            {
                // This is what .UseTestServer does
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                services.AddSingleton<IServer, TestServer>();

                services.AddEndpointsApiExplorer();
            });

        host.MapGet("/test", () => TypedResults.Ok(new TestWithEnum(TestEnum.Bar)));

        await host.StartAsync(CancellationToken.None);

        var response = await host.GetTestClient().GetStringAsync("/test");

        Assert.Contains(@"{""testEnum"":""bar""}", response);
    }

    [Fact]
    public async Task EnumsShouldBeDocumentedInSwaggerAsStrings()
    {
        var host = new SharedHostBuilder()
            .WithSwagger()
            .UseRouting()
            .Build(services =>
            {
                // This is what .UseTestServer does
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                services.AddSingleton<IServer, TestServer>();

                services.AddEndpointsApiExplorer();
            });

        host.MapGet("/test", () => TypedResults.Ok(new TestWithEnum(TestEnum.Baz)));

        await host.StartAsync(CancellationToken.None);

        var response = await host.GetTestClient().GetStringAsync("/swagger/v1/swagger.json");

        var document = new Microsoft.OpenApi.Readers.OpenApiStringReader().Read(
            response,
            out var diagnostic
        );

        Assert.NotNull(diagnostic);
        Assert.Empty(diagnostic.Warnings);

        Assert.NotNull(document);
        Assert.NotEmpty(document.Components.Schemas);
        Assert.NotEmpty(document.Paths);
        var schema = Assert.Contains("TestEnum", document.Components.Schemas);
        Assert.Equivalent(
            new List<IOpenApiAny>([
                new OpenApiString("foo"),
                new OpenApiString("bar"),
                new OpenApiString("baz"),
            ]),
            schema.Enum
        );
        Assert.Equal("string", schema.Type);
    }

    public enum TestEnum
    {
        Foo,
        Bar,
        Baz,
    }

    public record TestWithEnum(TestEnum TestEnum);

    class NoopHostLifetime : IHostLifetime
    {
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
