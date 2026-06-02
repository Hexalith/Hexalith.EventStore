# Sprint Change Proposal — Domain-Centric Modules / Zero-Boilerplate Platform

- **Date:** 2026-06-02
- **Author:** Administrator (via BMAD Correct Course)
- **Project:** Hexalith.EventStore
- **Mode:** Batch
- **Change classification:** **Major** (architectural-direction correction; new epics required)
- **Status:** Awaiting approval

---

## 1. Issue Summary

### Problem statement

Domain modules that build on Hexalith.EventStore (e.g. the `Hexalith.Tenants` submodule) currently
**re-implement platform infrastructure instead of consuming it**. A domain author today must hand-write
hosting, DAPR actor hosting, query routing, Aspire topology, ServiceDefaults, health checks, telemetry,
and event-subscription plumbing — none of which is domain logic.

**Target principle (the correction):**

> Domain modules must be **domain-centric**: they contain only aggregates, commands, events, projections,
> validators, and contracts. **All boilerplate required to run on Hexalith.EventStore must be provided by
> the EventStore client libraries** (a "domain-service SDK"), so a new domain costs ~2 lines of hosting
> plus domain code.

### How it was discovered

Triggered during brownfield documentation (the `docs/brownfield/*` set generated 2026-06-02). Comparing
the two existing domain implementations exposed a sharp divergence:

- The **reference Sample** (`samples/Hexalith.EventStore.Sample`) is already near-ideal.
- The **Hexalith.Tenants** submodule diverged into a standalone mini-platform that duplicates EventStore.

### Evidence

**A. The Sample proves the platform already supports the goal.**
`samples/Hexalith.EventStore.Sample` — 88% of `.cs` files are pure domain (Counter/Greeting). It references
only **two** platform libraries and wires in ~4 lines:

```csharp
// samples/Hexalith.EventStore.Sample/Program.cs
builder.AddServiceDefaults();            // observability, health, resilience
builder.Services.AddEventStore();        // reflection auto-discovers aggregates/projections — zero manual registration
app.UseEventStore();                     // 5-layer per-domain config cascade
app.MapDefaultEndpoints();
```

`.csproj` references: `Hexalith.EventStore.Client`, `Hexalith.EventStore.ServiceDefaults` (+ `Dapr.AspNetCore`).
No `.Server`, `.AppHost`, `.Aspire`, or own `.ServiceDefaults`. Discovery is convention-based
(`AssemblyScanner` + `NamingConventionEngine`); the seam is `IDomainProcessor` / `EventStoreAggregate<TState>`.

**B. Hexalith.Tenants re-implements the platform** (~3,200 boilerplate LOC of ~8,000 total):

| Tenants project / file | LOC | What it is | Verdict |
|---|---:|---|---|
| `Hexalith.Tenants.Contracts` | ~400 | commands/events/identities | ✅ domain — keep |
| `Hexalith.Tenants.Server` | ~960 | aggregates, projections, validators | ✅ mostly domain — keep |
| `Hexalith.Tenants.ServiceDefaults/Extensions.cs` | 152 | near-identical **copy** of `EventStore.ServiceDefaults` (only telemetry names differ) | ❌ duplicate |
| `Hexalith.Tenants.Aspire/*` (3 files) | 195 | re-implements DAPR state/pubsub/sidecar wiring | ❌ boilerplate |
| `Hexalith.Tenants.AppHost/*` (7 files) | 235 | re-implements Aspire topology | ❌ boilerplate |
| `Hexalith.Tenants.Client/*` (14 files) | 786 | generic event-subscription / projection-store / marker-dedup registration | ❌ should be generic in `EventStore.Client` |
| `Hexalith.Tenants` host: `Actors/TenantsProjectionActor.cs` | 672 | re-implements a projection/query actor | ❌ platform already provides `EventReplayProjectionActor` + keyed query routing |
| `Hexalith.Tenants` host: `Projections/ProjectionDispatcher.cs`, string query routing, `Telemetry/*`, `Health/DaprStateStoreHealthCheck.cs`, `Bootstrap/*`, `Program.cs` | ~1,500 | hosting, dispatch, telemetry, health, bootstrap | ❌ boilerplate |

