using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Shared.Host.Json;

/// <summary>
/// System.Text.Json only writes a type discriminator when a value is serialized through the
/// polymorphic base type's metadata. Values declared as a derived type (e.g. a
/// <c>List&lt;Derived&gt;</c> or an endpoint returning <c>Derived</c>) are written without one,
/// although the OpenAPI spec says the discriminator is always present. This modifier gives every
/// derived type of a string-discriminated hierarchy its own single-entry polymorphism options so
/// it always writes it.
/// </summary>
public static class PolymorphicTypeInfoModifier
{
    private const string _defaultDiscriminatorProperty = "$type";

    /// <exception cref="InvalidOperationException">
    /// A derived type is sealed, generic or a value type, which System.Text.Json can't make polymorphic.
    /// </exception>
    /// <exception cref="NotSupportedException">Reflecting over the type's attributes failed.</exception>
    /// <exception cref="TypeLoadException">An attribute type could not be loaded.</exception>
    /// <exception cref="AmbiguousMatchException">A base type declares <see cref="JsonPolymorphicAttribute"/> more than once.</exception>
    /// <exception cref="TargetInvocationException">The type's interfaces could not be resolved.</exception>
    public static void WriteDiscriminatorForDerivedTypes(JsonTypeInfo typeInfo)
    {
        if (
            typeInfo.Kind != JsonTypeInfoKind.Object
            || typeInfo.Type.IsAbstract
            || typeInfo.PolymorphismOptions is not null
        )
            return;

        var baseTypes = new List<Type>();
        for (
            var baseType = typeInfo.Type.BaseType;
            baseType is not null;
            baseType = baseType.BaseType
        )
            baseTypes.Add(baseType);
        baseTypes.AddRange(typeInfo.Type.GetInterfaces());

        foreach (var candidate in baseTypes)
        {
            var discriminator = candidate
                .GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
                .FirstOrDefault(a => a.DerivedType == typeInfo.Type)
                ?.TypeDiscriminator;

            // Integer discriminators are left alone, the generated clients only model string ones
            if (discriminator is not string id)
                continue;

            if (typeInfo.Type.IsSealed || typeInfo.Type.IsGenericType || typeInfo.Type.IsValueType)
                throw new InvalidOperationException(
                    $"{typeInfo.Type} is a derived type of {candidate} but is sealed, generic or a value type, "
                        + "so System.Text.Json can't write its type discriminator when it is serialized as "
                        + "its own type. Make it a non-sealed class/record."
                );

            typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName =
                    candidate
                        .GetCustomAttribute<JsonPolymorphicAttribute>(inherit: false)
                        ?.TypeDiscriminatorPropertyName
                    ?? _defaultDiscriminatorProperty,
                DerivedTypes = { new JsonDerivedType(typeInfo.Type, id) },
            };
            return;
        }
    }
}
