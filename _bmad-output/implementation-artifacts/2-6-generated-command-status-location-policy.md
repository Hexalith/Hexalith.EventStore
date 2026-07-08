---
baseline_commit: 1f3ae82282e177423afb523d2c3b49d9cc33f837
---

# Story 2.6: Generated Command-Status Location Policy

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an external API consumer,
I want a generated command's `202 Accepted` to point me at a status resource I can actually reach — or at nothing — never at a dangling URL,
so that I never poll a `404` status link and my client's base authority stays correct.

## Acceptance Criteria

1. **Runtime seam (Client, request-time resolution).** A new command-status Location builder lives in `Hexalith.EventStore.Client` (namespace `Hexalith.EventStore.Client.Gateway`): a **public** `ICommandStatusLocationBuilder` with `bool TryBuild(string statusKey, out string? location)`, plus a **public** `CommandStatusLocationOptions { Uri? GatewayStatusBase }`. The **internal** default implementation returns an **absolute** `{GatewayStatusBase}/api/v1/commands/status/{Uri.EscapeDataString(statusKey)}` and `true` when a base is configured, and `false` (with `location = null`) when it is not; it is unit-tested in `Client.Tests` (which already has `InternalsVisibleTo` to the Client assembly). It is registered **fail-closed by default** (`TryAddSingleton`) by `AddEventStoreGatewayClient`, so any host that wires the gateway client can construct a generated command controller without extra configuration and defaults to emitting no `Location`.

2. **Configured host → absolute `Location`.** When a gateway status base is configured, a generated command controller's `202 Accepted` emits `Location: {gatewayStatusBase}/api/v1/commands/status/{statusKey}` (RFC 7231 absolute), resolved at request time through the injected builder — never a compile-time constant. `statusKey` is the single gateway-owned tracking field on `SubmitCommandResponse` (today `CorrelationId`), referenced **once**, with **no** hard-coded assumption that `CorrelationId == MessageId`.

3. **Unconfigured host → fail-closed.** When no gateway status base is configured, the generated `202 Accepted` emits `Retry-After: 1` and **no** `Location` header. A relative `/api/v1/commands/status/...` URL is **never** emitted under any configuration (AD-17 fail-closed). The `202` body still carries the tracking key (`SubmitCommandResponse.CorrelationId`).

4. **Failure path unchanged.** A generated command that fails at the gateway maps the gateway problem response and emits **no** `Location` header (existing behavior preserved; `Retry-After` on the retryable-failure path is untouched).

5. **Emitter change with no CS9113 regression.** `RestApiControllerEmitter` emits the injected `ICommandStatusLocationBuilder` into the generated controller's primary constructor **only when the controller emits at least one command action** (query-only controllers are unchanged and must not gain an unread parameter — `TreatWarningsAsErrors=true` promotes CS9113 to a build break). The command action emits `if (statusLocationBuilder.TryBuild(__hexalithResponse.CorrelationId, out string? __hexalithStatusLocation)) Response.Headers["Location"] = __hexalithStatusLocation;`; the hard-coded relative `"/api/v1/commands/status/"` string literal is removed from the emitter.

6. **Tests replaced (generator-output + runtime), all call sites updated.** The pre-existing relative-string assertions in `RestApiControllerGenerationTests` and `RestApiGeneratedControllerErrorSemanticsTests` are **replaced** by policy assertions: absolute-when-configured, header-absent-when-unconfigured, and never-a-relative-URL. Every generated-controller construction site that assumes a single-argument constructor is updated for the new DI surface (source-string literal in `RestApiControllerGenerationTests`; both `Activator.CreateInstance(controllerType, gateway)` call sites).

7. **Sample host demonstrates both states.** The Sample external API host (`Sample.Api`) wires the option so it is configurable, defaulting fail-closed; `SampleApiGeneratedControllerRuntimeTests` proves, against the **real compiled** Sample controller, both configured (absolute `Location`) and unconfigured (no `Location`) behavior.

8. **Ledger closed + green gates.** The two command-status `Location` deferred-work entries (spec-2-2 and spec-2-3, currently `status: scheduled (2026-07-07).`) are flipped to `RESOLVED … by Story 2.6`. Release build is clean and all configured focused tests pass — `Client.Tests`, `RestApi.Generators.Tests`, and `Sample.Tests` — with no CA2007 / warnings-as-errors regressions. **Out of scope (do not touch):** FR27 command-status re-keying (Epic 4); rest-generator-hardening items S1/S3/S4/S5/S6; the `references/Hexalith.Tenants` submodule.

## Tasks / Subtasks

