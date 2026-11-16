using System.Reflection;
using System.Runtime.CompilerServices;
using Shared.Domain;
using StronglyTypedIds;

namespace Shared.Host.Test;

/// <summary>
/// Architecture tests enforce rules about the shape of the code that the compiler can't, by
/// inspecting the compiled types with reflection. Each one turns a convention from README.md
/// into a failing test, so a new type that breaks it is caught in CI instead of in review.
/// </summary>
public class ArchitectureTest
{
    private static readonly Assembly _domainAssembly = typeof(ExampleId).Assembly;

    private static IEnumerable<Type> StronglyTypedIds() =>
        _domainAssembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<StronglyTypedIdAttribute>() is not null);

    [Fact]
    public void ReflectionFindsTheTypesTheRulesAreCheckedAgainst()
    {
        // Without this, a refactor that makes the queries below find nothing would let every
        // other test in this class pass vacuously.
        Assert.Contains(typeof(ExampleId), StronglyTypedIds());
    }

    [Fact]
    public void StronglyTypedIdsCanBeMappedByTheEntityFrameworkValueConverter()
    {
        // The ValueConverterSelector in README.md matches ids by namespace and builds the
        // conversion from a private Value property and a private Guid constructor.
        var violations = StronglyTypedIds()
            .Where(t =>
                t.Namespace != typeof(ExampleId).Namespace
                || t.GetProperty(
                    "Value",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )?.PropertyType != typeof(Guid)
                || t.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, [typeof(Guid)])
                    is null
            )
            .Select(t => t.FullName);

        Assert.Empty(violations);
    }

    [Fact]
    public void DomainTypesHaveNoPublicSetters()
    {
        // See "Public Setters considered harmful" in README.md. `init` is allowed since it can
        // only be used while constructing the instance.
        var violations = _domainAssembly
            .GetExportedTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p =>
                p.SetMethod is { IsPublic: true } setter
                && !setter
                    .ReturnParameter.GetRequiredCustomModifiers()
                    .Contains(typeof(IsExternalInit))
            )
            .Select(p => $"{p.DeclaringType?.FullName}.{p.Name}");

        Assert.Empty(violations);
    }
}
