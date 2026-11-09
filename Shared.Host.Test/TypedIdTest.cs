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

public class TypedIdTest
{
    private static readonly string[] _noArgs = [];

    [Fact]
    public void SerializesAndDeserializesAsPrefixedString()
    {
        var id = ExampleId.New();

        // The [JsonConverter] the generator puts on the struct is honored automatically,
        // no JsonSerializerOptions setup required.
        var json = JsonSerializer.Serialize(id);

        Assert.StartsWith("\"example_", json);

        var roundTripped = JsonSerializer.Deserialize<ExampleId>(json);

        Assert.Equal(id, roundTripped);
    }

    [Fact]
    public async Task IsReturnedAsAnUnwrappedStringOverHttp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = ExampleId.New();
        var host = new SharedHostBuilder()
            .WithSwagger()
            .Build(
                _noArgs,
                (_, services) =>
                {
                    // This is what .UseTestServer does
                    services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                    services.AddSingleton<IServer, TestServer>();

                    services.AddEndpointsApiExplorer();
                }
            );

        host.MapGet("/example", () => TypedResults.Ok(new ExampleResponse(id)));

        await host.StartAsync(cancellationToken);

        var response = await host.GetTestClient().GetStringAsync("/example", cancellationToken);

        Assert.Equal($$"""{"id":"example_{{(Guid)id}}"}""", response);
    }

    [Fact]
    public async Task IsDocumentedAsAStringInSwagger()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = new SharedHostBuilder()
            .WithSwagger()
            .Build(
                _noArgs,
                (_, services) =>
                {
                    // This is what .UseTestServer does
                    services.AddSingleton<IHostLifetime, NoopHostLifetime>();
                    services.AddSingleton<IServer, TestServer>();

                    services.AddEndpointsApiExplorer();
                }
            );

        host.MapGet("/example", () => TypedResults.Ok(new ExampleResponse(ExampleId.New())));

        await host.StartAsync(cancellationToken);

        var response = await host.GetTestClient()
            .GetByteArrayAsync("/swagger/v1/swagger.json", cancellationToken);

        using var stream = new MemoryStream(response);

        var reader = new OpenApiJsonReader();

        var (document, diagnostic) = reader.Read(stream, new Uri("http://localhost"), new());

        Assert.NotNull(diagnostic);
        Assert.Empty(diagnostic.Warnings);

        Assert.NotNull(document?.Components?.Schemas);

        // The strongly-typed ID is documented as a plain string with a prefixed format,
        // not as an object with a wrapped "value" property.
        var idSchema = Assert.Contains(nameof(ExampleId), document.Components.Schemas);
        Assert.Equal(JsonSchemaType.String, idSchema.Type);
        Assert.Equal("example_uuid", idSchema.Format);
    }

    record ExampleResponse(ExampleId Id);
}
