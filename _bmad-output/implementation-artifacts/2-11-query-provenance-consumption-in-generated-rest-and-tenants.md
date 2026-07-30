---
created: 2026-07-15
baseline_commit: dd9fc940f78550a776888aac93ef4011cd9ffc07
story_id: "2.11"
story_key: 2-11-query-provenance-consumption-in-generated-rest-and-tenants
status: done
split_from: 2-8-query-response-provenance-contract-and-route-aware-gateway-etag
platform_owner: 1-2-domain-query-routing-and-response-provenance
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.11: Query Provenance Consumption In Generated REST And Tenants

Status: done

## Consumer-Only Review Boundary

- Generated REST forwards projection version, lifecycle/freshness, ETag, served-at,
  warnings, and paging only when Story 1.2 supplies valid `ProjectionBacked` evidence.
- `HandlerComputed`, `Unknown`, missing, or invalid provenance omits projection-backed
  headers and renders `Unknown`; no consumer derives lifecycle from ETag, HTTP success,
  payload fields, or SignalR.
- `304` requires a strong gateway-authoritative validator permitted by Story 1.2. That validator
  proves representation identity, not current lifecycle state: a projection-backed `304` with no
  valid lifecycle header resolves lifecycle to `Unknown` and never inherits the retained value.
- Persisted-state real-gateway tests must prove evidence origin. Existing platform tests
  remain historical Story 2.8 evidence now adopted by Story 1.2.
- Until Story 4.7 has Tenants maintainer approval, affected Tenants aliases remain
  `Unknown`; this child does not edit the producer.

`done` requires independent consumer-path review plus the Tenants maintainer-approved
PR/commit, exact Tenants SHA, accepted scope, and focused persisted-path evidence.

## Tenants Consumer Evidence Owned By Story 2.11 (owner decision, 2026-07-27)

The 2026-07-27 owner decision retains the exclusive boundary in `planning-artifacts/epics.md:72`.
This story owns all Tenants-UI gateway provenance/lifecycle classification, transport plumbing,
304 policy, and gateway consumer verification. Story 2.6 cites this evidence but does not adopt,
re-prove, or sign it off; Story 2.6 owns only host alignment and presentation of already-classified
state. This supersedes the 2026-07-26 ratified-overlap wording.

Adopted from Tenants commits `56c506c` and `8ab537e`
(`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs`):

- The `Provenance is not QueryResponseProvenance.ProjectionBacked` gate and fail-closed `Unknown`
  fallback in `ResolveFreshness` and `ResolveNotModifiedFreshness`.
- The lifecycle-precedes-legacy-`IsStale` authoritative selection rule.
- The four call-site rewrites from `result.Metadata?.IsStale == true` to
  `freshness is ReadModelFreshnessState.Stale` (tenant detail, user tenants, global administrators,
  tenant audit).
- The ten supporting theories in
  `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs`.

The 2026-07-27 working-tree patch extends the owned evidence in:

- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs` —
  normalize and transport lifecycle separately from freshness; fail closed on null/untrusted 304
  metadata; and keep retained rows consistent with the resolved state.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs` —
  prove 200 lifecycle precedence across every consuming surface and assert the null/untrusted 304
  matrix through lifecycle, row, visible kind, and reason.

The 2026-07-30 Story 2.6 fourth-pass review makes the retained-snapshot rule explicit: valid
projection provenance and a strong validator are necessary for `304` reuse, but they do not attest
to lifecycle. Missing or `Unknown` lifecycle evidence therefore fails closed to `Unknown`; only an
explicit valid lifecycle value on the `304` can establish the new lifecycle state.

Review-patch evidence is **292/292 passing** in the affected UI suites and **1226/1226 passing** in
the full UI suite. The correctness finding that blocked this story is **closed**:
`TenantCorrectionStartIntent` no longer gates on legacy freshness alone. `TenantAuditRow` transports
declared route provenance beside lifecycle, and the start gate requires
`ProjectionLifecyclePolicy.IsProjectionConfirmed(Provenance, Lifecycle)` in addition to a `Current`
compatibility view. The exhaustive invariant proves the required one-way relationship: an available
correction intent always satisfies `ProjectionLifecyclePolicy.CanMutate`; correction-specific checks
may still deny a mutation that the platform policy permits.

