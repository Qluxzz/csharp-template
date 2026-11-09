# C# template

This template contains everything I normally use when building a C# application and comments about each component

# Directory.Build.props

This is the shared settings for all new projects, this ensures you don't have to setup anything when creating a new project in the solution, every warning and error and styling and linting should be inherited from this file to your project.

# Directory.Packages.props

This project uses centralized package management so all packages used and their versions are defined in this one file. Since we treat warnings as errors if multiple packages had different versions and you have a project dependency you will get an error when it's trying to resolve the package version since they differ. This solves it and simplifies the package version handling by just using a single version for that package in all of your projects.

# Formatting

[CSharpier](https://csharpier.com/) is an opinionated code formatter, think Prettier for JavaScript/TypeScript but for C#. The reasoning behind using CSharpier is the same with using Prettier, I don't care about the formatting as long as everyone formats their code the same way and I want it to be automatically formatted. This helps out tremendously in code reviews where you can see clearly what has changed instead of someone adding a line break somewhere.

# Pipeline

The included pipeline verifies all files are formatted using csharpier, and runs all tests.

# Analyzers

## Microsoft.VisualStudio.Threading.Analyzers:

This analyzer makes sure you're handling Tasks in C# correctly, i.e awaiting them correctly, since synchronously waiting on Task, ValueTask, or awaiters is dangerous and may cause dead locks.

## Tetractic.CodeAnalysis.ExceptionAnalyzers

Exceptions should as the name say, only be used for exceptional things, since exceptions is not required to be documented the caller of your function can't trust the return type since in addition to what the function says it returns it might also throw a bunch of exceptions.

This analyzer forces you to either catch exceptions within your method, or annotate that your method can throw these exceptions, so the caller of your method is then forced to either handle them or also document them on their method.

## Nullable.Extended.Analyzer

This analyzer forbids the null forgiving operator, using it indicates a modelling issue, if you want to say that you know this not to be null at this point, why don't you model your types correctly so they cannot be null at this point? Usually however using the following `?? throw new Exception("include as much info you need to debug the issue if it unexpectedly was null here")` is a much better solution to using the null forgiving operator which will just result in a NullReferenceException without any info to how the object looked at the time of the exception

# General

## Background Services

Background services doesn't make use of the unhandled exception middleware, so if an exception is thrown in one, it will by default kill the host. This can be changed to only kill the background service itself but that's something you probably don't want either. So always remember to wrap your calling code in a try catch so it won't be killed by an unexpected exception.

## Controllers

Controllers should always be using [Results type](https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-10.0#resultstresult1-tresultn-type). This ensures that the controller method only return what is defined in the return type.

`IActionResult` or `IAction` has no typing information and it's up to you as the developer to annotate your method with `[ProducesResponseType]` attributes and remember to update these whenever the endpoint has changed, otherwise you're lying to the consumer. Using Results removes this possibility entirely.

One downside of Results is that they don't compose very well, you can't return a method that returns a subset of the result your method returns so you have to map it so they map 1-1.

Another downside is that Results only prevent underfilling, meaning you return something you don't say you return. But there's nothing preventing you from documenting that you return twenty return types and then only use one in your method.

### Error handling in controllers

A common pattern I've seen is:

```csharp
public async Task<Results<Ok, StatusCodeHttpResult>> MyEndpoint()
{
    try
    {
        var result = await DoSomething();
        return TypedResults.Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error while calling the DoSomething function.");
        return TypedResults.StatusCode(500);
    }
}
```

On the surface this looks okay, we're hiding the real error so we don't leak any implementation details. The downside of this obfuscation is that it's always active, regardless of what `ASPNETCORE_ENVIRONMENT` you're running. That means if you're running everything locally, or you're a frontender working against a shared dev environment, you won't have a clue as to what went wrong and have to bug the backend programmer to read the logs.

What if we only could have this obfuscation enabled in production, but not in development?

With a [custom exception handler](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0#exception-handler-lambda) we can do just that. So now your controller method looks like this:

```csharp
public async Task MyEndpoint()
{
    var result = await DoSomething();
    return Ok(result);
}
```

And in your Program.cs file you instead have this:

```csharp
if (app.Environment.IsProduction())
{
    // Catch all unhandled exceptions and just return a non descriptive "Internal Server Error" message
    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = Text.Plain;

            await context.Response.WriteAsync("Internal Server Error");
        });
    });
}
```

## Enum parsing

There are some footguns when parsing Enums that is worth mentioning.

```csharp
enum Test {
  Foo = 0, // This is the default values if not defined manually
  Bar = 1
}

// From a string
Enum.TryParse<Test>("Foo", out var maybeEnum) // true

// From a number
Enum.TryParse<Test>("1337", out var maybeEnum) // true
```

Now you might say, wait a minute, I didn't define 1337 in the enum, we only have two defined values 0 and 1. Yeah well enums in C# just has a backing type of by default an int. Which means that any value from -2,147,483,648 to 2,147,483,647 is valid for your enum, weather you define it or not. You can change the default backing type to a smaller data type by doing `enum Test : ushort`, but it doesn't solve the issue that undefined values are still valid for the enum.

So when parsing from a number you have to do an additional check if the parsed value is actually defined in your enum.

```csharp
enum Test {
  Foo = 0,
  Bar = 1
}

// From a number
Enum.TryParse<Test>("1337", out var maybeEnum) && Enum.IsDefined(maybeEnum) // false
```

## Public Setters considered harmful

Much of the C# code I see has objects modeled as this:

```csharp
public class Foo {
    public int Bar { get; set; }
    public int Baz { get; set; }
    ...
}
```

So why is this a problem? Well, having setters on all properties means anything you send your object to can mutate your instance. This is also something you can't annotate on your method, that you will mutate the instance you pass.

If you don't see a problem with this, I'm sorry and I suggest you try out working in a functional language, where you can't mutate data and come back when you've been enlightened.

Before records, my classes looked as this:

```csharp
public class Foo {
    public int Bar { get; }
    public int Baz { get; }
    ...

    public Foo(int bar, int baz) {
        Bar = bar;
        Baz = baz;
    }
}
```

Which means the values for Bar and Baz can only be set once, when creating an instance of Foo, after that they are read only. With records (ignoring their own flavor of footguns), the equivalent is:

```csharp
public record Foo(int Bar, int Baz);
```

## Parse don't validate, primitive obsession

https://gist.github.com/Qluxzz/706043bc37d0d1030e1cd9d3841918f5

## Type-safe IDs

If every id in your system is a `Guid`, the compiler can't stop you from passing a user's id where an order's id was expected, and nothing stops you from swapping the arguments in `Transfer(Guid from, Guid to)`. The id is "just a `Guid`", so all the meaning lives in your head and in variable names.

The fix is to give every id its own type. Instead of `Guid`, a `UserId` is a `UserId` and an `OrderId` is an `OrderId`, and the two are not interchangeable:

```csharp
using StronglyTypedIds;

[StronglyTypedId]
public partial struct UserId { }

[StronglyTypedId]
public partial struct OrderId { }
```

```csharp
void Ship(OrderId order) { ... }

Ship(userId); // compile error, a UserId is not an OrderId
Ship(orderId); // fine

// PlaceOrder(UserId user, OrderId order) can no longer have its arguments swapped by accident
```

With the StronglyTypedIdSchemaFilter this also includes metadata in the OpenAPI specification which can be used together with openapi-typescript to generate types which are typed as `order_${string}` which ensures type safety in the frontend as well when passing identifiers around.

## .Single and .First considered harmful

The .Single and .First methods of an IEnumerable makes it very convenient to validate that there are at least one element or just a single element in a list.

These two methods however have a big downside, which is debuggability. When .Single or .First fails, you will get an InvalidOperationException with no more info, which means trying to debug for what thing the list failed this condition is hard.

It's always better to use the following pattern:

```csharp
var singleItem = obj.With.Deeply.Nested.List.SingleOrDefault() ?? throw new UnreachableException($"We always expected this list to contain a single element, here's the entire object so you can debug it and discuss if this should be possible or if it's the cause of a deeper problem. Data: {JsonSerializer.Serialize(obj)}");
```

## System.Text.Json.JsonSerializer.DeserializeAsync can return null, and doesn't validate that our defined fields were included in the json payload

The signature for `System.Text.Json.JsonSerializer.Deserialize<T>` is `T?`, so when does this actually return null? If `T` isn't the correct object? No, it only returns null when the JSON value you're trying to deserialize is literally `null`, never else can this return null. If you try to deserialize an invalid value, the value will be default value for the type, and not fail.

```csharp
record Test(int Value);

System.Text.Json.JsonSerializer.Deserialize<Test>("null") // null
System.Text.Json.JsonSerializer.Deserialize<Test>("""{"x":10}""") // Test { Value = 0 }
```

So how can we enforce that we deserialized what we said we wanted? Well, for null, you can't enforce that you get T and not T?, other than checking and throwing your own exception if the result is null, something like:

```csharp
System.Text.Json.JsonSerializer.Deserialize<Test>("null") ?? throw new UnreachableException("The Json data was literally \"null\"")
```

And wrap that in a proxy for all Deserialize methods the JsonSerializer exposes, and then forbid the usage of the normal method using the [Roslyn banned api analyzer](https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.BannedApiAnalyzers/BannedApiAnalyzers.Help.md).

For non empty json data, if you've been following my advice of never having any public setters and having constructors in all your classes, you can get this by just enabling `RespectRequiredConstructorParameters` in the `JsonSerializerOptions`

```csharp

// Class
public class TestClass {
    public int Value { get; } // See, no setter, can only be set once, when decoding the object

    public TestClass(int value) {
        Value = value;
    }
}

System.Text.Json.JsonSerializer.Deserialize<TestClass>(
    """{"x":10}""",
    new JsonSerializerOptions() {
        RespectRequiredConstructorParameters = true
    }
); // JsonException "JSON deserialization for type 'Test' was missing required properties including: 'Value'.


// Record
record TestRecord(int Value);

System.Text.Json.JsonSerializer.Deserialize<Test>(
    """{"x":10}""",
    new System.Text.Json.JsonSerializerOptions() {
        RespectRequiredConstructorParameters = true
    }
) // JSON deserialization for type 'Test' was missing required properties including: 'Value'.

```

# Entity Framework

## PostgreSQL

Entity Framework by default creates all tables and columns using Pascal Case casing.

For PostgreSQL specifically, this forces double-quotes to be added since unquoted identifiers are automatically converted to lower-case, and the intellisense usually stops working entirely depending on what tool you're using, it tries to autocomplete it wrong.

For this case you can use https://github.com/efcore/EFCore.NamingConventions and make it so all tables, column, indexes are created using snake_case instead.

Just be mindful of ASP.NET Identity and other libraries that override this behavior in the onModelCreating method of the context. https://github.com/efcore/EFCore.NamingConventions/issues/2

## Storing type-safe IDs

[Type-safe IDs](#type-safe-ids) travel over the wire as prefixed strings, but in the database you want the `Guid` they wrap stored in a real `uuid` column — not an `example_...` string. EF Core doesn't know how to map a `UserId` struct on its own, and registering a `ValueConverter` for every id by hand gets old fast.

Because every id lives in the single `Shared.Domain` namespace, and the template gives each one a private `Value` property and a private `Guid` constructor, you can register one `ValueConverterSelector` that converts _every_ id in that namespace to its backing `Guid`. Add a new id and it's covered automatically, no per-id registration:

```csharp
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shared.Domain;

namespace Shared.Database;

public class StronglyTypedIdValueConverterSelector(ValueConverterSelectorDependencies dependencies)
    : ValueConverterSelector(dependencies)
{
    // The dictionary in the base type is private, so we need our own one here.
    private readonly ConcurrentDictionary<
        (Type ModelClrType, Type ProviderClrType),
        ValueConverterInfo
    > _converters = new();

    private readonly string _idModelNameSpace =
        typeof(ExampleId).Namespace
        ?? throw new Exception("Expected ExampleId type to have a namespace");

    public override IEnumerable<ValueConverterInfo> Select(
        Type modelClrType,
        Type? providerClrType = null
    )
    {
        var baseConverters = base.Select(modelClrType, providerClrType);
        foreach (var converter in baseConverters)
        {
            yield return converter;
        }

        // Extract the "real" type T from Nullable<T> if required
        var underlyingModelType = UnwrapNullableType(modelClrType);
        var underlyingProviderType = UnwrapNullableType(providerClrType);

        // 'null' means 'get any value converters for the modelClrType'
        if (underlyingProviderType is null || underlyingProviderType == typeof(Guid))
        {
            var modelNameSpace =
                underlyingModelType?.Namespace
                ?? throw new Exception("Underlying model type was null!");
            if (modelNameSpace == _idModelNameSpace)
            {
                yield return _converters.GetOrAdd(
                    (underlyingModelType, typeof(Guid)),
                    _ =>
                    {
                        // Create conversion expressions
                        var valueProperty = underlyingModelType.GetProperty(
                            "Value",
                            System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance
                        );
                        if (valueProperty is null)
                            throw new InvalidOperationException(
                                $"Type {underlyingModelType.Name} must have a 'Value' property"
                            );

                        // Create: id => id.Value
                        var modelParam = Expression.Parameter(underlyingModelType, "id");
                        var convertToProvider = Expression.Lambda(
                            Expression.Property(modelParam, valueProperty),
                            modelParam
                        );

                        // Create: guid => new TId(guid)
                        var guidParam = Expression.Parameter(typeof(Guid), "guid");
                        var constructor = underlyingModelType.GetConstructor(
                            System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance,
                            [typeof(Guid)]
                        );
                        if (constructor is null)
                            throw new InvalidOperationException(
                                $"Type {underlyingModelType.Name} must have a constructor accepting Guid"
                            );

                        var convertFromProvider = Expression.Lambda(
                            Expression.New(constructor, guidParam),
                            guidParam
                        );

                        // Create the converter type
                        var converterType = typeof(ValueConverter<,>).MakeGenericType(
                            underlyingModelType,
                            typeof(Guid)
                        );

                        ValueConverter factory(ValueConverterInfo info) =>
                            (ValueConverter)(
                                Activator.CreateInstance(
                                    converterType,
                                    convertToProvider,
                                    convertFromProvider,
                                    info.MappingHints
                                )
                                ?? throw new Exception(
                                    $"Failed to create instance for type {converterType}"
                                )
                            );

                        return new ValueConverterInfo(underlyingModelType, typeof(Guid), factory);
                    }
                );
            }
        }
    }

    private static Type? UnwrapNullableType(Type? type)
    {
        if (type is null)
        {
            return null;
        }

        return Nullable.GetUnderlyingType(type) ?? type;
    }
}
```

Then register it once when configuring the `DbContext`, so it applies to the whole model:

```csharp
options
    .ReplaceService<IValueConverterSelector, StronglyTypedIdValueConverterSelector>()
    .UseNpgsql(/* ... */);
```

It reaches each id's backing `Guid` by reflecting over the private `Value` property and constructor the template generates, which is what lets one selector cover every id instead of a hand-written `ValueConverter` per type.
