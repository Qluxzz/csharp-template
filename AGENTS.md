**Purpose:** How to build, format, and test this repo, what the build enforces on you, and the coding-style guidance that isn't already in `README.md`.

# Commands

CSharpier is a local tool (`.config/dotnet-tools.json`), so it is never on `PATH` — always go through `dotnet tool run`.

```bash
dotnet tool restore                      # required once per clone, before csharpier works
dotnet tool run csharpier check .        # CI gate — run before you call anything done
dotnet tool run csharpier format .       # fix formatting in place
dotnet test                              # builds and runs all tests
```

`TreatWarningsAsErrors` is on, so `dotnet test` also fails on any analyzer warning. There is no separate `dotnet build` step to run first.

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

While iterating, run the narrowest useful scope — a single test project, or `dotnet test --filter <name>` — and run the full `dotnet test` plus `csharpier check .` before you call the work done.

# How to work in this repo

- **Read before you write.** Look at the existing implementation and its tests before changing behaviour. The design guidance below tells you what good code looks like here; it is not permission to rewrite code the task didn't ask you to touch.
- **Follow what's there.** Match the established patterns unless the task is specifically to change them.
- **Make the smallest coherent change.** Unrelated cleanup buries the actual diff and makes review harder. If you spot something worth fixing elsewhere, mention it rather than folding it in.
- **Test changed behaviour.** Extend the existing test project (`Shared.Host.Test`) alongside a change rather than leaving it unverified, including the failure cases your change introduces.
- **When sources disagree,** enforced configuration (`Directory.Build.props`, `BannedSymbols.txt`) wins for anything that fails the build. For advisory conventions, explicit guidance here wins over `.editorconfig` — see the static-field note below for the current example.
- **Report what you didn't run.** If a check couldn't be run, say so explicitly instead of implying the work is verified.

# What the build enforces

These are compiler errors here, not preferences. `TreatWarningsAsErrors` is on and four analyzer packages are referenced from `Directory.Build.props`.

- **No `!` (null-forgiving operator).** `Nullable.Extended.Analyzer`. If you believe a value can't be null at that point, the type is modelled wrong — fix the type. When a null really is impossible-but-unprovable, the house pattern is `?? throw new UnreachableException(...)`, carrying the offending object so the next person can debug it rather than guess — the README's `.SingleOrDefault()` pattern is the reference example. Include the context that identifies *which* invariant broke, and only state you believe is safe to log; don't serialize secrets into an exception message.
- **Exceptions only for exceptional things, and always documented.** `Tetractic.CodeAnalysis.ExceptionAnalyzers` reports `EXC0100` as an error when a method can throw without an `<exception cref="...">` tag — no configuration needed, it applies to every relevant throwing method. The tag is documentation, not a compiler-enforced catch obligation: C# has no checked exceptions, so what it actually forces is that the next person has to carry the exceptional case forward in their own docs or handle it. See `DefaultOptions` in `Shared.Host/HostBuilder.cs` for the shape. This is also why controllers return `TypedResults`/`Results<...>` rather than catching-and-500-ing inline.
- **Forward `CancellationToken`** (CA2016) and **observe async results** (`Microsoft.VisualStudio.Threading.Analyzers`; VSTHRD110 needs an explicit pragma on the one legitimate fire-and-forget, in `SharedHost.Dispose`).
- **`DateTime` and `DateTimeOffset.Now` are banned** (`BannedSymbols.txt`). Use `DateTimeOffset.UtcNow`.
- **`GenerateDocumentationFile` is on.** XML comments on public API are picked up by Swagger, so a `<summary>` you write here becomes user-facing docs.

# Conventions the code follows

`.editorconfig` is the source of truth and CSharpier owns formatting — don't hand-format. Naming rules there are `suggestion`-severity, so they don't fail the build, but follow them anyway.

- File-scoped namespaces; `using` outside the namespace.
- Primary constructors where a class just wraps a single dependency (`public SharedHost(WebApplication webApplication)`).
- Private fields are `_camelCase`. Private **static readonly** fields are also `_camelCase` in this repo (see the two declared near the bottom of `SharedHostBuilder` in `Shared.Host/HostBuilder.cs`) even though `.editorconfig` still says PascalCase for that group — follow the code.
- Records over classes for anything that is just data. No public setters; see `README.md` for why.