**C. Even the Sample still carries removable hosting boilerplate** — it hand-maps `/process`,
`/replay-state`, `/project`, `/admin/operational-index-metadata` and ships `DomainServiceRequestRouter.cs`
and `AdminOperationalIndexMetadata.cs`. These are identical for every domain and belong in the library.
**This is the proof point:** the single library change that de-boilerplates Tenants also shrinks the Sample.

---

## 2. Impact Analysis

### Epic impact

There is **no PRD or epics yet** (`_bmad-output/planning-artifacts/` is empty; brownfield docs only). This
correction therefore **defines** the first epics rather than modifying existing ones. Three new epics are
proposed (Section 4).

### Story impact

None existing. New stories are scoped under the proposed epics.

### Artifact conflicts (to be reconciled)

| Artifact | Conflict | Action |
|---|---|---|
| `docs/brownfield/integration-architecture.md` | Describes domain services as "zero infrastructure access" but doesn't state the domain-centric / SDK-provides-boilerplate principle, and the Tenants reality contradicts it | Add principle + note SDK seam (patched as part of this deliverable) |
| `docs/brownfield/architecture.md` | §4 "The Parts" and §11 "Key Design Decisions" omit the domain-service authoring model | Add a domain-service SDK row + a design rule (patched) |
| `docs/brownfield/component-inventory.md` | No mention of the domain-service host surface | Add a short SDK note (patched) |
| `Hexalith.Tenants` submodule | Code structurally violates the principle | Refactor (Epic B) — separate PR in the submodule repo |
| `samples/Hexalith.EventStore.Sample` | Carries removable endpoint/router boilerplate | Shrink as the SDK proof (Epic C) |
| `CLAUDE.md` (root) | No documented domain-module authoring rule | Add rule after SDK lands (Epic C) |

### Technical impact

- **Additive, non-breaking platform work** (Epic A): new extension methods alongside existing
  `AddEventStore()` / `UseEventStore()`; no change to the `IDomainProcessor` seam or wire protocol.
- **Submodule refactor** (Epic B): deletes 3 Tenants projects, collapses a 672-LOC actor and dispatcher,
  re-points references. Highest technical risk centers on Tenants' **query model** (see Risk below).
- **CI/build:** Tenants `.slnx` loses 3 projects; references and DAPR component manifests in `deploy/` and
  `samples/dapr-components` may need updating. Container image list (CLAUDE.md) unaffected for EventStore;
  Tenants images governed by the submodule.

### Key risk / unknown — RESOLVED by the B1 spike (2026-06-02)

**Verdict: NO-GO on wholesale deletion of `TenantsProjectionActor`; GO on a capability-driven refactor.**

The platform offers two query models: **(a)** domain provides a stateless `/project`
(`ProjectionRequest`→`ProjectionResponse`) handler and the platform's `EventReplayProjectionActor`
(DAPR type `"ProjectionActor"`) stores/serves a single replayed read model; **(b)** domain hosts its own
actor inheriting `CachingProjectionActor` and routes to it via `SubmitQueryRequest.ProjectionActorType`.
Tenants uses **(b)** for real reasons the platform does not cover:

1. **Persisted multi-read-model state with merge-on-write** — `TenantReadModel` + a cross-aggregate
   `TenantIndexReadModel` + `TenantAuditReadModel`, via `DaprTenantProjectionStateStore` +
   `TenantProjectionWritePolicy` (optimistic concurrency). The platform stores only one replayed read model
   per actor; it has no persisted multi-key/merge read-model store.
2. **Encrypted, scope-validated cursor pagination** — `TenantQueryCursorCodec` (Data Protection).
3. **A query-handler seam** — a 672-LOC actor with a `switch` over 5 query types, each doing genuine
   domain logic (RBAC-scoped visibility, audit semantics, the index shape).

