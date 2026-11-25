**Purpose:** How to build, format, and test this repo, what the build enforces on you, and the coding-style guidance that isn't already in `README.md`.

# Commands

CSharpier is a local tool (`.config/dotnet-tools.json`), not a command on `PATH`. Run `dotnet tool restore` once per clone. To fix formatting in place, run `dotnet tool run csharpier format .`.

While iterating, run the narrowest useful tests: a single project or class (for example, `dotnet test --filter-class Shared.Host.Test.ParsedTypeTest`). Before finishing, run both checks from the repo root:

```bash
dotnet tool run csharpier check .        # CI formatting gate
dotnet test                              # builds and runs all tests
```

`dotnet test` builds first and fails on any analyzer warning (see "What the build enforces"), so there is no separate `dotnet build` step.

**Lock files.** `Directory.Build.props` sets both `RestorePackagesWithLockFile` and `RestoreLockedMode`. Adding a package or bumping a version makes restore fail until the lock file is regenerated:

```bash
dotnet restore --force-evaluate          # run this after ANY package change, and commit the lock file
```

That is the only place to pass an override. Every other restore stays in locked mode, so an unintended dependency change fails loudly instead of silently rewriting the graph.

`dotnet add package` restores implicitly, so in locked mode it fails with `NU1004` on the version it was just told to write. Pass `--no-restore` to any command that changes a version and then regenerate the lock files in the separate step above. That includes `dotnet outdated -u`, which shells out to `dotnet add package`:

```bash
dotnet outdated -u --no-restore          # bump versions without the implicit restore
dotnet restore --force-evaluate          # then regenerate the committed lock files
```

`--no-restore` also skips dotnet-outdated's restore preview, so it applies a major version that conflicts with another package without complaint — Swashbuckle 11 needing `Microsoft.OpenApi` 3.x while `Microsoft.AspNetCore.OpenApi` 10 pins `< 3.0.0` is the current example, which is why Swashbuckle is pinned to 10.x in `Directory.Packages.props`. Review the report and run the full `dotnet test` afterwards. A failed upgrade can leave half-written lock files, which surface as `NU1101` rather than the underlying version conflict; fixing the versions and re-running `dotnet restore --force-evaluate` clears that (delete the lock files if it does not).

Packages are centrally versioned in `Directory.Packages.props`. Never put a `Version=` on a `PackageReference`.

# How to work in this repo

- **Read before you write.** Look at the existing implementation and its tests before changing behaviour. The design guidance below tells you what good code looks like here; it is not permission to rewrite code the task didn't ask you to touch.
- **Follow what's there.** Match the established patterns unless the task is specifically to change them.
- **Make the smallest coherent change.** Unrelated cleanup buries the actual diff and makes review harder. If you spot something worth fixing elsewhere, mention it rather than folding it in.
- **Test changed behaviour.** Extend the existing test project (`Shared.Host.Test`) alongside a change rather than leaving it unverified, including the failure cases your change introduces.
- **When sources disagree,** enforced configuration (`Directory.Build.props`, `BannedSymbols.txt`) wins for anything that fails the build. For advisory conventions, explicit guidance here wins over `.editorconfig` — see the static-field note below for the current example.
- **Report what you didn't run.** If a check couldn't be run, say so explicitly instead of implying the work is verified.

# What the build enforces

These are compiler errors here, not preferences. `TreatWarningsAsErrors` is on and four analyzer packages are referenced from `Directory.Build.props`.

