using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Host.Swagger;

public static class Extensions
{
    private static readonly JsonNamingPolicy _defaultNamingPolicy = JsonNamingPolicy.CamelCase;

    private static readonly JsonStringEnumConverter _jsonStringEnumConverter = new(
        namingPolicy: _defaultNamingPolicy,
        allowIntegerValues: false
    );

    /// <exception cref="NotSupportedException">Should never happen</exception>
    private static JsonSerializerOptions DefaultOptions(JsonSerializerOptions options)
    {
        // All parameters in the constructor must exist in the JSON
        options.RespectRequiredConstructorParameters = true;
        // Always serialize as camelCase
        options.PropertyNamingPolicy = _defaultNamingPolicy;
        // Ignore casing during deserialization, since we don't always own the API we're calling, we can't enforce their casing.
        // If the API you're calling is using something like snake_case or kebab-case or another not just casing based naming policy,
        options.PropertyNameCaseInsensitive = true;

        // Without this all fields which aren't included in the JSON response will be set to default(T)
        // Which is usually not what you want, but you want to know that the data was invalid.

        // Also recommended to be set to true for new projects by Microsoft
        options.RespectRequiredConstructorParameters = true;

        options.Converters.Add(_jsonStringEnumConverter);

        return options;
    }

    public static IServiceCollection AddSwagger(
        this IServiceCollection services,
        Action<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>? custom = null
    )
    {
        // JSON OPTIONS
        // https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/2293

        // This makes Swagger report the enums as strings
        services
            .AddControllers()
            .AddJsonOptions(options => DefaultOptions(options.JsonSerializerOptions));

        // This is required for the controllers to actually return them as strings
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            DefaultOptions(options.SerializerOptions)
        );

        services.AddSwaggerGen(options =>
        {
            // This makes it so doc comments is included in the generated swagger
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

            options.NonNullableReferenceTypesAsRequired();
            options.SupportNonNullableReferenceTypes();

            if (custom is not null)
                custom(options);
        });

        return services;
    }

    public static IApplicationBuilder UseDefaultSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.DefaultModelsExpandDepth(3);
            options.DefaultModelExpandDepth(3);
            // The most common use case when we use Swagger is to try it out instead of looking at the types, since we're using type generation
            // So having this enabled by default makes it one less click to test the endpoints
            options.EnableTryItOutByDefault();
            // This makes it so if you reload the Swagger page, you don't have to enter your credentials again (if using credentials)
            // However, if you get a 401 you need to manually clear the token and re-enter your new ones.
            options.EnablePersistAuthorization();
        });

        return app;
    }
}
