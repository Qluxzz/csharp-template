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