# Coding style

The guide has three goals. Each rule below serves one of them. When a rule seems to argue against a goal in a specific case, follow the goal.

1. **A reader can follow the code top to bottom** without jumping between helpers or tracing flags.
2. **The types carry the rules**, so a value that breaks a business rule can't be constructed, and no caller has to remember the rule.
3. **State and side effects stay where you can see them**, so the same input gives the same result.

Background reading, not rules: [Carmack on inlined code](https://cbarrete.com/carmack.html), [The wrong abstraction](https://sandimetz.com/blog/2016/1/20/the-wrong-abstraction), [Making illegal states unrepresentable](https://fsharpforfunandprofit.com/posts/designing-with-types-making-illegal-states-unrepresentable/), [Primitive obsession](https://refactoring.guru/smells/primitive-obsession), [Parse, don't validate](https://lexi-lambda.github.io/blog/2019/11/05/parse-don-t-validate/).

## Code that reads straight through

- **A function names a sequence of steps, and its name has to earn the jump.** A helper called from one place usually costs more than it saves: the reader has to look it up to learn what the caller does. Inline it, and let sequential work read top to bottom in one longer function. Keep a helper when its name tells the reader something the code doesn't, when it isolates an invariant, or when inlining would bury the caller in nesting.
- **Shared code must stay simple for every caller.** Merging call sites is worth it while the shared path reads the same for all of them. When it starts growing a parameter per caller, plus a conditional to act on each one, it has become the wrong abstraction. The test: a parameter that every caller passes the same value for, or that only ever picks one branch, shouldn't exist. Inline the abstraction back into its callers, delete what each one doesn't use, and pull out only what is still genuinely shared. Duplication is cheaper than an abstraction everything has to bend around, and the effort already spent on it is no reason to keep it.

## Types that carry the rules

A rule that lives in a type is checked once, by the compiler. A rule that lives in callers is checked by each caller, and one of them eventually won't. The default for new data is a type that can only hold valid values.

- **A field accepts only what the domain accepts.** When you add a field or parameter, ask whether every value its primitive can hold is valid. For a `string`, that includes `""`, whitespace, a 1 MB blob, control characters, and the full text of *Moby Dick*. For an `int`, that includes `0`, negatives, and `int.MaxValue`. For a `Guid`, that includes the id of a different entity. If any of those must be rejected (because of a format, a length, a character set, a range, or what the value refers to), the field is not that primitive. Make it a `[ParsedString]` newtype (see Parsing at the boundary below); the only code it needs is the rule itself. Emails, slugs, country codes, amounts with a currency, and entity ids are all their own types. Unlike a single-use function, a single-use type is still worth having. A function only names steps, but a type carries its rule everywhere the value goes.
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
- **A value says what it means at the call site.** `Export(report, true)` tells the reader nothing. First, ask whether the flag should exist at all. If it only picks one branch, it is often two functions joined together (see above). If it should exist, use a two-value enum: `Export(report, Compression.Enabled)`. This also leaves room for a third case. Named arguments don't solve it, because callers can leave the name out. If the flag decides which other fields are set, use cases (above), not an enum. A `bool` is fine when the member's name already says what it means, such as `IsActive` or the result of `TryParse`. If an enum crosses the API boundary, read the enum parsing section in `README.md` first.
- **Model the domain, not today's caller.** If a type only has a flag or an `int?` because one function needed it, change the type to match what the domain actually is.

### Parsing at the boundary

The worked examples live in `README.md`. Read that section before writing anything that takes external input. A parsed string type is a `sealed partial record` with `[ParsedString(pattern, example)]` and one method, `private static T? TryParse(string value)`, which returns `null` for invalid input and otherwise normalizes the value and calls the generated private constructor. `Shared.Domain.Generators` generates the rest: the constructor, `Parse`, the public `TryParse`, `ToString`, `IParsedString<T>` (picked up by `ParsedStringSchemaFilter` for the OpenAPI schema), and the JSON converter. A type with the wrong shape is a `PS000x` compile error. `Shared.Domain/Auth0Id.cs` is the reference; copy it. Generated code is written to `obj/generated` if you need to read it. The generator only handles strings: for other primitives, write `IParsable<T>` and a `JsonConverter` by hand.

- **Parse, don't validate.** A parser turns loose input into a type that records what it checked. A validator checks the input and then throws that knowledge away, so every caller has to check again.
- **Parse once, at the entry point.** That entry point may be the framework's own hook, such as `IParsable<T>` binding or a `JsonConverter` for `[FromBody]`. Code past the entry point assumes the input is valid and doesn't parse again.
- **Never act on partially-valid input.** All failures happen during parsing, before any work starts.
- **Keep the published schema honest.** A parsed type must still be documented as the `string`/`integer` it is on the wire, or every generated client changes. Don't guess what Swashbuckle will do; check it. See `RespectRequiredConstructorParameters` and the enum converter in `DefaultOptions` (`Shared.Host/HostBuilder.cs`), and the tests in `Shared.Host.Test/ResponseModelTest.cs`.

## State and effects you can see

- **Computation takes inputs and returns a value.** Pass in the one or two pieces of state it needs instead of reading or mutating global or ambient state, even if that means more parameters. Getting from tangled state to mostly pure is a big win. Getting from mostly pure to fully pure is a small one.
- **Side effects happen in one place.** Prefer always running the operation and then ignoring or discarding the result over running it conditionally from scattered places. Fewer branches mean fewer surprises about state. This rule is about where effects happen. The logic that decides them should still be pure.
- **Value-like data is returned as a copy, not mutated.** Mutable state behind a shared reference is what makes a `record`'s generated `Equals` lie (`docs/RECORDS.md`). Mutating something you own privately, where the domain really is mutable, is fine.

# Comments

- **A comment earns its place only by explaining a why the code cannot show.** Never restate what the code says. Keep a comment only when something invisible forces a non-obvious choice: a library or protocol constraint, a domain rule, a deliberate trade-off, or an invariant the types can't carry. When you *do* need to say "this can't happen", say it and say why — a bare `throw` with no explanation is worse than none.
- **Never narrate the change in the source.** Explanations of why the code changed belong in the commit message or PR. A reader six months out has no "before", so the contrast resolves to nothing.

# Enforcing the rules

Enforce a rule at the earliest point that can catch it, in this order:

1. **The type system.** Breaking the rule is a compiler error. Examples: a strongly typed id, a record with no public setters, `Results<...>` instead of `IActionResult`, a parsed type instead of a validated `string`. This is always the first choice. See "Types that carry the rules" above.
2. **An analyzer or banned symbol.** When the rule is "never call X", `BannedSymbols.txt` or an existing analyzer turns it into a build error.
3. **An architecture test.** When the rule spans types and the compiler can't express it — "every id lives in this namespace and has a private `Guid` constructor", "domain types have no public setters", "every endpoint declares its authorization" — add a reflection-based test to `Shared.Host.Test/ArchitectureTest.cs`. A convention that only lives in a README or in review comments will eventually be broken; a test that names the offending type won't let it.
4. **A runtime guard**, only when none of the above can reach it, with a comment stating the invariant so the next person knows it's deliberate.

When writing an architecture test:

- **Name it after the rule and point at why it exists** (the README section, or the code that depends on it), so whoever trips it knows whether to fix their type or question the rule.
- **Collect every violation and assert on the list** (`Assert.Empty(violations)`), so the failure names each offending type instead of stopping at the first.
- **Guard against passing vacuously.** A reflection query that silently finds nothing after a refactor makes every rule pass. Keep `ReflectionFindsTheTypesTheRulesAreCheckedAgainst` asserting that a known type is discovered, and extend it when you add a query over a new set of types.
- **Don't add a rule with nothing to check yet.** A test over zero types proves nothing; add it alongside the first type it applies to.
- **Break it once.** Add a type that violates the rule, watch the test fail with a useful message, then remove it — the same "check both directions" rule as any other test.

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