The accepted admin-authored authority chain at `11d69920526f9881ad8c2216b28e82e497543c67`
covers the published baseline (`56c506c`, `8ab537e`, `a0c6d83`). The mutation-gate fix
`5eed7a97b87988e2f1e286a0483490ca7ef75d2b` is a parent of the maintainer-authored Tenants merge
`d2e5a1211f469041fdc593fd4e4678755f6863c8`, which is published on `origin/main`. The EventStore
gitlink pinned that exact approved merge at acceptance. The current EventStore gitlink and Tenants
checkout are the later commit `fc9a5d86436f95ace77930c0ec522fe2b3afdb45`; that does not alter the
accepted Story 2.11 identity.

## Tasks / Subtasks

- [x] Close the Story 2.11 mutation-gate fail-open (AC2 — never derive mutation eligibility from
      non-projection-backed evidence)
  - [x] Transport the declared route provenance onto `TenantAuditRow` beside lifecycle, failing
        closed to `Unknown` for absent or out-of-range values
  - [x] Set provenance on every audit row path: 200 payload, `304` retention, and missing-payload
        retention
  - [x] Gate `TenantCorrectionStartIntent.Evaluate` on
        `ProjectionLifecyclePolicy.IsProjectionConfirmed` in addition to `Current` freshness
- [x] Prove the gate with focused consumer tests (AC5 — projection-backed, handler-computed, unknown,
      and invalid-provenance paths asserted)
  - [x] Fail-closed theory for the legacy `IsStale == false` fall-through with `Unknown` lifecycle
  - [x] Fail-closed theories for `HandlerComputed` and `Unknown` provenance
  - [x] Fail-closed theories for every non-`Current` lifecycle under forced `Current` freshness
  - [x] Exhaustive one-way invariant proving an available intent always satisfies
        `ProjectionLifecyclePolicy.CanMutate`
  - [x] Gateway theories asserting provenance transport on the 200 and `304` audit paths
- [x] Prove the persisted consumer path through the production EventStore and Tenants gateways (AC5)
  - [x] Assert the live query reaches the persisted tenant projection and returns its tenant identity,
        projection timestamp, and `ProjectionBacked` route metadata
  - [x] Assert the pre-Story-4.7 producer alias cannot deserialize as audit evidence and safely degrades
        to unknown lifecycle/freshness with no correction-eligible rows
- [x] Repair correction tests that encoded the fail-open so their assertions isolate the intended
      unavailable reason

### Review Findings

- [x] [Review][Patch] HIGH — AC5 is still unclosed: Story 2.11 has no real-gateway,
  persisted-read-model consumer proof; the added tests use `CapturingGatewayClient` and the required
  Tier-3 integration lane was explicitly not run. [`_bmad-output/planning-artifacts/epics.md:1479`] —
  resolved with a passing production-gateway Tier-3 persisted-path test
- [x] [Review][Patch] MEDIUM — Audit-row provenance transport is under-verified: the 200 theory omits
  invalid/missing provenance, the 304 matrix does not assert row provenance except for
  `HandlerComputed`, missing-payload retention does not assert the reset to `Unknown`, and no component
  regression proves untrusted evidence hides the correction action.
  [`references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:1176`] —
  resolved with the complete transport matrix and component regression
- [x] [Review][Patch] MEDIUM — Repository identity evidence is stale: `5eed7a9` is already contained by
  `main`/`origin/main`, and the EventStore gitlink is `d2e5a121`, not `d90209b`; reconcile the exact
  approved Tenants identity and approval/status narrative before this artifact can gate completion.
  [`_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md:68`] —
  resolved against published merge `d2e5a121` and the matching EventStore gitlink