- [x] **Task 1 — Client runtime seam** (AC: 1)
  - [x] Add `src/Hexalith.EventStore.Client/Gateway/CommandStatusLocationOptions.cs` — `public sealed class CommandStatusLocationOptions { public Uri? GatewayStatusBase { get; set; } }`. Mirror the auto-property + XML-doc style of `Gateway/EventStoreGatewayClientOptions.cs`.
  - [x] Add `src/Hexalith.EventStore.Client/Gateway/ICommandStatusLocationBuilder.cs` — `public interface ICommandStatusLocationBuilder { bool TryBuild(string statusKey, [NotNullWhen(true)] out string? location); }` (`using System.Diagnostics.CodeAnalysis;`). The interface **must be public** — the generated controller references it by type name.
  - [x] Add `src/Hexalith.EventStore.Client/Gateway/CommandStatusLocationBuilder.cs` — `internal sealed class CommandStatusLocationBuilder(IOptions<CommandStatusLocationOptions> options) : ICommandStatusLocationBuilder`. `TryBuild`: `ArgumentException.ThrowIfNullOrWhiteSpace(statusKey)`; capture `options.Value.GatewayStatusBase`; if null → `location = null; return false;`; else `location = base.AbsoluteUri.TrimEnd('/') + "/api/v1/commands/status/" + Uri.EscapeDataString(statusKey); return true;`. Keep the impl `internal` (registered via the extension; minimizes the published Client package's public surface — same posture as Story 2.7's handler).
  - [x] In `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs` `AddEventStoreGatewayClient` (lines 28-43), add `services.TryAddSingleton<ICommandStatusLocationBuilder, CommandStatusLocationBuilder>();` before the `return` (add `using Microsoft.Extensions.DependencyInjection.Extensions;`). This guarantees the fail-closed default is always resolvable in any generated-controller host.
  - [x] Add a convenience extension in the same file: `public static IServiceCollection AddEventStoreCommandStatusLocation(this IServiceCollection services, Uri gatewayStatusBase)` — `ThrowIfNull` both args; reject a non-absolute or non-http/https URI with a support-safe `ArgumentException`; `services.Configure<CommandStatusLocationOptions>(o => o.GatewayStatusBase = gatewayStatusBase)`; `services.TryAddSingleton<ICommandStatusLocationBuilder, CommandStatusLocationBuilder>()`; return `services`. Mirror the absolute-origin validation shape of `samples/Hexalith.EventStore.Sample.Api/Services/DaprHttpEndpointResolver.cs:20-55`.
  - [x] Add `tests/Hexalith.EventStore.Client.Tests/Gateway/CommandStatusLocationBuilderTests.cs` (Tier 1) constructing the **real internal** `CommandStatusLocationBuilder` via `new CommandStatusLocationBuilder(Options.Create(new CommandStatusLocationOptions { GatewayStatusBase = ... }))` — mirror `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs:27`. Assert: configured base → absolute URL + `true`; no base → `false` and `location is null`; key escaping (`Uri.EscapeDataString`); trailing-slash base composes exactly one `/`. This is where the composition logic is proven; the generator/Sample runtime tests only need a fake (Tasks 3-4).

- [x] **Task 2 — Generator emitter change** (AC: 2, 3, 4, 5)
  - [x] `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` `AppendControllerStart` (lines 214-233): make the emitted primary constructor conditional. Pass a `bool hasCommandActions` into `AppendControllerStart` (compute it in `Emit` as `emittedMessages.Any(m => m.IsCommand)` and thread it through). Emit `(IEventStoreGatewayClient gateway, ICommandStatusLocationBuilder statusLocationBuilder) : ControllerBase` when `hasCommandActions`, else the current `(IEventStoreGatewayClient gateway) : ControllerBase`. **No new emitted `using`** — `AppendHeader` (line 201) already emits `using Hexalith.EventStore.Client.Gateway;`.
  - [x] `AppendCommandAction` (lines 235-281): **keep** line 272 (`Response.Headers["Retry-After"] = "1";`). **Replace** line 273 (the relative-string `Location`) with the builder call:
    ```
    if (statusLocationBuilder.TryBuild(__hexalithResponse.CorrelationId, out string? __hexalithStatusLocation))
    {
        Response.Headers["Location"] = __hexalithStatusLocation;
    }
    ```
    Keep line 274 (`return Accepted(__hexalithResponse);`). Do not touch the `MapGatewayException` failure path (lines ~577-580) — it already emits no `Location` and sets its own `Retry-After`.
  - [x] Confirm the emitted URL escaping now lives in the builder (`Uri.EscapeDataString` moved out of the generated code); the generated call passes the **raw** `__hexalithResponse.CorrelationId`. Single reference to the status key = AC 2 "single-sourced, no `CorrelationId == MessageId` assumption".

- [x] **Task 3 — Generator tests (output + runtime)** (AC: 6)
  - [x] `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`: update the constructor source-string literal at **line 32** to include `, ICommandStatusLocationBuilder statusLocationBuilder`. **Keep** the `Retry-After` assertions (lines 64, 486). **Replace** the relative-`Location` assertions (lines 65, 487): assert the source `ShouldContain("statusLocationBuilder.TryBuild(__hexalithResponse.CorrelationId, out")` and `ShouldNotContain("\"/api/v1/commands/status/\"")` (no hard-coded relative literal remains in generated source). Add a query-only-contract case (or verify an existing one) asserting the generated controller constructor **omits** `statusLocationBuilder` and the project still builds (guards the CS9113 conditional from AC 5).
  - [x] `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs`: update `CreateController` (line 802-824; `Activator.CreateInstance(controllerType, gateway)` at **line 817**) to also pass an `ICommandStatusLocationBuilder`; make it accept the builder (default a fail-closed one) so callers can pass a configured or unconfigured instance. **This project has no `InternalsVisibleTo` to the Client assembly** — construct via a tiny local **fake implementing the public `ICommandStatusLocationBuilder`** (configured fake → returns a fixed absolute URL + `true`; unconfigured fake → `location = null; return false`), NOT the internal `CommandStatusLocationBuilder`. Split/parameterize the `202` success test (`CommandAction_ValidBody_...`, lines 768-800; assertions at 797-799): (a) **configured** → `headers["Location"].ToString().ShouldBe("https://gateway.example/api/v1/commands/status/01KTESTCOMMANDSTATUS000000")` and `headers["Location"].ToString().ShouldNotStartWith("/")`; (b) **unconfigured** → `headers.ContainsKey("Location").ShouldBeFalse()` with `headers["Retry-After"].ToString().ShouldBe("1")`. Leave the gateway-failure no-`Location` test (lines 764-765) intact.
  - [x] Test harness reference: `RestApiGeneratorTestHarness` drives the generator via `CSharpGeneratorDriver` and `GetGeneratedSource(runResult, ".Controller.g.cs")`; `FakeEventStoreGatewayClient` default-returns `new SubmitCommandResponse("01KTESTCOMMAND000000000000")` when no handler is set.

