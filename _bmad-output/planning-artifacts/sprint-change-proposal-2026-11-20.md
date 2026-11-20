---
date: '2026-11-20'
status: applied-to-sprint-plan
approval: 'User approved the seven-finding sprint plan on 2026-11-20.'
mode: batch
change_scope: moderate
audit_baseline: 2ec21f631a2e292fba8c5537fe665ab275ed1081
handoff: Product Owner and EventStore developers
---

# Sprint change proposal for the repository audit

## Decision and scope

Schedule the tenant index collision, stale query validator and misleading subscriber guidance first. The first can cross an authorization boundary through a storage key, the second reproduced an incorrect cache decision, and the third can cause consumers to discard a distinct event. The user selected “Apply all seven findings to the sprint plan” on 20 November 2026.

This is a moderate correction within the current MVP. Four new backlog stories cover defects that can be delivered and tested independently. Four existing stories gain acceptance cases. The Product Owner can schedule the small documentation and fixture changes ahead of completion of their larger parent stories. The PRD readiness decision remains blocked, with the production profile and assurance decisions held by their existing owners.

## Findings and applied backlog edits

Evidence locations below refer to the audit baseline. The story sections contain the detailed acceptance criteria. [Canonical epic and story backlog](epics.md)

| Priority and finding | Before the correction | Applied edit and acceptance evidence |
| --- | --- | --- |
| 1 · High · Tenant isolation | A tenant named `all` is valid. The storage reader uses that same suffix for global reads, and the writer updates both buckets. A tenant-scoped response could include other tenants' statistics. This is source evidence. Actual exposure was unverified. | **New 5.15**, alongside endpoint authorization in 5.2. Separate every affected key family, validate returned scope and contain reads of ambiguous data before rebuilding. A tenant called `all` must see only its own statistics. An authorized global caller must see the exact combined totals, including after an interrupted upgrade. |
| 2 · High · Query validators | When a producer omits its ETag, a later projection lookup can attach revision 2's tag to revision 1's payload. The audit observed a subsequent 304 for that stale representation. | **New 2.18**. Omit an unbound validator from headers and response metadata. Exercise a deterministic interleaving through the gateway and client. Verify a matching producer-bound ETag can still yield 304 and an untagged or changed representation returns 200. |
| 3 · High · Subscriber deduplication | The deployment guide derives CloudEvents identity from correlation ID and sequence. Two aggregates can share both. The publisher already emits the persisted event message ID, and an existing publisher test covers the collision case. | **Amend 5.9 and 7.1**. Correct the operator example, then drive the received `(source, id)` pair through a subscriber. Two distinct events and one replay should produce exactly two handling effects. Reconcile the existing open deferred-work entry when the docs change lands. |
| 4 · Medium-high · Production claims and Phase 4 fixtures | “Any store/broker, zero-code swap” overstates evidence. Local Redis coverage and a profile codec do not qualify deployed storage, delivery or restore. In a separate check, one transition fixture expected an unbound G-TENANT although its matrix row now binds subjects. | **Amend 5.9 and 9.2**. Label tested, configured and unqualified profiles. Split the refusal fixtures and rerun the suite. **3.21, 7.22 and 3.19** retain candidate qualification, restore and profile acceptance. Preserve the live refusal for every gate missing its required evidence. |
| 5 · Medium · Admin stream activity | Case-insensitive matching merges distinct aggregate IDs. In an isolated tracker probe, a stream evicted at sequence 7 returned at sequence 8 with a displayed head of 1. The audit counted eight mocked Dapr reads and eight writes for a new stream. | **New 7.23**, coordinated with 5.15 at the shared writer. Project the committed head and counts. Verify case-only IDs, eviction/re-entry, duplicate and out-of-order delivery against persisted state. Measure command latency before deciding whether to move projection off the request path. |
| 6 · Medium · Query failure privacy | The adapter path classifies some failures by free-text substring and can pass arbitrary text to logs and HTTP 500 detail. The audit did not establish that customer data was disclosed. | **New 2.19**. Use known reason codes, an explicit legacy mapping, a generic internal detail and the correlation ID. Assert classifications and the absence of a synthetic sensitive marker in the response and captured log fields. |
| 7 · Medium · CI and onboarding signals | The advisory job permits failure and lists an absent project. The README links to an absent docs workflow and sample. Docs lint reported 26 issues in 11 files and the version check found four stale pins. Lychee 0.24.2 rejected the config and 0.23.0 reported 61 offline link errors. Some shared CI inputs follow mutable refs. | **Amend 7.13 and 5.9**. Test success, failure and missing-project conclusions independently of the release gate. Repair docs targets, reconcile pins and run a compatible link checker. Route workflow policy and rendering through Hexalith.CiCd. Check resolved refs against FR25 and the release lock. |

### Evidence at the audited revision

