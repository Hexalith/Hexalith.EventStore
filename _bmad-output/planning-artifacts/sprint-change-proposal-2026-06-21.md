# Sprint Change Proposal — REST Controller Source Generator (Domain UI Host)

- **Date:** 2026-06-21
- **Author:** Administrator (via BMAD Correct Course)
- **Project:** Hexalith.EventStore
- **Mode:** Incremental
- **Change classification:** **Major** (new epic; new contract seam; new published analyzer package; cross-repo incl. submodule)
- **Status:** Approved for implementation
- **Builds on:** `sprint-change-proposal-2026-06-02.md` (Domain-Centric Modules / Zero-Boilerplate Platform — Epics A–C, complete)

---

## 1. Issue Summary

### Problem statement

Domain modules built on Hexalith.EventStore still **hand-write the typed REST surface** that exposes their
commands and queries over HTTP. The 2026-06-02 correction made domain *services* domain-centric (generic DAPR
endpoints + handler seams provided by the SDK), but the **public, typed, OpenAPI-documented REST API** is still
boilerplate authored by hand in each domain.

**Target principle (the correction):**

> A domain module's typed REST controllers must be **generated**, not hand-written. A domain author marks its
> command messages with `ICommandContract` and its query messages with `IQueryContract` (optionally annotating
> routes), and a **Roslyn source generator** emits the typed REST controllers into the **domain UI host**, each
> delegating to the existing `IEventStoreGatewayClient`. Adding a domain endpoint costs a contract annotation,
> not a controller.

### How it was discovered

Direct stakeholder capability request (Administrator), following the completion of the domain-centric SDK
refactor. It is the logical next increment: Epic A removed hosting/dispatch boilerplate from the domain
*service*; this removes the controller boilerplate from the domain *presentation/UI* host.

### Evidence

**A. Hand-written per-domain REST facades exist and repeat.**

