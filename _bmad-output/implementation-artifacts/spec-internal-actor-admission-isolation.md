---
title: 'Isolate authenticated actor callback admission quotas'
type: bugfix
created: '2026-10-22'
status: done
route: dispatch
baseline_commit: c66a0a3345ad97b137959179a0583faf81d59e4b
review_loop_iteration: 0
context:
  - AGENTS.md
  - references/Hexalith.AI.Tools/hexalith-llm-instructions.md
---

<frozen-after-approval reason="explicit user implementation and qualification authorization">

## Intent

Folders Story 12.2 Task 36 strict acceptance fails waiting for BranchRefPolicyConfigured. The retained log first rejects IdempotencyAdmissionActor.ValidateAuthorityAsync and then MarkRecoveryAsync with HTTP429; recovery cannot persist unknown outcome and retries fail closed. Global tenant/consumer HTTP quotas collapse runtime callbacks into anonymous public traffic. The owning Aspire helper supplies no EventStore app-channel token; one shared verified-sidecar bucket would still couple unrelated actors.

Correct the EventStore-owned authenticated callback partition and qualify production HTTP admission against real Dapr Redis persistence with process recovery. No further product intent or irreversible action is needed.

## Boundaries & Constraints

Always preserve existing public/workload quota numbers, windows, queues, authentication, admission authority/fence validation, unknown-outcome fail-closed semantics, durable completion, and conflict refusal. Actor callbacks remain rate limited with the same configured ceilings, separated by actual actor identity after configured-token verification and matched endpoint recognition. Require app-channel authorization on mapped actor handlers; a channel proves no workload/human authority. Never trust path/header/caller-name claims alone or Development-unverified identity for partition isolation.

