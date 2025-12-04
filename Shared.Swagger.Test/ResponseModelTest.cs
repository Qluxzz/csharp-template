using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Any;

namespace Shared.Swagger.Test;

public class UnitTest1
{
    [Fact]
    public async Task EnumsShouldBeReturnedAsStrings()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddEndpointsApiExplorer();
                        services.AddSwagger();
                    })
                    .Configure(app =>
                    {
                        app.UseDefaultSwagger();
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                            endpoints.MapGet(
                                "/test",
                                () => TypedResults.Ok(new TestWithEnum(TestEnum.Bar))
                            )
                        );
                    })
            )
            .StartAsync();

        var response = await host.GetTestClient().GetStringAsync("/test");

        Assert.Contains(@"{""testEnum"":""bar""}", response);
    }

    [Fact]
    public async Task EnumsShouldBeDocumentedInSwaggerAsStrings()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddEndpointsApiExplorer();
                        services.AddSwagger();
                    })
                    .Configure(app =>
                    {
                        app.UseDefaultSwagger();
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                            endpoints.MapGet(
                                "/hello",
                                () => TypedResults.Ok(new TestWithEnum(TestEnum.Baz))
                            )
                        );
                    })
            )
            .StartAsync();

        var response = await host.GetTestClient().GetStringAsync("/swagger/v1/swagger.json");

        var document = new Microsoft.OpenApi.Readers.OpenApiStringReader().Read(
            response,
            out var diagnostic
        );

        Assert.NotNull(diagnostic);
        Assert.Empty(diagnostic.Warnings);

        Assert.NotNull(document);
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
}
