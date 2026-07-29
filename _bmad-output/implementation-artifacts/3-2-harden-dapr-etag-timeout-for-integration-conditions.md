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
  - tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/DaprETagServiceLiveSidecarTests.cs
---

# Story 3.2: Harden DAPR ETag Timeout For Integration Conditions

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

<!-- CORRECT-COURSE REWRITE (2026-07-08):
     FR18's implementation already shipped in PR #271 (commit 13320952, 2026-06-22) as change CP-5 of
     sprint-change-proposal-2026-06-22-ci-release-retier.md — the same PR that shipped Story 3.1
     (FR17). Both landed BEFORE this story file existed, and sprint-status.yaml still listed 3-2 as
     `backlog`. Per the Correct-Course Story Rewrite Gate this story is re-scoped from IMPLEMENT to
     VERIFY-AND-RECONCILE. The original epic Acceptance Criteria (epics.md:1593-1609 at HEAD) are preserved
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

**This is a verification-and-reconciliation story, not a greenfield implementation.** FR18 ("`DaprETagService` must allow an overridable actor request timeout while preserving the production default" — `prd.md:186`, `prd.md:392`) was delivered in **PR #271 / commit `13320952` (`fix(ci): re-tier live-daprd integration tests off the release gate`, 2026-06-22)** as **change CP-5** of `sprint-change-proposal-2026-06-22-ci-release-retier.md:155-161`. It shipped together with Story 3.1 (FR17); Story 3.1's Dev Notes already record the overlap (`3-1-...md:195`: "`DaprETagServiceLiveSidecarTests` constructs the service with `requestTimeout: 30s` (the FR18/Story 3.2 override, which also shipped in #271)").

**Why the override exists (the original defect it fixed):** `DaprETagServiceLiveSidecarTests.GetCurrentETagAsync_AfterRegenerate_ReturnsPersistedETag_NotFailOpenNull` flaked deterministically on cold CI runners. Cold-start actor activation exceeded the tight **3s `RequestTimeout`**, and `DaprETagService`'s fail-open `catch` then returned `null` — a false failure masquerading as a product bug (`sprint-change-proposal-2026-06-22-ci-release-retier.md:32-62`). The production code was correct; the test needed longer activation tolerance. CP-5 made the timeout overridable so the live test can pass `requestTimeout: 30s` while production keeps the 3s default.

The shipped seam (verified at baseline `87ac4450`):

- **Production seam.** `DaprETagService` (`src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:15-25`) has an optional `TimeSpan? requestTimeout = null` constructor parameter; `_proxyOptions.RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3)`. `_proxyOptions` is a per-instance `readonly` field (no longer `static`), so each instance carries its own timeout.
- **Registration unchanged.** `ServiceCollectionExtensions.cs:54` still registers `services.TryAddScoped<IETagService, DaprETagService>()`; the built-in DI container supplies the default (no argument) for the unregistered optional parameter, so production behavior is byte-for-byte unchanged (3s).
- **Override in use.** The two live-sidecar tests construct the service with `requestTimeout: TimeSpan.FromSeconds(30)` (`DaprETagServiceLiveSidecarTests.cs:69,95`) and assert the **real persisted ETag** (`AfterRegenerate` → `actual.ShouldBe(expectedETag)`), and a genuine cold null (`ColdActor` → `actual.ShouldBeNull()`).
- **Fail-open preserved.** The generic `catch (Exception)` still logs and returns `null`; bare `OperationCanceledException` is rethrown so cancellation stays distinguishable from adapter-edge failures (`DaprETagService.cs:50-60`). The deterministic unit suite covers null-return, fail-open-on-throw, cancellation, argument validation, and the **default 3s** path.

**The single genuine residual (concrete deliverable):** epic AC group 3 asks that "**both default and override paths are covered**" by "**focused unit or integration tests**." Today the **default** (3s) path has a deterministic release-gate assertion (`DaprETagServiceTests.cs:43` → `options.RequestTimeout == TimeSpan.FromSeconds(3)`), but the **override** path's mapping (a supplied timeout → `ActorProxyOptions.RequestTimeout`) is asserted **only** by the live-sidecar tests, which live in the physically separate `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` project that the deterministic release gate does not run (Story 3.1 replaced the old `Category!=LiveSidecar` trait filter with project separation; `ci.yml` lists only `tests/Hexalith.EventStore.Server.Tests` under `unit-test-projects`, unfiltered, and `docs/ci.md:60-62` forbids reintroducing the filter). So a regression that broke the override mapping (e.g. reverting `_proxyOptions` to `static` with a hard-coded 3s, or ignoring the parameter) would **not** be caught by the gated suite — only by the environment-gated live lane that may be `blocked`. AC4 closes this with a deterministic, mocked-factory unit test that pins the override→`ActorProxyOptions.RequestTimeout` mapping, mirroring the existing default-path assertion.

**Governing constraint:** NFR16 / AD-12 — "Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts" (`prd.md:282`; `architecture.md:143-147`). The live-sidecar `AfterRegenerate` test already satisfies this (it asserts the persisted Redis-backed ETag). NFR10 (lane separation) is Story 3.1's concern and the reason the override matters — the override keeps the live lane green without weakening the deterministic gate.