- `references/Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs` — 6 hand-written GET actions
  (`GetTenant`, `GetTenantAudit`, `GetTenantUsers`, `GetUserTenants`, `GetGlobalAdministrators`, `ListTenants`),
  each manually building a `QueryEnvelope` and dispatching via `DomainQueryDispatcher`.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs` — a hand-written typed wrapper
  over the gateway client. The same shape, re-authored per domain.

**B. The message contracts are (mostly) already discoverable.**

- Queries implement `IQueryContract` with `static abstract QueryType / Domain / ProjectionType`
  (`src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs`) — example
  `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/GetTenantQuery.cs`.
- **Commands have no marker interface** — they are bare `public record CreateTenant(...)`
  (`references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/CreateTenant.cs`), identified only by folder/name
  convention. This gap must be closed for reliable command discovery.

**C. The generator pattern already exists in-house — build-vs-reuse resolves to "reuse the pattern".**

- `references/Hexalith.PolymorphicSerializations/src/libraries/Hexalith.PolymorphicSerializations.CodeGenerators/SerializationMapperSourceGenerator.cs`
  — a production `IIncrementalGenerator` (netstandard2.0, `IsRoslynComponent`, packaged as
  `analyzers/dotnet/cs`, keyed off `[PolymorphicSerialization]`). The exact template.
- `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.SourceTools/FrontComposerGenerator.cs` — a second
  `IIncrementalGenerator` that already discovers a `[Command]` attribute and emits registration code. Precedent
  for attribute-driven message discovery + emission.

**D. The dispatch seam already exists.**

- `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs` —
  `SubmitCommandAsync(SubmitCommandRequest)` / `SubmitQueryAsync<T>(SubmitQueryRequest)` →
  `POST /api/v1/commands|queries`. Generated controllers delegate here, so the **gateway remains the single
  auth/validation/observability front door** (no in-process auth bypass).

---

## 2. Impact Analysis

### Architectural context (from the investigation)

Three REST layers exist today; only one is hand-written boilerplate:

| Layer | Example | Generated? |
|---|---|---|
| Generic gateway (domain-agnostic) | `POST /api/v1/commands`, `POST /api/v1/queries` (string `domain`/`type` + opaque JSON) | No — stays as-is |
| **Per-domain typed facade** | `TenantsQueryController`, `CounterQueryService` | **← target of generation** |
| DAPR-internal | SDK `MapEventStoreDomainService` (`/process`, `/query`, …) | No — already generic |

### Epic impact

- Epics A–C (2026-06-02) are **complete**; this change does not modify them.
- **Adds one new epic — Epic D: REST Controller Source Generator** (Section 4).
- Only CI-gated leftovers remain from Epic B (Tenants `IntegrationTests` DAPR-fixture migration; 5
  nested-submodule doc tests) — **unaffected**.
- Must remain compatible with the C3 guardrail (`DomainModuleAuthoringGuardrailTests`).

### Artifact conflicts (to be reconciled)

| Artifact | Conflict / change | Action |
|---|---|---|
| `src/Hexalith.EventStore.Contracts` | No command marker; no REST routing metadata | Add `ICommandContract`, `[RestRoute]`, `[RestApi]` (Epic D1) |
| `Directory.Packages.props` | No Roslyn analyzer deps | Add `Microsoft.CodeAnalysis.CSharp` / `.Analyzers` (D2) |
| `Hexalith.EventStore.slnx` | New analyzer + test projects | Add `RestApi.Generators` (+ `.Tests`) (D2/D4) |
| `references/Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs` | Hand-written; relocating to UI host | Delete; generate into `Hexalith.Tenants.UI` (D7, submodule PR) |
| `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs` | Hand-written wrapper | Replace with generated controller (D5) |
| `CLAUDE.md` | Package count (8→9); no controller-generation authoring rule | Patch (D8) |
| `docs/brownfield/architecture.md`, `integration-architecture.md` | Omit the generator + generated-REST principle | Patch (D8) |
| Release pipeline + package-governance tests | Expect 8 packages | Update to 9 (D8) |

### Technical impact

- **Additive, non-breaking platform work:** new contract types alongside existing ones; the generic gateway,
  DAPR seams, and `IEventStoreGatewayClient` are unchanged.
- **New project type for the EventStore repo:** a `netstandard2.0` Roslyn analyzer (`IsRoslynComponent`,
  `EnforceExtendedAnalyzerRules`). Precedent exists in the `PolymorphicSerializations` and `FrontComposer`
  submodules; this is the first one in the EventStore repo proper. Watch the interaction with the repo's
  `TreatWarningsAsErrors=true` (analyzer projects use the extended analyzer ruleset, not the runtime CA rules).
- **New published package (8 → 9):** `Hexalith.EventStore.RestApi.Generators` packaged as
  `analyzers/dotnet/cs` (modeled on `PolymorphicSerializations.CodeGenerators`). Release-pipeline-affecting —
  the A6-class decision, approved.

### Key behavioral consequence — Tenants query path relocates (confirmed)

Today `TenantsQueryController` lives in the Tenants **service** host and dispatches queries **in-process**
(`DomainQueryDispatcher`). Under the approved "UI host + gateway client" design, the generated controllers go
into `Hexalith.Tenants.UI` and call the **gateway** (`/api/v1/queries` → DAPR → domain `/query` handler). The
5 `IDomainQueryHandler`s are unchanged; only the **public REST facade relocates** (service-host in-process →
UI-host via gateway). Net effect: one extra network hop, but the service host becomes truly headless
(DAPR-only, no public controllers) and the gateway stays the single auth front door — *more* aligned with the
domain-centric end-state. Accepted as the intended design (Section 3).

### Risk / unknowns

| Risk | Severity | Mitigation |
|---|---|---|
| Generator correctness across route shapes (path params, `~/`-absolute routes, body-vs-route binding) | Medium | D4 snapshot/`CSharpGeneratorDriver` tests cover discovery, route attr, convention fallback, multi-message, misuse diagnostics; prove on real domains (D5–D7) |
| `ICommandContract` rollout touches every command record | Low | One-line adoption per command (`AggregateId` expression + static members); additive |
| Analyzer build under warnings-as-errors | Low-Medium | D2 spike emits a trivial artifact first to validate the toolchain before real emission |
| Tenants relocation changes request path | Medium | Flagged + accepted; gateway already routes handler-based queries (A7b `HandlerAwareQueryRouter`); submodule unit + integration tests gate D7 |

---

## 3. Recommended Approach

**Selected path: Direct Adjustment — additive new epic (Epic D)**, mirroring the proven Epic-A shape:
*build the platform capability (generator + contract seam) → prove it on real domains → codify
guardrails/docs.*

### Why this over alternatives

- **Not a Roslyn-free runtime mapping:** the explicit request is a *source code generator*, and the in-repo
  `IIncrementalGenerator` precedent (PolymorphicSerializations) makes compile-time generation low-risk, with
  real typed actions in OpenAPI and zero runtime reflection cost.
- **Not gateway-hosted controllers:** generating into the **domain UI host** (delegating to
  `IEventStoreGatewayClient`) keeps the gateway domain-agnostic and preserves its central
  auth/validation/status/archive/ETag pipeline — the generated controllers are thin typed BFF facades.
- **Not convention-only discovery:** commands lack a reliable discriminator today; the `ICommandContract`
  marker (symmetric with `IQueryContract`) plus `[RestRoute]` gives explicit, refactor-safe discovery and rich
  routes (e.g. `~/api/users/{userId}/tenants`).

### Effort / risk

| Epic | Effort | Risk | Notes |
|---|---|---|---|
| D — REST Controller Source Generator | Medium | **Low–Medium** | Additive, non-breaking; risk concentrated in generator robustness + command-marker rollout; proven by D5–D7 adoptions |

---

## 4. Detailed Change Proposals

### Epic D — EventStore REST Controller Source Generator

**Goal:** a domain author annotates command/query message contracts; a Roslyn incremental generator emits
typed, OpenAPI-documented REST controllers into the domain UI host, each delegating to
`IEventStoreGatewayClient` — eliminating hand-written controllers (`TenantsQueryController`) and BFF wrappers
(`CounterQueryService`).

| Story | Scope | Effort/Risk |
|---|---|---|
| **D1 — Contract seam** | In `Hexalith.EventStore.Contracts`: add `ICommandContract` (static-abstract `Domain`, `CommandType`; instance `AggregateId`); `[RestRoute(verb, template)]`; assembly-level `[RestApi(prefix, tag, tenantSource)]`. Define convention fallback. Unit tests. | Low / Low |
| **D2 — Generator skeleton + spike** | New `src/Hexalith.EventStore.RestApi.Generators` (netstandard2.0, `IsRoslynComponent`, analyzer-packaged), modeled on `SerializationMapperSourceGenerator`. Discovery: queries via `IQueryContract`, commands via `ICommandContract`. Emit a trivial artifact first to validate the toolchain. Add `Microsoft.CodeAnalysis.CSharp` to `Directory.Packages.props`; add projects to `.slnx`. | Medium / Medium |
| **D3 — Controller emission** | Generate per-domain `[ApiController][Authorize][Route][Tags]` controllers; one action per message with typed `[FromRoute]/[FromBody]/[FromQuery]` binding + `[ProducesResponseType]`; body builds `SubmitCommandRequest`/`SubmitQueryRequest`, calls `IEventStoreGatewayClient`, maps result → HTTP (202+Location / 200+ETag+304 / 4xx). **Body-authoritative** route/body binding with 400 on mismatch. Honors ULID rules, `ConfigureAwait(false)`, file-scoped ns, no copyright header; emits `partial` controllers. | Medium / Medium |
| **D4 — Generator tests** | `tests/Hexalith.EventStore.RestApi.Generators.Tests` (xUnit v3 + snapshot/Verify or `CSharpGeneratorDriver`): query/command discovery, route attr vs convention, multi-message, non-marker ignored, misuse diagnostics. | Medium / Low |
| **D5 — Proof: Sample BlazorUI (queries)** | Annotate Counter query; generate controllers in `Hexalith.EventStore.Sample.BlazorUI`; remove hand-written `CounterQueryService`. In-repo proof. | Low / Low |
| **D6 — Proof: Counter commands** | Add `ICommandContract` to Sample Counter command(s); generate POST command controller via `SubmitCommandAsync`. Proves command path end-to-end. | Low / Low |
| **D7 — Proof: Tenants UI host (submodule)** | Annotate Tenants commands + 6 query contracts (rich routes incl. `~/api/users/{userId}/tenants`); generate controllers into `Hexalith.Tenants.UI`; **delete `TenantsQueryController`**. Separate PR in the submodule; submodule unit + integration tests stay green. | Medium / Medium |
| **D8 — Packaging + docs + guardrail** | Publish generator as analyzer NuGet (8→9): release pipeline + package-governance tests. CLAUDE.md package list + authoring rule. Brownfield doc edits. Verify/extend `DomainModuleAuthoringGuardrailTests`. | Medium / Low |

**Sequencing:** D1 → D2 → D3 → D4 (foundation) → D5/D6 (in-repo proof, parallelizable) → D7 (submodule) → D8 (closeout).

### 4.1 Contract seam (D1) — author-facing API

**`src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs`** (new):
```csharp
namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Marks a command message as exposable through a generated REST endpoint.
/// Mirrors <see cref="Hexalith.EventStore.Contracts.Queries.IQueryContract"/>.
/// </summary>
public interface ICommandContract
{
    /// <summary>Gets the kebab-case command type discriminator (no colons).</summary>
    static abstract string CommandType { get; }

