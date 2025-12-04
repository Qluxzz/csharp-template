# C# template

This template contains everything I normally use when building a C# application and comments about each component

# Directory.Build.props

This is the shared settings for all new projects, this ensures you don't have to setup anything when creating a new project in the solution, every warning and error and styling and linting should be inherited from this file to your project.

# Controllers

Controllers should always be using [Results type](https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-10.0#resultstresult1-tresultn-type). This ensures that the controller method only return what is defined in the return type.

IActionResult or IAction has no typing information and it's up to you as the developer to annotate your method with `[ProducesResponseType]` attributes and remember to update these whenever the endpoint has changed, otherwise you're lying to the consumer. Using Results removes this possibility entirely.

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