Source of truth:

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md:32-62,155-161,190` — the origin defect, CP-5, and the explicitly-**optional** `IOptions`/appsettings follow-up (out of scope here).
- `_bmad-output/planning-artifacts/epics.md:1585-1609` — Story 3.2 original ACs (AC text at `:1593-1609`).
- `_bmad-output/planning-artifacts/prd.md:186,392` — FR18 text and FR-to-epic coverage.
- `_bmad-output/planning-artifacts/architecture.md:143-147` — AD-12 persisted-evidence; `prd.md:282` NFR16.
- `_bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md:195` — the FR18/#271 overlap acknowledgement + the lane-separation design (trait filter at the time; physical project separation at HEAD).

## Acceptance Criteria

> **Verification stance:** each AC is satisfied by *observing and recording evidence* that the shipped state matches the requirement at baseline `87ac4450`. Make a code change only where an AC calls for it (AC4 — the deterministic override-path unit test) or where verification surfaces a genuine regression from baseline. Do **not** re-implement the shipped constructor seam and do **not** change the 3s production default.

**AC1 — Production default (3s) is preserved and the registration stays compatible. [epic AC group 1 — verify]**
**Given** production code constructs `DaprETagService` through normal DI,
**When** no custom request timeout is supplied,
**Then** `_proxyOptions.RequestTimeout` resolves to the existing production default `TimeSpan.FromSeconds(3)` (`DaprETagService.cs:24` — `requestTimeout ?? TimeSpan.FromSeconds(3)`),
**And** `ServiceCollectionExtensions.cs:54` still registers `TryAddScoped<IETagService, DaprETagService>()` with the container supplying the default for the unregistered optional parameter (no `TimeSpan` service is registered),
**And** the existing deterministic unit test `DaprETagServiceTests.GetCurrentETagAsync_ReturnsETag_WhenActorReturnsValue` still asserts `options.RequestTimeout == TimeSpan.FromSeconds(3)` (`DaprETagServiceTests.cs:43`).

**AC2 — An explicit timeout threads into the actor proxy calls. [epic AC group 2 — verify]**
**Given** a live-sidecar test needs longer actor-activation tolerance,
**When** it constructs `DaprETagService` with an explicit timeout,
**Then** the service uses the supplied timeout for actor-proxy creation (`_proxyOptions.RequestTimeout` is set from `requestTimeout`, `DaprETagService.cs:23-25`, and `_proxyOptions` is passed to `CreateActorProxy<IETagActor>(…, _proxyOptions)` at `DaprETagService.cs:45-46`),
**And** the live-sidecar tests construct with `requestTimeout: TimeSpan.FromSeconds(30)` (`DaprETagServiceLiveSidecarTests.cs:69,95`),
**And** `GetCurrentETagAsync_AfterRegenerate_ReturnsPersistedETag_NotFailOpenNull` asserts the **real persisted** ETag (`actual.ShouldBe(expectedETag)`, `DaprETagServiceLiveSidecarTests.cs:76`) — i.e. the override lets the test assert persisted ETag behavior without relying on a fail-open null (NFR16 / AD-12).

**AC3 — Both default and override paths are covered, and fail-open is not weakened. [epic AC group 3 — verify]**
**Given** the focused unit and live-sidecar tests,
**When** they are enumerated,
**Then** the **default** path is covered deterministically (`DaprETagServiceTests.cs:43`), while the live-sidecar lane only **supplies** an override (`DaprETagServiceLiveSidecarTests.cs:69,95`) without ever asserting `ActorProxyOptions.RequestTimeout` — so before AC4 the override→`RequestTimeout` mapping was asserted in **no** lane,
**And** fail-open for genuine production actor failures is still covered and unchanged: `GetCurrentETagAsync_ReturnsNull_WhenActorThrows` (returns null, `DaprETagServiceTests.cs:144-162`), `GetCurrentETagAsync_ColdActor_ReturnsNull_WithoutThrowing` (genuine cold null over the live path, `DaprETagServiceLiveSidecarTests.cs:82-100`), and cancellation stays distinguishable (`GetCurrentETagAsync_OperationCanceledException_IsNotFailOpenNull`, `DaprETagServiceTests.cs:180-217`),
**And** it is explicitly recorded that the override→`ActorProxyOptions.RequestTimeout` mapping was asserted **nowhere** before AC4 — the live lane supplies the value but never reads it back, and the live project is not part of the deterministic gate — motivating AC4.

**AC4 — Close the deterministic override-path coverage gap. [reconciliation deliverable]**
**Given** the override path is asserted only by the environment-gated live-sidecar suite,
**When** focused, deterministic unit tests are added to `DaprETagServiceTests` (mocked `Substitute.For<IActorProxyFactory>()`, in the release-gate project `tests/Hexalith.EventStore.Server.Tests`, which `ci.yml` runs unfiltered via `unit-test-projects`),
**Then** it constructs `new DaprETagService(factory, NullLogger<DaprETagService>.Instance, requestTimeout: TimeSpan.FromSeconds(30))`, invokes `GetCurrentETagAsync`, and asserts `factory.Received(1).CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), ETagActor.ETagActorTypeName, Arg.Is<ActorProxyOptions>(o => o.RequestTimeout == TimeSpan.FromSeconds(30)))` — the exact mirror of the default-path assertion at `DaprETagServiceTests.cs:43`,
**And** it does **not** require a live sidecar and passes in the deterministic gate,
**And** the shipped production code is **not** modified to make it pass (if the test forces a production change, that is a regression discovery — record it; at baseline `87ac4450` no such change is needed).

**AC5 — FR18 is validated with recorded evidence and the ledger is reconciled.**
**Given** the validation commands in Dev Notes,
**When** they are run at baseline `87ac4450` (plus the AC4 test),
**Then** `Server.Tests` builds cleanly under `-warnaserror` (CA2007 is in `tests/Directory.Build.props` `NoWarn`),
**And** the deterministic release-gate project `tests/Hexalith.EventStore.Server.Tests` passes locally in full **including the new AC4 tests**, with pass/fail counts recorded in the Dev Agent Record,
**And** the live-sidecar project `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` is either run against a live sidecar with the `DaprETagServiceLiveSidecarTests` results recorded **with persisted-ETag evidence** (R2-A6 / NFR16 / AD-12), or its environment is classified as `blocked` via the Story 3.8 preflight (`scripts/generated-api-smoke-preflight.sh`) / the fixture's own prerequisite preflight — a missing local DAPR control plane is an environment blocker, **not** a product failure,
**And** the FR18 done-evidence and the `sprint-status.yaml` `3-2-…` entry are reconciled to reflect that FR18 is satisfied-by-#271 plus this verification and the AC4 coverage close.

**AC6 — No scope creep; the optional runtime-tuning follow-up stays deferred.**
**Given** the FR18 boundary ("overridable … while preserving the production default"),
**When** this story is implemented,
**Then** the timeout is **not** bound to `IOptions`/appsettings — that was flagged as an explicitly *optional* follow-up in the origin proposal (`sprint-change-proposal-2026-06-22-ci-release-retier.md:190`) and is **not** required by FR18; it is out of scope here,
**And** the 3s production default, the `_proxyOptions` shape, the fail-open `catch`/rethrow contract, and the live-sidecar 30s value and warm-up thresholds are **not** changed unless AC1-AC4 verification proves a defect,
**And** no new live-sidecar test class is added (AC4 is a deterministic mocked-factory unit test, not a new live test), and no NFR10 lane wiring (`ci.yml`/`integration.yml`/`release.yml`) is touched — that is Story 3.1 / 7.4 territory,
**And** the ULID rule is respected: no `Guid.TryParse` of any `messageId`/`correlationId`/`aggregateId`/`causationId` is introduced (none is expected in this story).

### Original Epic Acceptance Criteria (preserved for traceability — `epics.md:1593-1609`)

1. Production code constructs `DaprETagService` through normal DI; when no custom request timeout is supplied, the existing production default timeout is preserved and existing service registration remains compatible. → **verified by AC1** (satisfied; `DaprETagService.cs:24`, `ServiceCollectionExtensions.cs:54`).
2. A live-sidecar test that needs longer actor-activation tolerance constructs `DaprETagService` with an explicit timeout; the service uses the supplied timeout for actor-proxy calls, and the test can assert persisted ETag behavior without relying on fail-open nulls. → **verified by AC2** (satisfied; `DaprETagServiceLiveSidecarTests.cs:69,76,95`).
3. When focused unit or integration tests run, both default and override paths are covered, and the change does not weaken fail-open behavior for genuine production actor failures. → **default + fail-open verified by AC3; the override-path deterministic-coverage half is closed by AC4** (the shipped state covers the override only in the `LiveSidecar` lane).

## Tasks / Subtasks

- [x] **Task 1 — Verify the production default and registration (AC1).**
  - [x] Read `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`; confirm the `TimeSpan? requestTimeout = null` parameter and `_proxyOptions.RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3)` (and that `_proxyOptions` is a per-instance `readonly` field, not `static`).
  - [x] Confirm `ServiceCollectionExtensions.cs:54` registers `TryAddScoped<IETagService, DaprETagService>()` and that no `TimeSpan`/`ActorProxyOptions` service is registered that would override the default.
  - [x] Confirm `DaprETagServiceTests.cs:43` still asserts the 3s default. Record the enumeration as evidence.
- [x] **Task 2 — Verify the override threads into proxy creation (AC2).**
  - [x] Confirm `_proxyOptions` (built from `requestTimeout`) is the exact instance passed to `CreateActorProxy<IETagActor>(…, _proxyOptions)` (`DaprETagService.cs:45-46`).
  - [x] Confirm both live-sidecar tests construct with `requestTimeout: TimeSpan.FromSeconds(30)` and that `AfterRegenerate` asserts the persisted ETag (`ShouldBe(expectedETag)`), not a fail-open null.
- [x] **Task 3 — Verify path coverage and fail-open preservation; record the gap (AC3).**
  - [x] Enumerate the deterministic `DaprETagServiceTests` (default 3s, null-return, throw→null, pre-cancelled, OCE-not-fail-open, actor-id colon, self-routing format, remoting-interface, arg validation) and the live-sidecar override tests.
  - [x] Explicitly record that the override→`RequestTimeout` mapping was asserted in **no** lane before AC4 — the live project supplies 30s but never reads `RequestTimeout` back, and that project is not part of the deterministic gate — the motivation for Task 4.
- [x] **Task 4 — Add the deterministic override-path unit test (AC4).**
  - [x] In `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs`, add a `[Fact]` (no `LiveSidecar` trait) that mirrors `GetCurrentETagAsync_ReturnsETag_WhenActorReturnsValue` but constructs the service with `requestTimeout: TimeSpan.FromSeconds(30)` and asserts `Arg.Is<ActorProxyOptions>(o => o.RequestTimeout == TimeSpan.FromSeconds(30))` on the captured `CreateActorProxy<IETagActor>` call.
  - [x] Keep the assertion on `ETagActor.ETagActorTypeName` (not `Arg.Any<string>()`) to match the existing default-path test's precision.
  - [x] Confirm the test fails against a hypothetical revert (parameter ignored / `static` 3s) and passes against the shipped code — the durable guard for the override contract in the deterministic gate. Do **not** modify production code to make it pass.
- [x] **Task 5 — Validate and record evidence (AC5).**
  - [x] Run the Dev Notes validation commands; record the build result and the full pass counts for the deterministic release-gate project, including the new AC4 tests.
  - [x] For the live-sidecar subset: run `DaprETagServiceLiveSidecarTests` against a live sidecar (VM bootstrap in Dev Notes) and record results **with persisted-ETag evidence**, or classify the environment as `blocked` via `scripts/generated-api-smoke-preflight.sh` / the fixture preflight. Never treat a missing control plane as a product failure.
  - [x] Reconcile FR18 done-evidence in the Dev Agent Record (satisfied-by-#271 + this verification + AC4 close) and flip the `sprint-status.yaml` `3-2-…` entry out of `backlog` on completion.
- [x] **Task 6 — Enforce scope boundaries (AC6).**
  - [x] Confirm no `IOptions`/appsettings binding was added, the 3s default and fail-open contract are unchanged, no NFR10 lane wiring was touched, no new live-sidecar class was added, and no `Guid.TryParse` of an id field was introduced.

### Review Findings

Adversarial code review 2026-07-29 (4 layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor).
Reviewed range `efe97914..HEAD` = commits `c21a0bfc` + `8ce58f83` (the spec's `baseline_commit` `87ac4450` is 402 commits stale and was not usable as a diff base).
Every finding below was independently re-verified against the working tree before being recorded.

- [x] [Review][Decision] *(RESOLVED 2026-07-29 — owner chose: remove here, fix canonical. The 5-line block was deleted from all three entry points, restoring byte-identity with the Builds and Tenants copies; the one genuine gap it carried, the missing `revert` type, was added to the canonical `references/Hexalith.AI.Tools/hexalith-git-instructions.md` type table under explicit submodule-edit approval.)* **Three shared AI entry points were edited against this story's explicit "No CLAUDE.md edits" boundary** — `c21a0bfc` adds an identical 5-line Conventional-Commits block to `CLAUDE.md:51-55`, `AGENTS.md:51-55` and `.github/copilot-instructions.md:51-55`. Dev Notes → Scope Boundaries (`:219`) states verbatim "**No CLAUDE.md edits**", and Top Guardrails (`:170`) states "The only expected file edit is a new unit `[Fact]`". Consequence beyond scope: `AGENTS.md:3-7` declares its text "intentionally shared … in the superproject **and its root-declared submodules**", and `diff AGENTS.md references/Hexalith.Builds/AGENTS.md` now reports exactly `51,55d50` — EventStore's entry points have diverged from the Builds and Tenants copies. `SharedInstructionEntryPointTests` compares only the three *local* files, so nothing detects that divergence. Options: (a) ratify the scope exception and propagate the block to the sibling repos, (b) revert the three edits out of this story and land them separately, (c) ratify as-is and accept sibling divergence.
- [x] [Review][Decision] *(RESOLVED 2026-07-29 — owner chose: revert the duplication, fix the canonical source. Guard markers were left unchanged because there is no longer duplicated text to catch; `CommitMessagePolicyTests` + `SharedInstructionEntryPointTests` verified 17/17 after the removal.)* **The added commit-policy text duplicates policy the repo deliberately delegates, and slips past the anti-duplication guard by paraphrase** — `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs:65-78` asserts "The Copilot entry point must delegate commit policy instead of duplicating '{marker}'". `CommitHeaderFormat` (`:11`) is `<type>[optional scope][!]: <description>`; the new text writes `<type>[scope][!]: <description>` (drops "optional") and "Start descriptions with a lowercase imperative verb" against the guard's marker "Start the description with a lowercase letter". Both are semantically identical and lexically different, so the suite stays green (17/17) while the guard's stated intent is defeated. It also creates a third, already-divergent type list: the new text and `commitlint.config.mjs:7` list 10 types including `revert`; the canonical `references/Hexalith.AI.Tools/hexalith-git-instructions.md:200-207` type table lists 9 and omits `revert`. Nothing pins the entry-point list to `commitlint.config.mjs` — `CommitMessagePolicyTests:218-220` only checks `extends`, the presence of `'type-enum'`, and the absence of `'chore'`. Options: (a) revert the duplication and add the missing rules to the canonical shared file instead, (b) keep it and widen the guard's marker list so future paraphrases are caught, (c) keep it and add a test pinning the enumerated types to `commitlint.config.mjs`.
- [x] [Review][Decision] *(RESOLVED 2026-07-29 — owner ratified the bump as an intentional dependency advance. Recorded in the Debug Log and File List, and the `NU1102` evidence re-attributed to it.)* **The `references/Hexalith.Builds` gitlink bump silently raises `HexalithTenantsVersion` 5.0.0 → 5.1.0 repo-wide, undeclared anywhere in the story** — `86aa4cbd → 13cad866` is a single Builds commit, `fix(deps): update HexalithTenantsVersion to 5.1.0`, whose only diff is `Props/Directory.Packages.props:11`. That property version-controls six `Hexalith.Tenants.*` packages consumed by `Admin.Server`, `DomainService`, `AppHost` and both Server test projects, all of which reference them version-lessly under central package management. It appears in no AC, no task, and not in the File List. Options: (a) ratify the bump as an intentional dependency advance and record it in the story + File List, (b) revert the Builds gitlink out of this story and land it as a `build(deps)` commit of its own.
- [x] [Review][Decision] *(RESOLVED 2026-07-29 — owner accepted both as-is; CI is green on both SHAs and the release parks at the owner-approval gate. Recorded as a process note in the Debug Log.)* **Both commits landed direct-to-`main` against the story's own guardrail, and `8ce58f83`'s `fix:` type arms a spurious release** — `origin/main == HEAD == 8ce58f83`; neither subject carries a PR number, unlike the two preceding commits (`#334`, `#335`), and `main` is ruleset-gated. Dev Notes `:177` instructs "branch (`test/...` or `fix/...`) rather than committing to `main`". Separately, `.releaserc.json:5` uses `@semantic-release/commit-analyzer` with no preset override → angular defaults, where `fix` ⇒ patch. `8ce58f83 fix: update sprint status and validate package-mode builds` changes only two `.md`/`.yaml` files plus a submodule pointer, yet arms a patch release that runs the full `publishCmd` (14 NuGet packages + `publish-containers.sh`). Conversely `c21a0bfc docs:` carries the actual test change *and* the Tenants dependency bump with no release impact. Both contradict the rule the same diff adds ("use `build(deps)` for dependency or submodule pointer updates"), which commitlint cannot catch — it validates only the type token. Options: (a) accept both (CI is green on both SHAs; the release parks at the owner-approval gate anyway), (b) act on the release before it fires, (c) record as a process defect and add a durable guard.
- [x] [Review][Patch] Task 6 scope audit and the File List are false at HEAD — `:317` claims "production `src/**` … have no story diff" and `:327` claims "the change is test-and-ledger only", but the story's own commits touch 9 files; the File List (`:331-333`) names 3, omitting `CLAUDE.md`, `AGENTS.md`, `.github/copilot-instructions.md` and the three `references/*` gitlinks [`_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`:317,327,331-333]
- [x] [Review][Patch] The new AC4 test cannot detect the exact regression AC1/Task 1 names — the `Arg.Is<ActorProxyOptions>` predicate runs at **assert** time against a captured mutable reference, so reverting `_proxyOptions` to `private static` (assigned in the constructor) leaves both the 3s and 30s facts green, because xUnit runs a class's facts sequentially and each constructs-then-asserts immediately. Fix: capture at call time (`factory.When(...).Do(ci => captured = ci.Arg<ActorProxyOptions>().RequestTimeout)`) and assert the snapshot; add one fact constructing two `DaprETagService` instances with different timeouts and asserting each call carried its own value [`tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs`:67-70]
- [x] [Review][Patch] The Dev Agent Record is self-refuting — `:318` and `:339` state the package-evidence correction "remains an unstaged story/ledger delta", but commit `8ce58f83` committed those very sentences; `git status --porcelain` is empty and `origin/main == HEAD`. Same false-attestation pattern flagged in the Story 3.1 post-merge review [`_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`:318,339]
- [x] [Review][Patch] The `NU1102` is misattributed to a "transient" cache — `:315` credits a forced no-cache restore for resolving `Hexalith.Tenants.Contracts 5.1.0`, but 5.1.0 could not resolve at all until the Builds gitlink moved to `13cad866` (the pin was 5.0.0). This is the only mention of the Builds bump anywhere in the story record [`_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`:315]
- [x] [Review][Patch] AC4's justifying mechanism does not exist at HEAD — AC4 (`:115`), Story Context (`:76`), the bolded guardrail (`:171`) and the Validation Commands (`:233,237`) all assert `ci.yml` runs `--filter "Category!=LiveSidecar"`. `ci.yml` contains no `--filter`; it delegates to `domain-ci.yml@main` with `tests/Hexalith.EventStore.Server.Tests` in `unit-test-projects`, and `grep -rn LiveSidecar tests/Hexalith.EventStore.Server.Tests/` returns zero hits. `docs/ci.md:60-62` explicitly forbids reintroducing that filter. The deliverable does land in the gate — only the recorded reason is wrong [`_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`:76,115,171,233,237]
- [x] [Review][Patch] Stale anchors were checked off as verified in a story whose deliverable is accurate recorded evidence — registration is at `ServiceCollectionExtensions.cs:54`, not `:37` (asserted at `:96`, `:146`, `:198`); the live 30s constructions are at `DaprETagServiceLiveSidecarTests.cs:69,95`, not `:66,90` (asserted at `:103`, `:104`, `:109`, `:206-207`); the frontmatter `source_files` entry `:34` still points at the pre-Story-3.1 path `tests/Hexalith.EventStore.Server.Tests/Integration/`; `epics.md:884-907` (`:83`, `:136`) now holds Story 1.15 — Story 3.2 is at `:1585-1609`; `prd.md` FR18 is at `:186`/`:392` not `:138`/`:312`, NFR16 at `:282` not `:214`; AC3's own in-file anchors shifted +27 from the AC4 insertion. Also correct AC3's claim that the live lane "covered" the override: those tests *supply* 30s but assert only the persisted ETag and the cold null — they never read `RequestTimeout`, so the mapping was asserted **nowhere** before this change [`_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`:34,83,96,103,104,109,136,146,198]
- [x] [Review][Patch] The "Expected baselines from #271" line was never reconciled — `:249` states ≈2168 passed / 25 skipped and 28 live, while the recorded actuals are 2,868 / 25 and 49. Off by ~700 and ~21 tests, which makes the stated "+1 after AC4" arithmetic meaningless as a check. (The 2,867 → 2,868 pair itself is internally coherent and was reproduced independently during review) [`_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`:249]
- [x] [Review][Patch] The new test asserts less than the default-path test it claims to mirror — the result is discarded (`_ = await …`), there is no `actor.Received(1).GetCurrentETagAsync()`, and the `ActorId` is `Arg.Any<ActorId>()` where the sibling at `:26` pins `id.GetId() == "counter:tenant1"` and `:36-38` asserts the returned ETag. The new fact would pass even if the call had failed open [`tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs`:64,68]
- [x] [Review][Patch] Persisted-ETag evidence is restated rather than cited — Dev Notes `:278` requires citing the actual self-routing ETag value returned by `AfterRegenerate`, "not just the test's green/red"; `:316` records only "passed 2/2, including exact equality with the Redis-backed ETag" with no observed value or run artifact. This is the one AC where NFR16/AD-12 makes green/red insufficient [`_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`:316]
- [x] [Review][Defer] The timeout's *behavioral* contract is unverified anywhere — no test proves a slow actor actually fails open at the configured window [`src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`:19-25] — deferred, pre-existing
- [x] [Review][Defer] `_proxyOptions` is always non-null, so `ActorProxyFactory.DefaultOptions` (the DI-configured `HttpEndpoint`/`DaprApiToken`) is bypassed on the ETag path only [`src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`:23-25] — deferred, pre-existing
- [x] [Review][Defer] Override values are unvalidated: `TimeSpan.Zero`/negative throws inside the `try` → permanent silent fail-open; `Timeout.InfiniteTimeSpan` removes the bound the class documents [`src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`:23-25] — deferred, pre-existing
- [x] [Review][Defer] Nothing verifies the `references/Hexalith.Tenants` gitlink is coherent with the `HexalithTenantsVersion` package pin, and no test exercises the DI construction path for the optional `TimeSpan?` [`src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`:54] — deferred, pre-existing