Split: ~⅓ of the actor is domain logic that must stay; ~⅔ is hand-rolled infra (the actor host + DAPR
registration, the query-type dispatch, the cursor codec, the read-model store) that the platform should
provide. So Epic B becomes: **add 3 platform capabilities, then collapse the actor to ~200–300 LOC of
domain query handlers** (no DAPR actor host, no cursor codec, no state-store plumbing). These 3 capabilities
are added to Epic A as **A7–A9** (§4.1); they — not the speculative `/project`-only generalization — are the
platform last-mile that Epic B needs. (Note: the platform already shares `CachingProjectionActor` as the
ETag/caching base for both models, so that infra is not re-implemented by Tenants.)

---

## 3. Recommended Approach

**Selected path: Hybrid — Additive platform SDK (Direct Adjustment) + Tenants boilerplate removal (targeted Rollback).**

- **Direct Adjustment (libraries):** add the missing "last-mile" host/consumer/Aspire extensions so the
  boilerplate has a home. Non-breaking; the Sample and Tenants both adopt it.
- **Targeted Rollback (Tenants):** remove the duplicated infrastructure that should never have been written
  in the domain module, re-pointing it at the platform SDK.

### Why this over alternatives

- **Not "Tenants-only refactor":** without the SDK seam, the boilerplate has nowhere to go — you'd delete
  Tenants' copies only to hand-write equivalents again in the next domain. The library gap must close first.
- **Not "libraries-only":** Tenants would remain a misleading reference and keep drifting. The principle
  only becomes real when a non-trivial domain (Tenants) demonstrably runs on the SDK.
- **Sequencing:** Epic A (enabler) → Epic B (Tenants adopts) ; Epic C (Sample proof + guardrails) runs
  alongside A and closes after A.

### Effort / risk

| Epic | Effort | Risk | Notes |
|---|---|---|---|
| A — Domain-Service SDK (platform last-mile) | Medium | **Low** | Additive, non-breaking; covered by new + existing unit tests |
| B — Refactor Hexalith.Tenants to domain-centric | Medium-High | **Medium** | Query-seam spike gates the 672-LOC actor removal; submodule integration tests must stay green |
| C — Shrink Sample + codify the pattern (guardrails/docs/template) | Low | Low | Sample Program.cs proof; CLAUDE.md rule; optional analyzer/CI check |

---

## 4. Detailed Change Proposals

### 4.1 New Epics (seed the upcoming Brownfield PRD/Epics)

#### Epic A — EventStore Domain-Service SDK (platform last-mile)

Goal: a domain module needs **only** a reference to the SDK + its domain code + ≤2 hosting lines.

- **A1 (spike + API):** ✅ **DONE (2026-06-02).** Spike outcome corrected the home: these extensions
  need `WebApplicationBuilder`/endpoint routing + `AddServiceDefaults`, which `Hexalith.EventStore.Client`
  cannot take (it is a published, ASP.NET-free NuGet library, and `ServiceDefaults` is `IsPackable=false`).
  They live in a **new `src/Hexalith.EventStore.DomainService`** SDK project (`IsPackable=false` for now;
  see A6) referencing Client + ServiceDefaults. API delivered in
  `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`:
  ```csharp
  public static WebApplicationBuilder AddEventStoreDomainService(
      this WebApplicationBuilder builder, Action<EventStoreOptions>? configure = null);
  public static WebApplication UseEventStoreDomainService(this WebApplication app);   // UseEventStore + MapDefaultEndpoints + MapEventStoreDomainService
  public static WebApplication MapEventStoreDomainService(this WebApplication app);   // /process, /replay-state, /project, /admin/operational-index-metadata
  ```
  Target Program.cs becomes:
  ```csharp
  var builder = WebApplication.CreateBuilder(args);
  builder.AddEventStoreDomainService();
  var app = builder.Build();
  app.UseEventStoreDomainService();
  app.Run();
  ```
- **A2:** ✅ **DONE (2026-06-02).** Moved the Sample's `DomainServiceRequestRouter` and
  `AdminOperationalIndexMetadata` into the SDK and deleted the Sample copies; the SDK's
  `MapEventStoreDomainService` now maps `/`, `/process`, `/replay-state`, `/admin/operational-index-metadata`.