| Finding | Repository locations |
| --- | --- |
| 1 | `src/Hexalith.EventStore.Contracts/Identity/TenantCanonicalizer.cs:22`, `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageQueryService.cs:48` and `:93`, `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs:208` |
| 2 | `src/Hexalith.EventStore/Controllers/QueriesController.cs:387` and `:453`, `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs:761` and `:791` |
| 3 | `deploy/README.md:46`, `docs/concepts/event-envelope.md:253`, `src/Hexalith.EventStore.Server/Events/EventPublisher.cs:222`, `tests/Hexalith.EventStore.Server.Tests/Events/SubscriberIdempotencyTests.cs:147`, `_bmad-output/implementation-artifacts/deferred-work.md:6679` |
| 4 | `README.md:53`, `deploy/README.md:41`, `docs/architecture/production-profile-v2.md:3`, `tools/tests/test_phase_4_transition.py:536`, `tools/phase_4_transition.py:517` |
| 5 | `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs:80`, `:95`, `:108` and `:173` |
| 6 | `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs:133` and `:152`, `src/Hexalith.EventStore/ErrorHandling/QueryExecutionFailedExceptionHandler.cs:88` |
| 7 | `.github/workflows/advisory-tests.yml:22`, `:30` and `:73`, `.github/workflows/codeql.yml:20`, `README.md:8` and `:47`, `docs/ci.md:3`, `scripts/validate-docs.sh:40` |

## Delivery order and estimates

Estimates are engineering effort. Production scheduling depends on Platform capacity, an agreed candidate and existing qualification prerequisites.

| Window | Owner and delivery order | Trade-off or dependency |
| --- | --- | --- |
| Quick wins · 2–4 engineer-days | Admin developer contains the ambiguous tenant read first (5.15). Gateway developer removes the detached validator (2.18). Docs and subscriber owners correct the identity example and test it (5.9/7.1). Clarify the README claim immediately. Test owner corrects the stale refusal scenario (9.2) and identifies the advisory inventory mismatch (7.13). | Some tenant metrics may show unavailable while indexes are rebuilt. An untagged legacy producer may send more 200 responses. Both outcomes are preferable to incorrect data. The docs and fixture corrections can proceed independently. |
| Near term · 5–10 engineer-days | Admin owners complete 5.15 versioning, rebuild and rollback rehearsal, then deliver 7.23 against those keys. Gateway owner delivers 2.19. Docs and CI owners close the local validation gaps and verify the rendered advisory result with a failing fixture. | Coordinate the shared Admin writer once. Keep aggregate sequence distinct from lifetime count wherever erasure or incomplete evidence prevents inference. Inventory existing adapter sentinels before changing classification. Upstream CI delivery can extend elapsed time. |
| Larger work · 1–3 weeks for an initial qualification cycle | EventStore supplies backend-neutral assertions. Platform executes candidate comparisons required by 3.21. Owners take one eligible target through durable delivery, ordering limits, recovery and a 7.22 restore drill, then submit its exact inputs to 3.19. | The current candidate has unresolved checks. Existing selection, AD-26 and release gates control eligibility. Missing runtime guarantees or external capacity can extend the estimate. A broad multi-backend certification is outside this increment. |

Keep the synchronous activity writer until comparative measurements justify a change. The eight mocked reads and writes establish an operation count only. Track the isolated versus concurrent behavior of `ProcessPublicationTests.RegistrationCostDoesNotGrowWithHistoricalPages` in 7.13. It timed out once in the broad run and passed in isolation. Consider extracting recovery logic from `AggregateActor.cs` only when that path next changes and its behavior can be protected by focused tests (conditional estimate 2–4 days).

## Artifact and architecture impact

- **Sprint tracker.** Added backlog rows 2.18, 2.19, 5.15 and 7.23. Stories 5.9, 7.1 and 7.13 already had backlog rows. Story 9.2 already had an in-progress row. [Sprint status](../implementation-artifacts/sprint-status.yaml)
- **PRD.** Section 11.2 gains supporting declarations for NFR2 (5.15), NFR8 (2.18), NFR12 (2.18 and 2.19) and NFR16 (5.15 and 7.23). Existing primary owners keep their closure obligations. FR25 requires governed security callers to use `@main`. Any proposal to pin those callers needs a policy decision. [Product requirements](prd.md)
- **Architecture and UX.** The design already requires scoped authorization, representation-specific validators, stable event identity and honest Admin evidence. These fixes affect the storage projection, query gateway and operator guidance. The existing screens and service boundaries support the corrections. All five recorded epic input digests already differed from their source files at the audited commit. Story 9.3 owns that reconciliation. Future guarded transitions must bind the applicable current subjects. [Architecture](architecture.md) · [UX handoff](ux.md)
- **Existing specifications.** 5.2 continues to own endpoint filters. 5.15 covers the storage boundary behind them. 7.1 owns subscriber behavior while 5.9 owns the immediately deliverable guide. The required 3.21 broker comparisons, 7.22 restore drill and 3.19 profile decision already have owners. The 9.2 fixture correction belongs in its next test slice. Past evidence and completed story records retain their original scope.

## Handoff and verification

The Product Owner owns backlog sequencing. The Admin, gateway and subscriber developers own the named implementation and regression tests. The test owner should run the complete transition fixture suite and the live matrix independently. In the audit, fixtures passed 42/43 while the matrix correctly reported 12 high-risk gates below required assurance. Those are separate acceptance signals. The CI owner routes policy and renderer changes to Hexalith.CiCd. The Platform owner supplies the exact target and observed results for qualification and restore.

Direct adjustment keeps each defect with its owning component and gives the maintainer a reproducible exit condition. Rolling back completed stories would discard working producer identity, authorization and positive conditional-query paths. Reducing MVP scope would still leave the tenant collision and stale cache responses reachable. The repository can deliver the local fixes while the profile remains unqualified.

Applied in batch on 20 November. The repository's coverage reader resolved each new story to its declared supporting requirements. All four added tracker rows read `backlog`, all 159 prior tracker dispositions were preserved, and three focused story-completion guard tests passed. The proposal's five local links resolved. Implementation, production migration and CI policy delivery remain separate work.