- **No `!` (null-forgiving operator).** `Nullable.Extended.Analyzer`. If you believe a value can't be null at that point, the type is modelled wrong — fix the type. When a null really is impossible-but-unprovable, the house pattern is `?? throw new UnreachableException(...)`, carrying enough context that the next person can debug it rather than guess — the README's `.SingleOrDefault()` pattern is the reference example. Include the context that identifies *which* invariant broke, and only state you believe is safe to log; don't serialize secrets into an exception message.
- **Exceptions a caller can hit are documented.** `Tetractic.CodeAnalysis.ExceptionAnalyzers` reports `Ex0100` as an error when a method can throw without an `<exception cref="...">` tag; see `DefaultOptions` in `Shared.Host/HostBuilder.cs` for the shape. An exception doesn't appear in the signature, so the tag is what makes that exit visible at the call site; C# has no checked exceptions, so the caller still chooses whether to handle it or document it in turn. When the analyzer flags an exception that cannot occur, suppress `Ex0100` around the smallest block and say why in a comment (see `Auth0Id.TryParse` and `StronglyTypedIdSchemaFilter`). The test project suppresses `Ex0100` entirely.
- **Forward `CancellationToken`** (CA2016) and **observe async results** (`Microsoft.VisualStudio.Threading.Analyzers`; VSTHRD110 needs an explicit pragma on the one legitimate fire-and-forget, in `SharedHost.Dispose`).
- **Time comes from an injected `TimeProvider`.** `BannedSymbols.txt` bans `DateTime`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow` and `TimeProvider.System`. The host registers `TimeProvider.System` once; take `TimeProvider` as a dependency and call `GetUtcNow()`. Tests control time by registering their own `TimeProvider` through `customServices` (see `TimeProviderTest`).

# Conventions the code follows

`.editorconfig` is the source of truth and CSharpier owns formatting — don't hand-format. Naming rules there are `suggestion`-severity, so they don't fail the build, but follow them anyway.

- File-scoped namespaces; `using` outside the namespace.
- Primary constructors where a class just wraps a single dependency (`public SharedHost(WebApplication webApplication)`).
- Private fields are `_camelCase`. Private **static readonly** fields are also `_camelCase` in this repo (see the two declared near the bottom of `SharedHostBuilder` in `Shared.Host/HostBuilder.cs`) even though `.editorconfig` still says PascalCase for that group — follow the code.
- Records over classes for anything that is just data. No public setters; see `README.md` for why.
- Swagger publishes the XML comments on public API, so a `<summary>` becomes user-facing docs. `CS1591` is suppressed, so a missing summary doesn't fail the build; write one where it tells an API consumer something the name doesn't.
- Return expected failures as values (`TypedResults`/`Results<...>` in controllers), not exceptions.

# Coding style

Use these three goals to judge design choices. The build requirements above still apply.

1. **A reader can follow the code top to bottom** without jumping between helpers or tracing flags.
2. **Abstractions are easy to use correctly and hard to misuse.** Callers can do the right thing without remembering a rule the abstraction could carry for them.
3. **Pure decisions give the same result for the same inputs; clock reads, I/O, and mutation are explicit.**

When weighing these goals, **start with the clearest design that meets the requirement.** Add structure when it prevents a plausible mistake or removes a decision that callers would otherwise have to keep getting right. Weigh that benefit against what people now have to construct, convert, or keep in sync. If the extra code only moves the same decision elsewhere, prefer the simpler design.

Background reading, not rules: [Carmack on inlined code](https://cbarrete.com/carmack.html), [The wrong abstraction](https://sandimetz.com/blog/2016/1/20/the-wrong-abstraction), [How do I make this hard to misuse?](https://ozlabs.org/~rusty/index.cgi/tech/2008-03-30.html), [Making illegal states unrepresentable](https://fsharpforfunandprofit.com/posts/designing-with-types-making-illegal-states-unrepresentable/), [Primitive obsession](https://refactoring.guru/smells/primitive-obsession), [Parse, don't validate](https://lexi-lambda.github.io/blog/2019/11/05/parse-don-t-validate/).

## Code that reads straight through

- **A function names a sequence of steps, and its name has to earn the jump.** A helper called from one place usually costs more than it saves: the reader has to look it up to learn what the caller does. Inline it, and let sequential work read top to bottom in one longer function. Keep a helper when its name tells the reader something the code doesn't, when it isolates an invariant, or when inlining would bury the caller in nesting.
- **Shared code must stay simple for every caller.** Merging call sites is worth it while the shared path reads the same for all of them. When it starts growing a parameter per caller, plus a conditional to act on each one, it has become the wrong abstraction. The test: a parameter that every caller passes the same value for, or that only ever picks one branch, shouldn't exist. Inline the abstraction back into its callers, delete what each one doesn't use, and pull out only what is still genuinely shared. Duplication is cheaper than an abstraction everything has to bend around, and the effort already spent on it is no reason to keep it.

## Abstractions that carry the rules

The question to ask of any abstraction, whether a type, method or endpoint, is: *what does a caller have to know to use this correctly, and what happens if they don't?* These guarantees are listed strongest first (condensed from Rusty Russell's hard-to-misuse scale, linked above):

1. Misuse is impossible: it doesn't compile.
2. The obvious way to use it is the right way.
3. Misuse fails immediately and loudly, at the boundary.

If callers have to read the docs or the implementation to avoid a likely mistake, or an easy mistake silently produces a wrong result, look for a clearer shape. Sometimes that means inlining the abstraction back into its callers (see "Shared code must stay simple"). Primitive obsession, unparsed input passed inward and checked piecemeal ("shotgun parsing"), and flag arguments are common sources of such mistakes.

A type can carry a rule beyond the place where it's checked. A parser checks a value at runtime; the compiler then distinguishes the parsed type from the unchecked primitive.

- **A field accepts only what the domain accepts.** When you add a field or parameter, ask whether every value its primitive can hold is valid. For a `string`, that includes `""`, whitespace, a 1 MB blob, control characters, and the full text of *Moby Dick*. For an `int`, that includes `0`, negatives, and `int.MaxValue`. For a `Guid`, that includes the id of a different entity. Consider a domain type when callers rely on a format, range or identity. For example, `Auth0Id` uses `Parse`/`TryParse` so callers don't have to recognise a valid id themselves (see Parsing at the boundary below). Even a single use can justify a type if a mistake would be costly. For a value checked and consumed in one place, a local guard may be clearer.
- **Valid combinations are their own cases.** When fields are only valid together in certain combinations, every consumer has to know the rule. Examples: two nullable fields where at least one must be set, a `Status` enum plus fields that only mean something for one status, or a `bool` that decides which other fields are filled in. Model each valid combination as a case of a closed hierarchy instead:

  ```csharp
  // "A contact needs an email, a postal address, or both" — but nothing stops both being null.
  public record Contact(string Name, Email? Email, PostalAddress? Address);

  // Each valid combination is a case; the invalid one has no constructor.
  public abstract record ContactInfo
  {
      private ContactInfo() { } // closes the hierarchy: no cases can be added outside this type
      public sealed record EmailOnly(Email Email) : ContactInfo;
      public sealed record PostOnly(PostalAddress Address) : ContactInfo;
      public sealed record EmailAndPost(Email Email, PostalAddress Address) : ContactInfo;
  }
  public record Contact(string Name, ContactInfo ContactInfo);
  ```

  This template targets `net10.0` (C# 14), which can't check that a `switch` covers every case. A `switch` over `ContactInfo` therefore still needs a `_ => throw new UnreachableException(...)` arm, and adding a case doesn't cause compile errors at the call sites. The rule still lives in one definition instead of in every caller. C# 15 (.NET 11) adds `closed` hierarchies and `union` types, which make the compiler check that a `switch` covers every case. When the template moves to `net11.0`, mark hierarchies like `ContactInfo` as `closed`, use `union` to combine types you don't own, and drop the discard arms.
- **Each fact has one owner; everything else is derived.** Storing the same knowledge twice is another way to make an impossible state representable: the copies can disagree. If a value can be computed from other values, compute it rather than storing it. A record holding both `Lines` and `Total`, or a `Status` alongside the fields that determine it, can contradict itself, while a computed `Total` can't. The same applies across artifacts: the C# type owns the API contract, so the OpenAPI document and client types are generated from it, never hand-written. When a fact truly must be written twice, add a test that compares the copies so drift fails the build. This is DRY in its original sense of one authoritative representation per piece of knowledge, not "never write similar-looking code twice". Similar code is covered by "Shared code must stay simple" above.
- **A value says what it means at the call site.** `Export(report, true)` tells the reader nothing. First, ask whether the flag should exist at all. If it only picks one branch, it is often two functions joined together (see above). A named argument can clarify one call. If the choice needs to be clear even when callers omit the argument name, use a two-value enum: `Export(report, Compression.Enabled)`. This also leaves room for a third case. If the flag decides which other fields are set, use cases (above), not an enum. A `bool` is fine when the member's name already says what it means, such as `IsActive` or the result of `TryParse`. If an enum crosses the API boundary, read the enum parsing section in `README.md` first.
- **No "remember to".** Put required inputs in the constructor and steps that must happen together in one method. Make the default or zero value valid or choose a representation that rules it out.

  Distinct id types catch an `OrderId` passed as an `AccountId`, not two `AccountId`s swapped. Named arguments (`Transfer(from: payerId, to: payeeId)`) make the direction visible; role types such as `SourceAccountId` help when they preserve a direction already established earlier. Wrapping two ids just before the call does neither better.

- **Model the domain, not today's caller.** If a type only has a flag or an `int?` because one function needed it, change the type to match what the domain actually is.

### Parsing at the boundary

The recipe for a parsed type lives in the "Custom parsed types" section of `README.md`, and `Shared.Domain/Auth0Id.cs` is the reference implementation. Read both before writing anything that takes external input, and copy that shape rather than a description of it here.

- **Parse external input once, at the edge.** Turn the whole input into domain types or reject it with an explicit error before acting on any of it. Pass the typed values inward so callers don't repeat format, length or range checks. A well-formed order can still fail if an item is out of stock.

  Edges include HTTP requests, SignalR hub arguments, queue messages, external API responses, database rows, configuration, files and CLI arguments. For HTTP, use `IParsable<T>` model binding for route values or a `JsonConverter` for a parsed type in a `[FromBody]` payload. A type needs a `JsonConverter` only if it crosses JSON.

- **Keep the published schema honest.** A parsed type must still be documented as the `string`/`integer` it is on the wire, or every generated client changes. Don't guess what Swashbuckle will do; check it. See `MapType<Auth0Id>` and `Auth0IdParameterFilter` in `Shared.Host/Swagger/` (route values bound through `TryParse` need the filter too), `RespectRequiredConstructorParameters` and the enum converter in `DefaultOptions` (`Shared.Host/HostBuilder.cs`), and the tests in `Shared.Host.Test/ResponseModelTest.cs` and `IsDocumentedAsAStringInSwagger` in `Shared.Host.Test/ParsedTypeTest.cs`.

## State and effects you can see

- **Computation takes inputs and returns a value.** Pass in the one or two pieces of state it needs instead of reading or mutating global or ambient state, even if that means more parameters. For time, read `GetUtcNow()` from the injected `TimeProvider` where the operation runs and pass the result into decisions such as `IsExpired(expiresAt, now)`. Getting from tangled state to mostly pure is a big win. Getting from mostly pure to fully pure is a small one.
- **Side effects happen in one place.** Decide with pure logic, then perform the effect in a single place, rather than running it conditionally from scattered places. Fewer places that act mean fewer surprises about state.
- **Value-like data is returned as a copy, not mutated.** Mutable state behind a shared reference is what makes a `record`'s generated `Equals` lie (`docs/RECORDS.md`). Mutating something you own privately, where the domain really is mutable, is fine.

# Comments

- **A comment earns its place only by explaining a why the code cannot show.** Never restate what the code says. Keep a comment only when something invisible forces a non-obvious choice: a library or protocol constraint, a domain rule, a deliberate trade-off, or an invariant the types can't carry. When you *do* need to say "this can't happen", say it and say why — a bare `throw` with no explanation is worse than none.
- **Never narrate the change in the source.** Explanations of why the code changed belong in the commit message or PR. A reader six months out has no "before", so the contrast resolves to nothing.

# Enforcing the rules

Prefer the earliest check that expresses the rule clearly:

1. **The type system.** When a rule fits a type or method signature, the compiler can catch misuse at every call. Examples: a strongly typed id, a record with no public setters, `Results<...>` instead of `IActionResult`, a parsed type instead of a validated `string`. See "Abstractions that carry the rules" above.
2. **An analyzer or banned symbol.** When the rule is "never call X", `BannedSymbols.txt` or an existing analyzer turns it into a build error.
3. **An architecture test.** When the rule spans types and the compiler can't express it — "every id lives in this namespace and has a private `Guid` constructor", "domain types have no public setters", "every endpoint declares its authorization" — add a reflection-based test to `Shared.Host.Test/ArchitectureTest.cs`. A convention that only lives in a README or in review comments will eventually be broken; a test that names the offending type won't let it.
4. **A runtime guard.** Check the value where the relevant facts are known. A guard is often the clearest choice for a rule that depends on runtime state or applies in just one place. Explain the invariant in a comment when the reason for the check isn't apparent.

For test naming and verification, see [Tests](#tests). When writing an architecture test:

- **Explain why the rule exists.** Point to the README section or the code that depends on it, so whoever trips it knows whether to fix the type or question the rule.
- **Report every offending type.** Collect all violations and assert on the list (`Assert.Empty(violations)`). Temporarily add a violating type, check that the failure names it, then remove it.
- **Guard against empty reflection results.** Add a rule alongside the first type it applies to. When a rule queries a new set of types, extend `ReflectionFindsTheTypesTheRulesAreCheckedAgainst` to assert that it discovers a known type.

# Tests

Tests pin down behaviour so the implementation can change freely. A test that breaks under a correct refactor is a defect in the test: it sits at too low a level. Move it up to where the outcome is visible instead of rewriting it against the new internals. Tests should change only when behaviour changes, including the contract at a boundary (API payload, SignalR message, emitted event, endpoint response, published OpenAPI schema).

- **Test at the highest level that stays fast and can still reach the cases**: an HTTP endpoint through `TestServer` (see `ResponseModelTest`), a SignalR hub. Go lower only for a unit with a contract of its own, such as pure domain logic with many edge cases or a converter shared by several callers (see `SerializesAndDeserializesAsPrefixedString` in `TypedIdTest`), and then drive it through its public API. Never test private helpers or internal state directly.
- **Assert outcomes**: return values, persisted state, response bodies, published schemas, and what crosses a system boundary. Do not assert which in-process collaborators were called, how often, or in what order.
- **Mock only what the code does not own**: external systems, the network or API client, and the clock (`TimeProvider`). Run in-process collaborators for real, or use a fake. Verifying a call on a boundary mock is fine when that call *is* the behaviour, for example the command sent to an external system or the request payload sent to an API. "Own" refers to the responsibility being replaced, not where the code lives: a project interface that wraps an external transport is a boundary, and replacing it through a real DI seam is fine. Use a real database (Testcontainers) rather than mocking repositories, `DbContext` or `IQueryable`; an in-memory query runs as LINQ to objects and misses translation failures and SQL semantics.
- **Don't shape production code for tests.** That means no `InternalsVisibleTo`, visibility changes, flags or parameters that exist only so a test can reach in.
- **Name tests after the rule they protect**, not the method under test (`EnumsShouldBeReturnedAsStrings`, `UnauthorizedAccessIsNotAllowedIfUseBearerTokenBeenCalled`).
- **Before finishing, check both directions.** Would the test still pass if the implementation were rewritten correctly? Would it fail if the behaviour broke? Both answers must be yes. If you are unsure about the second, break the code briefly, watch the test fail, then revert the break and rerun.

# Where the rest of the house style lives

`README.md` is written to serve humans and agents alike — prefer extending it over duplicating it here.

- `README.md` — controllers and `TypedResults`, error handling middleware, enum parsing footguns, public setters, `.Single`/`.First`, `JsonSerializer.Deserialize` nullability, Entity Framework naming, the `IQueryable` repository pattern.
- `docs/RECORDS.md` — record equality and the reference-equality trap.
- `Directory.Build.props` / `BannedSymbols.txt` / `.editorconfig` — the enforced configuration. Read these rather than guessing at the rules.