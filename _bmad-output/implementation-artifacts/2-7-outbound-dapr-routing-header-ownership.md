# Story 2.7: Outbound DAPR Routing-Header Ownership

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform maintainer,
I want outbound DAPR service-invocation clients to own and replace the sidecar routing headers through a single platform handler,
so that a caller- or inbound-supplied `dapr-app-id` / `dapr-api-token` can never duplicate or hijack sidecar routing or leak a token, and no host re-implements the handler.

## Acceptance Criteria

1. **Replace semantics (the policy).** A platform-owned outbound DAPR service-invocation handler in `Hexalith.EventStore.Client`, when it processes an outbound request, **removes** any pre-existing `dapr-app-id` then sets the configured app id as the **single** value; **removes** any pre-existing `dapr-api-token` unconditionally, then sets the configured token **only when one is present** (else the request carries no token header). It never uses a bare `TryAddWithoutValidation` (append) for these headers.
2. **Innermost in the chain.** The handler runs **last** (innermost, closest to the network) in each host's client handler chain — after any inbound bearer / header-forwarding handler — so it has the final say over the control-plane headers.
3. **Centralized + reusable across both wiring shapes.** The handler is wired through a single platform extension usable by **both** client shapes in use:
   - the typed gateway client `AddEventStoreGatewayClient(...)` (Sample.Api, Sample.BlazorUI), and
   - a plain named `AddHttpClient(...)` builder (Admin.UI's `"AdminApi"` client, app id `eventstore-admin`).
   Sample.Api, Sample.BlazorUI, and Admin.UI wire only the platform extension; their three local `DaprAppIdHandler` copies are **deleted**.
4. **Regression proof (seeded pre-existing header).** A unit test seeds a conflicting `dapr-app-id` **and** `dapr-api-token` on the outbound request, runs the handler, and asserts the captured outgoing request carries **exactly one** authoritative value for each (the injected value discarded). A second case: with **no** token configured but a pre-existing `dapr-api-token` seeded, the outgoing request carries **no** token header. Happy-path single-value behavior remains asserted.
5. **Guardrail (regression lock).** A structural test fails if any host declares its own DAPR routing-header `DelegatingHandler` or uses `TryAddWithoutValidation` for `dapr-app-id` / `dapr-api-token`. It passes only when the sole setter of these headers is the platform handler.
6. **Tenants submodule = coordinated follow-up.** The identical `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/DaprAppIdHandler.cs` is **not** modified in this story. The required equivalent fix is recorded as a coordinated submodule follow-up requiring maintainer approval (Story 2.4 lineage).
7. **Green gates.** Release build is clean and all configured focused tests pass — including the new replacement and guardrail tests — with no CA2007 / warnings-as-errors regressions.

## Tasks / Subtasks

- [ ] **Task 1 — Canonical platform handler** (AC: 1, 2)
  - [ ] Add `src/Hexalith.EventStore.Client/Handlers/DaprServiceInvocationHandler.cs` — `internal sealed class DaprServiceInvocationHandler(string appId, string? apiToken) : DelegatingHandler`. In `SendAsync`: `ArgumentNullException.ThrowIfNull(request)`; `Headers.Remove("dapr-app-id")` then `TryAddWithoutValidation("dapr-app-id", appId)`; `Headers.Remove("dapr-api-token")` then, only when `apiToken is { Length: > 0 }`, `TryAddWithoutValidation("dapr-api-token", apiToken)`.
  - [ ] Keep it `internal` — publicly exposed only via the extension in Task 2 (minimizes the published Client package's public surface). Match the existing `Hexalith.EventStore.Client` file style (see `Registration/EventStoreServiceCollectionExtensions.cs`).
- [ ] **Task 2 — Reusable wiring extension** (AC: 3)
  - [ ] Add `public static IHttpClientBuilder AddEventStoreDaprServiceInvocation(this IHttpClientBuilder builder, string appId, string? apiToken = null)` in `Client/Registration/EventStoreServiceCollectionExtensions.cs` that appends the handler via `builder.AddHttpMessageHandler(() => new DaprServiceInvocationHandler(appId, apiToken))`. Ensure it appends **last** so it stays innermost (call it after the auth/forwarding handler registration — document the ordering requirement in XML docs, referencing AD-18).
  - [ ] Validate args (`ThrowIfNullOrWhiteSpace(appId)`).
- [ ] **Task 3 — Migrate the three in-repo hosts** (AC: 3)
  - [ ] `samples/Hexalith.EventStore.Sample.Api/Program.cs:76` → replace `.AddHttpMessageHandler(() => new DaprAppIdHandler("eventstore", daprApiToken))` with `.AddEventStoreDaprServiceInvocation("eventstore", daprApiToken)`. Delete `samples/Hexalith.EventStore.Sample.Api/Services/DaprAppIdHandler.cs`.
  - [ ] `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs:64` → same replacement (`"eventstore"`). Delete `samples/Hexalith.EventStore.Sample.BlazorUI/Services/DaprAppIdHandler.cs`.
  - [ ] `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:115` → replace `.AddHttpMessageHandler(() => new DaprAppIdHandler("eventstore-admin", daprApiToken))` with `.AddEventStoreDaprServiceInvocation("eventstore-admin", daprApiToken)` on the `"AdminApi"` `IHttpClientBuilder`. Delete `src/Hexalith.EventStore.Admin.UI/Services/DaprAppIdHandler.cs`.
  - [ ] Grep for any remaining `DaprAppIdHandler` references in this repo (usings, tests) and update them.
- [ ] **Task 4 — Replacement unit tests** (AC: 1, 4)
  - [ ] Add tests in `tests/Hexalith.EventStore.Client.Tests` (Tier 1) using a capturing `HttpMessageHandler` (mirror the `CaptureHandler` pattern in `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGatewayHandlerTests.cs`): (a) seed conflicting `dapr-app-id`+`dapr-api-token` → assert `GetValues(...).ShouldBe(["eventstore"])` / `["secret-token"]` (single authoritative value); (b) no token configured + seeded token → `request.Headers.Contains("dapr-api-token").ShouldBeFalse()`; (c) clean request happy path unchanged.
  - [ ] Update `SampleApiGatewayHandlerTests` so it exercises the platform extension (not a deleted local handler); keep its 202/ETag/304 assertions intact.
- [ ] **Task 5 — Guardrail structural test** (AC: 5)
  - [ ] Add a structural/guardrail test (extend `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiStructuralTests.cs` and/or add one in `Client.Tests` / `Admin.UI.Tests`) that fails if a host assembly declares a `DelegatingHandler` type setting `dapr-app-id`, or if host source under `**/Services/` contains `DaprAppIdHandler` or `TryAddWithoutValidation("dapr-app-id"` / `"dapr-api-token"`. Prefer a source-scan for the `TryAddWithoutValidation` clause (reflection can't see the call); assembly-reflection for the "no host-local handler type" clause. Emit a support-safe failure message naming the offending host + the AD-18 rule.
  - [ ] Confirm `SampleApiStructuralTests` does not still assert the *existence* of the now-deleted local handler; update those assertions to point at the platform handler.
- [ ] **Task 6 — Tenants follow-up + gates** (AC: 6, 7)
  - [ ] Do **not** edit the Tenants submodule. Confirm the coordinated follow-up is recorded (deferred-work.md already annotated; leave a `File List` note).
  - [ ] Run: `dotnet build Hexalith.EventStore.slnx -c Release`; `dotnet test tests/Hexalith.EventStore.Client.Tests/`; `dotnet test tests/Hexalith.EventStore.Sample.Tests/`; `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/`. All green, no CA2007 regressions.

## Dev Notes

### What this is (and is not)
- **Governed by architecture invariant AD-18** — *Outbound Sidecar Control-Plane Headers Are Handler-Owned* (`_bmad-output/planning-artifacts/architecture.md`). The story implements AD-18 verbatim. Full rationale + decision record: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-outbound-dapr-routing-header-policy.md`.
- **Honest severity (do not overstate — Epic 1 retro R1-A8 false-CRITICAL rule):** in the three hosts today the outbound `HttpRequestMessage` is freshly created per call and the inbound-forwarding handlers forward only `Authorization`, so there is no *known live* exploit path that seeds a conflicting `dapr-app-id`. This is a **defense-in-depth + invariant guarantee**: the handler must own the control-plane headers regardless of what any current or future forwarding handler does. Frame commit messages and notes accordingly (`fix:`/`refactor:`, not a security-CRITICAL claim).

### Root defect (pre-existing pattern)
Four byte-identical `DaprAppIdHandler` copies each do `request.Headers.TryAddWithoutValidation("dapr-app-id", appId)` — **append**, not replace. `HttpHeaders.TryAddWithoutValidation` adds an additional value when one already exists; DAPR's resolution of a duplicated `dapr-app-id` is undefined. The fix is `Remove` + `TryAddWithoutValidation`.

### Files being changed
| File | Change | Current state / notes |
| --- | --- | --- |
| `src/Hexalith.EventStore.Client/Handlers/DaprServiceInvocationHandler.cs` | **NEW** | Canonical `internal sealed` handler, replace semantics. |
| `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs` | **UPDATE** | Already holds `AddEventStoreGatewayClient` + K&R brace style. Add `AddEventStoreDaprServiceInvocation(IHttpClientBuilder, appId, apiToken?)`. |
| `samples/Hexalith.EventStore.Sample.Api/Program.cs` | **UPDATE** | Line ~76 wires local handler after `InboundBearerForwardingHandler`. |
| `samples/Hexalith.EventStore.Sample.Api/Services/DaprAppIdHandler.cs` | **DELETE** | app id `eventstore`. |
| `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` | **UPDATE** | Line ~64 wires local handler after `EventStoreApiAuthorizationHandler`. |
| `samples/Hexalith.EventStore.Sample.BlazorUI/Services/DaprAppIdHandler.cs` | **DELETE** | app id `eventstore`. |
| `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs` | **UPDATE** | Line ~110-115: **plain `AddHttpClient("AdminApi", …)`**, NOT `AddEventStoreGatewayClient`; app id `eventstore-admin`. This is why the extension must target `IHttpClientBuilder`, not be baked into the gateway-client method. |
| `src/Hexalith.EventStore.Admin.UI/Services/DaprAppIdHandler.cs` | **DELETE** | app id `eventstore-admin`. |
| `tests/Hexalith.EventStore.Client.Tests/*` | **NEW** | Replacement unit tests (Tier 1). |
| `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGatewayHandlerTests.cs` | **UPDATE** | Re-point off the deleted local handler; keep behavior assertions. |
| `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiStructuralTests.cs` | **UPDATE** | Guardrail; remove any assertion of the deleted local handler's existence. |
| `references/Hexalith.Tenants/**/DaprAppIdHandler.cs` | **DO NOT TOUCH** | Submodule; coordinated maintainer-approval follow-up only. |

### Constraints & conventions (project-context.md / architecture.md)
- **Handler ordering:** `AddHttpMessageHandler` registers outer→inner in call order; the DAPR handler must be added **last** so it is innermost and wins (AD-18 point 3). Do not reorder it before the auth/forwarding handler.
- **Keep `Authorization` untouched** — DAPR forwards it unchanged; JWT/RBAC/tenant enforcement at the gateway must be preserved (the current handlers document this).
- **CA2007 / warnings-as-errors:** current handlers `return base.SendAsync(...)` without awaiting (no `ConfigureAwait` needed). If you await, add `.ConfigureAwait(false)`. `TreatWarningsAsErrors=true`.
- **Identity:** N/A here (no id parsing), but never introduce `Guid.TryParse` on id fields.
- **Domain-centric (AD-2):** the whole point is to *remove* per-host transport boilerplate — hosts must end with zero DAPR-header handler code. `Hexalith.EventStore.Client` is a **published package** (`tools/release-packages.json`); the new public extension is an intentional, minimal API addition — do not expose the handler type itself.
- **Style:** match the file you edit (`Client` extensions use same-line braces; the sample handlers vary). Don't add copyright headers.

### Testing standards
- **Framework:** xUnit v3 + Shouldly (`ShouldBe`), never raw `Assert.*`. Run test projects individually.
- **Evidence for AC4 is the outgoing request headers** captured via a stub `HttpMessageHandler` (see `CaptureHandler` in `SampleApiGatewayHandlerTests`). This is a Tier-1 transport unit test — the Tier-2/3 "assert persisted state-store end-state" rule (R2-A6) does **not** apply here; there is no state store in this path. Do not fabricate a Redis assertion.
- **Tiers touched:** Client.Tests (T1), Sample.Tests (T1), Admin.UI.Tests (T1) — all CI-gated.

### Previous-story intelligence
- **Story 2.3 (Sample External API Host Proof)** is where this defect was found (`_bmad-output/implementation-artifacts/spec-2-3-sample-external-api-host-proof.md`; deferred-work.md spec-2-3 entry, now reconciled to AD-18 + this story). The `SampleApiGatewayHandlerTests`/`SampleApiStructuralTests` established the capture-handler and structural-test patterns to reuse.
- **Story 2.6 (Generated Command-Status Location Policy, AD-17)** is a sibling Epic-2 post-retro hardening story added the same day — independent surface (command-status `Location`), no code overlap; do not conflate.
- **Concurrency caution:** a parallel auto-dev loop is active in this repo and may auto-commit to `main`. Check `git status`/refs before committing; keep this story's diff self-contained to the files above.

### Project Structure Notes
- Handler lives in `Client/Handlers/` alongside `DomainProcessorBase`/`IDomainProcessor`; wiring in `Client/Registration/` alongside `AddEventStoreGatewayClient`. No new project. No change to `Hexalith.EventStore.Gateway` (that package is the *inbound* server-host composition, not outbound client transport).
- No AppHost/DAPR YAML change (AD-9 not triggered) — app ids `eventstore` / `eventstore-admin` are unchanged; only *how* the header is set changes.

### References
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-18 - Outbound Sidecar Control-Plane Headers Are Handler-Owned]
- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.7: Outbound DAPR Routing-Header Ownership]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-outbound-dapr-routing-header-policy.md]
- [Source: _bmad-output/implementation-artifacts/deferred-work.md#spec-2-3 DaprAppIdHandler entry (reconciled 2026-07-07)]
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs#AddEventStoreGatewayClient]
- [Source: samples/Hexalith.EventStore.Sample.Api/Program.cs:71-76] [Source: samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs:60-64] [Source: src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:109-115]
- [Source: _bmad-output/project-context.md#Framework-Specific Rules — outbound DAPR routing headers handler-owned/replaced (AD-18)]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed - comprehensive developer guide created.

### File List
