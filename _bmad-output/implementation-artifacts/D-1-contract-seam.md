---
baseline_commit: 1d1e61f3fde973832ea845aadb719a9d9ffc18b4
---

# Story D.1: Contract Seam — ICommandContract + REST routing attributes

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain-module author building on Hexalith.EventStore**,
I want **a marker interface for command messages plus declarative REST-routing attributes in `Hexalith.EventStore.Contracts`**,
so that **a later Roslyn source generator (Epic D, stories D2–D4) can reliably discover my commands and queries and emit typed REST controllers — replacing hand-written controllers and BFF wrappers**.

## Story Context

This is **story D1 of Epic D — REST Controller Source Generator** (foundation story; no story precedes it in this epic). It delivers only the **author-facing contract seam** — the public API that domain authors annotate. It does **not** build the generator, emit any controllers, or annotate any real domain. Those are downstream stories:

- **D2** — generator skeleton + spike (consumes this seam; adds `Microsoft.CodeAnalysis.CSharp`, `.slnx` entries)
- **D3** — controller emission (implements the convention fallback *behavior* defined here)
- **D4** — generator tests
- **D5/D6** — prove on Sample/Counter (in-repo)
- **D7** — prove on Tenants UI host (submodule PR — **out of scope here**)
- **D8** — packaging + docs + guardrail