    /// <summary>Gets the kebab-case domain name.</summary>
    static abstract string Domain { get; }

    /// <summary>Gets the aggregate id this command targets (used for routing + envelope).</summary>
    string AggregateId { get; }
}
```
Adoption cost per command: one expression-bodied property (e.g. `public string AggregateId => TenantId;`) plus
the two static members.

**`src/Hexalith.EventStore.Contracts/Rest/RestRouteAttribute.cs`** (new):
```csharp
namespace Hexalith.EventStore.Contracts.Rest;

public enum RestVerb { Get, Post, Put, Patch, Delete }

/// <summary>Overrides the generated HTTP verb + route template for a command/query message.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RestRouteAttribute(RestVerb verb, string template) : Attribute
{
    public RestVerb Verb { get; } = verb;
    public string Template { get; } = template; // e.g. "{tenantId}/users", "~/api/users/{userId}/tenants"
}
```

**`src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs`** (new):
```csharp
namespace Hexalith.EventStore.Contracts.Rest;

public enum RestTenantSource { Claims, Route, System }

/// <summary>Opts a domain assembly into REST controller generation and sets shared options.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class RestApiAttribute(string routePrefix, string? tag = null,
    RestTenantSource tenantSource = RestTenantSource.Claims) : Attribute
{
    public string RoutePrefix { get; } = routePrefix; // default convention: "api/{domain}"
    public string? Tag { get; } = tag;
    public RestTenantSource TenantSource { get; } = tenantSource;
}
```
`IQueryContract` is unchanged at runtime; queries gain an optional `[RestRoute]`. **Convention fallback** (no
`[RestRoute]`): commands → `POST {prefix}`; queries → `GET {prefix}` (or `POST` if the query carries a body
payload).

### 4.2 Generated-controller exemplar (D3) — emitted output

```csharp
// <auto-generated/> by Hexalith.EventStore.RestApi.Generators
#nullable enable
namespace Hexalith.Tenants.UI.Generated;

