---
title: '6.1-P1R published EventStore 3.128.2 qualification'
type: 'feature'
created: '2026-10-27'
status: 'in-progress'
approved: '2026-10-27'
approval_decision: 'approve-and-stop'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'b1dd186ad49aba1c0c21d452c75fc7769feba1da'
tracking_scope: 'qualification successor; no sprint or acceptance changes'
context:
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/compatibility-matrix.md'
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/qualification-contract.md'
---

<frozen-after-approval reason="human-owned successor scope; approved 2026-10-27">

## Intent

**Problem:** The user selects 3.128.2; historical 3.115.0 results cannot qualify it. October 23 retired evidence tooling.

**Approach:** Use ordinary tests and isolated consumers for the seventeen scenarios and Reminder/evolution additions. Record commands, counts, source and CI here; raw output stays temporary or in CI artifacts.

## Boundaries & Constraints

**Always:** Use nuget.org Client/Contracts/DomainService/Server/ServiceDefaults 3.128.2, tag `v3.128.2`, source `935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8`, tag Builds `db897eac9f2819220b32d237109d7255218b46a9`. Observe executor provenance separately. Retain comparison-only 3.70.1/3.110.0, rollback=null, and the original Redis profile: Dapr runtime 1.18.4, Redis digest `c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf`. Isolate runtime binaries without changing installed Dapr. Use one writer, Dapr-owned application state access, real provider backup/fresh-database restore, exact process/container ownership and repeated cleanup. Preserve Projects AD-17 freeze/forward recovery. Owners/conformance remain pending and P1R usability false.

**Never:** Restore retired validators/seals/packet machinery, rewrite historical results, rebuild published versions, inherit another runtime's pass, modify production code/Projects/Builds/dependency pins/sprint/readiness/acceptance, initialize nested submodules, publish, stage, commit or push. Provider backup and bounded fixture diagnostics confer no application storage authority.

## I/O & Edge-Case Matrix

| Given | When | Then |
| --- | --- | --- |
| Exact selected packages | Independent Release consumers execute | Verify signatures, graphs and loaded DLL hashes; reject source substitution. |
| Coherent metadata/proof floor 5/head 12/snapshot 9 and second tenant | Backup, restore, append 13, restart/replay | Assert state/sequence/event sets, unchanged floor/prior bytes/snapshot/second tenant. |
| Malformed history or incapable old endpoint | Actual versioned operations execute | Preserve failures, typed refusals and inventories; incompatibility cannot qualify. |
| Startup failure, timeout or cancellation | Repeated cleanup executes | All owned resources disappear; shared identities/states remain unchanged. |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Events/P1RRemediationPersistenceTests.cs` — reuse persistence assertions; its logical fixture restore is insufficient.
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/P1RWireRegressionTests.cs` — authority/projection fields; replace self-roundtrips with cross-version processes.
- LiveSidecar `Integration/ReminderRecoveryLiveSidecarTests.cs` and Client `Registration/EventEvolutionManifestRegistrationTests.cs` — direct callbacks/readback do not prove natural delivery/registered evolution.
- `scripts/validate-consumer-package-references.py` — isolation pattern.

## Tasks & Acceptance

**Execution:**

- [ ] `tests/Hexalith.EventStore.PublishedCompatibility.Tests/{Hexalith.EventStore.PublishedCompatibility.Tests.csproj,ConsumerMaterializer.cs,Consumers/Host/Host.csproj,Consumers/Domain/Domain.csproj,Consumers/Probe/Probe.csproj}` — materialize Host/Domain/Probe outside repository build inheritance: nine Release consumers with `CI=true`, isolated caches, exact locks/assets and physically loaded identities; three separate Debug consumers from exact tagged source. Bind actual comparison provenance independently; preserve user changes.
- [ ] `tests/Hexalith.EventStore.PublishedCompatibility.Tests/PublishedScenarioTests.cs` and `Consumers/{Host,Domain,Probe}/` — execute provenance, legacy-metadata, metadata-read, metadata-write, full-replay, snapshot-tail, retained-covered, retained-uncovered, missing-event, invalid-evidence, query-wire, projection-wire, mixed-api, checkout, post-upgrade-restore, pre-upgrade-restore, failure-cleanup. Preserve canonical directions. Pair shared package/source semantics and persisted coordinates. Exercise actual writers/security refusals, nonnull query admission proof, projection position 987, natural Reminder delivery with submitted=0 before/after restart, and registered evolution with unchanged original bytes. Pre-upgrade restore remains containment, not lossless rollback.
- [ ] `tests/Hexalith.EventStore.PublishedCompatibility.Tests/{QualificationRuntime.cs,OwnershipRegressionTests.cs}` — enforce finite deadlines, exact descendant/container ownership including interrupted creation, transactional initialization and continued cleanup. Test wrong version/source/configuration, malformed outputs, timeout, detached descendants and preservation failure. Separate execution from compatibility; exceptions, missing/skipped cases, zero assertions and unavailable comparisons remain nonpassing. Derive totals from executed assertions.
- [ ] `.github/workflows/integration.yml` — add an isolated job for this project's Release execution with its own pinned runtime/backend and TRX/log uploads on failure; keep existing job/profile unchanged. Enroll the project in `Hexalith.EventStore.slnx`.
- [ ] This spec — run the matrix/regressions; record commands, counts, source and CI link or unavailable status. Preserve failures and pending acceptance.

**Acceptance Criteria:**

- Given complete execution, when results aggregate, then all seventeen scenarios and both additions have measured outcomes and required incompatible/unavailable lanes prevent technical qualification.
- Given matching package/source fixtures, when compared, then equivalence/delta is measured; source cannot replace package execution.
- Given pending owners, when tests finish, then acceptance/gates/usability remain unchanged.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Preflight: five actual 3.128.2 downloads have matching nuspec source and pass `dotnet nuget verify <archive> --all` (5/5, exit 0). Logs remain under `/tmp/p1r-31282-preflight-02ae65js`; this is provenance only.
- Existing candidate [CI](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/40240634704) and [release](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/40242204827) succeeded; neither ran this successor suite.
- `dotnet test tests/Hexalith.EventStore.PublishedCompatibility.Tests/Hexalith.EventStore.PublishedCompatibility.Tests.csproj -c Debug -m:1` — future local coordinator run; spawned package consumers remain Release. Use built xUnit assembly class filters for focused failures.
- `bash scripts/check-committed-files.sh` and `git diff --check` — require passing repository guards.