- [x] **Task 4 — Sample host wiring + runtime proof** (AC: 7)
  - [x] `samples/Hexalith.EventStore.Sample.Api/Program.cs` (DI region lines 66-76): after `AddEventStoreGatewayClient(...)`, read an optional absolute config value (e.g. `builder.Configuration["EventStore:GatewayStatusBase"]`); when present and a valid absolute http/https URI, call `builder.Services.AddEventStoreCommandStatusLocation(uri)`. When absent, do nothing — the fail-closed default from `AddEventStoreGatewayClient` applies. Keep the change minimal; do not alter the DAPR/bearer-forwarding wiring.
  - [x] `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs`: update `CreateController` (lines 375-388; `Activator.CreateInstance(controllerType, gateway)` at **line 381**) to pass an `ICommandStatusLocationBuilder` — again a **local fake of the public interface** (Sample.Tests has no Client-internals access). Replace the relative-`Location` assertions in `IncrementCounter_WhenBodyMatchesRoute_...` (lines 177-179) and `CounterCommands_WhenBodiesMatchRoute_...` (lines 213-215): prove **configured** (absolute `{base}/api/v1/commands/status/{statusId}`) and **fail-closed** (no `Location`, `Retry-After == "1"`) against the real compiled `Hexalith.EventStore.Sample.Api.Generated.CounterRestController`. This is the AC-7 demonstration.
  - [x] Do **not** regress the sibling `SampleApiGatewayHandlerTests` / structural tests — those belong to Story 2.7's surface; if the generated-controller construction signature is referenced there, update mechanically without changing their intent. *(Updated `SampleApiStructuralTests.CounterRestController_IsOnlyGeneratedControllerAndUsesGatewayBoundary` ctor-parameter assertion mechanically: now asserts both `IEventStoreGatewayClient` + `ICommandStatusLocationBuilder`.)*

- [x] **Task 5 — Close ledger entries + gates** (AC: 8)
  - [x] `_bmad-output/implementation-artifacts/deferred-work.md`: flip the **spec-2-2** entry (lines 135-138, summary "hard-code `/api/v1/commands/status/{id}` as a relative status `Location`") and the **spec-2-3** entry (lines 152-155, summary "Sample API command success responses expose the generator's relative … status location") from `status: scheduled (2026-07-07).` to a `**RESOLVED <date> by Story 2.6**` form (mirror the existing resolved entries at lines 5 and 173). Do not modify DW-2, DW-3, or any query-ETag/freshness entries.
  - [x] Leave a `File List` note that rest-generator-hardening backlog item **S2** is now enforced here (its S1/S3/S4/S5/S6 siblings remain open under their action item). Do not edit the backlog file's other items.
  - [x] Run gates: `dotnet build Hexalith.EventStore.slnx -c Release`; `dotnet test tests/Hexalith.EventStore.Client.Tests/`; `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/`; `dotnet test tests/Hexalith.EventStore.Sample.Tests/`. All green, no CA2007 / warnings-as-errors regressions.

## Dev Notes

### What this is (and is not)
- **Governed by architecture invariant AD-17** — *Generated Command-Status Location Is Absolute, Gateway-Authoritative, And Fail-Closed* (`_bmad-output/planning-artifacts/architecture.md:194-206`). This story implements AD-17 verbatim. Full rationale + decision record: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-generated-api-command-status-location-policy.md`.
- **This story absorbs rest-generator-hardening item S2** (`_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md:48`) — S2 was blocked on this policy; it is enforced here and nowhere else. The other Second-Wave items (S1 request-size limit, S3 safe query `ArgumentException` text, S4 rejection extension allowlist, S5 tenant-source handling, S6 `RestQueryBinding` reconciliation) stay in the backlog under their own action item — **do not** pull them in.
- **Honest severity (Epic 1 retro R1-A8 — don't overstate).** The defect is real and test-corroborated: a dedicated external API host (e.g. `Sample.Api`) maps only generated controllers + default endpoints, so the emitted **relative** `/api/v1/commands/status/{id}` resolves against the *external host* (not the gateway) and `404`s. It is a correctness/dangling-route fix, not a security-CRITICAL. Frame the commit as `fix(generators): …`. The new public Client seam (`ICommandStatusLocationBuilder`, `CommandStatusLocationOptions`, `AddEventStoreCommandStatusLocation`) is a **minimal, backward-compatible** additive API — `AddEventStoreGatewayClient` auto-registers the fail-closed default, so existing hosts keep building and behave fail-closed until they opt in.

### Root defect (pre-existing pattern)
`RestApiControllerEmitter.AppendCommandAction` emits a compile-time **relative** URL: `Response.Headers["Location"] = "/api/v1/commands/status/" + Uri.EscapeDataString(__hexalithResponse.CorrelationId);` (`RestApiControllerEmitter.cs:273`). Three defects (all corroborated by tests + the deferred ledger):
1. **Dangling route (404):** the external host never maps `/api/v1/commands/status/...`.
2. **Wrong authority + relative form:** that route is served by the **platform gateway** (`CommandStatusController`, `[Route("api/v1/commands/status")]` → `[HttpGet("{correlationId}")]`). The platform's own `CommandsController.Submit` deliberately builds an **absolute** URI (`CommandsController.cs:125-126`, "RFC 7231: Location header should be absolute URI"). The generated host cannot copy that trick — `{Request.Scheme}://{Request.Host}` is the *external host's* authority, not the gateway's — hence a request-time runtime option.
3. **Status-key ambiguity:** the platform sets `CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? request.MessageId : request.CorrelationId` (`CommandsController.cs:118`). FR27/Epic 4 re-keys command status/archive by `MessageId`. AD-17 pins the policy to *the single tracking field on the response* (`SubmitCommandResponse.CorrelationId`) so the value migrates transparently when Epic 4 re-keys — **do not** introduce any `MessageId`-based composition here.

