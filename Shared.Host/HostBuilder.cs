using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Shared.Host.Swagger;
using static System.Net.Mime.MediaTypeNames;

namespace Shared.Host;

public record BearerTokenSettings(
    string ValidAudience,
    string ValidateIssuer,
    SymmetricSecurityKey SigningKey
);

public record SignalRSettings(Dictionary<Hub, string> Hubs);

public class SharedHostBuilder
{
    private bool _useSwagger = false;

    private bool _useSignalR { get; set; }

    [MemberNotNullWhen(true, nameof(_bearerTokenSettings))]
    private bool _useBearerToken { get; set; }
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
        _useBearerToken = true;
        _bearerTokenSettings = settings;
        return this;
    }

    public SharedHost Build(Action<IServiceCollection>? customServices = null)
    {
        var options = new WebApplicationOptions() { };

        var builder = WebApplication.CreateSlimBuilder(options);

        builder.Services.AddRouting();

        builder.WebHost.UseUrls("http://localhost:8080");

        if (_useSwagger)
        {
            void Custom(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
            {
                if (_useBearerToken)
                {
                    options.AddSecurityDefinition(
                        "Bearer",
                        new OpenApiSecurityScheme
                        {
                            Description =
                                "JWT Authorization header using the Bearer scheme.\nEnter your token in the text input below.",
                            Name = "Authorization",
                            In = ParameterLocation.Header,
                            Type = SecuritySchemeType.Http,
                            Scheme = "Bearer",
                            BearerFormat = "JWT",
                        }
                    );
                    options.AddSecurityRequirement(
                        new OpenApiSecurityRequirement()
                        {
                            {
                                new OpenApiSecurityScheme
                                {
                                    Reference = new OpenApiReference
                                    {
                                        Type = ReferenceType.SecurityScheme,
                                        Id = "Bearer",
                                    },
                                    Scheme = "oauth2",
                                    Name = "Bearer",
                                    In = ParameterLocation.Header,
                                },
                                new List<string>()
                            },
                        }
                    );
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
            builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);
        }

        if (_useBearerToken)
        {
            builder
                .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = _bearerTokenSettings.ValidateIssuer,
                        ValidateAudience = true,
                        ValidAudience = _bearerTokenSettings.ValidAudience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = _bearerTokenSettings.SigningKey,
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

            builder
                .Services.AddAuthorizationBuilder()
                .SetFallbackPolicy(policy)
                .SetDefaultPolicy(policy);
        }

        // Registering services is not dependant on the order
        customServices?.Invoke(builder.Services);

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

        if (_useBearerToken)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        if (_useSwagger)
        {
            app.UseDefaultSwagger();
        }

        return new(app);
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