- **A3:** Generic event-subscription / projection-consumer in `Hexalith.EventStore.Client` (or new
  `Hexalith.EventStore.Client.Subscriptions`): generalize Tenants' `TenantEventProcessor`,
  `TenantServiceCollectionExtensions` (marker-dedup), and subscription endpoints into
  `AddEventStoreDomainEvents<TOptions>()` + an `IEventStoreProjectionConsumer` contract.
- **A4:** Generic Aspire domain-module extension in `Hexalith.EventStore.Aspire`:
  `builder.AddEventStoreDomainModule(appId, project)` wiring DAPR sidecar + state-store + pub/sub references
  (replaces `Hexalith.Tenants.Aspire`).
- **A5:** Convention-driven telemetry + DAPR state-store health check provided by the platform (domain name →
  ActivitySource/Meter/health names), removing per-domain `Telemetry/*` and `Health/*`.
- **A6 (optional):** `dotnet new hexalith-domain` template and/or NuGet-publish the
  `Hexalith.EventStore.DomainService` SDK (+ make ServiceDefaults packable) — raises the published-package
  count; release-affecting, PM/Architect decision.

**Platform capabilities surfaced by the B1 spike (the real query-side last-mile — these gate Epic B):**

- **A7 — Query-handler seam (no per-domain actor).** Let a domain serve queries as **plain domain code**
  (one `IDomainQueryHandler` per query type) instead of subclassing `CachingProjectionActor` + hand-writing a
  string `switch`. Split into two increments:
  - **A7a — domain-side seam: ✅ DONE (2026-06-02).** In the `DomainService` SDK:
    `IDomainQueryHandler { string Domain; string QueryType; Task<QueryResult> ExecuteAsync(QueryEnvelope, CancellationToken); }`,
    `DomainQueryDispatcher` (matches by domain + query type), discovery/registration in
    `AddEventStoreDomainService`, and a `/query` endpoint in `MapEventStoreDomainService` (returns
    `QueryResult`, wire-symmetric with `/process`). Zero core changes; 3 new SDK tests.
  - **A7b — gateway-side dispatch (capability-declared routing chosen).** Split:
    - **A7b-1 — capability reporting: ✅ DONE (2026-06-02).** The SDK declares each domain's handler-served
      query types in the operational-index metadata (`AdminOperationalIndexMetadata.DomainMetadata.QueryTypes`,
      populated from the DI-registered `IDomainQueryHandler`s via the `/admin/operational-index-metadata`
      endpoint). Additive/backward-compatible — the gateway ignores the new JSON field until A7b-2. SDK tests 8/8.
    - **A7b-2 — gateway capture + invoker: ✅ DONE (2026-06-02).** Added `QueryTypes` (optional, backward
      compatible) to the gateway's `AdminOperationalIndexDomainMetadata` DTO + `MergeDomainMetadata`;
      `AdminOperationalIndexHostedService` now materializes `QueryTypesByDomain` (`BuildSnapshot`) and writes
      `admin:query-types:{domain}` at startup. New `IDomainQueryHandlerRegistry` /
      `DaprDomainQueryHandlerRegistry` (cached, fail-safe to "no handler") and `IDomainQueryInvoker` /
      `DaprDomainQueryInvoker` (DAPR `/query`, mirrors `DaprDomainServiceInvoker`). `BuildSnapshot`
      query-types covered by a unit test (Client.Tests 400/400). DAPR adapters: CI-verified.
    - **A7b-3 — routing: ✅ DONE (2026-06-02).** `HandlerAwareQueryRouter` decorates `IQueryRouter`
      (registered after `AddEventStoreServer` via `AddEventStoreDomainQueryRouting`): handler-based
      `(domain, queryType)` → invoke `/query` via the invoker; else delegate to the projection-actor router.
      Handler-based queries bypass projection ETag (ProjectionType left null → no controller change needed).
      3 decorator unit tests (NSubstitute) in the new `Hexalith.EventStore.QueryRouting.Tests`. The
      gateway→domain `/query` round-trip is CI/integration-only (no DAPR locally). **All A7b code lives in the
      gateway — Server untouched.**

    With A7a + A7b complete, Tenants drops the 672-LOC actor host. **The entire domain side of the seam
    (A7a + A7b-1) is now done and tested; only the gateway side (A7b-2/3) remains — core + locally
    unverifiable.**