**Source of truth:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` §4.1 ("Contract seam (D1) — author-facing API"), Epic D story table (D1 row), and §4.2 (exemplar showing how these types are consumed).

## Acceptance Criteria

1. **`ICommandContract` exists** at `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs`, namespace `Hexalith.EventStore.Contracts.Commands`, mirroring `IQueryContract`:
   - `static abstract string CommandType { get; }` — kebab-case command-type discriminator, no colons (contract documented; matches `IQueryContract.QueryType` semantics).
   - `static abstract string Domain { get; }` — kebab-case domain name.
   - `string AggregateId { get; }` — **instance** member; the aggregate id this command targets (used for routing + envelope).
   - Full XML docs on the interface and **every** member (CS1591 is live — see Dev Notes).

2. **`RestVerb` enum + `RestRouteAttribute`** exist at `src/Hexalith.EventStore.Contracts/Rest/RestRouteAttribute.cs`, namespace `Hexalith.EventStore.Contracts.Rest`:
   - `enum RestVerb { Get, Post, Put, Patch, Delete }` — each member XML-documented.
   - `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]`, `sealed`, primary constructor `RestRouteAttribute(RestVerb verb, string template)`.
   - Public read-only `RestVerb Verb` and `string Template` properties.
   - Constructor validation: `ArgumentNullException.ThrowIfNull(template)` only (see "Validation decision" in Dev Notes — empty/whitespace templates are **allowed**; semantic route validation is the generator's job in D3).
   - Applicable to **both** command and query contract classes (queries gain an *optional* `[RestRoute]`; `IQueryContract` itself is unchanged).

3. **`RestTenantSource` enum + `RestApiAttribute`** exist at `src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs`, namespace `Hexalith.EventStore.Contracts.Rest`:
   - `enum RestTenantSource { Claims, Route, System }` — each member XML-documented.
   - `[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]`, `sealed`, primary constructor `RestApiAttribute(string routePrefix, string? tag = null, RestTenantSource tenantSource = RestTenantSource.Claims)`.
   - Public read-only `string RoutePrefix`, `string? Tag`, `RestTenantSource TenantSource` properties.
   - Constructor validation: `ArgumentNullException.ThrowIfNull(routePrefix)` only.

4. **Convention fallback is *defined* (documented), not implemented.** XML doc `<remarks>` on the relevant types capture the fallback rules verbatim from §4.1:
   - No `[RestRoute]` → commands map to `POST {prefix}`; queries map to `GET {prefix}` (or `POST {prefix}` if the query carries a body payload).
   - Default route-prefix convention is `api/{domain}` (the value an author passes to `[RestApi]`).
   - **No generator/route-computation code is added in D1** — these remarks are the spec D3 implements.

5. **Unit tests** added under `tests/Hexalith.EventStore.Contracts.Tests/`, mirroring existing test style (xUnit v3, Shouldly, `internal` stub types, no XML docs on tests):
   - `Commands/ICommandContractTests.cs` — static members accessible on a stub; instance `AggregateId` accessible; a second stub proving `Domain`/`CommandType` differ per type.
   - `Rest/RestRouteAttributeTests.cs` — valid construction sets `Verb`/`Template`; `ThrowIfNull(template)` throws `ArgumentNullException`; empty/whitespace template is accepted; `AttributeUsage` is `Class`, non-multiple, non-inherited; each `RestVerb` value is defined.
   - `Rest/RestApiAttributeTests.cs` — valid construction sets all three properties; default `Tag` is `null` and default `TenantSource` is `Claims`; `ThrowIfNull(routePrefix)` throws; `AttributeUsage` is `Assembly`, non-multiple; each `RestTenantSource` value is defined.

6. **Build + tests green.** `dotnet build src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj -c Release` and the Contracts.Tests project build clean under `TreatWarningsAsErrors=true` (no CS1591), and `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` passes (all existing + new tests).

7. **Scope is additive and contained.** No changes to `IQueryContract`, the generator (none exists yet), `Directory.Packages.props`, `Hexalith.EventStore.slnx`, any submodule (`Hexalith.Tenants`), or any Sample/Counter code. No new package references.

## Tasks / Subtasks

- [x] **Task 1: Add `ICommandContract`** (AC: 1)
  - [x] Create `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs`, namespace `Hexalith.EventStore.Contracts.Commands`.
  - [x] Declare `static abstract string CommandType { get; }`, `static abstract string Domain { get; }`, instance `string AggregateId { get; }`.
  - [x] Mirror `IQueryContract.cs` exactly for doc style, blank-line-after-opening-brace, **K&R same-line braces**.

- [x] **Task 2: Add `RestVerb` + `RestRouteAttribute`** (AC: 2, 4)
  - [x] Create `src/Hexalith.EventStore.Contracts/Rest/RestRouteAttribute.cs`, namespace `Hexalith.EventStore.Contracts.Rest`.
  - [x] Define `enum RestVerb { Get, Post, Put, Patch, Delete }` with XML doc per member (co-located in this file, per §4.1).
  - [x] Define `sealed` attribute with `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]`, ctor `(RestVerb verb, string template)`, `ArgumentNullException.ThrowIfNull(template)`, public `Verb`/`Template`.
  - [x] Add `<remarks>` documenting the verb/template convention fallback (AC 4) and example templates (`"{tenantId}"`, `"~/api/users/{userId}/tenants"`).

- [x] **Task 3: Add `RestTenantSource` + `RestApiAttribute`** (AC: 3, 4)
  - [x] Create `src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs`, namespace `Hexalith.EventStore.Contracts.Rest`.
  - [x] Define `enum RestTenantSource { Claims, Route, System }` with XML doc per member (co-located, per §4.1).
  - [x] Define `sealed` attribute with `[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]`, ctor `(string routePrefix, string? tag = null, RestTenantSource tenantSource = RestTenantSource.Claims)`, `ArgumentNullException.ThrowIfNull(routePrefix)`, public `RoutePrefix`/`Tag`/`TenantSource`.
  - [x] Add `<remarks>` documenting the default `api/{domain}` prefix convention (AC 4).

- [x] **Task 4: Unit tests** (AC: 5)
  - [x] `tests/Hexalith.EventStore.Contracts.Tests/Commands/ICommandContractTests.cs` — model after `Queries/IQueryContractTests.cs` (internal stub records implementing `ICommandContract`).
  - [x] `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestRouteAttributeTests.cs` — model after `Queries/EventStoreQueryTypeAttributeTests.cs` (incl. the `AttributeUsage_AllowsClassOnly` shape).
  - [x] `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestApiAttributeTests.cs` — assembly-target AttributeUsage + defaults.

- [x] **Task 5: Verify (red→green→build)** (AC: 6, 7)
  - [x] `dotnet build src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj -c Release` — clean (Build succeeded, no warnings/errors).
  - [x] `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` — all pass (541 passed, 0 failed).
  - [x] `git status` shows only the 3 new src files + 3 new test files (plus this story file). No submodule / props / slnx churn.

## Dev Notes

### 🔴 Top guardrails (most likely to break the build)

1. **CS1591 is LIVE in `Hexalith.EventStore.Contracts`.** The csproj sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` *unconditionally* (`src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj:5`), overriding the repo-wide default (which only generates docs when `ApiReferenceBuild=true`). CS1591 is suppressed **only** under `ApiReferenceBuild=true` (`Directory.Build.props:57-61`). A normal `dotnet build` therefore enforces XML docs, and `TreatWarningsAsErrors=true` (`Directory.Build.props:34`) turns a missing doc into a **build error**. → **Every public member needs an XML doc comment**, including each enum member (`RestVerb.Get`, `RestTenantSource.Claims`, …) and each attribute property. (Note: the project-context note "XML docs are NOT generated by default" is *false for this project* — it opts in explicitly.)
2. **Brace style = K&R (same-line `{`), NOT Allman.** The sibling files use same-line braces: `public interface IQueryContract {` (`src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs:9`), `public sealed class EventStoreQueryTypeAttribute : Attribute {`. `.editorconfig:52` sets `csharp_new_line_before_open_brace = all:warning`, but `EnforceCodeStyleInBuild` is **unset**, so IDE0011/brace formatting is **not** enforced at build time — the existing K&R code compiles fine. The proposal §4.1/§4.2 code samples are written Allman; **ignore their brace style** and **match the immediate siblings (`Queries/*.cs`)**. Also mirror their leading blank line at top-of-file and blank-line-after-opening-brace habits.
3. **`ConfigureAwait(false)`** — not relevant here (no awaits in these contract types), but keep it in mind: there is no async surface in D1.