### Design decision (settles the proposal's open choice)
The proposal offered two seams — an options class **or** an `ICommandStatusLocationBuilder`. **Use the builder**, matching the proposal's emitted-code sketch `builder.TryBuild(statusKey, out var uri)`: it centralizes URL composition + `Uri.EscapeDataString` + trailing-slash handling in one unit-testable place in `Hexalith.EventStore.Client`, keeps the generated code to a single `if`, and lets the generated controller depend on one injected interface. `CommandStatusLocationOptions.GatewayStatusBase` is the config surface; `CommandStatusLocationBuilder` is the default impl; both live in `Hexalith.EventStore.Client.Gateway` (namespace already imported by the generated header). This mirrors the established `EventStoreGatewayClientOptions` idiom (`services.Configure(...)` bound + `IOptions<T>` consumed).

> **Why not reuse `EventStoreGatewayClientOptions.BaseAddress`?** That base is the **DAPR sidecar origin** used for outbound gateway calls, not the browser-facing gateway authority a client would poll. It is not reusable as `gatewayStatusBase`; a distinct option is intentional.

### The DI-arity trap (must-read — three call sites + one warning)
Adding a second constructor parameter to the generated controller breaks anything that assumes arity 1. You **must** update all four:
| Site | File:line | Fix |
| --- | --- | --- |
| Source-string literal | `tests/…/RestApiControllerGenerationTests.cs:32` | Append `, ICommandStatusLocationBuilder statusLocationBuilder` to the expected ctor string. |
| Reflection construct | `tests/…/RestApiGeneratedControllerErrorSemanticsTests.cs:817` | `Activator.CreateInstance(controllerType, gateway, statusLocationBuilder)`. |
| Reflection construct | `tests/…/SampleApiGeneratedControllerRuntimeTests.cs:381` | Same — pass a builder. |
| **CS9113 / warnings-as-errors** | generated query-only controllers | Emit the param **only when a command action exists** (AC 5). An unread primary-ctor param is `CS9113` → build break under `TreatWarningsAsErrors=true`. `gateway` is always used (query actions call `gateway.SubmitQueryAsync`); `statusLocationBuilder` is used only by command actions. |

### Files being changed
| File | Change | Notes |
| --- | --- | --- |
| `src/Hexalith.EventStore.Client/Gateway/CommandStatusLocationOptions.cs` | **NEW** | Public `Uri? GatewayStatusBase`. |
| `src/Hexalith.EventStore.Client/Gateway/ICommandStatusLocationBuilder.cs` | **NEW** | **Public** interface — generated controller references it by type. |
| `src/Hexalith.EventStore.Client/Gateway/CommandStatusLocationBuilder.cs` | **NEW** | `internal sealed`; composes absolute URL, escapes key, fail-closed when base null. |
| `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs` | **UPDATE** | `AddEventStoreGatewayClient` (28-43) adds `TryAddSingleton` fail-closed default; new `AddEventStoreCommandStatusLocation(services, Uri)`. |
| `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` | **UPDATE** | `AppendControllerStart` (214-233) conditional ctor param; `AppendCommandAction` (235-281) replace line 273; thread `hasCommandActions` from `Emit`. |
| `samples/Hexalith.EventStore.Sample.Api/Program.cs` | **UPDATE** | Optional config read → `AddEventStoreCommandStatusLocation`; default fail-closed. |
| `tests/Hexalith.EventStore.Client.Tests/Gateway/CommandStatusLocationBuilderTests.cs` | **NEW** | Unit-test the real internal builder (has `InternalsVisibleTo`). |
| `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` | **UPDATE** | ctor literal (32), keep Retry-After (64/486), replace Location strings (65/487), query-only case. |
| `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs` | **UPDATE** | `CreateController` (817) + `202` test (768-800) configured/unconfigured. |
| `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs` | **UPDATE** | `CreateController` (381) + Location assertions (177-179, 213-215) configured/fail-closed. |
| `_bmad-output/implementation-artifacts/deferred-work.md` | **UPDATE** | Close spec-2-2 (135-138) and spec-2-3 (152-155) Location entries. |
| `references/Hexalith.Tenants/**` | **DO NOT TOUCH** | Submodule; inherits the fail-closed default automatically (it calls `AddEventStoreGatewayClient`). Configuring its base is a coordinated maintainer follow-up (Story 2.4 lineage), not this story. |

