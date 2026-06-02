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

### Key risk / unknown (must be de-risked first)

`TenantsProjectionActor` (672 LOC) is **not purely boilerplate** — it carries domain-specific query
handling: 5 query types, audit filtering, and a Data-Protection-backed **pagination cursor codec**
(`Queries/TenantQueryCursorCodec.cs`). Before deleting it we must confirm the platform query pipeline
(`EventReplayProjectionActor` + `QueriesController` + keyed projection routing) supports, as domain-author
extension points: (a) multiple query types per domain, (b) pagination/continuation tokens, (c) per-query
authorization/audit filtering. **Epic B story 1 is a spike to validate this seam** before any deletion.

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

- **A1 (spike + API):** Add host extensions to `Hexalith.EventStore.Client` (new
  `src/Hexalith.EventStore.Client/Registration/EventStoreDomainServiceHostExtensions.cs`):
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
- **A2:** Move the Sample's `DomainServiceRequestRouter` and `AdminOperationalIndexMetadata` into the library
  (generalized) so they are no longer hand-written per domain.
- **A3:** Generic event-subscription / projection-consumer in `Hexalith.EventStore.Client` (or new
  `Hexalith.EventStore.Client.Subscriptions`): generalize Tenants' `TenantEventProcessor`,
  `TenantServiceCollectionExtensions` (marker-dedup), and subscription endpoints into
  `AddEventStoreDomainEvents<TOptions>()` + an `IEventStoreProjectionConsumer` contract.
- **A4:** Generic Aspire domain-module extension in `Hexalith.EventStore.Aspire`:
  `builder.AddEventStoreDomainModule(appId, project)` wiring DAPR sidecar + state-store + pub/sub references
  (replaces `Hexalith.Tenants.Aspire`).
- **A5:** Convention-driven telemetry + DAPR state-store health check provided by the platform (domain name →
  ActivitySource/Meter/health names), removing per-domain `Telemetry/*` and `Health/*`.
- **A6 (optional):** `dotnet new hexalith-domain` template and/or a `Hexalith.EventStore.DomainService`
  meta-package (references Client + ServiceDefaults + Dapr.AspNetCore).

#### Epic B — Refactor Hexalith.Tenants to domain-centric (submodule)

- **B1 (spike):** Validate the platform query seam covers Tenants' 5 query types + pagination cursor + audit
  filtering (see Section 2 risk). Output: a go/no-go and the domain-author query extension pattern.
- **B2:** Delete `Hexalith.Tenants.ServiceDefaults`; reference `Hexalith.EventStore.ServiceDefaults`.
- **B3:** Delete `Hexalith.Tenants.Aspire`; orchestrate via `AddEventStoreDomainModule` (A4).
- **B4:** Remove/relocate `Hexalith.Tenants.AppHost` — domain modules don't ship their own AppHost; dev
  orchestration runs through the EventStore AppHost (or a shared dev AppHost).
- **B5:** Collapse the `Hexalith.Tenants` host: remove `Actors/TenantsProjectionActor.cs`,
  `Projections/ProjectionDispatcher.cs`, `Projections/DaprTenantProjectionStateStore.cs`, `Telemetry/*`,
  `Health/*`, string query routing; Program.cs becomes the 2-line SDK form. Keep domain query handlers as
  domain code registered by convention.
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

## 6. Approval

- [x] Approved for implementation — 2026-06-02
- [ ] Approved with changes (noted below)
- [ ] Rejected / revise

Notes: Approved by Administrator. Directed to **start Epic A immediately** (A1 spike + the
`AddEventStoreDomainService` / `UseEventStoreDomainService` / `MapEventStoreDomainService` host extensions in
the EventStore client libraries). Epic B (Tenants) remains gated behind the B1 query-seam spike.
