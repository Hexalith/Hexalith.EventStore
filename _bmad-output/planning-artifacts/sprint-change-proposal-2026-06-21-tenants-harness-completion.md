# Sprint Change Proposal — Tenants→EventStore Platform Feature Reconciliation (Test-Harness Extraction)

- **Date:** 2026-06-21
- **Author:** Administrator (via BMAD Correct Course)
- **Project:** Hexalith.EventStore
- **Mode:** Batch
- **Change classification:** **Minor** (verification + one-line-per-method consuming-side build fix; the platform feature itself was already implemented and committed)
- **Status:** Implemented / verified
- **Trigger:** "Implement any needed EventStore features required by the Hexalith.Tenants change proposals. Read all proposals in `references/Hexalith.Tenants/_bmad-output/planning-artifacts`."
- **Source proposal:** `references/Hexalith.Tenants/_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-20-eventstore-test-harness-extraction.md` (APPROVED 2026-06-20)

---

## 1. Issue Summary

All Tenants planning artifacts were analysed to extract every requirement that falls on the
**Hexalith.EventStore platform** (rather than on Tenants-domain or FrontComposer code). Reviewed: the 13
sprint-change proposals, both implementation-readiness report sets, the FrontComposer readiness request,
the fallback-approval record, and the Tenants `architecture.md` + `epics.md`.

**Exactly one approved, net-new EventStore platform feature is required by the Tenants proposals:** the
domain-agnostic **DAPR/Aspire integration-test harness**, extracted into a new 9th published package
`Hexalith.EventStore.Testing.Integration` (approved proposal `…-2026-06-20-eventstore-test-harness-extraction.md`).

Everything else the Tenants artifacts reference on the platform is **already shipped** (command/query/
status endpoints, ETags, signed cursors, SDK extensions, SignalR nudge) or is **out of scope for now**:
the eight UI/Fluent/FrontComposer proposals need no EventStore change; tenant query-routing (2026-06-06)
states the EventStore submodule is "only consumed, not modified"; multi-replica cursor durability is
deferred to EventStore **Epic 11**; sensitive-value masking is a future platform item; `FC-AUD`/`FC-CNS`
belong to FrontComposer.

## 2. Resolution Status

### 2.1 Platform feature — ALREADY IMPLEMENTED (no new work needed)

The harness package was implemented and committed to EventStore in **commit `1b321f1a`**
("feat: Add integration testing framework for DAPR-backed Hexalith domain modules", 2026-06-21):

- `src/Hexalith.EventStore.Testing.Integration/` — `DaprLocalEndpoints`, `DaprDiagnostics`,
  `DaprTestPrerequisites` / `DaprPerformanceTestPrerequisites`, `DaprFactAttribute` /
  `DaprPerformanceFactAttribute` / `DaprTestSerializationAttribute` / `DaprTestExecutionGate`,
  `DaprDomainServiceTestFixtureBase` (abstract sidecar fixture with `AppId` / `DeadLetterTopic` /
  `ConfigureDomainConfiguration` / `ConfigureDomainServices` / `MapDomainEndpoints` / `RecordProcessFailure`
  hooks), and `AspireTopologyFixtureBase<TAppHost>` (abstract topology fixture with `ResourceNames` /
  `AlivenessResourceNames` / `ExtraAppArgs` / `Client(name)` hooks).
- `tests/Hexalith.EventStore.Testing.Integration.Tests/` — the relocated `DaprTestPrerequisiteDiagnosticsTests`
  retargeted to the generic `DaprDiagnostics` API and `DaprLocalEndpoints` ports.
- `Hexalith.EventStore.slnx` — both projects registered.
- `Directory.Packages.props` — `xunit.v3.extensibility.core 3.2.2` added.

Notable platform decision baked into that commit (and confirmed correct here): the package references
`Client` + `Server` + `DomainService` but **deliberately not** `Hexalith.EventStore.Testing` — that
package carries a prerelease `NSubstitute 6.0.0-rc.1` dependency that would make the stable, published
9th package fail `NU5104`; the harness base classes consume no `Testing` type. Domain test projects keep
their own direct `Hexalith.EventStore.Testing` reference for the in-memory fakes. Env vars are generalised
to `HEXALITH_EVENTSTORE_TEST_*` with the legacy `HEXALITH_TENANTS_TEST_*` names still honoured.

This run independently reconstructed the package from the approved proposal; the result is **byte-identical
to the committed `1b321f1a` state** (`git diff HEAD` empty), which cross-validates the committed
implementation against the proposal.

### 2.2 Consuming-side gap — FIXED (the only net-new change this run)