### Constraints & conventions (project-context.md / architecture.md)
- **Generated code style:** file-scoped namespaces, nullable, `ConfigureAwait(false)` on awaited calls, warnings-as-errors. The builder call is synchronous (no `await`, no `ConfigureAwait`). `SubmitCommandAsync` already has `.ConfigureAwait(false)` — leave it.
- **`Hexalith.EventStore.Client` is a published package** (`tools/release-packages.json`). Keep the new public surface minimal: interface + options public; impl `internal`; one extension method. Same restraint as Story 2.7.
- **Identity:** the status key is a string tracking field — never `Guid.TryParse`/`Ulid.TryParse` it here; pass it through and let `Uri.EscapeDataString` handle it.
- **No `.sln`; `.slnx` only.** No copyright headers. `_camelCase` private fields, `I`-prefixed interface, Allman braces where the file uses them (Client extensions use same-line braces — match the file you edit).
- **AD-3 / AD-4:** the status resource belongs to the **gateway**; the generated host maps **no** command-status route of its own. Do not add a pass-through status controller to `Sample.Api`.
- **AD-9 not triggered:** no AppHost/DAPR YAML change; app ids unchanged. Only *how/whether* the `Location` header is set changes.

### Testing standards
- **xUnit v3 + Shouldly** (`ShouldBe`, `ShouldContain`, `ShouldNotStartWith`), never raw `Assert.*`. Run test projects individually.
- **Evidence is the emitted source string (output tests) and the outgoing HTTP response headers (runtime tests)** — this is a controller/transport surface, not a state-store path, so the Tier-2/3 "assert persisted Redis/CloudEvent end-state" rule (R2-A6 / AD-12) does **not** apply; do not fabricate a state-store assertion. AD-12 here means *generator-output + runtime tests*, which this story delivers.
- **Both behaviors must be asserted** (configured absolute; unconfigured absent) plus the negative (never relative, `ShouldNotStartWith("/")`), on both the abstract generator test and the real compiled Sample controller.
- **Internal-vs-fake split (avoids an `InternalsVisibleTo` rabbit hole):** `Client.Tests` has `InternalsVisibleTo` to the Client assembly → it unit-tests the **real** `internal CommandStatusLocationBuilder` directly via `Options.Create(...)`. `RestApi.Generators.Tests` and `Sample.Tests` reference Client as a **project but without internals access** → they construct generated controllers with a **local fake of the public `ICommandStatusLocationBuilder`**, never the internal impl. Do not add new `InternalsVisibleTo` entries.
- **Tiers touched:** `Client.Tests` (T1, CI-gated), `RestApi.Generators.Tests` (T1, CI-gated), and `Sample.Tests` (T1, CI-gated).

### Previous-story intelligence
- **Stories 2.2 (generator emission) and 2.3 (Sample host proof)** are **done/frozen**. The relative-`Location` behavior they shipped **is** superseded — that supersession is carried *explicitly* by this new story and the named test edits, not by a silent change to a closed story (**Story Rewrite Gate satisfied**; see the policy proposal §2). Specs: `spec-2-2-…md`, `spec-2-3-…md`; ledger entries reconciled to AD-17 + this story.
- **Story 2.7 (Outbound DAPR Routing-Header Ownership, AD-18)** is a sibling Epic-2 post-retro hardening story added the same day, `ready-for-dev`. It touches `Sample.Api/Program.cs`, `Sample.BlazorUI`, `Admin.UI`, `Client/Registration/EventStoreServiceCollectionExtensions.cs`, and `Sample.Tests`. **Overlap risk:** both stories edit `EventStoreServiceCollectionExtensions.cs`, `Sample.Api/Program.cs`, and files under `tests/…/Sample.Tests/SampleApi/`. Coordinate edits (additive, non-conflicting: 2.7 adds a DAPR handler + wiring; 2.6 adds the command-status option + builder). Do not delete or move 2.7's `DaprAppIdHandler`/wiring as part of this story.
- **Concurrency caution:** a parallel auto-dev loop may auto-commit/push to `main` and absorb uncommitted edits. Check `git status`/refs before committing; keep this story's diff scoped to the files above.

### Project Structure Notes
- New Client types live in `src/Hexalith.EventStore.Client/Gateway/` alongside `EventStoreGatewayClientOptions.cs`; wiring in `Client/Registration/` alongside `AddEventStoreGatewayClient`. No new project.
- The generated controller namespace is RootNamespace + `.Generated` (e.g. `Hexalith.EventStore.Sample.Api.Generated.CounterRestController`), resolved by `RestApiNamespaceResolver`.
- `SubmitCommandResponse` has two shapes: the Contracts base (`src/Hexalith.EventStore.Contracts/Commands/SubmitCommandResponse.cs:11-13` — `CorrelationId`, optional `ResultPayload`, **no `MessageId`**) which the generated controller + gateway client use, and a Models compat wrapper in the platform host. `statusKey = .CorrelationId` on both.