- **A8 — Generic persisted read-model store. ✅ DONE (2026-06-02).** A platform abstraction for persisted,
  incrementally-updated read models with optimistic-concurrency merge-on-write and multi-key/index support —
  generalizing Tenants' `DaprTenantProjectionStateStore` + `TenantProjectionWritePolicy`. (The default
  `EventReplayProjectionActor` only stores a single full-replay read model per actor.) Delivered in
  `Hexalith.EventStore.Client` (the published library both the SDK and domain code consume — this is a
  domain-service-side concern, not a gateway one): `IReadModelStore` + `ReadModelEntry<T>` (thin ETag-aware
  DAPR wrapper, generalizing `ITenantProjectionStateStore`/`ProjectionStateRead<T>`), `DaprReadModelStore`
  (first-write-wins), and `ReadModelWritePolicy` — the optimistic-concurrency reload-and-merge retry loop
  generalizing both Tenants write methods into one `UpdateAsync<T>(store, key, Func<T?,T> update, …)`
  primitive plus `ApplyEventsAsync` (event-replay) and `MergeAsync` (cross-aggregate index/singleton)
  convenience overloads. Tenant-specific telemetry/diagnostics replaced by a lightweight optional
  `ReadModelWriteContext` + generic `LoggerMessage`s. DI: `AddEventStoreReadModelStore()`. Multi-key/index is
  served by addressing different keys (a fixed key merged on every write is the index pattern). For B7,
  `InMemoryReadModelStore` (realistic ETag/first-write-wins + conflict-injection hook) added to
  `Hexalith.EventStore.Testing`. **11 new tests** (Client.Tests now 411/411; Testing.Tests 144/144); whole
  graph builds clean (0 warnings under warnings-as-errors). DAPR round-trip is CI/integration only (no DAPR
  locally).
- **A9 — Generic pagination cursor codec.** A reusable Data-Protection-backed, scope-validated cursor
  utility generalizing `TenantQueryCursorCodec`; domains supply only the scope fields.
- **A3 (revised):** Generalize the projection-BUILD side — the `/project` endpoint dispatch (Model a) and the
  downstream event-subscription/projection-consumer plumbing (Tenants' `TenantEventProcessor` /
  marker-dedup registration) — paired with A8. Distinct from the query-READ seam (A7).

#### Epic B — Refactor Hexalith.Tenants to domain-centric (submodule)

- **B1 (spike):** ✅ **DONE (2026-06-02).** Verdict: **NO-GO on wholesale actor deletion; GO on a
  capability-driven refactor** (see §2). Tenants needs platform capabilities A7 (query-handler seam), A8
  (persisted read-model store), A9 (cursor codec). Once those exist, the actor collapses to domain query
  handlers; until then it stays.
- **B2:** Delete `Hexalith.Tenants.ServiceDefaults`; reference `Hexalith.EventStore.ServiceDefaults`.
- **B3:** Delete `Hexalith.Tenants.Aspire`; orchestrate via `AddEventStoreDomainModule` (A4).
- **B4:** Remove/relocate `Hexalith.Tenants.AppHost` — domain modules don't ship their own AppHost; dev
  orchestration runs through the EventStore AppHost (or a shared dev AppHost).
- **B5 (revised per B1):** Collapse the `Hexalith.Tenants` host **once A7–A9 exist**: replace
  `Actors/TenantsProjectionActor.cs` + `Projections/ProjectionDispatcher.cs` with ~5 domain query handlers
  (`IDomainQueryHandler`, A7); drop `Projections/DaprTenantProjectionStateStore.cs` (use A8) and
  `Queries/TenantQueryCursorCodec.cs` (use A9); remove `Telemetry/*` / `Health/*` (A5); Program.cs becomes
  the 2-line SDK form. **Keep** the domain logic: RBAC-scoped visibility rules, audit semantics, read-model
  shapes, projection handlers, and pagination-policy/scope choices (~200–300 LOC). Net: the 672-LOC actor
  becomes domain query handlers; no DAPR actor host, no hand-rolled cursor codec or state store.
