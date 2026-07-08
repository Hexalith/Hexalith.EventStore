---
baseline_commit: 87ac445074a8302f39325262eb72738fbbc17647
created: 2026-07-08
story_key: 3-2-harden-dapr-etag-timeout-for-integration-conditions
epic: "Epic 3 - Release And Repository Reliability"
requirements: FR18
governing_nfr: NFR16
story_type: verification-and-reconciliation
correct_course: >-
  FR18's implementation (overridable DaprETagService actor request timeout, production default 3s
  preserved) shipped in PR #271 (commit 13320952, 2026-06-22) as CP-5 of
  sprint-change-proposal-2026-06-22-ci-release-retier.md — the SAME PR that shipped Story 3.1
  (FR17). Story 3.1's own Dev Notes already record this ("DaprETagServiceLiveSidecarTests constructs
  the service with requestTimeout: 30s (the FR18/Story 3.2 override, which also shipped in #271)").
  This story is re-scoped from IMPLEMENT to VERIFY-AND-RECONCILE per the Correct-Course Story Rewrite
  Gate. The epic ACs describe exactly what shipped, so none is factually wrong; each is mapped to its
  verified location. The one genuine residual is a deterministic test-coverage gap: the OVERRIDE path
  is currently exercised only by the environment-gated live-sidecar test (Category=LiveSidecar),
  never by a release-gate unit test — so epic AC group 3 ("both default and override paths are
  covered … focused unit or integration tests") is only half-covered in the deterministic gate. That
  is the concrete code deliverable (AC4). Do NOT re-implement the shipped production seam.
source_files:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md
  - _bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md
  - _bmad-output/implementation-artifacts/3-8-generated-api-dapr-aspire-smoke-preflight.md
  - _bmad-output/implementation-artifacts/deferred-work.md
  - src/Hexalith.EventStore.Server/Queries/DaprETagService.cs
  - src/Hexalith.EventStore.Server/Queries/IETagService.cs
  - src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
  - tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs
  - tests/Hexalith.EventStore.Server.Tests/Integration/DaprETagServiceLiveSidecarTests.cs
---

# Story 3.2: Harden DAPR ETag Timeout For Integration Conditions

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

<!-- CORRECT-COURSE REWRITE (2026-07-08):
     FR18's implementation already shipped in PR #271 (commit 13320952, 2026-06-22) as change CP-5 of
     sprint-change-proposal-2026-06-22-ci-release-retier.md — the same PR that shipped Story 3.1
     (FR17). Both landed BEFORE this story file existed, and sprint-status.yaml still listed 3-2 as
     `backlog`. Per the Correct-Course Story Rewrite Gate this story is re-scoped from IMPLEMENT to
     VERIFY-AND-RECONCILE. The original epic Acceptance Criteria (epics.md:892-907) are preserved
     verbatim under "Original Epic Acceptance Criteria" below, each mapped to its verified
     implementation location. No epic AC wording is factually wrong (unlike Story 3.1's "release
     workflow" wording), because the epic ACs describe exactly what shipped. The single genuine gap
     is that the OVERRIDE path is only covered by the environment-gated live-sidecar test, not by a
     deterministic release-gate unit test — closing that (AC4) is the one code deliverable. Do NOT
     re-implement the shipped production constructor seam and do NOT change the 3s production
     default. -->

## Story

As a **test maintainer**,
I want **to verify that the overridable `DaprETagService` actor request timeout already shipped (production default 3s preserved) and to close the one deterministic test-coverage gap — the override path is exercised only by the environment-gated live-sidecar test — while reconciling the sprint-status ledger and FR18 done-evidence with that shipped reality**,
so that **cold-start integration latency cannot produce false fail-open ETag results, the override contract is verified inside the deterministic release gate (not just the flaky live lane), and FR18 stops showing `backlog` while its code is merged**.

## Story Context

**This is a verification-and-reconciliation story, not a greenfield implementation.** FR18 ("`DaprETagService` must allow an overridable actor request timeout while preserving the production default" — `prd.md:138`, `prd.md:312`) was delivered in **PR #271 / commit `13320952` (`fix(ci): re-tier live-daprd integration tests off the release gate`, 2026-06-22)** as **change CP-5** of `sprint-change-proposal-2026-06-22-ci-release-retier.md:155-161`. It shipped together with Story 3.1 (FR17); Story 3.1's Dev Notes already record the overlap (`3-1-...md:195`: "`DaprETagServiceLiveSidecarTests` constructs the service with `requestTimeout: 30s` (the FR18/Story 3.2 override, which also shipped in #271)").

**Why the override exists (the original defect it fixed):** `DaprETagServiceLiveSidecarTests.GetCurrentETagAsync_AfterRegenerate_ReturnsPersistedETag_NotFailOpenNull` flaked deterministically on cold CI runners. Cold-start actor activation exceeded the tight **3s `RequestTimeout`**, and `DaprETagService`'s fail-open `catch` then returned `null` — a false failure masquerading as a product bug (`sprint-change-proposal-2026-06-22-ci-release-retier.md:32-62`). The production code was correct; the test needed longer activation tolerance. CP-5 made the timeout overridable so the live test can pass `requestTimeout: 30s` while production keeps the 3s default.

The shipped seam (verified at baseline `87ac4450`):

- **Production seam.** `DaprETagService` (`src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:15-25`) has an optional `TimeSpan? requestTimeout = null` constructor parameter; `_proxyOptions.RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3)`. `_proxyOptions` is a per-instance `readonly` field (no longer `static`), so each instance carries its own timeout.
- **Registration unchanged.** `ServiceCollectionExtensions.cs:37` still registers `services.TryAddScoped<IETagService, DaprETagService>()`; the built-in DI container supplies the default (no argument) for the unregistered optional parameter, so production behavior is byte-for-byte unchanged (3s).
- **Override in use.** The two live-sidecar tests construct the service with `requestTimeout: TimeSpan.FromSeconds(30)` (`DaprETagServiceLiveSidecarTests.cs:66,90`) and assert the **real persisted ETag** (`AfterRegenerate` → `actual.ShouldBe(expectedETag)`), and a genuine cold null (`ColdActor` → `actual.ShouldBeNull()`).
- **Fail-open preserved.** The generic `catch (Exception)` still logs and returns `null`; bare `OperationCanceledException` is rethrown so cancellation stays distinguishable from adapter-edge failures (`DaprETagService.cs:50-60`). The deterministic unit suite covers null-return, fail-open-on-throw, cancellation, argument validation, and the **default 3s** path.

**The single genuine residual (concrete deliverable):** epic AC group 3 asks that "**both default and override paths are covered**" by "**focused unit or integration tests**." Today the **default** (3s) path has a deterministic release-gate assertion (`DaprETagServiceTests.cs:43` → `options.RequestTimeout == TimeSpan.FromSeconds(3)`), but the **override** path's mapping (a supplied timeout → `ActorProxyOptions.RequestTimeout`) is asserted **only** by the live-sidecar tests, which are tagged `[Trait("Category", "LiveSidecar")]` and are **filtered out of the deterministic release gate** (`ci.yml` runs `--filter "Category!=LiveSidecar"`, per Story 3.1). So a regression that broke the override mapping (e.g. reverting `_proxyOptions` to `static` with a hard-coded 3s, or ignoring the parameter) would **not** be caught by the gated suite — only by the environment-gated live lane that may be `blocked`. AC4 closes this with a deterministic, mocked-factory unit test that pins the override→`ActorProxyOptions.RequestTimeout` mapping, mirroring the existing default-path assertion.

**Governing constraint:** NFR16 / AD-12 — "Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts" (`prd.md:214`; `architecture.md:115-119`). The live-sidecar `AfterRegenerate` test already satisfies this (it asserts the persisted Redis-backed ETag). NFR10 (lane separation) is Story 3.1's concern and the reason the override matters — the override keeps the live lane green without weakening the deterministic gate.

Source of truth:

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md:32-62,155-161,190` — the origin defect, CP-5, and the explicitly-**optional** `IOptions`/appsettings follow-up (out of scope here).
- `_bmad-output/planning-artifacts/epics.md:884-907` — Story 3.2 original ACs.
- `_bmad-output/planning-artifacts/prd.md:138,312` — FR18 text and FR-to-epic coverage.
- `_bmad-output/planning-artifacts/architecture.md:115-119` — AD-12 persisted-evidence; `prd.md:214` NFR16.
- `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md:195` — the FR18/#271 overlap acknowledgement + the `Category!=LiveSidecar` gate design.

## Acceptance Criteria

> **Verification stance:** each AC is satisfied by *observing and recording evidence* that the shipped state matches the requirement at baseline `87ac4450`. Make a code change only where an AC calls for it (AC4 — the deterministic override-path unit test) or where verification surfaces a genuine regression from baseline. Do **not** re-implement the shipped constructor seam and do **not** change the 3s production default.

**AC1 — Production default (3s) is preserved and the registration stays compatible. [epic AC group 1 — verify]**
**Given** production code constructs `DaprETagService` through normal DI,
**When** no custom request timeout is supplied,
**Then** `_proxyOptions.RequestTimeout` resolves to the existing production default `TimeSpan.FromSeconds(3)` (`DaprETagService.cs:24` — `requestTimeout ?? TimeSpan.FromSeconds(3)`),
**And** `ServiceCollectionExtensions.cs:37` still registers `TryAddScoped<IETagService, DaprETagService>()` with the container supplying the default for the unregistered optional parameter (no `TimeSpan` service is registered),
**And** the existing deterministic unit test `DaprETagServiceTests.GetCurrentETagAsync_ReturnsETag_WhenActorReturnsValue` still asserts `options.RequestTimeout == TimeSpan.FromSeconds(3)` (`DaprETagServiceTests.cs:43`).

**AC2 — An explicit timeout threads into the actor proxy calls. [epic AC group 2 — verify]**
**Given** a live-sidecar test needs longer actor-activation tolerance,
**When** it constructs `DaprETagService` with an explicit timeout,
**Then** the service uses the supplied timeout for actor-proxy creation (`_proxyOptions.RequestTimeout` is set from `requestTimeout`, `DaprETagService.cs:23-25`, and `_proxyOptions` is passed to `CreateActorProxy<IETagActor>(…, _proxyOptions)` at `DaprETagService.cs:45-46`),
**And** the live-sidecar tests construct with `requestTimeout: TimeSpan.FromSeconds(30)` (`DaprETagServiceLiveSidecarTests.cs:66,90`),
**And** `GetCurrentETagAsync_AfterRegenerate_ReturnsPersistedETag_NotFailOpenNull` asserts the **real persisted** ETag (`actual.ShouldBe(expectedETag)`, `DaprETagServiceLiveSidecarTests.cs:71-73`) — i.e. the override lets the test assert persisted ETag behavior without relying on a fail-open null (NFR16 / AD-12).

**AC3 — Both default and override paths are covered, and fail-open is not weakened. [epic AC group 3 — verify]**
**Given** the focused unit and live-sidecar tests,
**When** they are enumerated,
**Then** the **default** path is covered deterministically (`DaprETagServiceTests.cs:43`) and the **override** path is covered by the live-sidecar lane (`DaprETagServiceLiveSidecarTests.cs:66,90`),
**And** fail-open for genuine production actor failures is still covered and unchanged: `GetCurrentETagAsync_ReturnsNull_WhenActorThrows` (returns null, `DaprETagServiceTests.cs:66-84`), `GetCurrentETagAsync_ColdActor_ReturnsNull_WithoutThrowing` (genuine cold null over the live path, `DaprETagServiceLiveSidecarTests.cs:79-95`), and cancellation stays distinguishable (`GetCurrentETagAsync_OperationCanceledException_IsNotFailOpenNull`, `DaprETagServiceTests.cs:102-139`),
**And** it is explicitly recorded that the override→`ActorProxyOptions.RequestTimeout` mapping is currently asserted **only** in the `Category=LiveSidecar` lane (which the deterministic gate filters out) — motivating AC4.

**AC4 — Close the deterministic override-path coverage gap. [reconciliation deliverable]**
**Given** the override path is asserted only by the environment-gated live-sidecar suite,
**When** a focused, deterministic unit test is added to `DaprETagServiceTests` (mocked `Substitute.For<IActorProxyFactory>()`, **no** `LiveSidecar` trait, so it runs in `ci.yml`'s `Category!=LiveSidecar` gate),
**Then** it constructs `new DaprETagService(factory, NullLogger<DaprETagService>.Instance, requestTimeout: TimeSpan.FromSeconds(30))`, invokes `GetCurrentETagAsync`, and asserts `factory.Received(1).CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), ETagActor.ETagActorTypeName, Arg.Is<ActorProxyOptions>(o => o.RequestTimeout == TimeSpan.FromSeconds(30)))` — the exact mirror of the default-path assertion at `DaprETagServiceTests.cs:43`,
**And** it does **not** require a live sidecar and passes in the deterministic gate,
**And** the shipped production code is **not** modified to make it pass (if the test forces a production change, that is a regression discovery — record it; at baseline `87ac4450` no such change is needed).

**AC5 — FR18 is validated with recorded evidence and the ledger is reconciled.**
**Given** the validation commands in Dev Notes,
**When** they are run at baseline `87ac4450` (plus the AC4 test),
**Then** `Server.Tests` builds cleanly under `-warnaserror` (CA2007 is in `tests/Directory.Build.props` `NoWarn`),
**And** the deterministic subset (`--filter "Category!=LiveSidecar"`) passes locally **including the new AC4 test**, with pass/fail counts recorded in the Dev Agent Record,
**And** the live-sidecar subset (`--filter "Category=LiveSidecar"`) is either run against a live sidecar with the `DaprETagServiceLiveSidecarTests` results recorded **with persisted-ETag evidence** (R2-A6 / NFR16 / AD-12), or its environment is classified as `blocked` via the Story 3.8 preflight (`scripts/generated-api-smoke-preflight.sh`) / the fixture's own prerequisite preflight — a missing local DAPR control plane is an environment blocker, **not** a product failure,
**And** the FR18 done-evidence and the `sprint-status.yaml` `3-2-…` entry are reconciled to reflect that FR18 is satisfied-by-#271 plus this verification and the AC4 coverage close.

**AC6 — No scope creep; the optional runtime-tuning follow-up stays deferred.**
**Given** the FR18 boundary ("overridable … while preserving the production default"),
**When** this story is implemented,
**Then** the timeout is **not** bound to `IOptions`/appsettings — that was flagged as an explicitly *optional* follow-up in the origin proposal (`sprint-change-proposal-2026-06-22-ci-release-retier.md:190`) and is **not** required by FR18; it is out of scope here,
**And** the 3s production default, the `_proxyOptions` shape, the fail-open `catch`/rethrow contract, and the live-sidecar 30s value and warm-up thresholds are **not** changed unless AC1-AC4 verification proves a defect,
**And** no new live-sidecar test class is added (AC4 is a deterministic mocked-factory unit test, not a new live test), and no NFR10 lane wiring (`ci.yml`/`integration.yml`/`release.yml`) is touched — that is Story 3.1 / 7.4 territory,
**And** the ULID rule is respected: no `Guid.TryParse` of any `messageId`/`correlationId`/`aggregateId`/`causationId` is introduced (none is expected in this story).

### Original Epic Acceptance Criteria (preserved for traceability — `epics.md:892-907`)

1. Production code constructs `DaprETagService` through normal DI; when no custom request timeout is supplied, the existing production default timeout is preserved and existing service registration remains compatible. → **verified by AC1** (satisfied; `DaprETagService.cs:24`, `ServiceCollectionExtensions.cs:37`).
2. A live-sidecar test that needs longer actor-activation tolerance constructs `DaprETagService` with an explicit timeout; the service uses the supplied timeout for actor-proxy calls, and the test can assert persisted ETag behavior without relying on fail-open nulls. → **verified by AC2** (satisfied; `DaprETagServiceLiveSidecarTests.cs:66,71-73,90`).
3. When focused unit or integration tests run, both default and override paths are covered, and the change does not weaken fail-open behavior for genuine production actor failures. → **default + fail-open verified by AC3; the override-path deterministic-coverage half is closed by AC4** (the shipped state covers the override only in the `LiveSidecar` lane).

## Tasks / Subtasks

- [ ] **Task 1 — Verify the production default and registration (AC1).**
  - [ ] Read `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`; confirm the `TimeSpan? requestTimeout = null` parameter and `_proxyOptions.RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3)` (and that `_proxyOptions` is a per-instance `readonly` field, not `static`).
  - [ ] Confirm `ServiceCollectionExtensions.cs:37` registers `TryAddScoped<IETagService, DaprETagService>()` and that no `TimeSpan`/`ActorProxyOptions` service is registered that would override the default.
  - [ ] Confirm `DaprETagServiceTests.cs:43` still asserts the 3s default. Record the enumeration as evidence.
- [ ] **Task 2 — Verify the override threads into proxy creation (AC2).**
  - [ ] Confirm `_proxyOptions` (built from `requestTimeout`) is the exact instance passed to `CreateActorProxy<IETagActor>(…, _proxyOptions)` (`DaprETagService.cs:45-46`).
  - [ ] Confirm both live-sidecar tests construct with `requestTimeout: TimeSpan.FromSeconds(30)` and that `AfterRegenerate` asserts the persisted ETag (`ShouldBe(expectedETag)`), not a fail-open null.
- [ ] **Task 3 — Verify path coverage and fail-open preservation; record the gap (AC3).**
  - [ ] Enumerate the deterministic `DaprETagServiceTests` (default 3s, null-return, throw→null, pre-cancelled, OCE-not-fail-open, actor-id colon, self-routing format, remoting-interface, arg validation) and the live-sidecar override tests.
  - [ ] Explicitly record that the override→`RequestTimeout` mapping is asserted **only** under `Category=LiveSidecar`, which the deterministic gate excludes — the motivation for Task 4.
- [ ] **Task 4 — Add the deterministic override-path unit test (AC4).**
  - [ ] In `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs`, add a `[Fact]` (no `LiveSidecar` trait) that mirrors `GetCurrentETagAsync_ReturnsETag_WhenActorReturnsValue` but constructs the service with `requestTimeout: TimeSpan.FromSeconds(30)` and asserts `Arg.Is<ActorProxyOptions>(o => o.RequestTimeout == TimeSpan.FromSeconds(30))` on the captured `CreateActorProxy<IETagActor>` call.
  - [ ] Keep the assertion on `ETagActor.ETagActorTypeName` (not `Arg.Any<string>()`) to match the existing default-path test's precision.
  - [ ] Confirm the test fails against a hypothetical revert (parameter ignored / `static` 3s) and passes against the shipped code — the durable guard for the override contract in the deterministic gate. Do **not** modify production code to make it pass.
- [ ] **Task 5 — Validate and record evidence (AC5).**
  - [ ] Run the Dev Notes validation commands; record the build result and the deterministic (`Category!=LiveSidecar`) pass counts, including the new AC4 test.
  - [ ] For the live-sidecar subset: run `DaprETagServiceLiveSidecarTests` against a live sidecar (VM bootstrap in Dev Notes) and record results **with persisted-ETag evidence**, or classify the environment as `blocked` via `scripts/generated-api-smoke-preflight.sh` / the fixture preflight. Never treat a missing control plane as a product failure.
  - [ ] Reconcile FR18 done-evidence in the Dev Agent Record (satisfied-by-#271 + this verification + AC4 close) and flip the `sprint-status.yaml` `3-2-…` entry out of `backlog` on completion.
- [ ] **Task 6 — Enforce scope boundaries (AC6).**
  - [ ] Confirm no `IOptions`/appsettings binding was added, the 3s default and fail-open contract are unchanged, no NFR10 lane wiring was touched, no new live-sidecar class was added, and no `Guid.TryParse` of an id field was introduced.

## Dev Notes

### Top Guardrails

- **DO NOT re-implement.** FR18 shipped in PR #271 (`13320952`, CP-5). This story verifies the shipped constructor seam against the current baseline, closes the deterministic override-path coverage gap (AC4), and reconciles status/done-evidence. Changing the 3s default, the `_proxyOptions` shape, the fail-open `catch`/rethrow, or the live-sidecar 30s value is **out of scope** unless verification proves a genuine regression from baseline `87ac4450`.
- **The only expected file edit is a new unit `[Fact]`** in `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs` (AC4). Everything else is observe-and-record + the `sprint-status.yaml` reconciliation. No production `src/**` edit is expected.
- **The AC4 test must NOT carry `[Trait("Category", "LiveSidecar")]`.** Its whole point is to verify the override mapping **inside the deterministic release gate** (`ci.yml` runs `--filter "Category!=LiveSidecar"`, per Story 3.1). It uses a mocked `Substitute.For<IActorProxyFactory>()` — no sidecar, no Docker.
- **`IOptions`/appsettings binding stays deferred (AC6).** The origin proposal listed it as an *optional* runtime-tuning follow-up (`sprint-change-proposal-2026-06-22-ci-release-retier.md:190`), and FR18 does not require it. Do not gold-plate.
- **Persisted-evidence rule (AD-12 / R2-A6 / NFR16):** any live-sidecar result you record must rest on the persisted Redis-backed ETag (`AfterRegenerate` asserts `ShouldBe(expectedETag)`), not on a status code or mock count. Preserve that when recording evidence.
- **Environment ≠ defect:** a missing local DAPR control plane (placement/scheduler/redis) is a `blocked` classification, not a product failure. Use the Story 3.8 preflight and the fixture's prerequisite preflight to classify before asserting.
- **`ConfigureAwait(false)`** on every awaited call in any test/helper you add (CA2007-as-error is neutralized in test projects via `tests/Directory.Build.props` `NoWarn`, but keep the codebase idiom).
- **ULID rule** (project-wide): never `Guid.TryParse` a `messageId`/`correlationId`/`aggregateId`/`causationId`. No id parsing is expected here; do not introduce any.
- **Concurrency caution (MEMORY `concurrent-bmad-loop-git`):** a parallel auto-dev loop may auto-commit/push to `main` and absorb uncommitted edits; check refs before committing and branch (`test/...` or `fix/...`) rather than committing to `main`.

### Current Code State Read During Story Creation (baseline `87ac4450`, all verified)

**Production seam — `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`:**
```csharp
public partial class DaprETagService(
    IActorProxyFactory actorProxyFactory,
    ILogger<DaprETagService> logger,
    TimeSpan? requestTimeout = null) : IETagService {
    private readonly ActorProxyOptions _proxyOptions = new() {
        RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3),
    };
    // GetCurrentETagAsync: null/blank guards + ThrowIfCancellationRequested; actorId = "{projectionType}:{tenantId}";
    // CreateActorProxy<IETagActor>(new ActorId(actorId), ETagActor.ETagActorTypeName, _proxyOptions);
    // await proxy.GetCurrentETagAsync(); catch(OperationCanceledException) rethrow; catch(Exception) -> Log + return null.
}
```
- `_proxyOptions` is a per-instance `readonly` field (CP-5 changed it from `static readonly`), so each instance's timeout is independent — this is what makes the override real.
- The remoting-interface invocation and the OCE-rethrow are load-bearing (see the in-file comments at `:36-44,50-55` and `sprint-change-proposal-2026-05-25-etag-actor-proxy-nre.md`) — **do not touch them**.

**Registration — `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:37`:** `services.TryAddScoped<IETagService, DaprETagService>();` — the container instantiates `DaprETagService` with only its two DI-resolved dependencies, leaving `requestTimeout` at its `null` default → 3s. No `TimeSpan`/`ActorProxyOptions` is registered.

**Deterministic unit tests — `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs` (10 facts/theories, all use the 2-arg constructor = default path):**
- `:18-44` `GetCurrentETagAsync_ReturnsETag_WhenActorReturnsValue` — the **only** `RequestTimeout` assertion: `options.RequestTimeout == TimeSpan.FromSeconds(3)` (`:43`). This is the default-path guard AC4 mirrors for the override.
- `:46-64` null-return; `:66-84` throw→null (fail-open); `:86-100` pre-cancelled→throws before proxy creation; `:102-139` OCE-is-not-fail-open-null; `:141-163` actor-id colon separator; `:165-186` self-routing format; `:188-218` remoting-interface regression pin; `:220-246` argument validation (null/blank projectionType/tenantId).
- **No fact constructs the service with an explicit `requestTimeout`.** → the AC4 gap.

**Live-sidecar tests — `tests/Hexalith.EventStore.Server.Tests/Integration/DaprETagServiceLiveSidecarTests.cs` (`[Trait("Category","LiveSidecar")]`, `[Collection("DaprTestContainer")]`):**
- `:45-74` `GetCurrentETagAsync_AfterRegenerate_ReturnsPersistedETag_NotFailOpenNull` — real `ActorProxyFactory` over the fixture sidecar; `RegenerateAsync` seeds a self-routing ETag; service constructed with `requestTimeout: 30s` (`:66`); asserts `actual.ShouldBe(expectedETag)` (persisted, non-null) — the NFR16 end-state assertion.
- `:76-95` `GetCurrentETagAsync_ColdActor_ReturnsNull_WithoutThrowing` — never-regenerated actor; `requestTimeout: 30s` (`:90`); asserts genuine cold `null` distinguished from the pre-fix NRE fail-open null.
- These are the **only** places the override is exercised, and they are excluded from the release gate by the `Category!=LiveSidecar` filter (Story 3.1) — hence AC4.

**Build-status reality — `tests/Directory.Build.props:10`:** `CA2007` is in `NoWarn` for test projects, so `Server.Tests` builds cleanly under `-warnaserror`. (`CLAUDE.md`'s old "Server.Tests does not build" text is Story 3.1's reconciliation, not this story's — do not re-edit it here.)

### Scope Boundaries (what NOT to do)

- **No production `src/**` change.** The seam is shipped and correct; AC4 is test-only.
- **No `IOptions`/appsettings binding** (optional follow-up, `…ci-release-retier.md:190`; not required by FR18).
- **No change to the 3s default, `_proxyOptions` shape, fail-open `catch`/rethrow, or the live-sidecar 30s value / warm-up thresholds** unless verification proves a defect.
- **No NFR10 lane wiring changes** (`ci.yml`/`integration.yml`/`release.yml`) — Story 3.1 / 7.4 own those.
- **No new live-sidecar class** and no touching the shared `DaprTestContainerFixture` — AC4 is a deterministic mocked-factory unit test.
- **No CLAUDE.md edits** — the Server.Tests build-status reconciliation is Story 3.1's deliverable, not this one.

### Validation Commands

Run per project (never solution-level `dotnet test`; `.slnx` for restore/build only). Use `-p:UseHexalithProjectReferences=false` to match CI (package/Release mode); rerun `dotnet restore` when switching modes.

```bash
# Build the release gate's test project the way CI does (must succeed, 0 warnings under -warnaserror)
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
  --configuration Release -warnaserror -p:UseHexalithProjectReferences=false

# Deterministic release-gate subset (no DAPR needed) — must pass, INCLUDING the new AC4 test.
# Narrow to the ETag service tests first for a fast inner loop:
dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release \
  --filter "FullyQualifiedName~DaprETagServiceTests&Category!=LiveSidecar" \
  -p:UseHexalithProjectReferences=false
# Then the full deterministic gate as CI runs it:
dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release \
  --filter "Category!=LiveSidecar" -p:UseHexalithProjectReferences=false

# Live-sidecar lane — REQUIRES a live control plane. Bootstrap first (VM/slim mode):
#   sudo dockerd &>/tmp/dockerd.log & ; sudo chmod 666 /var/run/docker.sock
#   $HOME/.dapr/bin/placement --port 50005 &
#   $HOME/.dapr/bin/scheduler --port 50006 --etcd-data-dir /tmp/dapr-scheduler-data &
#   (or `dapr init`). Then classify environment with the Story 3.8 preflight before asserting:
#   scripts/generated-api-smoke-preflight.sh
dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release \
  --filter "FullyQualifiedName~DaprETagServiceLiveSidecarTests" -p:UseHexalithProjectReferences=false
```

Expected baselines from #271 (for comparison, not a hard gate): release-gate subset ≈ 2168 passed / 0 failed / 25 skipped (**+1 after AC4**); live-sidecar subset = 28 passed / 0 failed. If the live subset cannot run locally, record `blocked` with the preflight classification — do not mark AC5 failed for a missing control plane. Clean any stale DAPR placement before running the live lane (MEMORY `tier3-integration-test-constraints`): a shared/long-lived placement with dead fixed-name actor hosts (`ETagActor` uses a fixed const type name) causes ~60s hangs.

### Implementation Hints

- **AC4 test skeleton** (adapt from `DaprETagServiceTests.cs:18-44`; keep the exact NSubstitute idiom the file already uses, `_ =` discards and all):
  ```csharp
  [Fact]
  public async Task GetCurrentETagAsync_UsesSuppliedRequestTimeout_WhenOverrideProvided() {
      string selfRoutingETag = SelfRoutingETag.GenerateNew("counter");
      IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
      IETagActor actor = Substitute.For<IETagActor>();
      _ = actor.GetCurrentETagAsync().Returns(selfRoutingETag);
      _ = factory.CreateActorProxy<IETagActor>(
          Arg.Any<ActorId>(), ETagActor.ETagActorTypeName, Arg.Any<ActorProxyOptions>()).Returns(actor);

      var service = new DaprETagService(
          factory, NullLogger<DaprETagService>.Instance, requestTimeout: TimeSpan.FromSeconds(30));

      _ = await service.GetCurrentETagAsync("counter", "tenant1");

      _ = factory.Received(1).CreateActorProxy<IETagActor>(
          Arg.Any<ActorId>(),
          ETagActor.ETagActorTypeName,
          Arg.Is<ActorProxyOptions>(options => options.RequestTimeout == TimeSpan.FromSeconds(30)));
  }
  ```
  This is the exact mirror of the default-path assertion at `:43`, differing only in the constructor arg and the asserted `TimeSpan`. It is deterministic, needs no sidecar, and pins the override contract in the release gate.
- Because a mocked `IActorProxyFactory` never builds a real proxy, the test asserts the **mapping** (`requestTimeout` → `ActorProxyOptions.RequestTimeout` handed to `CreateActorProxy`), which is precisely the contract the live lane cannot pin deterministically. The live lane proves the *end-to-end persisted* behavior; this unit proves the *wiring*.
- If Task 1/2/3 verification uncovers a real regression from baseline (e.g. `_proxyOptions` reverted to `static`, or the parameter dropped), fix that **narrow** gap and note it — but at baseline `87ac4450` no such regression exists.
- When recording live-sidecar evidence, cite the persisted ETag returned by `AfterRegenerate` (self-routing `{base64url(projectionType)}.{guid}` format, `ShouldContain('.')`), not just the test's green/red.

### References

- [Source: _bmad-output/planning-artifacts/prd.md#FR18] (`:138`, `:312`) — requirement text + FR-to-epic coverage.
- [Source: _bmad-output/planning-artifacts/prd.md#NFR16] (`:214`) and [architecture.md#AD-12] (`:115-119`) — persisted-evidence for the live-sidecar assertion.
- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.2] (`:884-907`) — original ACs.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md#CP-5] (`:155-161`), origin defect (`:32-62`), optional IOptions follow-up (`:190`).
- [Source: _bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md] (`:195`) — the FR18/#271 overlap + `Category!=LiveSidecar` gate design.
- [Source: src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:15-25,45-60] — the shipped seam.
- [Source: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:37] — registration.
- [Source: tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs:18-44] — default-path assertion to mirror.
- [Source: tests/Hexalith.EventStore.Server.Tests/Integration/DaprETagServiceLiveSidecarTests.cs:45-95] — override usage + persisted-ETag / cold-null assertions.
- [Source: tests/Directory.Build.props:10] — CA2007 NoWarn (build-status truth).
- [Source: _bmad-output/implementation-artifacts/3-8-generated-api-dapr-aspire-smoke-preflight.md] — `scripts/generated-api-smoke-preflight.sh` for environment classification.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — fixed-actor-name (`ETagActor`) shared-placement 60s-hang (stays deferred; clean placement before the live lane).

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Implementation Plan / Decisions

### Debug Log References

### Completion Notes List

### File List

### Change Log
