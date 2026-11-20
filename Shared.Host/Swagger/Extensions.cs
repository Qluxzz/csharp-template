using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shared.Domain;
using Shared.Host.TypedIds;

namespace Shared.Host.Swagger;

public static class Extensions
{
    public static IServiceCollection AddSwagger(
        this IServiceCollection services,
        Action<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>? custom = null
    )
    {
        services.AddSwaggerGen(options =>
        {
            // This makes it so doc comments is included in the generated swagger
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

            options.NonNullableReferenceTypesAsRequired();
            options.SupportNonNullableReferenceTypes();

            // Strongly-typed IDs serialize as their unwrapped, prefixed string (e.g. "example_<guid>").
            // This filter makes the OpenAPI schema report them as that string instead of an object.
            options.SchemaFilter<StronglyTypedIdSchemaFilter>();

            // Auth0Id is a class with no public properties, so Swashbuckle would publish it as an
            // empty object, but on the wire it is a string.
            options.MapType<Auth0Id>(() =>
                new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Pattern = Auth0Id.Pattern,
                    Examples = [JsonValue.Create(Auth0Id.Example)],
                }
            );
            options.ParameterFilter<Auth0IdParameterFilter>();

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