### Files to CREATE (all new — confirmed none exist via grep)

| File | Contents |
|---|---|
| `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs` | `ICommandContract` interface |
| `src/Hexalith.EventStore.Contracts/Rest/RestRouteAttribute.cs` | `RestVerb` enum + `RestRouteAttribute` (new `Rest/` folder) |
| `src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs` | `RestTenantSource` enum + `RestApiAttribute` |
| `tests/Hexalith.EventStore.Contracts.Tests/Commands/ICommandContractTests.cs` | interface stub tests |
| `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestRouteAttributeTests.cs` | attribute tests |
| `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestApiAttributeTests.cs` | attribute tests |

No files are modified. No `.csproj`, no `Directory.Packages.props`, no `.slnx` edits (folders are globbed into the SDK build automatically; the existing projects pick up new `.cs` files with no project-file change).

### Exemplar to mirror — `IQueryContract.cs` (verbatim sibling)

```csharp

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Defines mandatory query metadata as typed static members.
/// ...
/// </summary>
public interface IQueryContract {
    /// <summary>
    /// Gets the query type name used for actor ID routing (first segment).
    /// Must be kebab-case, no colons (reserved as actor ID separator).
    /// Example: "get-counter-status".
    /// </summary>
    static abstract string QueryType { get; }

    /// <summary>...</summary>
    static abstract string Domain { get; }

    /// <summary>...</summary>
    static abstract string ProjectionType { get; }
}
```

`ICommandContract` is the symmetric twin: replace `QueryType`→`CommandType`, drop `ProjectionType`, add the **instance** `string AggregateId { get; }`. Note the leading blank line before `namespace` and the same-line brace — reproduce both.

### Exemplar to mirror — attribute validation (`EventStoreQueryTypeAttribute.cs`)

The established validation idiom is `ArgumentNullException.ThrowIfNull(x)` + (for that attribute) a whitespace + colon check. For D1 attributes, use **only `ThrowIfNull`** on the reference-type params (`template`, `routePrefix`) — see the validation decision below. Reuse the `AttributeUsage_AllowsClassOnly` test shape from `EventStoreQueryTypeAttributeTests.cs:44-53` for the AttributeUsage assertions.

