using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace Shared.Host.Test;

public class ResponseModelTest
{
    [Fact]
    public async Task NullableEnumsShouldBeDocumentedInSwaggerAsOptional()
    {
        var host = new SharedHostBuilder()
            .WithSwagger()
            .Build(services =>
            {
                // This is what .UseTestServer does
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                services.AddSingleton<IServer, TestServer>();

                services.AddEndpointsApiExplorer();
            });

        host.MapGet("/test", () => TypedResults.Ok(new TestWithNullableEnum(TestEnum.Baz)));

        await host.StartAsync(CancellationToken.None);

        var response = await host.GetTestClient().GetByteArrayAsync("/swagger/v1/swagger.json");

        await File.WriteAllBytesAsync("output", response);

        using var stream = new MemoryStream(response);

        var reader = new OpenApiJsonReader();

        var (document, diagnostic) = reader.Read(stream, new Uri("http://localhost"), new());

        Assert.NotNull(diagnostic);
        Assert.Empty(diagnostic.Warnings);

        Assert.NotNull(document?.Components?.Schemas);
        var enumSchema = Assert.Contains(nameof(TestEnum), document.Components.Schemas);
        Assert.NotNull(enumSchema);

        var recordSchema = Assert.Contains(
            nameof(TestWithNullableEnum),
            document.Components.Schemas
        );
        Assert.NotNull(recordSchema.Properties);

        var recordSchemaEnumSchema = Assert.IsType<OpenApiSchemaReference>(
            recordSchema.Properties["nullable"]
        );

        Assert.Equal(recordSchemaEnumSchema.Target, enumSchema);
    }

    record TestWithNullableEnum(TestEnum? Nullable);

    [Fact]
    public async Task EnumsShouldBeReturnedAsStrings()
    {
        var host = new SharedHostBuilder()
            .WithSwagger()
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
            .Build(services =>
            {
                // This is what .UseTestServer does
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                services.AddSingleton<IServer, TestServer>();

                services.AddEndpointsApiExplorer();
            });

        host.MapGet("/test", () => TypedResults.Ok(new TestWithEnum(TestEnum.Baz)));

        await host.StartAsync(CancellationToken.None);

        var response = await host.GetTestClient().GetByteArrayAsync("/swagger/v1/swagger.json");

        using var stream = new MemoryStream(response);

        var reader = new OpenApiJsonReader();

        var (document, diagnostic) = reader.Read(stream, new Uri("http://localhost"), new());

        Assert.NotNull(diagnostic);
        Assert.Empty(diagnostic.Warnings);

        Assert.NotNull(document);
        Assert.NotNull(document.Components);
        Assert.NotNull(document.Components.Schemas);
        Assert.NotEmpty(document.Components.Schemas);
        Assert.NotEmpty(document.Paths);
        var schema = Assert.Contains(nameof(TestEnum), document.Components.Schemas);
        Assert.NotNull(schema.Enum);
        Assert.Equivalent(
            new List<string>() { "foo", "bar", "baz" },
            schema.Enum.Select(x => x.GetValue<string>())
        );
        Assert.Equal(Microsoft.OpenApi.JsonSchemaType.String, schema.Type);
    }

    public enum TestEnum
    {
        Foo,
        Bar,
        Baz,
    }

    public record TestWithEnum(TestEnum TestEnum);
}