The Tenants submodule already carries the consuming fixtures (thin `AspireTopologyFixture` and
`TenantsDaprTestFixture` subclasses, repointed `.csproj`, deleted moved files), but as committed they did
**not compile** against the package: `TenantsDaprTestFixture`'s three `protected override` hooks
(`ConfigureDomainConfiguration`, `ConfigureDomainServices`, `MapDomainEndpoints`) dereference their
reference-type parameters without null-validation, which the Tenants analyzer rules promote to a
build-breaking **CA1062**. This is the expected cross-repo sequencing seam: the consuming code was
authored before the package existed and was never compiled against it.

**Fix applied (Tenants submodule):** add `ArgumentNullException.ThrowIfNull(...)` guards to the three
overrides (7 inserted lines in `tests/Hexalith.Tenants.IntegrationTests/Fixtures/TenantsDaprTestFixture.cs`).
`AspireTopologyFixture` needs no change (its overrides are property getters). This is the standard
boundary-validation pattern the repo's `project-context.md` already mandates; future domain authors
overriding these SDK hooks must do the same.

## 3. Verification Evidence

| Check | Result |
|---|---|
| `Hexalith.EventStore.Testing.Integration` build (Release, `TreatWarningsAsErrors`) | **0 warnings, 0 errors** |
| `Hexalith.EventStore.Testing.Integration.Tests` | **13 passed, 0 failed** |
| `dotnet pack` of the 9th package (stable) | **clean** — no `NU5104` (no prerelease dep leak) |
| `Hexalith.EventStore.slnx` restore | **clean** |
| Tenants `IntegrationTests` build (Release) after the CA1062 fix | **0 warnings, 0 errors** |
| Tenants DAPR end-to-end tests (`DaprEndToEnd*`, real daprd sidecars) | **17 passed, 0 failed, 0 skipped** |
| Tenants full integration run | 211 passed, 1 skipped, **12 failed (pre-existing, unrelated — see §4)** |

The 17/17 real-sidecar pass confirms the extracted `DaprDomainServiceTestFixtureBase` is behaviour-preserving.

## 4. Pre-existing, Out-of-Scope Issue (noted, not changed)

The 12 Tenants `AspireTopology*` failures are **not** caused by the harness extraction. They fail inside the
**unmodified Tenants AppHost** (`Hexalith.Tenants.AppHost/Program.cs:37`, `AddProject<HexalithEventStore>`):

```
Aspire.Hosting.DistributedApplicationException : Project file
'…/references/Hexalith.Tenants/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj' was not found.
```

The AppHost's cross-repo `IProjectMetadata` resolves EventStore at a path that assumes EventStore is nested
*inside* Tenants (layout 1). In this dev checkout the layout is inverted (EventStore is the root repo with
Tenants nested inside it, layout 3), so the path does not exist. The identical `CreateAsync<…AppHost>(…)`
call the original fixture made would fail the same way; this is an AppHost project-path-resolution limitation
under the inverted layout, independent of the test-harness work, and is left for a separate decision.

## 5. Deferred — D6 Read-Model Freshness Metadata (recorded handoff)

The Tenants **deferred-work proposal `…-2026-06-19-deferred-work.md`** (APPROVED) flags that a truthful D6
freshness signal (`current`/`aging`/`stale`) cannot be derived from `IReadModelStore` /
`ReadModelEntry<TValue>` alone — they expose only `Value` + `ETag`, and `TenantQueryResult.FromPayload`
stamps `ServedAt = UtcNow` (response time, not projection age). That proposal **explicitly permits either** a
shared EventStore platform capability **or a recorded EventStore handoff**; per this run's decision the
platform change is **deferred** and recorded here as a handoff:

> **EventStore platform handoff (D6 freshness).** If/when Tenants commits to a truthful aging/stale badge,
> add persisted projection-age metadata to the read-model contract — e.g. extend `ReadModelEntry<TValue>`
> with a projection timestamp/version, or add a companion `IReadModelMetadataStore` — rather than having
> Tenants hand-roll per-read-model metadata persistence (forbidden by the domain-centric boundary). No API
> surface is added speculatively until a consumer commits, to avoid shipping unused contract on a published
> package.

## 6. Handoff

- **Scope:** Minor. The platform feature is implemented and green; this run's net change is the 7-line CA1062
  fix in the Tenants submodule plus verification.
- **EventStore repo:** no outstanding action for the harness; D6 freshness remains an open, deferred handoff (§5).
- **Tenants repo (submodule):** the CA1062 guard fix is applied in the working tree; commit it (Conventional
  Commits, e.g. `fix(test): null-guard domain-service fixture overrides for CA1062`) and bump the EventStore
  submodule pointer to a commit that includes `1b321f1a`. The pre-existing AppHost layout issue (§4) is a
  separate item.
```