- [x] [Review][Patch] LOW — Replace the bidirectional claim that the intent and `CanMutate` "can no
  longer disagree" with the actual one-way safety invariant, and make the policy-agreement test prove
  that an available intent never exists when `CanMutate` denies rather than checking one positive tuple.
  [`_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md:61`] —
  resolved with an exhaustive one-way invariant matrix
- [x] [Review][Patch] LOW — Document the public `TenantAuditRow` record and its new positional
  `Provenance` property with the required XML documentation.
  [`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantAuditRow.cs:8`] —
  resolved with record, positional-property, and factory documentation
- [x] [Review][Patch] LOW — Add the modified `sprint-status.yaml` artifact to the story File List.
  [`_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md:161`] —
  resolved
- [x] [Review][Patch] HIGH — Member, configuration, metadata, tenant-lifecycle, and
  global-administrator projection gates still accept `Freshness == Current` without requiring
  projection-confirmed lifecycle/provenance, so legacy-current/unknown-lifecycle responses can still
  arm mutations outside the correction-start surface.
  [`_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md:145`] — resolved 2026-07-30 by the owner-approved Story 2.6 scope expansion; all affected gates now require current lifecycle evidence and fail closed for `Unknown`

## Dev Agent Record

### Implementation Plan

The ledger entry (`deferred-work.md:618`) recorded this defect as not mechanically fixable because
`TenantAuditRow` carried neither `Provenance` nor `Lifecycle`, leaving three design options: widen the
row, add a gateway-computed flag, or stop deriving `Current` from legacy evidence. Tenants `55e6000`
added lifecycle transport, so option one became the smallest correct change and was chosen: a
gateway-computed flag would duplicate derived state that can drift from the policy, and removing the
legacy `Current` derivation would turn every lifecycle-less route read-only, which belongs to the
Story 4.7 producer-conformance boundary, not to this consumer story.

Provenance is transported rather than inferred. `ResolveLifecycle` already normalizes against
provenance and fails closed to `Unknown`, so `Lifecycle == Current` implies a projection-backed route;
gating on lifecycle alone would have been functionally equivalent but would rest on a cross-file
invariant instead of the evidence the platform policy actually consumes. Carrying provenance lets the
gate call `ProjectionLifecyclePolicy` directly, which is what an independent consumer-path review has
to verify.

Red-green-refactor: the eight new fail-closed cases were written first and observed failing
(`IsAvailable` was `True` where the policy denies mutation), then the gate was added.

### Debug Log

- RED: `TenantCorrectionStartIntentTests` 8 failed / 15 passed — the legacy fall-through, both
  non-projection-backed provenance values, and all five non-`Current` lifecycles armed a correction.
- GREEN: `TenantCorrectionStartIntentTests` 23/23.
- Full UI suite exposed 41 failures across 7 correction/audit test classes. Each built audit rows with
  `Current` freshness and default `Unknown` lifecycle/provenance — the fail-open shape itself. Row
  factories were updated to projection-confirmed evidence; no production behavior was changed to
  accommodate them.
- The Tenants mutation-gate commit `5eed7a9` was merged into `origin/main` by the maintainer-authored
  merge `d2e5a121`, and the EventStore gitlink pins that exact merge. The working checkout later
  advanced to unrelated `origin/main` commit `7e445f3`; the Story 2.11 acceptance identity remains
  the pinned merge.
- The first Tier-3 run exposed the live pre-Story-4.7 producer alias: `get-tenant-audit` currently
  returns the tenant-detail projection shape rather than `PaginatedResult<TenantAuditEntry>`. A null
  `Items` collection is now treated as missing payload, preserving fail-closed consumer behavior.

### Completion Notes