### Validation decision (deliberate divergence — document in code if helpful)

- **`RestRouteAttribute.template`**: `ArgumentNullException.ThrowIfNull(template)` only. Do **not** reject empty/whitespace. Rationale: a route template is structural; an empty template legitimately means "route at the prefix root with the overridden verb", and full route-shape validation (path-param balance, `~/`-absolute handling, body-vs-route binding) is the **generator's** responsibility in D3. Keeping D1 a thin seam avoids over-constraining authors and avoids duplicating D3's logic.
- **`RestApiAttribute.routePrefix`**: `ArgumentNullException.ThrowIfNull(routePrefix)` only — same rationale.
- **`ICommandContract.CommandType` "no colons"**: documented contract only (the interface has no constructor to validate in, exactly like `IQueryContract`). Enforcement, if any, lands in the generator/resolver (D2+), consistent with how `EventStoreQueryTypeAttribute` — not `IQueryContract` — carries the colon check today.

### How these types are consumed (context only — DO NOT build any of this in D1)

From §4.2 exemplar (the generated controller D3 will emit):
- A query class implementing `IQueryContract` + optional `[RestRoute(RestVerb.Get, "{tenantId}")]` → generated `GetTenantAsync` GET action.
- A command record implementing `ICommandContract` + `[RestRoute(RestVerb.Post, "~/api/counters/{counterId}/increment")]`, with `AggregateId => CounterId` → generated POST action that builds `SubmitCommandRequest(... AggregateId: body.AggregateId, CommandType: IncrementCounter.CommandType ...)` and calls `IEventStoreGatewayClient.SubmitCommandAsync`.
- `[assembly: RestApi("api/tenants", "tenants", RestTenantSource.Claims)]` → controller `[Route("api/tenants")]`, `[Tags("tenants")]`, and tenant resolution from claims.

Adoption cost per command (D6/D7, not now): one expression-bodied `public string AggregateId => TenantId;` + the two static members.

### Project Structure Notes