Never disable limiters, increase budgets, change dependency pins, publish, stage, commit, push, branch, touch unrelated AppHosts, change Folders production persistence, acknowledge conflicts, or alter historical evidence. Preserve all existing work (initial dirty spec/sprint files are recorded). Preserve inherited replay implementation and identity/continuity/CAS/epoch/batch guarantees; coordinator source hash is 23f82c1898318e7d9a1fc0e6892586d210a5d48debab58087f2c9c2176b1cbe9. Task36, package/PD10, SM4 and whole-story gates remain separate.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior | Failure handling |
|---|---|---|---|
| Cross-actor saturation | Real actor A exhausts unchanged configured quota; B admission continues | B can validate/recover/complete through own bounded partition | A retains HTTP429 |
| Same actor saturation | Repeated valid calls to one actor | Existing quota produces HTTP429 and Retry-After | No bypass |
| Forgery | Missing/wrong/duplicate token, public lookalike path, human copied claims | No isolated callback partition; mapped actors reject untrusted channel | 401/403 or existing 429 |
| Workload boundary | Channel token without valid workload operation assertion | Public/workload operations stay denied | Existing authentication policies |
| Recovery | Terminal command then real EventStore/sidecar process replacement | Same identity/result replay, one retained event and terminal admission/status | Ambiguous outcomes never authorize repeat effect |
| Conflicting intent | Existing key with changed payload/target | Conflict refusal; durable state unchanged | No acknowledgement |
| Public traffic | Tenant and consumer quotas reached | Existing public HTTP429/Retry-After | No budget change |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore/Extensions/{ServiceCollectionExtensions,EventStoreRateLimitPartition}.cs`: chained global tenant/consumer limiters; already supports verified sidecar key but currently one bucket. Compiled in Gateway via linked source, inspect csproj.
- `src/Hexalith.EventStore/Program.cs`: production HTTP middleware order and actor mapping. Mark actual mapped routes with trusted endpoint metadata and sidecar policy; inspect Dapr 1.18.10 route values rather than guess.
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`: AddHexalithEventStore owns token wiring immediately after WithDaprSidecar. Reuse HexalithEventStoreAppChannelExtensions.
- `src/Hexalith.EventStore.ServiceDefaults/Authentication/`: existing constant-time configured-token verifier, SidecarChannel scheme/policy; reuse without weakening workload proof.
- `tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreWorkloadRateLimitTests.cs`: bounded partition and forged principal controls.
- `tests/Hexalith.EventStore.IntegrationTests/EventStore/*RateLimitingTests.cs`: existing production pipeline public throttling controls.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs`: exact production EventStore/Sample OS-process shadow launch, JWT/token wiring, persisted checks and restart pattern. Do not change its unrelated qualification matrix. Redis component/read seams in DaprTestContainerFixture; that in-process fixture alone omits HTTP limiter and cannot qualify this fix.
- Folders evidence packet `/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/evidence/story-12-2-inherited-owner-requalification-20261022T084922Z/`: handoff, strict-admission-failure, strict-live log/XML, qualification and external Git observation are required causal inputs; do not modify.

## Tasks & Acceptance

- [x] Add focused production-pipeline regression tests for the matrix. Run before production changes and retain expected red failure.
- [x] Correct token wiring, authenticated callback endpoint recognition and actor-scoped bounded keys; no numeric quota edits.
- [x] Add a focused process fixture/test using production HTTP host, actual Sample/domain processing, real Dapr Redis state, authentic channel callbacks and process replacement. Assert retained command status, aggregate metadata/event count, admission terminal/replay contents, conflict refusal, public throttling and callback authentication. Keep fixture-owned resources isolated and clean up exactly those resources.
- [x] Run focused green controls and relevant existing admission/auth/rate-limit regressions; retain exact commands/logs/XML and blockers in `_bmad-output/implementation-artifacts/evidence/internal-actor-admission-20261022/`.
- [x] Record causal evidence, source/binary identities and cleanup for Folders handoff; keep remaining gates explicit.

Given the production host and real Dapr persistence, when unrelated callback traffic saturates its own unchanged quota, then authenticated admission operations still complete durably. Given process replacement, when the original command is retried, then identity/result replay is exact and no second event is appended. Given ordinary public calls, when tenant/consumer budgets are exhausted, then HTTP429 remains enforced.

## Implementation Notes

Consumer-path audit: Folders references Gateway/DomainService and supplies its own Program. The correction must cover its existing raw MapActorsHandlers route through verified Dapr delegate provenance, beyond the standalone marker. Review preserves public quota override lookup while structurally separating internal actor partitions from raw public subject strings.

Dispatch uses the user's explicit instruction to implement and qualify; no new permission checkpoint is needed. Do not load skills from references. Test builds use Debug/project references, individual test assemblies, no dependency updates. Reuse existing infrastructure read-only if needed, but prefer owned dynamic-port Redis/control plane and namespace. An unrelated Tenants AppHost is active; never stop or rebuild it.

## Spec Change Log

## Review Triage Log

| parent-custom-host | medium | Folders owns a distinct Program with raw MapActorsHandlers; private standalone marker does not reach consumed path. Patch recognition of actual matched Dapr delegate provenance and exact route metadata with configured-token verification, plus a raw-host regression. No consumer persistence/host changes. | patch |

| blind-1 | medium | Actor callback keys and raw human subjects share a string namespace; verified from global consumer limiter. Patch structured actor/public partition keys while preserving override lookup. | patch |
| blind-2 | medium | Generic failure status alone is a weak guard oracle. Patch authenticated inspection/decision readback and unchanged state checks after each refusal; production diagnostics deliberately redact exception messages. | patch |
| blind-3 | medium | Public throttle oracle permits anonymous/authentication failure false positives. Patch exact 100 authenticated 404 responses then 429, partition identity and Retry-After. | patch |
| blind-4 | medium | Saturation can expire before command. Patch throttled actor checks immediately before and after compared operations. | patch |
| blind-5 | medium | Recovery terminal oracle only compares payload. Patch full replay result and original identity through AdmitAsync and persisted rows. | patch |
| blind-6 | medium | First failed docker rm short-circuits cleanup. Patch independent idempotent cleanup with absence verification. | patch |
| blind-7 | medium | Overall passed flag can ignore cleanup failure. Patch separate qualification/cleanup outcomes and require both. | patch |
| blind-8 | medium | Standalone process test is not discovered by normal live test project. Patch a discoverable live qualification test invoking the same owned runner. | patch |
| edge-1 | medium | Same cleanup short-circuit and success-flag defect as blind-6/7; grouped patch. | patch |
| edge-2 | medium | Same saturation expiration oracle as blind-4; grouped patch. | patch |
| edge-3 | medium | Same generic refusal oracle as blind-2; grouped patch. | patch |
| verification-1 | medium | Normal tests only exercise DELETE/synthetic values while process script covers PUT. Discoverable live test will invoke exact persisted-state/process scenario. | patch |

## Verification

Build individually with `dotnet build <test-project> -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false`; invoke built xUnit v3 assemblies using `-class` / `-method`, `-showLiveOutput`, `-result-xml <packet-file>`. Preserve failed attempts, exact command receipts, persisted snapshots and cleanup. Existing repository instruction calls for Aspire baseline; exact-target describe reported no EventStore AppHost running, and fixed 8080/3501 endpoints are already owned by unrelated Tenants topology. Use isolated fixture processes for red baseline/qualification, with no unrelated topology mutation.

Verified outcome: final-admission-auth-rate.xml passes547; topology passes18; cleanup controls pass4; live-final-v2.xml passes1 discoverable test with47 assertions, no skips. Final-script frozen baseline reproduces actor B429. All review patches are implemented, including raw custom-host provenance. The failed health matrix and interrupted broader suite remain separate, with no Task36/package/PD10/SM4/story closure. User instruction prohibits commits and overrides the workflow commit step.