[ApiController]
[Authorize]
[Route("api/tenants")]
[Tags("tenants")]
public sealed partial class TenantsRestController(
    IEventStoreGatewayClient gateway,
    IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    // from: [RestRoute(RestVerb.Get, "{tenantId}")] on GetTenantQuery
    [HttpGet("{tenantId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetTenantAsync(
        [FromRoute] string tenantId,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        CancellationToken cancellationToken)
    {
        var request = new SubmitQueryRequest(
            Tenant: ResolveTenant(), Domain: GetTenantQuery.Domain,
            AggregateId: tenantId, QueryType: GetTenantQuery.QueryType);
        EventStoreQueryResult result = await gateway
            .SubmitQueryAsync(request, ifNoneMatch, cancellationToken).ConfigureAwait(false);
        return MapQueryResult(result); // generated helper: 200/304/4xx + ETag
    }

    // from: ICommandContract + [RestRoute(RestVerb.Post, "{counterId}/increment")] on IncrementCounter
    [HttpPost("~/api/counters/{counterId}/increment")]
    [ProducesResponseType(typeof(SubmitCommandResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IncrementCounterAsync(
        [FromRoute] string counterId, [FromBody] IncrementCounter body,
        CancellationToken cancellationToken)
    {
        var request = new SubmitCommandRequest(
            MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
            Tenant: ResolveTenant(), Domain: IncrementCounter.Domain,
            AggregateId: body.AggregateId, CommandType: IncrementCounter.CommandType,
            Payload: JsonSerializer.SerializeToElement(body));
        SubmitCommandResponse response = await gateway
            .SubmitCommandAsync(request, cancellationToken).ConfigureAwait(false);
        return AcceptedAtAction(null, new { correlationId = response.CorrelationId }, response);
    }

    private string ResolveTenant() => /* per [RestApi] TenantSource: claims | route | "system" */;
    private IActionResult MapQueryResult(EventStoreQueryResult r) => /* status/ETag mapping */;
}
```
**Binding decision:** route param is RESTful but the `[FromBody]` command is authoritative for the aggregate id;
a route-vs-body mismatch returns 400 (no `with`-rewrite codegen needed). Generated code delegates to
`IEventStoreGatewayClient` (gateway keeps auth/validation/status/archive), uses `UniqueIdHelper` for ULIDs,
`ConfigureAwait(false)`, file-scoped namespaces, no copyright header, and `partial` controllers for extension.

### 4.3 Documentation & governance edits (D8)

**`CLAUDE.md` — NuGet Packages**
```
OLD:
## NuGet Packages (8 published)
Hexalith.EventStore.Contracts, Client, Server, SignalR, Testing, Aspire, ServiceDefaults, DomainService

NEW:
## NuGet Packages (9 published)
Hexalith.EventStore.Contracts, Client, Server, SignalR, Testing, Aspire, ServiceDefaults, DomainService, RestApi.Generators

`RestApi.Generators` is a Roslyn analyzer package (analyzers/dotnet/cs, netstandard2.0) that
emits typed REST controllers from `ICommandContract`/`IQueryContract` messages into a domain UI host.
```

**`CLAUDE.md` — "Domain-Module Authoring (domain-centric)" → Rules** (new bullet):
```
- **REST controllers** — a domain **must not** hand-write per-message REST controllers or BFF
  query/command wrappers. Mark command messages with `ICommandContract` and query messages with
  `IQueryContract` (+ optional `[RestRoute(verb, template)]`, assembly-level `[RestApi(prefix, tag, tenantSource)]`),
  and the `Hexalith.EventStore.RestApi.Generators` analyzer emits the typed controllers into the
  **domain UI host**, each delegating to `IEventStoreGatewayClient` (the gateway stays the auth/validation
  front door). Hand-rolled controllers like the former `TenantsQueryController` are the anti-pattern this replaces.
```

**`CLAUDE.md` — "Host shape" note** (appended):
```
The domain UI host carries no hand-written command/query controllers — they are generated from the
domain's message contracts by the RestApi.Generators analyzer.
```

**`docs/brownfield/architecture.md`** — §4 "The Parts": add a `Hexalith.EventStore.RestApi.Generators` row
(Roslyn analyzer; input = `ICommandContract`/`IQueryContract`; output = typed UI-host controllers). §11 "Key
Design Decisions": add *"Per-domain REST surface is generated, not hand-written; generated controllers are
gateway-backed BFF facades so central auth/validation/observability is never bypassed."*

**`docs/brownfield/integration-architecture.md`** — extend the domain-centric principle: the typed per-domain
REST API is a generated artifact in the UI host; the headless domain service exposes only DAPR endpoints.

**Release/governance** — register the analyzer package in the release pipeline (`.releaserc.json` / pack
scripts); update package-governance tests to expect **9** packages; confirm `DomainModuleAuthoringGuardrailTests`
passes (optionally extend it to flag hand-written `*Controller` in a domain when a generated one exists).

---

## 5. Implementation Handoff

- **Scope classification:** **Major** (new epic; new contract seam; new published package; cross-repo incl.
  submodule).
- **Routing:**
  - **PM / Architect** (John / Winston): fold Epic D into the planning artifacts; own the published-package
    decision (D8, A6-class) and confirm the contract seam public API (D1).
  - **Developer** (Amelia): implement D1 → D2 → D3 → D4 (platform), then D5/D6 (in-repo proof), then D7
    (submodule PR), then D8 (closeout). D7 is a separate PR in `Hexalith.Tenants`.
- **Success criteria:**
  1. A domain marks its command messages with `ICommandContract` and query messages with `IQueryContract`
     (+ optional `[RestRoute]`/`[RestApi]`) and the analyzer emits compiling, OpenAPI-visible typed controllers
     into the domain UI host — **no hand-written controller**.
  2. `samples/Hexalith.EventStore.Sample.BlazorUI` runs on generated controllers with `CounterQueryService`
     removed; the Counter command path is generated (POST) and exercised.
  3. `Hexalith.Tenants.UI` runs on generated controllers with `TenantsQueryController` deleted; submodule unit
     + integration tests green.
  4. Generator covered by `Hexalith.EventStore.RestApi.Generators.Tests`; whole-graph Release build clean under
     `TreatWarningsAsErrors`; package count is 9 and the release pipeline packs the analyzer.

---

## 6. Approval

- [x] Approved for implementation — 2026-06-21
- [ ] Approved with changes (noted below)
- [ ] Rejected / revise

Notes: Approved by Administrator. Scope: **Major** — route to PM/Architect (fold Epic D into planning
artifacts, own the published-package decision D8/A6-class, confirm the D1 contract public API), then
Developer for implementation D1 → D2 → D3 → D4 → D5/D6 → D7 → D8. D7 is a separate PR in the
`Hexalith.Tenants` submodule.
