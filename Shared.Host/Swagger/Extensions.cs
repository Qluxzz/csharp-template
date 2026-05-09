using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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
