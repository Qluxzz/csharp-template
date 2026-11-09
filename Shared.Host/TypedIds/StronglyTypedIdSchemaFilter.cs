using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using StronglyTypedIds;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shared.Host.TypedIds;

/// <summary>
/// Our StronglyTypedIds are unwrapped when they are serialized
/// This schema filter makes it so that is reflected in the schema as well
/// </summary>
public sealed class StronglyTypedIdSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concreteSchema)
            return;

        var type = context.Type;

        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // Reflecting over an already-loaded type that carries a single, non-inheritable attribute,
        // and clearing the concrete (never read-only) schema collections, cannot throw at runtime
        // here. We suppress the exception analyzer rather than handle exceptions that cannot occur.
#pragma warning disable Ex0100
        var attr = underlyingType.GetCustomAttribute<StronglyTypedIdAttribute>();
        if (attr is null)
            return;

        // Without clearing this the strongly typed ids
        // are all created as a component instead of just a plain string
        // which hides their description and formatting
        concreteSchema.Properties?.Clear();
        concreteSchema.Required?.Clear();
        concreteSchema.AllOf?.Clear();
        concreteSchema.AnyOf?.Clear();
        concreteSchema.OneOf?.Clear();
#pragma warning restore Ex0100

        var prefix = InferPrefix(underlyingType.Name);

        concreteSchema.Type = JsonSchemaType.String;
        concreteSchema.Format = $"{prefix}uuid";

        concreteSchema.AddExtension(
            "x-strongly-typed-id",
            new JsonNodeExtension(
                new JsonObject { ["name"] = underlyingType.Name, ["prefix"] = prefix }
            )
        );

        var isNullable = underlyingType != type;
        if (isNullable)
        {
            concreteSchema.Type |= JsonSchemaType.Null;
        }
    }

    /// <summary>
    /// Derives a kebab-case prefix from a strongly-typed ID name.
    /// "ChecklistInstanceId" → "checklist-instance_"
    /// </summary>
    private static string InferPrefix(string typeName)
    {
        var baseName = typeName.EndsWith("Id", StringComparison.Ordinal)
            ? typeName[..^2]
            : typeName;

        // Insert a '-' at each lower→upper boundary, e.g. "ChecklistInstance" → "checklist-instance".
        var result = new StringBuilder(baseName.Length + 1);
        for (var i = 0; i < baseName.Length; i++)
        {
            if (i > 0 && char.IsUpper(baseName[i]) && char.IsLower(baseName[i - 1]))
                result.Append('-');
            result.Append(char.ToLowerInvariant(baseName[i]));
        }
        result.Append('_');

        return result.ToString();
    }
}
