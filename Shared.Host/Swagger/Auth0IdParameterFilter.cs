using Microsoft.OpenApi;
using Shared.Domain;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shared.Host.Swagger;

/// <summary>
/// Route and query values bound through <see cref="Auth0Id.TryParse"/> are reported by
/// ApiExplorer as plain strings, so the <c>MapType</c> registration for <see cref="Auth0Id"/>
/// never applies to them. This points those parameters at the same schema the bodies use.
/// </summary>
public sealed class Auth0IdParameterFilter : IParameterFilter
{
    public void Apply(IOpenApiParameter parameter, ParameterFilterContext context)
    {
        if (
            parameter is OpenApiParameter concreteParameter
            && context.ParameterInfo?.ParameterType == typeof(Auth0Id)
        )
        {
            concreteParameter.Schema = context.SchemaGenerator.GenerateSchema(
                typeof(Auth0Id),
                context.SchemaRepository
            );
        }
    }
}
