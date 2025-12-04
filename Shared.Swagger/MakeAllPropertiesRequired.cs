using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shared.Swagger;

/// <summary>
/// Filter that makes all response properties required
/// </summary>
public class MakeAllPropertiesRequired : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        MakeRequiredRecursively(schema);
    }

    private static void MakeRequiredRecursively(OpenApiSchema schema)
    {
        if (schema.Type == "object")
        {
            schema.Required ??= new HashSet<string>();
            foreach (var property in schema.Properties)
            {
                schema.Required.Add(property.Key);
                MakeRequiredRecursively(property.Value);
            }
        }
    }
}