- **B6:** Reduce `Hexalith.Tenants.Client` to domain-specific consumer handlers
  (`Handlers/TenantProjectionEventHandler.cs`, `ITenantEventHandler.cs`); generic plumbing comes from A3.
- **B7:** Reduce `Hexalith.Tenants.Testing` to domain helpers; generic in-memory stores from platform Testing.
- **B8:** Keep `Hexalith.Tenants.Contracts` and `Hexalith.Tenants.Server` (the domain library) as the
  domain-centric core. Update `Hexalith.Tenants.slnx`, `deploy/`, and DAPR component manifests.

#### Epic C — Codify the pattern (proof + guardrails)

- **C1:** Shrink `samples/Hexalith.EventStore.Sample/Program.cs` to the SDK form; delete the moved
  `DomainServiceRequestRouter.cs` / `AdminOperationalIndexMetadata.cs` (proof the SDK works).
- **C2:** Add a domain-module authoring rule to root `CLAUDE.md` and the brownfield docs.
- **C3 (optional):** Guardrail — an analyzer or CI check that flags a domain module referencing
  `*.AppHost`/`*.Aspire`/`*.ServiceDefaults` of its own, or re-declaring a projection actor.

### 4.2 Brownfield documentation edits (delivered with this proposal)

- `docs/brownfield/architecture.md` — add the domain-service SDK to §4 and a design rule to §11.
- `docs/brownfield/integration-architecture.md` — add the domain-centric principle and SDK seam.
- `docs/brownfield/component-inventory.md` — add a short domain-service host-surface note.

---

## 5. Implementation Handoff

- **Scope classification:** **Major** (new epics; cross-repo incl. submodule).
- **Routing:**
  - **PM / Architect** (John / Winston): fold Epics A–C into the Brownfield PRD/epics; own the query-seam
    decision from B1; confirm SDK public API shape (A1).
  - **Developer** (Amelia): implement A → B → C; B gated by the B1 spike.
- **Success criteria:**
  1. A new/existing domain module references only domain libraries + the SDK (no own
     `*.AppHost`/`*.Aspire`/`*.ServiceDefaults`), with Program.cs ≤ 2 hosting lines.
  2. Hexalith.Tenants runs on the platform with the 672-LOC projection actor and duplicated infra projects
     removed; submodule unit + integration tests green.
  3. The Sample shrinks to the SDK form with `DomainServiceRequestRouter` deleted.
  4. Existing EventStore unit-test tiers remain green; SDK additions covered by tests.

---

## 6. Implementation status — Epic A (started 2026-06-02)

**Delivered (A1 + A2):**

- New SDK project `src/Hexalith.EventStore.DomainService` (`IsPackable=false`; `FrameworkReference`
  `Microsoft.AspNetCore.App`; references Client + ServiceDefaults). Added to `Hexalith.EventStore.slnx`.
  - `EventStoreDomainServiceExtensions` — `AddEventStoreDomainService` (calling-assembly + explicit-assembly
    overloads, each with/without options), `UseEventStoreDomainService`, `MapEventStoreDomainService`.
    The calling-assembly overloads use `GetCallingAssembly` + `[MethodImpl(NoInlining)]` and forward the
    captured **domain** assembly to `AddEventStore(assemblies)` — so discovery never targets the SDK assembly.
  - `DomainServiceRequestRouter` + `AdminOperationalIndexMetadata` moved in from the Sample (verbatim logic).
- Sample converted to the target shape: `Program.cs` is now a 2-line host
  (`AddEventStoreDomainService()` / `UseEventStoreDomainService()`), and `Hexalith.EventStore.Sample.csproj`
  references **only** the SDK (Client/ServiceDefaults/Contracts flow transitively). The Sample's
  `DomainServiceRequestRouter.cs` and `AdminOperationalIndexMetadata.cs` were deleted.
- New test project `tests/Hexalith.EventStore.DomainService.Tests` (4 tests), including the no-arg
  calling-assembly discovery contract. Coupled Sample tests repointed off the moved router.