Closed the HIGH mutation-gate fail-open routed to this story by the Story 2.6 second-pass code review.
A producer emitting no lifecycle evidence with legacy `IsStale: false`, or any response whose lifecycle
header is stripped in transit so `EventStoreGatewayClient` collapses lifecycle to `Unknown` while
retaining `IsStale: false`, can no longer arm a tenant correction. Global-administrator corrections
inherit the fix through `Intent.IsAvailable`, which `GlobalAdministratorCorrectionSnapshot.CanSubmit`
already gates on.

Baseline evidence at Tenants `5eed7a9` was all green: UI 1221/1221 (gateway subset 269/269,
correction-intent subset 23/23), Server 738/738, Contracts 113/113, Testing 181/181, Client 50/50 —
2303 total. Review-patch evidence at `7e445f3` is also green: affected UI tests 292/292, full UI suite
1226/1226, Release UI and integration builds with 0 warnings and 0 errors, and the Tier-3 persisted
consumer test 1/1. The Tier-3 proof uses `EventStoreGatewayClient` and `TenantQueryGateway` against the
live topology, asserts persisted tenant identity and projection timestamp plus `ProjectionBacked`
route metadata, and proves the incompatible pre-Story-4.7 audit alias degrades to unknown, empty,
non-actionable evidence.

The original 2026-07-27 scope decision limited this story's implementation to correction start. On
2026-07-30 the owner explicitly expanded Story 2.6's review-remediation scope: member,
configuration, metadata, tenant-lifecycle, and global-administrator gates now require current
lifecycle evidence and reject `Unknown` or any other non-current lifecycle.

### Follow-Up Resolution (2026-07-30)

- The scope decision was made and the follow-up is closed. Existing member and global-administrator
  lifecycle gates remain in force; configuration set/remove and metadata edit flows now consume the
  detail lifecycle; lifecycle availability treats every value except `Current` as unavailable and
  re-evaluates open flows whenever parent evidence changes.

## File List

- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantAuditRow.cs` (modified)
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantCorrectionStartIntent.cs` (modified)
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs` (modified)
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Hexalith.Tenants.UI.csproj` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/State/TenantCorrectionStartIntentTests.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/State/GlobalAdministratorCorrectionSnapshotTests.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/State/TenantCorrectionPreviewSnapshotTests.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Components/CorrectionStartPanelTests.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Components/GlobalAdministratorCorrectionPanelTests.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Components/AuditDataGridCorrectionTests.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Components/AuditEvidenceReceiptTests.cs` (modified)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Components/TenantAuditPageTests.cs` (modified)
- `_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md` (modified)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)

The accepted Tenants change `5eed7a97b87988e2f1e286a0483490ca7ef75d2b` is contained by the
maintainer-authored and published merge `d2e5a1211f469041fdc593fd4e4678755f6863c8`. The EventStore
gitlink pinned that merge at acceptance and now points to later Tenants commit `fc9a5d86436f95ace77930c0ec522fe2b3afdb45`.
The 2026-07-30 review patches remain uncommitted in the Tenants working tree.

## Change Log

- 2026-07-27 — Closed the Story 2.11 mutation-gate fail-open: audit rows transport declared route
  provenance and the correction start gate applies `ProjectionLifecyclePolicy`. 8 new fail-closed
  cases plus a policy-agreement positive control and 4 gateway transport theories; 41 correction tests
  that encoded the fail-open repaired. Initial dev-session commit: Tenants `5eed7a9`; subsequently
  published through maintainer-authored merge `d2e5a121` and pinned by the EventStore gitlink.
- 2026-07-27 — Applied all code-review patches: added exhaustive provenance/invariant coverage,
  public API documentation, live persisted-path consumer proof, safe handling of the pre-Story-4.7
  producer shape, and reconciled the accepted Tenants merge/gitlink identity. Story advanced to `done`.
- 2026-07-30 — Reconciled the fourth-pass `304` policy: validator/provenance evidence permits retained
  representation reuse but cannot establish lifecycle, so absent or `Unknown` lifecycle fails closed
  instead of inheriting the previous value. Added the regression test and recorded closure of the
  broader mutation-gate follow-up under the owner-approved Story 2.6 scope expansion.