Dismissed as noise (6): the `[scope]` bracket notation nit (the canonical shared file uses the same `[optional scope]` convention); preflight exit 3 = `EX_NO_TOPOLOGY` vs AC5's word "blocked" (moot — the live suite actually ran 49/49, AC5's primary branch); the `ConfigureAwait(false)` guardrail on the added test (the whole file omits it; CA2007 is `NoWarn` in `tests/Directory.Build.props`); the `ready-for-dev → review` transition skipping `in-progress`; `ci.yml:18` consuming `domain-ci.yml@main` unpinned (documented policy in `docs/ci.md:44-49`); "the override parameter has no production caller" (true — all 13 `new DaprETagService` sites are tests — but that is the design, not a defect).

## Dev Notes

### Top Guardrails

- **DO NOT re-implement.** FR18 shipped in PR #271 (`13320952`, CP-5). This story verifies the shipped constructor seam against the current baseline, closes the deterministic override-path coverage gap (AC4), and reconciles status/done-evidence. Changing the 3s default, the `_proxyOptions` shape, the fail-open `catch`/rethrow, or the live-sidecar 30s value is **out of scope** unless verification proves a genuine regression from baseline `87ac4450`.
- **The only expected file edit is a new unit `[Fact]`** in `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs` (AC4). Everything else is observe-and-record + the `sprint-status.yaml` reconciliation. No production `src/**` edit is expected.
- **The AC4 test must live in `tests/Hexalith.EventStore.Server.Tests`, not the live project.** Its whole point is to verify the override mapping **inside the deterministic release gate**. Story 3.1 replaced the `Category!=LiveSidecar` trait filter with physical project separation: `ci.yml` lists `tests/Hexalith.EventStore.Server.Tests` under `unit-test-projects` and runs it **unfiltered**, while `integration.yml` runs `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`. Do not reintroduce the trait filter (`docs/ci.md:60-62`). The test uses a mocked `Substitute.For<IActorProxyFactory>()` — no sidecar, no Docker.
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