**Verification (SDK 10.0.300 via `~/.dotnet`):** build clean (0 warnings under `TreatWarningsAsErrors`);
DomainService.Tests 4/4, Sample.Tests (tests/) 74/74, Sample.Tests (samples/) 4/4, Client.Tests 399/399.

**Also done — B1 query-seam spike (2026-06-02):** two-codebase investigation (platform query pipeline +
Tenants query implementation). Verdict recorded in §2 and Epic B/B5: **NO-GO on deleting
`TenantsProjectionActor` wholesale; GO on a capability-driven refactor.** Surfaced the real query-side
platform last-mile as **A7** (query-handler seam), **A8** (persisted read-model store), **A9** (cursor
codec) — see §4.1. This also confirmed A4/A5 (and the revised A3) have **no current consumer in the Sample**
and should be built with Epic B's Tenants needs in view, not speculatively from the Sample alone.

**Also delivered — A7a (query-handler seam, domain side) (2026-06-02):** `IDomainQueryHandler` +
`DomainQueryDispatcher` + discovery/registration + `/query` endpoint in the `DomainService` SDK. Build clean;
SDK tests now **7/7** (added registration, dispatch-match, dispatch-no-match); Sample.Tests still 74/74. Zero
core changes. Proven on the SDK test project's `widget` domain (the Sample's counter query is already served
by Model (a), so a Sample handler would be artificial).

**Also delivered — A7 COMPLETE (query-handler seam, both sides) (2026-06-02):**
- Domain side (SDK): `IDomainQueryHandler`, `DomainQueryDispatcher`, `/query` endpoint, operational-index
  `QueryTypes` reporting. SDK tests **8/8**.
- Gateway side (`Hexalith.EventStore`, Server untouched): `IDomainQueryHandlerRegistry` /
  `DaprDomainQueryHandlerRegistry`, `IDomainQueryInvoker` / `DaprDomainQueryInvoker`, `HandlerAwareQueryRouter`
  decorator (`AddEventStoreDomainQueryRouting`), and `admin:query-types:{domain}` materialization/store.
  Decorator unit tests **3/3** (`Hexalith.EventStore.QueryRouting.Tests`); `BuildSnapshot` query-types in
  Client.Tests **400/400**.

**Verification:** gateway + IntegrationTests build clean across the whole graph (incl. Tenants/Sample/AppHost,
0 warnings under warnings-as-errors). DAPR adapters + the gateway→domain `/query` round-trip are CI/integration
only (no DAPR locally).

A domain can now author queries as plain handlers, advertise them, and the gateway routes to them — **no
per-domain projection/query actor required.**

**Also delivered — A8 (generic persisted read-model store) (2026-06-02):** `IReadModelStore` +
`ReadModelEntry<T>`, `DaprReadModelStore`, and `ReadModelWritePolicy` (optimistic-concurrency reload-and-merge:
`UpdateAsync`/`ApplyEventsAsync`/`MergeAsync`) in `Hexalith.EventStore.Client`; `AddEventStoreReadModelStore()`
DI; `InMemoryReadModelStore` test fake in `Hexalith.EventStore.Testing` (for B7). 11 new tests (Client.Tests
411/411, Testing.Tests 144/144); whole-graph Release build clean. See §4.1 A8 for the full surface.

**Remaining Epic A (revised, ordered by Epic-B need):** **A9** (cursor codec) → A3 (generalize `/project`
build-side + projection-consumer) → A4 (Aspire `AddEventStoreDomainModule`) → A5 (convention telemetry/health)
→ A6 (publish SDK; release-affecting, PM/Architect). A9 still gates Epic B (Tenants' cursor codec; A8's
read-model store is now available); A4/A5 are best sequenced with Epic B.

## 7. Approval

- [x] Approved for implementation — 2026-06-02
- [ ] Approved with changes (noted below)
- [ ] Rejected / revise

Notes: Approved by Administrator. Directed to **start Epic A immediately** (A1 spike + the
`AddEventStoreDomainService` / `UseEventStoreDomainService` / `MapEventStoreDomainService` host extensions in
the EventStore client libraries). Epic B (Tenants) remains gated behind the B1 query-seam spike.
