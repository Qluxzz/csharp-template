# C# template

This template contains everything I normally use when building a C# application and comments about each component

# Directory.Build.props

This is the shared settings for all new projects, this ensures you don't have to setup anything when creating a new project in the solution, every warning and error and styling and linting should be inherited from this file to your project.

# Controllers

Controllers should always be using [Results type](https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-10.0#resultstresult1-tresultn-type). This ensures that the controller method only return what is defined in the return type.

IActionResult or IAction has no typing information and it's up to you as the developer to annotate your method with `[ProducesResponseType]` attributes and remember to update these whenever the endpoint has changed, otherwise you're lying to the consumer. Using Results removes this possibility entirely.

## Error handling in controllers

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

On the surface this looks okay, we're hiding the real error so we don't leak any implementation details. The downside of this obfuscation is that it's always active, regardless of what ASPNETCORE_ENVIRONMENT you're running. That means if you're running everything locally, or you're a frontender working against a shared dev environment, you won't have a clue as to what went wrong and have to bug the backend programmer to read the logs.

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

# Formatting

[CSharpier](https://csharpier.com/) is an opinionated code formatter, think Prettier but for C#. The reasoning behind using CSharpier is the same with using Prettier, I don't care about the formatting as long as everyone formats their code the same way and I want it to be automatically formatted. This helps out tremendously in code reviews where you can see clearly what has changed instead of someone adding a line break somewhere.

# Analyzers

## Microsoft.VisualStudio.Threading.Analyzers:

This analyzer makes sure you're handling Tasks in C# correctly, i.e awaiting them correctly, since synchronously waiting on Task, ValueTask, or awaiters is dangerous and may cause dead locks.

## Tetractic.CodeAnalysis.ExceptionAnalyzers

Exceptions should as the name say, only be used for truly unexpected things, since exceptions is not required to be documented the caller of your function can't trust the return type since in addition to what the function says it returns it might also throw a bunch of exceptions.

This analyzer forces you to either catch exceptions within your method, or annotate that your method can throw these exceptions.

## Nullable.Extended.Analyzer

This analyzer forbids the null forgiving operator, using it indicates a modelling issue, if you want to say that you know this not to be null at this point, why don't you model your types correctly so they cannot be null at this point? Usually however using the following `?? throw new Exception("include as much info you need to debug the issue if it unexpectedly was null here")` is a much better solution to using the null forgiving operator which will just result in a NullReferenceException without any info to how the object looked at the time of the exception

# Background Services

Background services doesn't make use of the unhandled exception middleware, so if an exception is thrown in one, it will by default kill the host. This can be changed to only kill the background service itself but that's something you probably don't want either. So always remember to wrap your calling code in a try catch so it won't be killed by an unexpected exception.

# General

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