**Registration — `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:54`:** `services.TryAddScoped<IETagService, DaprETagService>();` — the container instantiates `DaprETagService` with only its two DI-resolved dependencies, leaving `requestTimeout` at its `null` default → 3s. No `TimeSpan`/`ActorProxyOptions` is registered.

**Deterministic unit tests — `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs` (10 facts/theories, all use the 2-arg constructor = default path):**
- `:18-44` `GetCurrentETagAsync_ReturnsETag_WhenActorReturnsValue` — the **only** `RequestTimeout` assertion: `options.RequestTimeout == TimeSpan.FromSeconds(3)` (`:43`). This is the default-path guard AC4 mirrors for the override.
- Post-AC4 anchors: `:124-141` null-return; `:144-161` throw→null (fail-open); `:164-177` pre-cancelled→throws before proxy creation; `:180-216` OCE-is-not-fail-open-null; `:219-240` actor-id colon separator; `:243-263` self-routing format; `:266-295` remoting-interface regression pin; `:297-320` argument validation (null/blank projectionType/tenantId).
- **No fact constructs the service with an explicit `requestTimeout`.** → the AC4 gap.

**Live-sidecar tests — `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/DaprETagServiceLiveSidecarTests.cs` (`[Trait("Category","LiveSidecar")]` at `:36`, `[Collection("DaprTestContainer")]`; Story 3.1 moved this file out of `Server.Tests` into its own project):**
- `:45-74` `GetCurrentETagAsync_AfterRegenerate_ReturnsPersistedETag_NotFailOpenNull` — real `ActorProxyFactory` over the fixture sidecar; `RegenerateAsync` seeds a self-routing ETag; service constructed with `requestTimeout: 30s` (`:66`); asserts `actual.ShouldBe(expectedETag)` (persisted, non-null) — the NFR16 end-state assertion.
- `:76-95` `GetCurrentETagAsync_ColdActor_ReturnsNull_WithoutThrowing` — never-regenerated actor; `requestTimeout: 30s` (`:90`); asserts genuine cold `null` distinguished from the pre-fix NRE fail-open null.
- These are the **only** places the override is *supplied*, they never assert `ActorProxyOptions.RequestTimeout`, and their project is not in the release gate's `unit-test-projects` list (Story 3.1 replaced the trait filter with project separation) — hence AC4.

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
  --filter "FullyQualifiedName~DaprETagServiceTests" \
  -p:UseHexalithProjectReferences=false
