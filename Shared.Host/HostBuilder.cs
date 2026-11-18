using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Shared.Host.Json;
using Shared.Host.Swagger;
using static System.Net.Mime.MediaTypeNames;

namespace Shared.Host;

public record BearerTokenSettings(
    JwtAudience ValidAudience,
    JwtIssuer ValidIssuer,
    SymmetricSecurityKey SigningKey
);

public class SharedHostBuilder
{
    private bool _useSwagger = false;

    private bool _useSignalR { get; set; }

    private BearerTokenSettings? _bearerTokenSettings = null;

    public SharedHostBuilder WithSwagger()
    {
        _useSwagger = true;
        return this;
    }

    public SharedHostBuilder WithSignalR()
    {
        _useSignalR = true;
        return this;
    }

    /// <summary>
    /// Makes it so all endpoints require an authenticated user.
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public SharedHostBuilder WithBearerToken(BearerTokenSettings settings)
    {
        _bearerTokenSettings = settings;
        return this;
    }

    public SharedHost Build(
        string[] args,
        Action<IConfiguration, IServiceCollection>? customServices = null
    )
    {
        var options = new WebApplicationOptions() { Args = args };

        var builder = WebApplication.CreateSlimBuilder(options);

        builder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
            options.UseUtcTimestamp = false;
            options.SingleLine = true;
        });

        builder.Services.AddRouting();

        // Only on by default in Development, so a singleton capturing a scoped service
        // would otherwise only surface in production
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });

        // MVC, minimal APIs and SignalR each own a separate JsonSerializerOptions,
        // configuring only one silently leaves the others on the framework defaults.
        // The MVC one is also what Swashbuckle reads when generating the schema
        // https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/2293
        builder
            .Services.AddControllers()
            .AddJsonOptions(options => DefaultOptions(options.JsonSerializerOptions));

        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            DefaultOptions(options.SerializerOptions)
        );

        if (_useSwagger)
        {
            void Custom(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
            {
                if (_bearerTokenSettings is not null)
                {
                    // This allows you to impersonate users by supplying their bearer token directly
                    const string impersonateUser = "Impersonate user";

                    options.AddSecurityDefinition(
                        impersonateUser,
                        new OpenApiSecurityScheme
                        {
                            Description =
                                "Here you can use a token directly to impersonate another user if you have their token",
                            Name = "Authorization",
                            In = ParameterLocation.Header,
                            Type = SecuritySchemeType.Http,
                            Scheme = "Bearer",
                            BearerFormat = "JWT",
                        }
                    );

                    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(impersonateUser, document)] = [],
                    });
                }

                if (_useSignalR)
                {
                    options.AddSignalRSwaggerGen();
                }
            }
            builder.Services.AddSwagger(Custom);
        }

        if (_useSignalR)
        {
            builder
                .Services.AddSignalR(o => o.EnableDetailedErrors = true)
                .AddJsonProtocol(options => DefaultOptions(options.PayloadSerializerOptions));
        }

        if (_bearerTokenSettings is { } bearerTokenSettings)
        {
            builder
                .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = bearerTokenSettings.ValidIssuer.ToString(),
                        ValidateAudience = true,
                        ValidAudience = bearerTokenSettings.ValidAudience.ToString(),
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = bearerTokenSettings.SigningKey,
                    };

                    if (_useSignalR)
                    {
                        // We have to hook the OnMessageReceived event in order to
                        // allow the JWT authentication handler to read the access
                        // token from the query string when a WebSocket or
                        // Server-Sent Events request comes in.

                        // Sending the access token in the query string is required when using WebSockets or ServerSentEvents
                        // due to a limitation in Browser APIs. We restrict it to only calls to the
                        // SignalR hub in this code.
                        // See https://docs.microsoft.com/aspnet/core/signalr/security#access-token-logging
                        // for more information about security considerations when using
                        // the query string to transmit the access token.
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                var accessToken = context.Request.Query["access_token"];

                                if (!string.IsNullOrEmpty(accessToken))
                                {
                                    // Read the token out of the query string
                                    context.Token = accessToken;
                                }
                                return Task.CompletedTask;
                            },
                        };
                    }
                });

            // Require Authorization for all endpoints
            // Use [AllowAnonymous] to override for specific endpoints
            var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(policy);
        }

        // Registering services is not dependant on the order
        customServices?.Invoke(builder.Configuration, builder.Services);

        var app = builder.Build();

        // Here the order of the UseX methods is important
        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware#middleware-order

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // Catch all unhandled exceptions and just return a non descriptive "Internal Server Error" message
            app.UseExceptionHandler(exceptionHandlerApp =>
                exceptionHandlerApp.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    context.Response.ContentType = Text.Plain;

                    await context.Response.WriteAsync("Internal Server Error");
                })
            );
        }

        if (_bearerTokenSettings is not null)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        if (_useSwagger)
        {
            app.UseDefaultSwagger();
        }

        app.MapControllers();

        return new(app);
    }

    private static readonly JsonNamingPolicy _defaultNamingPolicy = JsonNamingPolicy.CamelCase;

    private static readonly JsonStringEnumConverter _jsonStringEnumConverter = new(
        namingPolicy: _defaultNamingPolicy,
        allowIntegerValues: false
    );

    /// <exception cref="NotSupportedException">Failed to add json converter</exception>
    private static JsonSerializerOptions DefaultOptions(JsonSerializerOptions options)
    {
        // Always serialize as camelCase
        options.PropertyNamingPolicy = _defaultNamingPolicy;
        // Ignore casing during deserialization, since we don't always own the API we're calling, we can't enforce their casing.
        // If the API you're calling is using something like snake_case or kebab-case or another not just casing based naming policy,
        options.PropertyNameCaseInsensitive = true;

        // Without this all fields which aren't included in the JSON response will be set to default(T)
        // Which is usually not what you want, you want to know that the data was invalid.
        // Also recommended to be set to true for new projects by Microsoft
        options.RespectRequiredConstructorParameters = true;

        // Without this a JSON null is written straight into a non-nullable reference property,
        // since converters aren't called for null, so a parsed type like Auth0Id could be null
        // where the compiler believes it can't be
        options.RespectNullableAnnotations = true;

        // JavaScript clients and Postgres jsonb don't preserve key order, so the
        // type discriminator isn't guaranteed to be the first property
        options.AllowOutOfOrderMetadataProperties = true;

        options.Converters.Add(_jsonStringEnumConverter);

        options.TypeInfoResolver = (
            options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()
        ).WithAddedModifier(PolymorphicTypeInfoModifier.WriteDiscriminatorForDerivedTypes);

        return options;
    }
}

public class SharedHost(WebApplication webApplication) : IHost
{
    private readonly WebApplication _webApplication = webApplication;

    public IServiceProvider Services => _webApplication.Services;

    public Task StartAsync(CancellationToken cancellationToken) =>
        _webApplication.StartAsync(cancellationToken);

    public Task RunAsync(CancellationToken cancellationToken) =>
        _webApplication.RunAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        _webApplication.StopAsync(cancellationToken);

    public RouteHandlerBuilder MapGet(string pattern, Delegate handler) =>
        _webApplication.MapGet(pattern, handler);

    public HubEndpointConventionBuilder MapHub<T>(string pattern)
        where T : Hub => _webApplication.MapHub<T>(pattern);

#pragma warning disable VSTHRD110 // Observe result of async calls
    void IDisposable.Dispose() => _webApplication.DisposeAsync();
#pragma warning restore VSTHRD110 // Observe result of async calls
}
