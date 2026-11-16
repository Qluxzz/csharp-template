using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Shared.Domain;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shared.Host.Swagger;

/// <summary>
/// Documents every <see cref="IParsedString{TSelf}"/> as the string it is on the wire, using the
/// pattern and example the type declares, instead of the empty object Swashbuckle would infer.
/// </summary>
public sealed class ParsedStringSchemaFilter : ISchemaFilter, IParameterFilter
{
    // Reflecting over an already-loaded type and calling a method whose generic constraint was
    // just checked cannot throw here, so the exception analyzer is suppressed.
#pragma warning disable Ex0100
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concreteSchema || !IsParsedString(context.Type))
            return;

        var (pattern, example) = ((string, string))(
            typeof(ParsedStringSchemaFilter)
                .GetMethod(nameof(Describe), BindingFlags.NonPublic | BindingFlags.Static)
                ?.MakeGenericMethod(context.Type)
                .Invoke(null, null)
            ?? throw new UnreachableException(
                $"{nameof(Describe)} returned nothing for {context.Type.FullName}"
            )
        );

        concreteSchema.Properties?.Clear();
        concreteSchema.Required?.Clear();
        concreteSchema.AllOf?.Clear();
        concreteSchema.AnyOf?.Clear();
        concreteSchema.OneOf?.Clear();
        concreteSchema.AdditionalPropertiesAllowed = true;
        concreteSchema.Type = JsonSchemaType.String;
        concreteSchema.Pattern = pattern;
        concreteSchema.Examples = [JsonValue.Create(example)];
    }

    // Route and query values bound through TryParse are reported by ApiExplorer as plain
    // strings, so the schema filter above never sees the real type. Point the parameter at the
    // same component the bodies use.
    public void Apply(IOpenApiParameter parameter, ParameterFilterContext context)
    {
        if (
            parameter is OpenApiParameter concreteParameter
            && context.ParameterInfo?.ParameterType is { } type
            && IsParsedString(type)
        )
        {
            concreteParameter.Schema = context.SchemaGenerator.GenerateSchema(
                type,
                context.SchemaRepository
            );
        }
    }

    private static bool IsParsedString(Type type) =>
        type.GetInterfaces()
            .Any(i =>
                i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(IParsedString<>)
                && i.GenericTypeArguments[0] == type
            );
#pragma warning restore Ex0100

    private static (string Pattern, string Example) Describe<T>()
        where T : IParsedString<T> => (T.Pattern, T.Example);
}
