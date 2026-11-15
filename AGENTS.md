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

Source: https://cbarrete.com/carmack.html — "John Carmack on inlined code and functional programming". Apply these when writing or refactoring code.

- **Inline single-use functions.** If a helper is called from exactly one place, inline it into the caller instead of extracting it. Prefer fewer, larger, sequential functions over a tree of tiny helpers. Sequential work should read top-to-bottom in one place — ignore the "functions should be a page or two long" rule of thumb. This is a default, not a transformation to apply mechanically: keep a single-use helper when its name genuinely clarifies intent, when it isolates an invariant worth pinning down, or when inlining leaves the caller buried in nesting. Extraction that adds a name you have to look up is not abstraction, it's indirection.
- **Collapse call sites.** Avoid `FullUpdate()` calling `PartialUpdateA()`/`PartialUpdateB()` when a caller just needs one part: if a function is called from many places, see if the full work can be done once in a single place and the unneeded part inhibited there. Merging call sites this way is only worth it when the shared path stays simple — if it grows a parameter for every caller, you have built the wrong abstraction (see below).
- **Centralize side effects.** Within the execution phase, prefer doing the operation and then inhibiting or ignoring the result over conditionally performing it in scattered places. Fewer conditional branches means consistent execution paths and fewer state surprises. This governs _where_ effects live, not whether the computation that decides them is pure.
- **Prefer pure functions.** Near-pure work should take inputs and return a value without reading or mutating global/ambient state. Pass in the one or two pieces of state it needs rather than reaching for globals, even if that means more parameter passing. Refactor toward purity on a continuum: "spaghetti-state → mostly-pure" is a big win; "almost-pure → fully-pure" is a small one.
- **Return copies, don't mutate.** Prefer a method that returns a transformed copy over one that mutates its receiver in place. This matters more in C# than it looks: mutable state behind a shared reference is what makes a `record`'s generated `Equals` lie (`docs/RECORDS.md`). The concern is *shared* mutable state in value-like domain data, not mutation as such — mutating something you privately own, where the domain genuinely is mutable, is still fine.

# Comments

- **A comment earns its place only by explaining a why the code cannot show.** Never restate what the code says. Keep a comment only when something invisible forces a non-obvious choice: a library or protocol constraint, a domain rule, a deliberate trade-off, or an invariant the types can't carry. When you *do* need to say "this can't happen", say it and say why — a bare `throw` with no explanation is worse than none.
- **Never narrate the change in the source.** Explanations of why the code changed belong in the commit message or PR. A reader six months out has no "before", so the contrast resolves to nothing.

# The wrong abstraction

Source: https://sandimetz.com/blog/2016/1/20/the-wrong-abstraction

- **Duplication is far cheaper than the wrong abstraction.** This is what reconciles Metz with Carmack's "inline single-use helpers" and "collapse call sites". Carmack asks you not to build a tree of tiny helpers _or_ to merge every call site into one flagged path; Metz asks you not to build one god-helper that everything bends to fit. Both push toward code that reads straight through.
- **Warning signs:** you are adding another parameter to a shared function, and then another conditional inside it to decide what to do with that parameter. It was right when written; it no longer is.
- **When you see those signs, go back.** Inline the abstraction into its callers, let each keep only the subset it uses, delete the rest, then re-isolate the duplication that is genuinely shared and re-extract a correct abstraction. Often the callers were running fairly unique code all along.
- **Don't be trapped by the sunk cost fallacy.** Complex and hard to understand is a signal to delete, not an asset to preserve.
- **Practical test:** if every call site passes the same value for a new parameter, or the parameter only ever selects one branch, the abstraction is wrong — drop the parameter, or inline the abstraction.

# Parsing

The worked examples live in `README.md` — read that section before writing anything that takes external input. The general principle, which the README assumes rather than restates:

- **Parse, don't validate.** A parser takes less-structured input and hands back more-structured output, preserving in its return type what it learned. A validator throws that knowledge away and makes every caller re-derive it.
- **Push the burden of proof up to the boundary, but no further.** Parse once at the entry point, then let the interior assume validity. That boundary may be the framework's own hook — `IParsable<T>` binding, a `JsonConverter` for `[FromBody]` — which is still the entrypoint. Don't parse below it.
- **Never act on partially-valid input.** Split work into a parse phase where all failures happen, then an execution phase.
- **Keep the published schema honest.** A refined input type still has to be documented as the `string`/`integer` it is on the wire, or every generated client churns. Don't guess what Swashbuckle will do — check it. This repo leans on `RespectRequiredConstructorParameters` and the enum converter for exactly this; see `DefaultOptions` in `Shared.Host/HostBuilder.cs` and the tests in `Shared.Host.Test/ResponseModelTest.cs`.
- **Don't let the types be driven by today's function.** If the representation only has a flag or an `int?` because one caller needed it, change the representation to what the domain actually is.
- **These are ideals, not hard requirements.** Capturing every invariant in the type system isn't always practical. Leave a guard in place when you must, but comment the invariant so the next person knows it's deliberate rather than forgotten.

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