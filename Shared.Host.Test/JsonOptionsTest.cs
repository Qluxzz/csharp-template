using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shared.Host.Json;

namespace Shared.Host.Test;

public class JsonOptionsTest
{
    private static readonly string[] _noArgs = [];

    public static TheoryData<bool, bool> Configurations =>
        new()
        {
            { false, false },
            { true, false },
            { false, true },
            { true, true },
        };

    /// <summary>
    /// MVC, minimal APIs and SignalR each have their own JsonSerializerOptions,
    /// all of them must get the defaults regardless of which features are enabled.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void AllSerializersShouldUseTheDefaultOptions(bool withSwagger, bool withSignalR)
    {
        var builder = new SharedHostBuilder();
        if (withSwagger)
            builder = builder.WithSwagger();
        if (withSignalR)
            builder = builder.WithSignalR();

        var host = builder.Build(_noArgs);

        var serializers = new List<JsonSerializerOptions>
        {
            host
                .Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
                .Value.JsonSerializerOptions,
            host
                .Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
                .Value.SerializerOptions,
        };
        if (withSignalR)
            serializers.Add(
                host.Services.GetRequiredService<
                    IOptions<JsonHubProtocolOptions>
                >().Value.PayloadSerializerOptions
            );

        foreach (var options in serializers)
        {
            Assert.Equal(
                """{"testEnum":"bar"}""",
                JsonSerializer.Serialize(new WithEnum(TestEnum.Bar), options)
            );
            Assert.Equal(
                """{"$type":"cat","lives":9}""",
                JsonSerializer.Serialize(new Cat(9), options)
            );
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WithEnum>("{}", options));
        }
    }

    [Fact]
    public async Task DerivedTypeReturnedDirectlyShouldIncludeDiscriminator()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = new SharedHostBuilder().Build(_noArgs, (_, services) =>
        {
            // This is what .UseTestServer does
            services.AddSingleton<IHostLifetime, NoopHostLifetime>();
            services.AddSingleton<IServer, TestServer>();
        });

        host.MapGet("/cats", () => TypedResults.Ok(new List<Cat> { new(9) }));

        await host.StartAsync(cancellationToken);

        var response = await host.GetTestClient().GetStringAsync("/cats", cancellationToken);

        Assert.Equal("""[{"$type":"cat","lives":9}]""", response);

        await host.StopAsync(cancellationToken);
    }

    [Fact]
    public void SerializingThroughBaseTypeShouldWriteDiscriminatorOnce()
    {
        var options = DefaultOptions();

        Assert.Equal(
            """{"$type":"cat","lives":9}""",
            JsonSerializer.Serialize<Animal>(new Cat(9), options)
        );
        Assert.Equal(
            """{"kind":"dog","goodBoy":true}""",
            JsonSerializer.Serialize(new Dog(true), options)
        );
    }

    [Fact]
    public void DiscriminatorShouldNotHaveToBeTheFirstProperty()
    {
        var animal = JsonSerializer.Deserialize<Animal>(
            """{"lives":9,"$type":"cat"}""",
            DefaultOptions()
        );

        Assert.Equal(new Cat(9), animal);
    }

    [Fact]
    public void SealedDerivedTypeShouldThrow()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(
                PolymorphicTypeInfoModifier.WriteDiscriminatorForDerivedTypes
            ),
        };

        Assert.Throws<InvalidOperationException>(() => options.GetTypeInfo(typeof(SealedChild)));
    }

    [Fact]
    public void SingletonCapturingScopedServiceShouldFailAtBuild()
    {
        var exception = Assert.Throws<AggregateException>(() =>
            new SharedHostBuilder().Build(_noArgs, (_, services) =>
            {
                services.AddScoped<ScopedDependency>();
                services.AddSingleton<SingletonCapturingScoped>();
            })
        );

        Assert.Contains(nameof(ScopedDependency), exception.Message);
    }

    private static JsonSerializerOptions DefaultOptions() =>
        new SharedHostBuilder()
            .Build(_noArgs)
            .Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;

    public enum TestEnum
    {
        Foo,
        Bar,
    }

    public record WithEnum(TestEnum TestEnum);

    [JsonDerivedType(typeof(Cat), "cat")]
    public abstract record Animal;

    public record Cat(int Lives) : Animal;

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(Dog), "dog")]
    public abstract record Pet;

    public record Dog(bool GoodBoy) : Pet;

    [JsonDerivedType(typeof(SealedChild), "child")]
    private abstract record SealedBase;

    private sealed record SealedChild : SealedBase;

    private class ScopedDependency;

    private class SingletonCapturingScoped(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }
}