# Then the full deterministic gate as CI runs it (unfiltered — the live tests live in their own
# project now, so do NOT reintroduce a Category filter; see docs/ci.md:60-62):
dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release \
  -p:UseHexalithProjectReferences=false

# Live-sidecar lane — REQUIRES a live control plane. Bootstrap first (VM/slim mode):
#   sudo dockerd &>/tmp/dockerd.log & ; sudo chmod 666 /var/run/docker.sock
#   $HOME/.dapr/bin/placement --port 50005 &
#   $HOME/.dapr/bin/scheduler --port 50006 --etcd-data-dir /tmp/dapr-scheduler-data &
#   (or `dapr init`). Then classify environment with the Story 3.8 preflight before asserting:
#   scripts/generated-api-smoke-preflight.sh
dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/ --configuration Release \
  --filter "FullyQualifiedName~DaprETagServiceLiveSidecarTests" -p:UseHexalithProjectReferences=false
```

Expected baselines (refreshed 2026-07-29 during code review; the original "#271" figures of ≈2168 deterministic / 28 live were ~700 and ~21 tests stale and have been discarded): the deterministic project `tests/Hexalith.EventStore.Server.Tests` sat at 2,867 passed / 0 failed / 25 skipped before this story, and the live project `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` at 49 passed / 0 failed. Add one per new deterministic fact. If the live subset cannot run locally, record `blocked` with the preflight classification — do not mark AC5 failed for a missing control plane. Clean any stale DAPR placement before running the live lane (MEMORY `tier3-integration-test-constraints`): a shared/long-lived placement with dead fixed-name actor hosts (`ETagActor` uses a fixed const type name) causes ~60s hangs.

### Implementation Hints

- **AC4 shape (as implemented, hardened by the 2026-07-29 code review).** The naive mirror of the default-path test — `Arg.Is<ActorProxyOptions>(o => o.RequestTimeout == …)` on `Received(1)` — is **not sufficient**, and the original skeleton in this file was replaced for that reason. NSubstitute stores call arguments **by reference** and evaluates `Received()` matchers at *assert* time, not at *call* time. `ActorProxyOptions` is a mutable class, so that predicate really asserts "the options object reachable from the recorded call holds 30s **when the assertion runs**". Two regressions therefore slip through it: handing over the right object and mutating it afterwards, and — the one AC1/Task 1 explicitly cares about — reverting `_proxyOptions` to a `static` field assigned in the constructor, because xUnit runs a class's facts sequentially and each fact constructs-then-asserts immediately, so the shared field always holds that fact's own value by the time it is read.
  The implemented pair closes both:
  ```csharp
  // 1. Snapshot the timeout INSIDE the factory callback => captured at call time.
  TimeSpan? observedRequestTimeout = null;
  _ = factory.CreateActorProxy<IETagActor>(
      Arg.Is<ActorId>(id => id.GetId() == "counter:tenant1"),
      ETagActor.ETagActorTypeName,
      Arg.Any<ActorProxyOptions>()).Returns(callInfo => {
          observedRequestTimeout = callInfo.Arg<ActorProxyOptions>().RequestTimeout;
          return actor;
      });
  // ... construct with requestTimeout: 30s, invoke, then:
  observedRequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));

  // 2. Per-instance independence: construct BOTH services before invoking EITHER.
  //    That ordering is what makes a shared/static ActorProxyOptions observable.
  observedRequestTimeouts.ShouldBe([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)]);
  ```
  The override fact also mirrors the default-path test's *result* assertions (`result.ShouldBe(selfRoutingETag)`, `ShouldContain('.')`, `actor.Received(1).GetCurrentETagAsync()`) and pins the `ActorId` to `"counter:tenant1"`, so it cannot pass on a silently failed-open call.
- Because a mocked `IActorProxyFactory` never builds a real proxy, these tests assert the **mapping** (`requestTimeout` → the `ActorProxyOptions.RequestTimeout` handed to `CreateActorProxy`) and its **per-instance isolation** — not that the timeout is honoured on the wire. Neither lane asserts the latter; that gap is recorded in `deferred-work.md` under the 2026-07-29 story-3.2 code review.
- If Task 1/2/3 verification uncovers a real regression from baseline (e.g. `_proxyOptions` reverted to `static`, or the parameter dropped), fix that **narrow** gap and note it — but at baseline `87ac4450` no such regression exists.
- When recording live-sidecar evidence, cite the persisted ETag returned by `AfterRegenerate` (self-routing `{base64url(projectionType)}.{guid}` format, `ShouldContain('.')`), not just the test's green/red.

### References

- [Source: _bmad-output/planning-artifacts/prd.md#FR18] (`:138`, `:312`) — requirement text + FR-to-epic coverage.
- [Source: _bmad-output/planning-artifacts/prd.md#NFR16] (`:214`) and [architecture.md#AD-12] (`:115-119`) — persisted-evidence for the live-sidecar assertion.
- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.2] (`:884-907`) — original ACs.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md#CP-5] (`:155-161`), origin defect (`:32-62`), optional IOptions follow-up (`:190`).
- [Source: _bmad-output/implementation-artifacts/3-1-re-tier-live-sidecar-tests-from-release-gate.md] (`:195`) — the FR18/#271 overlap + the lane-separation design (trait filter then; physical project separation at HEAD).
- [Source: src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:15-25,45-60] — the shipped seam.
- [Source: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:54] — registration.
- [Source: tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs:18-44] — default-path assertion to mirror.
- [Source: tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/DaprETagServiceLiveSidecarTests.cs:46-100] — override usage + persisted-ETag / cold-null assertions.
- [Source: tests/Directory.Build.props:10] — CA2007 NoWarn (build-status truth).
- [Source: _bmad-output/implementation-artifacts/3-8-generated-api-dapr-aspire-smoke-preflight.md] — `scripts/generated-api-smoke-preflight.sh` for environment classification.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — fixed-actor-name (`ETagActor`) shared-placement 60s-hang (stays deferred; clean placement before the live lane).

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Implementation Plan / Decisions

- Verify the shipped FR18 seam and current physical test-project topology before changing tests.
- Add only the deterministic 30-second override mapping test required by AC4; use a temporary ignored-parameter mutation to prove the test's red phase, then restore the shipped production source unchanged.
- Validate the exact Release/package-mode commands against both physical test projects and classify unavailable runtime prerequisites separately from product failures.

### Debug Log References

- 2026-07-29 Task 1: verified `requestTimeout ?? TimeSpan.FromSeconds(3)`, per-instance `readonly ActorProxyOptions`, unchanged scoped DI registration, and the existing 3-second unit assertion. Commit `13320952` is an ancestor of HEAD.
- 2026-07-29 topology reconciliation: Story 3.1 moved `DaprETagServiceLiveSidecarTests` to `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`; current validation uses physical project separation rather than the superseded trait filters in this story's original command examples.
- 2026-07-29 Task 1 gates: Release/package-mode Server.Tests build succeeded with 0 warnings and 0 errors; focused default-path test passed 1/1; full deterministic project passed 2,867, skipped 25, failed 0 (2,892 total).
- 2026-07-29 Task 2: verified that the constructor-derived `_proxyOptions` instance is passed directly to `CreateActorProxy<IETagActor>`. Both tests in the dedicated live-sidecar project supply 30 seconds; `AfterRegenerate` asserts the exact ETag persisted through the real actor path.
- 2026-07-29 Task 3: enumerated the deterministic cases for default timeout, null-return, throw-to-null, pre-cancellation, propagated OCE, colon actor ID, self-routing format, remoting invocation, and projection/tenant argument validation. Before Task 4, every deterministic construction used the two-argument/default path; the only explicit 30-second constructions were the two `Category=LiveSidecar` tests in the physically separate live project — and those *supply* the value without ever asserting `ActorProxyOptions.RequestTimeout`, so the mapping was asserted in no lane at all.
- 2026-07-29 Task 4 red/green proof: with a temporary ignored-override mutation that forced 3 seconds, `GetCurrentETagAsync_UsesSuppliedRequestTimeout_WhenOverrideProvided` failed on the expected NSubstitute call mismatch; after restoring the shipped source unchanged, the focused test passed 1/1 and the complete `DaprETagServiceTests` class passed 15/15.
- 2026-07-29 Task 5 deterministic gates: the Release build completed with 0 warnings and 0 errors under `-warnaserror`; `DaprETagServiceTests` passed 15/15 and the complete deterministic project passed 2,868, skipped 25, failed 0 (2,893 total, 3m56s).
- 2026-07-29 **CORRECTION (code review)** — the earlier `NU1102` was **not** transient and a no-cache restore did **not** resolve it. `Hexalith.Tenants.Contracts 5.1.0` could not resolve at all while `references/Hexalith.Builds` pointed at `86aa4cbd`, whose `Props/Directory.Packages.props:11` pinned `HexalithTenantsVersion` to **5.0.0**. What actually made the package-mode restore succeed is the Builds gitlink advance to `13cad866` (single commit `fix(deps): update HexalithTenantsVersion to 5.1.0`) carried in this story's own commit `c21a0bfc`. That bump is repo-wide — it version-controls six `Hexalith.Tenants.*` packages consumed by `Admin.Server`, `DomainService`, `AppHost` and both Server test projects, all of which reference them version-lessly under central package management. **Ratified by the owner during the 2026-07-29 review** as an intentional dependency advance and recorded here and in the File List; it was previously undeclared.
- 2026-07-29 Task 5 live gates: Story 3.8 preflight reported Docker, DAPR, placement, and scheduler healthy but no discoverable Aspire topology (exit **3** = `EX_NO_TOPOLOGY`, not the `EX_BLOCKED=2` classification AC5 names — moot, since the live lane actually ran). The dedicated project then restored and built cleanly in CI/package mode; the fixture-owned ETag class passed 2/2 and the full live-sidecar project passed 49/49.
- 2026-07-29 Task 6 scope audit: production `src/**`, live-sidecar tests/fixtures, and CI/release lane files have no story diff; no `IOptions`/appsettings binding, new live class, or identifier `Guid.TryParse` was introduced. The 3-second default, per-instance options, fail-open/rethrow contract, and both existing 30-second live values are unchanged.
- 2026-07-29 **CORRECTION (code review)** — the accompanying claim that "the change is test-and-ledger only" was **false**. The story's own commits touch 9 files, not 3: beyond the test and the two ledger/story files they carry an identical 5-line commit-policy block added to `CLAUDE.md`, `AGENTS.md` and `.github/copilot-instructions.md` (against this story's explicit "No CLAUDE.md edits" boundary), and three `references/*` gitlink bumps (`Hexalith.Builds`, `Hexalith.Memories`, `Hexalith.Tenants`). The audit sentence was narrowly true only for `src/**` and `.github/workflows/**`. All 9 files are now in the File List.
- 2026-07-29 concurrency note: while package-mode validation ran, external workspace automation committed and pushed the test and then-current story/ledger edits to `main` as `c21a0bfc`.
- 2026-07-29 **CORRECTION (code review)** — the earlier claim that the package-availability evidence "remains an unstaged story/ledger delta" was false at HEAD: commit `8ce58f83` committed those very sentences, `git status --porcelain` was empty and `origin/main == HEAD == 8ce58f83`. Nothing was left unstaged.
- 2026-07-29 process note (owner-accepted): both story commits (`c21a0bfc`, `8ce58f83`) landed **direct to `main`** without a PR, against this story's own Dev Notes guardrail and the repository's PR ruleset; CI, Commitlint, CodeQL, Advisory and Integration all succeeded on both SHAs. Separately, `8ce58f83` is typed `fix:`, which under `.releaserc.json`'s angular-default commit-analyzer arms a **patch release** (14 NuGet packages + containers) for a commit that changes only two documentation/ledger files and a submodule pointer, while `c21a0bfc` — which carries the actual test change and the Tenants dependency bump — is typed `docs:` and triggers nothing. Both contradict the `build(deps)` convention. Owner decision 2026-07-29: **accept as-is**; the release parks at the owner-approval gate.

- 2026-07-29 **code-review remediation run** (all commands Release + `-p:UseHexalithProjectReferences=false`, i.e. CI/package mode):
  - `Server.Tests` build: **0 warnings, 0 errors** under `-warnaserror`.
  - `Contracts.Tests` build: **0 warnings, 0 errors** under `-warnaserror`; `CommitMessagePolicyTests` + `SharedInstructionEntryPointTests` **17/17**; full project **778/778**, 0 failed, 0 skipped. (Required because the review removed the duplicated commit-policy block from the three entry points; `AGENTS.md` is again byte-identical to `references/Hexalith.Builds/AGENTS.md` and `references/Hexalith.Tenants/AGENTS.md`, and the three local entry points remain identical to each other.)
  - `DaprETagServiceTests` focused: **16/16** (was 15 — the review added `GetCurrentETagAsync_KeepsRequestTimeoutPerInstance`).
  - Full deterministic release-gate project `tests/Hexalith.EventStore.Server.Tests`, **unfiltered** as `ci.yml` runs it: **2,869 passed / 0 failed / 25 skipped (2,894 total, 4m02s)** — 2,868 → 2,869 for the one new fact.
  - Live project `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`: ETag class **2/2**, full project **49/49**, 0 failed.
- 2026-07-29 **mutation proof for the hardened AC4 pair (code review).** `_proxyOptions` was temporarily replaced with a shared `static` `ActorProxyOptions` mutated per construction — exactly the regression AC1/Task 1 names. Result: **1 failed / 15 passed**, the single failure being `GetCurrentETagAsync_KeepsRequestTimeoutPerInstance`. `GetCurrentETagAsync_UsesSuppliedRequestTimeout_WhenOverrideProvided` **passed under the mutation**, which is the concrete demonstration that a single-instance assertion cannot detect shared state and why the second fact was required. The shipped source was then restored byte-for-byte (`git diff` on `DaprETagService.cs` is empty); **no production code was changed**.
- 2026-07-29 **persisted-ETag evidence, cited by value (Dev Notes requirement).** After the live run, the Redis actor state store holds, for the `AfterRegenerate` identity `counter:etag-live-0b7ad363a5c04bb49babd396e7c18cd3`, key `eventstore-live-a9513f4bd8ba43e0ade121e89557cec5||ETagActor||counter:etag-live-0b7ad363a5c04bb49babd396e7c18cd3||etag` → `data = "Y291bnRlcg.WswWSbq7aEmAbYmizH3MBA"`, `version = 1`. `Y291bnRlcg` is exactly `base64url("counter")`, confirming the self-routing `{base64url(projectionType)}.{guid}` format, and the value is the one the service returned rather than a fail-open null (NFR16 / AD-12 / R2-A6).

### Completion Notes List

- Task 1 complete: the shipped 3-second production default and normal DI construction remain intact, with no `TimeSpan` or `ActorProxyOptions` registration overriding the optional constructor default.
- Task 2 complete: the explicit timeout is threaded into actor-proxy creation and the dedicated live lane retains its persisted-ETag end-state assertion.
- Task 3 complete: fail-open and cancellation coverage remains green, and the missing deterministic override-to-`RequestTimeout` assertion is confirmed as the sole code gap.
- Task 4 complete: added a deterministic release-gate fact that pins an explicit 30-second constructor override to the exact `ActorProxyOptions` passed to `ETagActor`; mutation testing proved it detects an ignored override without retaining any production change.
- Task 5 complete: FR18 remains satisfied by PR #271/commit `13320952`; this story adds the missing deterministic guard and records clean CI/package-mode builds plus deterministic and live regression evidence.
- Task 6 complete **as corrected**: runtime tuning, production defaults/behavior, lane wiring, live fixtures, and identifier parsing remain untouched — but the change was **not** "test-and-ledger only". It also carried three AI-entry-point edits (since reverted) and three submodule gitlink bumps including a repo-wide Tenants package-version advance (owner-ratified). See the Task 6 correction in the Debug Log.

### File List

Story commits `c21a0bfc` + `8ce58f83` (9 files — the first three were the only ones originally listed; the rest were added by the 2026-07-29 code review):

- `_bmad-output/implementation-artifacts/3-2-harden-dapr-etag-timeout-for-integration-conditions.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs`
- `CLAUDE.md` — 5-line commit-policy block added by `c21a0bfc`, **removed again** by the code review (duplication of delegated policy)
- `AGENTS.md` — same
- `.github/copilot-instructions.md` — same
- `references/Hexalith.Builds` — gitlink `86aa4cbd → 13cad866`; raises `HexalithTenantsVersion` 5.0.0 → 5.1.0 repo-wide (owner-ratified 2026-07-29)
- `references/Hexalith.Memories` — gitlink bumped twice (`a4517654 → 0c351ff9` in `c21a0bfc`, `0c351ff9 → 5106c935` in `8ce58f83`)
- `references/Hexalith.Tenants` — gitlink `536596f4 → 96bdfd8a`

Added by the 2026-07-29 code review (uncommitted at the time of writing):

- `_bmad-output/implementation-artifacts/deferred-work.md` — four deferred findings
- `references/Hexalith.AI.Tools/hexalith-git-instructions.md` — **submodule edit, owner-approved**: adds the missing `revert` row to the canonical type table. Must be committed from that repository's own root.

### Change Log

- 2026-07-29: Verified the shipped FR18 seam, added deterministic 30-second timeout override coverage, validated deterministic and live-sidecar suites, and reconciled Story 3.2 to review.
- 2026-07-29: Corrected transient package-availability evidence after a forced no-cache restore resolved Tenants.Contracts 5.1.0; the complete CI/package-mode matrix is green.
- 2026-07-29: Recorded concurrent absorption of the implementation into pushed commit `c21a0bfc`.
- 2026-07-29: **Adversarial code review** (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor over `efe97914..HEAD`). 4 decisions resolved by the owner, 10 patches applied, 4 findings deferred, 6 dismissed. Substantive changes: hardened the AC4 coverage into a call-time-capture fact plus a per-instance-independence fact (the original assertion could not detect a shared `static` `ActorProxyOptions`, proven by mutation); removed the duplicated commit-policy block from the three shared entry points and added the missing `revert` row to the canonical `hexalith-git-instructions.md` instead; corrected the false "unstaged delta" and "test-and-ledger only" attestations; re-attributed the `NU1102` to the Builds `HexalithTenantsVersion` 5.0.0 → 5.1.0 bump; completed the File List from 3 files to 9; reconciled every stale line anchor and the superseded `Category!=LiveSidecar` gate description to the physical project separation that exists at HEAD; discarded the obsolete "#271" expected baselines; and cited the persisted ETag by value. No production `src/**` change.