### References
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-17 - Generated Command-Status Location Is Absolute, Gateway-Authoritative, And Fail-Closed] (lines 194-206)
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-3] (61-65) · [Source: …#AD-4] (67-71) · [Source: …#AD-10] (103-107) · [Source: …#AD-12] (115-119) · [Source: …#Consistency Conventions — Command status] (233)
- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.6: Generated Command-Status Location Policy] (770-805)
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-generated-api-command-status-location-policy.md]
- [Source: _bmad-output/planning-artifacts/backlog/rest-generator-hardening.md#S2] (48, 54, 62, 67)
- [Source: src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs#AppendCommandAction] (235-281, Location line 273) · [#AppendControllerStart] (214-233) · [#AppendHeader] (201)
- [Source: src/Hexalith.EventStore/Controllers/CommandsController.cs] (118 CorrelationId default, 125-126 RFC 7231 absolute) · [Source: src/Hexalith.EventStore/Controllers/CommandStatusController.cs] (route `api/v1/commands/status`, `[HttpGet("{correlationId}")]`)
- [Source: src/Hexalith.EventStore.Contracts/Commands/SubmitCommandResponse.cs:11-13]
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs#AddEventStoreGatewayClient] (28-43) · [Source: src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClientOptions.cs] (6-31) · [Source: src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj:9 — InternalsVisibleTo Client.Tests]
- [Source: tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs:27 — Options.Create(new …Options()) precedent]
- [Source: samples/Hexalith.EventStore.Sample.Api/Program.cs] (66-76) · [Source: samples/Hexalith.EventStore.Sample.Api/Services/DaprHttpEndpointResolver.cs] (20-55)
- [Source: tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs] (32, 64-65, 486-487) · [Source: …/RestApiGeneratedControllerErrorSemanticsTests.cs] (768-800, 797-799, 817, 764-765) · [Source: tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs] (177-179, 213-215, 375-388)
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] (spec-2-2 entry 135-138; spec-2-3 entry 152-155)
- [Source: _bmad-output/project-context.md#Framework-Specific Rules — DAPR abstracts state; gateway is the policy boundary]

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (Claude Opus 4.8, 1M context) — bmad-dev-story workflow.

### Debug Log References

- `dotnet test tests/Hexalith.EventStore.Client.Tests/` → **527/527** passed (includes 7 new `CommandStatusLocationBuilderTests` cases against the real internal builder).
- `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` → **110/110** passed (was 108; +split configured/unconfigured 202 test, +query-only CS9113 guard).
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/` → **116/116** passed (was 115; +fail-closed increment test; structural ctor-arity assertion updated mechanically).
- `dotnet build Hexalith.EventStore.slnx -c Release` → **Build succeeded. 0 Warning(s), 0 Error(s).** (Server.Tests included in the slnx and clean in Release; no CA2007 regression introduced.)

### Completion Notes List

Implements architecture invariant **AD-17** (Generated Command-Status Location Is Absolute, Gateway-Authoritative, And Fail-Closed) verbatim. Absorbs rest-generator-hardening Second-Wave item **S2**; S1/S3/S4/S5/S6 remain open under their own action item.

- ✅ **AC1 — Client runtime seam.** New public `ICommandStatusLocationBuilder` + public `CommandStatusLocationOptions { Uri? GatewayStatusBase }` + `internal sealed CommandStatusLocationBuilder`, all in `Hexalith.EventStore.Client.Gateway`. `AddEventStoreGatewayClient` `TryAddSingleton`s the builder **fail-closed by default**; `AddEventStoreCommandStatusLocation(Uri)` opts into absolute mode (rejects non-absolute / non-http(s) / userinfo-bearing URIs with a support-safe `ArgumentException`). Real internal builder unit-tested in `Client.Tests` (has `InternalsVisibleTo`).
- ✅ **AC2/AC3 — absolute-when-configured / fail-closed-when-not.** Generated 202 emits `Location: {gatewayStatusBase}/api/v1/commands/status/{statusKey}` at request time via the injected builder; no header when unconfigured. The relative `"/api/v1/commands/status/"` literal is removed from the emitter — the generated call passes the **raw** `__hexalithResponse.CorrelationId` (single reference; no `CorrelationId == MessageId` assumption). `Uri.EscapeDataString` moved into the builder.
- ✅ **AC4 — failure path unchanged.** `MapGatewayException` untouched (no `Location`, own `Retry-After`).
- ✅ **AC5 — no CS9113 regression.** `ICommandStatusLocationBuilder` primary-ctor parameter is emitted **only when the controller has ≥1 command action**; query-only controllers keep the single-arg ctor. Explicit `Run_QueryOnlyController_OmitsCommandStatusLocationBuilderConstructorParameter` test guards the build break.
- ✅ **AC6 — tests replaced, all call sites updated.** Relative-string assertions replaced by policy assertions (absolute-when-configured, absent-when-unconfigured, never-relative `ShouldNotStartWith("/")`). All generated-controller construction sites updated for the new DI surface — source-string literal + both `Activator.CreateInstance` reflection sites (each using a **local public-interface fake**, never the internal impl, since those projects have no `InternalsVisibleTo`).
- ✅ **AC7 — Sample host demonstrates both states.** `Sample.Api` wires `AddEventStoreCommandStatusLocation` from optional config `EventStore:GatewayStatusBase`, defaulting fail-closed; `SampleApiGeneratedControllerRuntimeTests` proves absolute-when-configured and no-`Location`-when-unconfigured against the real compiled `CounterRestController`.
- ✅ **AC8 — ledger closed + green gates.** `deferred-work.md` spec-2-2 and spec-2-3 command-status `Location` entries flipped to `RESOLVED 2026-07-07 by Story 2.6`. Release build clean; all three configured focused suites green.

**Additional correctness hardening (defense-in-depth, matching the codebase's existing pattern):** added `statusLocationBuilder` and `__hexalithStatusLocation` to `RestApiControllerEmitter.ReservedActionIdentifiers` — the sibling ctor param `gateway` and every generated `__hexalith*` local were already reserved so a user route/query parameter sanitized to that name emits a friendly `RestApiDuplicateParameter` diagnostic instead of uncompilable generated code.

**Coordination note:** Story **2.7** (AD-18, outbound DAPR routing-header ownership) is a `ready-for-dev` sibling that also edits `EventStoreServiceCollectionExtensions.cs`, `Sample.Api/Program.cs`, and `Sample.Tests`. This story's edits are additive and non-conflicting; the existing `DaprAppIdHandler` wiring in `Sample.Api/Program.cs` was left intact (2.7 owns its removal).

### File List

**Production (new):**
- `src/Hexalith.EventStore.Client/Gateway/CommandStatusLocationOptions.cs`
- `src/Hexalith.EventStore.Client/Gateway/ICommandStatusLocationBuilder.cs`
- `src/Hexalith.EventStore.Client/Gateway/CommandStatusLocationBuilder.cs`

**Production (modified):**
- `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs` — fail-closed `TryAddSingleton` default in `AddEventStoreGatewayClient`; new `AddEventStoreCommandStatusLocation(Uri)`; `using Microsoft.Extensions.DependencyInjection.Extensions;`.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` — conditional `ICommandStatusLocationBuilder` ctor param (command-action gate); replaced relative `Location` literal with `statusLocationBuilder.TryBuild(...)`; reserved `statusLocationBuilder` + `__hexalithStatusLocation`.
- `samples/Hexalith.EventStore.Sample.Api/Program.cs` — optional `EventStore:GatewayStatusBase` → `AddEventStoreCommandStatusLocation`; default fail-closed.

**Tests (new):**
- `tests/Hexalith.EventStore.Client.Tests/Gateway/CommandStatusLocationBuilderTests.cs`

**Tests (modified):**
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` — ctor literal; policy Location assertions (happy-path + referenced-command); new query-only CS9113 guard.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs` — `CreateController` DI surface; split configured/unconfigured 202 tests; local public-interface fake.
- `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs` — `CreateController` DI surface; configured (absolute) + fail-closed Location proofs; local public-interface fake.
- `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiStructuralTests.cs` — mechanical ctor-parameter assertion update (2-arg: `IEventStoreGatewayClient` + `ICommandStatusLocationBuilder`).

**Artifacts (modified):**
- `_bmad-output/implementation-artifacts/deferred-work.md` — spec-2-2 & spec-2-3 command-status Location entries flipped to `RESOLVED 2026-07-07 by Story 2.6`. *(This story enforces rest-generator-hardening backlog item **S2**; S1/S3/S4/S5/S6 remain open under their action item — backlog file not modified.)*
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story status `ready-for-dev` → `in-progress` → `review`.

### Change Log

- 2026-07-07: Implemented AD-17 generated command-status Location policy (Story 2.6). Added Client `ICommandStatusLocationBuilder`/`CommandStatusLocationOptions` seam (fail-closed default + `AddEventStoreCommandStatusLocation`); changed `RestApiControllerEmitter` to resolve an absolute `Location` at request time (conditional ctor param, no relative literal); replaced generator-output + runtime tests with configured/unconfigured/never-relative policy assertions; wired Sample host; closed spec-2-2/spec-2-3 deferred entries. Absorbs rest-generator-hardening S2. Gates green: Release build 0/0; Client.Tests 527, RestApi.Generators.Tests 110, Sample.Tests 116.

## Review Findings

_Senior code review (bmad-code-review, 3-layer adversarial: Blind Hunter · Edge Case Hunter · Acceptance Auditor), 2026-07-08. Diff baseline `1f3ae822`..`87ac4450`. Acceptance Auditor confirmed all 8 ACs satisfied and cleared the CA2007/Server.Tests concern. 4 findings survived triage (1 decision, 3 patch); 5 dismissed as noise/by-design._

**Decision-needed**

- [x] [Review][Decision] **Accepted as-is (kept strict throw) — 2026-07-08.** Blank/whitespace `CorrelationId` fails open (500) instead of fail-closed [src/Hexalith.EventStore.Client/Gateway/CommandStatusLocationBuilder.cs:15] — Resolution: keep the spec-mandated, test-asserted throw; the path is unreachable in the integrated system (gateway defaults `CorrelationId` to the always-present ULID `MessageId`), so no code/spec/test change. The generated command action calls `statusLocationBuilder.TryBuild(__hexalithResponse.CorrelationId, …)` inside `try { … } catch (EventStoreGatewayException ex)` (`RestApiControllerEmitter.cs:281`, catch at `:287`). A blank/whitespace key makes `ArgumentException.ThrowIfNullOrWhiteSpace` throw an `ArgumentException` — not an `EventStoreGatewayException` — so it escapes the catch → HTTP 500 *after the command was already submitted*: the one spot where the component's fail-closed thesis inverts to fail-open. **Tension:** the throw is spec-mandated (Task 1) and test-asserted (`CommandStatusLocationBuilderTests.TryBuild_WithBlankStatusKey_Throws`), and reachability is ~nil (the platform gateway defaults `CorrelationId` to the always-present ULID `MessageId`). Options — (a) **Keep** the strict throw: a blank key is an upstream contract violation that should surface loudly (accept/dismiss); (b) **Harden to fail-closed**: builder returns `false` (no `Location`, still `202`) on a blank key, updating the spec + the `_Throws` test accordingly. (blind+edge)

**Patch**

- [x] [Review][Patch] **Applied 2026-07-08.** Base-URI robustness — query/fragment base composes a broken 404 `Location`; relative/invalid base via direct `Configure` throws at request time (500) [src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:65; src/Hexalith.EventStore.Client/Gateway/CommandStatusLocationBuilder.cs:23] — (a) `AddEventStoreCommandStatusLocation` rejects only non-absolute / non-http(s) / userinfo; a base carrying a query or fragment (e.g. `https://gw.example/?x=1`) passes, and the builder's `AbsoluteUri.TrimEnd('/') + "/api/v1/commands/status/" + key` swallows the path into the query/fragment → client polls a 404 (defeats AD-17's whole purpose). (b) The builder null-checks the base then calls `.AbsoluteUri` unconditionally; a relative/invalid `Uri` set directly via `services.Configure<CommandStatusLocationOptions>` (bypassing the validating helper) throws `InvalidOperationException` at request time → 500 (fail-open). Fix: validator also rejects non-empty `Query`/`Fragment` (keep a path prefix — it composes correctly for proxy-hosted gateways); builder guards `!gatewayStatusBase.IsAbsoluteUri` → fail-closed (`location = null; return false`). (blind+edge)

- [x] [Review][Patch] **Applied 2026-07-08.** New published public surface has no tests — `AddEventStoreCommandStatusLocation` validation branches + fail-closed DI default unexercised [src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:59] — `Hexalith.EventStore.Client` ships as a NuGet package; the extension's three throw conditions and the "always resolvable, fail-closed by default" DI guarantee (`AddEventStoreGatewayClient` `TryAddSingleton`) have zero coverage — a refactor that drops the `TryAddSingleton` or inverts a validation predicate stays green. Fix: add `Client.Tests` cases asserting (i) each `AddEventStoreCommandStatusLocation` rejection (non-absolute, non-http(s), userinfo) throws `ArgumentException`; (ii) after `AddEventStoreGatewayClient`, `BuildServiceProvider().GetRequiredService<ICommandStatusLocationBuilder>().TryBuild(...)` resolves and fails closed. (blind)

- [x] [Review][Patch] **Applied 2026-07-08.** Sample host config gate disagrees with the extension validator → userinfo/malformed value crashes startup instead of failing closed [samples/Hexalith.EventStore.Sample.Api/Program.cs:82] — The inline guard checks absolute + http/https but not userinfo (nor query/fragment); a value like `https://u:p@gw.example` passes it, reaches `AddEventStoreCommandStatusLocation`, which throws → unhandled at `builder.Build()`/startup, contradicting the adjacent comment "When the value is absent or malformed the … fail-closed default applies." Fix: mirror the extension's checks in the gate (skip → fail-closed), or wrap the call in try/catch and fall back to the fail-closed default. (blind+edge+auditor)

**Dismissed (5):** query-only reserved-name "false positive" — the reserved list is intentionally global (already mixes command-only `__hexalithRequest`/`__hexalithResponse` with query-only `ifNoneMatch`; new entries follow the same conservative pattern); double `AddEventStoreCommandStatusLocation` last-wins — standard additive `IServiceCollection.Configure` semantics; unbounded `Location` length — value = bounded config base + fixed path + ULID key; `AbsoluteUri` host/port/IDN normalization — standard and correct for a browser-facing origin; builder-vs-platform escaping asymmetry — identical for ULIDs, the only key type.

**Housekeeping (not a diff finding):** the Acceptance Auditor found the CLAUDE.md "known pre-existing build failure: `Server.Tests` does not build (CA2007)" note is likely **stale** — `tests/Directory.Build.props:10` `NoWarn`s `CA2007` for all test projects (the full-solution Release build below is clean with `Server.Tests` included). Worth reconciling separately.

**Review resolution — 2026-07-08.** Decision accepted as-is (kept strict throw). All 3 patch findings applied:
- Base-URI robustness — `AddEventStoreCommandStatusLocation` now also rejects a base with a query or fragment (`EventStoreServiceCollectionExtensions.cs`); `CommandStatusLocationBuilder.TryBuild` now fails closed on a non-absolute base instead of throwing `.AbsoluteUri` at request time.
- Test coverage — added `ServiceCollectionExtensionsTests` cases: fail-closed DI default resolves via `AddEventStoreGatewayClient`; configured base resolves an absolute builder; `AddEventStoreCommandStatusLocation` rejects non-origin (ftp / userinfo / query / fragment), relative, and null-services inputs (+8 tests).
- Sample host — `Sample.Api/Program.cs` config gate now mirrors the extension's userinfo/query/fragment checks so a malformed value is skipped (fail-closed) rather than crashing startup.

Gates re-run green: `dotnet build Hexalith.EventStore.slnx -c Release` → **0 Warning(s), 0 Error(s)**; `Client.Tests` **535/535** (was 527); `Sample.Tests` **116/116**; `RestApi.Generators.Tests` **110/110**.