- New `Rest/` folder under Contracts is consistent with the existing one-concern-per-folder layout (`Commands/`, `Queries/`, `Events/`, …). `ICommandContract` belongs in the existing `Commands/` folder (symmetry with `IQueryContract` in `Queries/`).
- §4.1 co-locates each enum in the same file as its attribute — follow that (don't split `RestVerb`/`RestTenantSource` into separate files).
- Test mirror folders: `tests/.../Commands/` and a new `tests/.../Rest/` (mirrors the src layout; `Commands/` test folder already exists).
- The SDK-style projects compile all `**/*.cs` by default; adding files needs **no** `.csproj` edit.

### Testing standards (from project-context + observed conventions)

- **xUnit v3** (`xunit.v3`), **Shouldly** assertions only (`ShouldBe`, `Should.Throw<T>`) — never raw `Assert.*`. Global usings for `Xunit` + `Shouldly` are already provided by the test csproj (`<Using>` items) — no per-file `using` needed for those.
- Test stub types are `internal` and implement the contract inline (see `Queries/IQueryContractTests.cs:8-18`). Records or classes both fine; for `ICommandContract` a `record`/`class` with `public string AggregateId => "...";` works.
- No XML docs required on test types/methods (test project does not generate docs).
- **Run the test project individually** — never solution-level `dotnet test`. Use `.slnx` for restore/build only.
- This is a Tier-1 (Contracts) suite — runs in CI; must be green to close the story.

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md#4.1 Contract seam (D1) — author-facing API] — exact API shapes for `ICommandContract`, `RestRouteAttribute`, `RestApiAttribute`, convention fallback.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md#Epic D story table] — D1 row scope/effort; sequencing D1→D8.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md#4.2] — generated-controller exemplar (downstream consumption, context only).
- [Source: src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs] — sibling interface to mirror (style + symmetry).
- [Source: src/Hexalith.EventStore.Contracts/Queries/EventStoreQueryTypeAttribute.cs] — attribute + validation idiom to mirror.
- [Source: tests/Hexalith.EventStore.Contracts.Tests/Queries/IQueryContractTests.cs] — test pattern for interface stubs.
- [Source: tests/Hexalith.EventStore.Contracts.Tests/Queries/EventStoreQueryTypeAttributeTests.cs] — test pattern for attributes (AttributeUsage assertion).
- [Source: src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj:5] — `GenerateDocumentationFile=true` (CS1591 live).
- [Source: Directory.Build.props:34,52-61] — `TreatWarningsAsErrors`, CS1591 suppression gate.
- [Source: .editorconfig:47,52] — file-scoped namespaces; brace rule is warning-only / not build-enforced.
- [Source: _bmad-output/project-context.md] — repo-wide critical rules (ULID, ConfigureAwait, no copyright headers, no version in csproj).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (Amelia / bmad-agent-dev)

### Debug Log References

- **CS1734 (build, fixed)** — first Release build of Contracts failed: `<paramref name="routePrefix"/>` in the **type-level** `<remarks>` of `RestApiAttribute` is invalid because `routePrefix` is a *constructor* parameter, not a parameter of the type (classic constructor was used, not a primary constructor). Reworded the remarks to reference `<see cref="RoutePrefix"/>` / plain text. Re-build: `Build succeeded`.
- Implementation decision: used **classic constructors** (not the primary-constructor form shown in proposal §4.1) for both attributes, to host `ArgumentNullException.ThrowIfNull(...)` validation in the established sibling idiom (`EventStoreQueryTypeAttribute`). Behaviour/public surface is identical to the proposal.

### Completion Notes List

- All 7 ACs satisfied. Added the D1 contract seam in `Hexalith.EventStore.Contracts`: `ICommandContract` (Commands/), `RestVerb`+`RestRouteAttribute` and `RestTenantSource`+`RestApiAttribute` (new Rest/ folder), each fully XML-documented (CS1591 is live for this project).
- TDD red→green: wrote the three test files first, confirmed a compile-failure RED (CS0234/CS0246/CS0103 for the five missing types), then implemented the types to GREEN.
- Style: mirrored sibling **K&R same-line braces** (not the proposal's Allman samples; IDE0011 is not build-enforced here). Enum members carry `<summary>` docs with trailing comma, matching `CommandStatus.cs`.
- Convention fallback (AC4) is **defined as documentation only** (XML `<remarks>` on `RestRouteAttribute`/`RestApiAttribute`): commands→`POST {prefix}`, queries→`GET {prefix}` (or `POST` with body), default prefix `api/{domain}`. No generator/route logic added — that is D2/D3.
- Validation (deliberate, documented divergence): only `ArgumentNullException.ThrowIfNull` on `template`/`routePrefix`; empty/whitespace allowed (route shape is the generator's concern in D3). `IQueryContract` unchanged; `[RestRoute]` targets `Class` so it applies to query contracts too.
- Verification: `dotnet build ...Contracts.csproj -c Release` → Build succeeded (no warnings under `TreatWarningsAsErrors`). `dotnet test ...Contracts.Tests` → **541 passed, 0 failed** (14 new test cases + existing suite, no regressions).
- Scope contained: only 6 new files created; no edits to `IQueryContract`, `Directory.Packages.props`, `Hexalith.EventStore.slnx`, samples, or the `Hexalith.Tenants` submodule. SDK-style projects pick up new `.cs` files with no `.csproj` change.

### File List

**New (source):**
- `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs`
- `src/Hexalith.EventStore.Contracts/Rest/RestRouteAttribute.cs`
- `src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs`

**New (tests):**
- `tests/Hexalith.EventStore.Contracts.Tests/Commands/ICommandContractTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestRouteAttributeTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestApiAttributeTests.cs`

## Change Log

| Date | Change |
|---|---|
| 2026-06-21 | D1 contract seam implemented: `ICommandContract` + `RestRouteAttribute`/`RestVerb` + `RestApiAttribute`/`RestTenantSource` with unit tests. Contracts builds clean in Release; Contracts.Tests 541 passed / 0 failed. Status → review. |
