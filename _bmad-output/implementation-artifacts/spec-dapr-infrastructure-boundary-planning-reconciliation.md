---
title: 'Apply the Dapr Infrastructure Boundary Proposal'
type: 'refactor'
created: '2026-10-05'
status: 'ready-for-dev'
route: 'dispatch'
baseline_commit: 'ff7f07d1ff12b94c7b53646581ca3843b2c09548'
concurrent_paths:
  - src/Hexalith.EventStore.Client/Events/EventDomainRegistry.cs
  - src/Hexalith.EventStore.Client/Events/EventLogicalViewResolver.cs
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-05.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** P1–P7 have not reached planning, documentation or tracking. Portability claims and unconditional Azure SDK selection conflict with the required Dapr boundary.

**Approach:** Apply the initial planning/documentation slice. Author backlog specifications for enforcement/notification qualification and a detached draft crypto amendment; runtime work follows their evidence gates.

## Boundaries & Constraints

**Always:** Require suitable Dapr operations with profile-specific correctness/security/compatibility guarantees. Unknown suitability stays unresolved. Exceptions require evidence, owner, exact paths and review/removal trigger. Preserve concurrent edits, readiness/production blocks, actor ownership, notification contracts, tenant isolation, freshness and existing lifecycle values. Keep 2.13/3.17 backlog, 6.6/8.3 in progress and 8.6 backlog. Historical approvals remain historical.

**Never:** Implement guards/migrations, select provider packages, approve exceptions, provision services, alter runtime/topology/submodules, or claim qualification. Preserve shared payload authority, 8.3 frozen intent, approval packets, fixtures and validators byte-for-byte. Policy approval does not approve the amendment; no evidence is repinned.

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs` is linked into Gateway. Retain Redis pending qualification. Reuse `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DependencyModeEvaluationTests.cs` for future graph enforcement.
- `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md` is frozen: normative digest `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`, full-file SHA-256 `542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82`. `AR-20260913-01` approves the replacement; `AR-20260914-01` approves 8.2 and authorizes 8.3. The separate 8.1 wrapper is not part of those hash bindings.

## Tasks & Acceptance

**Execution:**

- [ ] `_bmad-output/planning-artifacts/prd.md` — add §8.4, FR37 clarification and supporting FR/NFR traceability without changing identities or readiness verdicts.
- [ ] `_bmad-output/planning-artifacts/architecture.md` — broaden AD-1 while retaining 6.6 constraints; clarify AD-3/AD-26; reconcile AD-23 authority; label Dapr infrastructure, public transport, telemetry and unresolved SignalR edges accurately.
- [ ] `_bmad-output/planning-artifacts/epics.md` — add 2.13/3.17 with coverage/dependencies and Given/When/Then criteria; update epic lists, supporting coverage, 3.16 dependency policy, 7.11 Dapr-path evidence, and 8.6 conditional qualification; reconcile current payload citations while retaining historical records.
- [ ] `_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md` and `_bmad-output/implementation-artifacts/3-17-dapr-boundary-inventory-and-enforcement.md` — author actionable backlog specifications. Cover replica delivery/reconnect/duplicates/outage/tenant denial, evaluated/transitive/linked/generated inputs, HTTP/credential bypasses, guard negatives and analysis limits.
- [ ] `_bmad-output/implementation-artifacts/8-1-shared-payload-protection-security-spec-and-adr.md` — reconcile present summary with current approved authority and existing done state; preserve historical evidence.
- [ ] `_bmad-output/implementation-artifacts/spec-shared-payload-protection-dapr-amendment-2026-10-05.md` — draft non-authorizing deltas to shared spec §§5/11/16 and PF-01; bind unchanged authority hashes; enumerate required key-operation qualification and 8.3–8.11 impact/reapproval gates. Preserve pdenc-v2/AAD, custody, typed failures, historical reads and rollback; leave qualification unresolved.
- [ ] `docs/concepts/dapr-infrastructure-boundary.md` and `docs/architecture/dapr-infrastructure-exceptions.yaml` — publish policy/preliminary classification with owned unresolved stories and an empty accepted-exception register. Final inventory/guard belongs to 3.17.
- [ ] `docs/concepts/architecture-overview.md` and `docs/guides/dapr-component-reference.md` — qualify portability claims and link the boundary guide. `_bmad-output/planning-artifacts/ux.md` — record unchanged routes/states and transport-failure/tenant-isolation obligations.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` — add the exact proposed backlog rows and synchronize update dates; preserve all other values and guarded comments.
- [ ] `_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-05.md` — append actual approval/application disposition; retain historical decisions and pending qualification.

**Acceptance Criteria:**

- Given the proposal, when reconciled, then P1–P7 have consistent policy, coverage and actionable backlog specifications, without inferred qualification/completion.
- Given retained direct Redis and unknown crypto suitability, when documentation is read, then operations/paths, owning stories and evidence gates are explicit and no approved exception is fabricated.
- Given frozen payload/predecessor evidence and existing tracker values, when the diff is validated, then their protected bytes/values remain unchanged and the detached amendment is visibly draft/unapproved.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- `git diff --check ff7f07d1ff12b94c7b53646581ca3843b2c09548` — no whitespace errors; inspect scope/protected bytes.
- `npx markdownlint-cli2 docs/concepts/dapr-infrastructure-boundary.md docs/concepts/architecture-overview.md docs/guides/dapr-component-reference.md` and matching `lychee --config lychee.toml --offline` inputs — changed public docs pass lint/local links.
- Parse YAML with duplicate rejection; compare every pre-existing tracker value and guarded block to baseline; verify new keys/coverage/links and current payload hashes.
- `node scripts/payload-protection/verify-golden-vectors.mjs` and `python3 scripts/payload-protection/verify-golden-vectors.py` — frozen vector verification passes without claims for gated cases.
- Build/run `tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` individually; run its complete executable after tracker comment edits. Report exact blockers and focused evidence.
