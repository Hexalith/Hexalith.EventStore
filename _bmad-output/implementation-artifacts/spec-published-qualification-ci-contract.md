---
title: 'Preserve historical qualification facts in CI'
type: 'bugfix'
created: '2026-11-01'
status: 'in-review'
route: 'dispatch'
baseline_commit: '1de42b706f52af0da6513f0b7663002606d17156'
review_loop_iteration: 0
---

<frozen-after-approval reason="operator-authorized bounded CI repair">

## Intent

**Problem:** Exact published SDK source `1de42b706f52af0da6513f0b7663002606d17156` reports a green published-compatibility job while its runner fails one of fifty tests. The pipeline masks the runner exit; the historical matrix also requires compatibility from comparison-only packages and successful incompatible boundary refusals.

**Approach:** Propagate the real runner exit and distinguish historical characterization from technical qualification. Retain all 239 executed scenarios, their unsafe effects, serialization errors, failed assertions and incompatible results. Match only finite, source-grounded historical deficits while requiring all supported observations to pass. Keep qualification receipts unchanged and nonqualifying.

## Boundaries & Constraints

**Always:** Base the isolated branch on exact SDK `1de42b706f52af0da6513f0b7663002606d17156`. Preserve selected historical package `3.128.2`, source `935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8`, all actual archive/loaded identity checks, persisted assertions, exact case inventory and nineteen scenario families. Ground comparison deficits in the existing 3.128.2 comparison-only spec and actual source/API behavior. Verify unsupported legacy effects as retained observations, never successful protected refusals. Keep `ScenarioObservation.Passing`, `qualified=false`, `p1rUsable=false`, rollback=null and pending owner/conformance decisions intact. Run normal pinned lint/hooks and branch CI/Integration.

**Never:** Modify SDK runtime/library/native hosts, published versions, original checkouts or producer outputs; initialize nested submodules; adopt newer main; repurpose the candidate-specific 3.119 refusal table; filter/drop cases or allow every old-version failure. Do not publish a new package release or claim historical P1R/rollback compatibility.

## I/O & Edge-Case Matrix

| Scenario | Input | Required outcome |
| --- | --- | --- |
| Pipeline failure | Runner exits nonzero before tee succeeds | Preserve nonzero job exit and diagnostic log. |
| Pipeline success | Runner succeeds | Retain success and complete log. |
| Historical measurement | Complete immutable comparison set and known exact deficits | Characterization succeeds; unsafe execution failures/incompatibilities and nonqualification remain unchanged. |
| Supported regression | Selected package/source or supported legacy operation gains any failure | Fail CI. |
| Unknown deficit | Known historical case gains a new failure ID, different exception, unavailable or missing measurement | Fail CI. |
| Missing/false evidence | Missing/duplicate/unexpected case, false qualification or erased failure totals | Fail CI. |

</frozen-after-approval>

## Code Map

- `.github/workflows/integration.yml`: current default `/usr/bin/bash -e` masks the piped runner failure; explicit bash enables pipefail.
- `PublishedScenarioTests.cs`: unchanged complete execution and strict summary writer; replace only the terminal universal qualification assertion with explicit characterization validation.
- `ScenarioObservation.cs`: strict technical qualification predicate remains unchanged.
- `PublishedCluster.cs`: actual manifest missing-callable refusal deliberately returns incompatible; all runtime assertions remain unchanged.
- Existing 3.128.2 spec explicitly preserves comparison-only 3.70.1/3.110.0 deficits. `PublishedManifestBoundaryTests` independently requires successful incompatible manifest observations.

## FileScope

- `.github/workflows/integration.yml`
- `.github/workflows/ci.yml`
- `.github/workflows/commitlint.yml`
- `tools/tests/test_published_compatibility_workflow.py`
- `tests/Hexalith.EventStore.PublishedCompatibility.Tests/PublishedScenarioTests.cs`
- `tests/Hexalith.EventStore.PublishedCompatibility.Tests/HistoricalMatrixMeasurementContract.cs`
- `tests/Hexalith.EventStore.PublishedCompatibility.Tests/HistoricalMatrixMeasurementContractTests.cs`
- `tests/Hexalith.EventStore.PublishedCompatibility.Tests/HistoricalMatrixEvidenceContract.cs`
- `tests/Hexalith.EventStore.PublishedCompatibility.Tests/HistoricalExecutionCommand.cs`
- `tests/Hexalith.EventStore.PublishedCompatibility.Tests/HistoricalMatrixSyntheticFixture.cs`
- `tests/Hexalith.EventStore.PublishedCompatibility.Tests/Fixtures/HistoricalMatrixCharacterizationFixture.json`
- `_bmad-output/implementation-artifacts/spec-published-qualification-ci-contract.md`

## Tasks & Acceptance

- [x] Explicit bash failure propagation and four real workflow/producer controls.
- [x] Finite complete case inventory and exact grounded historical deficit expectations; all other observations require strict Passing.
- [x] Twenty-four focused supported, unknown-cause, missing/duplicate-case and false-qualification controls.
- [x] Validate the actual retained red matrix and eight adverse mutations without changing its bytes.
- [x] Validate the exact candidate with pinned commitlint; prepare the complete frozen independent-review packet.

Given the unchanged 239-case runtime, when it executes, then characterization CI passes only its declared finite results and retains the original nonqualifying summary. Given any unexpected supported defect or new historical deficit, when validated, then CI fails. Given a failed executable, when output is piped into tee, then the workflow fails with that executable's exit code.

## Implementation Notes

Parent explicitly authorized implementation without a human checkpoint. All four agent slots were occupied during investigation, so deep source/contract searches ran inline. Original failed run `41249104063`, job `124678675845`, retained log `/tmp/es31301-tag-published-compatibility.log`: 49 passed, 1 failed, 0 skipped; actual matrix 239 observations, 74 execution failures, 49 incompatible; no SDK3.130.1 runtime defect inferred from the historical comparison.

## Spec Change Log

## Review Triage Log

## Verification

Focused Python shell controls, focused C# contract controls, actual retained matrix red/green validation, pinned lint/hooks and normal remote branch CI/Integration. No original producer builds.

### Local verification checkpoint

- Isolated Release PublishedCompatibility test-project build: 0 warnings/errors; no SDK runtime/native project built.
- Focused `HistoricalMatrixMeasurementContractTests`: 24 passed, 0 failed/skipped; actual TRX `/tmp/es31301-ci-contract-controls/contract-controls.trx`.
- Python `test_published_compatibility_workflow.py`: initial actual pipeline red (2 pass/1 fail, producer23 became0), explicit bash green (3/3), then dispatch-wiring control green (4/4).
- Actual retained 239-case matrix validation accepts unchanged 74 failed/49 incompatible/qualified=false. Eight mutations reject supported failures, new legacy causes, missing/duplicate/unknown cases, false qualification, unrelated exceptions and erased failure totals. `/tmp/es31301-ci-contract-retained-controls.json`; original receipt hash unchanged.
- Exact candidate pinned commitlint: 0 problems/warnings; `/tmp/es31301-ci-contract-commitlint.log`. Normal commit hooks have not yet run because commit is held for review.
- Exact published tag live job `124678675653` completed 304 passed, 0 failed, 4 existing worker-entry skips; serialization metadata 1/1. Historical job `124678675845` remains an actual 49/1 false-green red baseline, not a qualified result.
- CI and Commitlint gain only manual dispatch events so the finite qualification branch can run normal checks without merging newer runtime main. Integration remains its ordinary full two-job workflow with branch-specific concurrency.

### Remaining delivery gates

Independent review, triaged narrow patches, normal commit/hooks/push, exact branch CI/Commitlint/Integration and actual completed logs remain pending. No new package release or original checkout/source/output mutation is authorized by this patch.


## Review Triage Log

Three independent layers reviewed frozen diff6d0183f40527. Every finding is recorded separately; duplicate-root-cause rows remain visible.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | medium | patch | The expected-outcome tuple has no assertion minimum; retained-case mutation reduces26240 assertions to485 while still accepted. Existing assertion counts are demonstrated state; bind finite case minima. |
| B2 | medium | patch | ValidateObservation never reads Measurements; the actual retained receipt remains accepted after nulling all payloads. Require applicable known evidence keys and safety coordinates. |
| B3 | medium | patch | Validate compares aggregate totals but not serialized observations; an empty saved observations array is accepted against the complete in-memory inventory. Validate saved observation equivalence. |
| B4 | medium | patch | ValidateReceiptClaims omits Builds/executor/executorInputs/executorDiffHash; deleted or inconsistent provenance in the summary passes. Preserve actual capture tuple consistency. |
| B5 | medium | patch | The XML exception allowance binds only generic dotnet-prefix plus exception substring; an unrelated executable path passes. Bind the retained failure to the implicated consumer and XML operation. |
| B6 | false | reject | All real observations with Error are constructed as failed/unverified or unavailable: PublishedScenarioTests.ObserveAsync catch at249-251 and all manual error Adds. Supported Passing+Error tuple is unreachable from the actual producer; synthetic contradictory input alone does not prove this failure route. |
| B7 | medium | patch | Both committed full Validate tests stop at inventory refusal; successful complete-input and aggregate negative controls exist only in temporary artifacts. Add durable independent complete-inventory controls. |
| E1 | medium | patch | Same demonstrated missing expected assertion/measurement contract asB1/B2; exact failure IDs alone accept reduced assertions and erased evidence. Retain this separate finding row. |
| G1 | medium | patch | Preverified gap: no retained complete-inventory test reaches receipt-total guards; deleting failed-total comparison leaves committed tests green. Add independent mutations for each aggregate. |

### Bounded review corrections

All nine triage rows above remain unchanged. The following five patch groups address the accepted findings without another review loop or any runtime, library, native-host or package change. Commit, push and dispatch remain held for parent verification of closure.

| Findings | Correction | Focused evidence |
| --- | --- | --- |
| B1, B2, E1 | Bind every finite case to independently frozen assertion floors and required measured records. Check prepared consumer/loaded identities, owned runtime and persisted coordinates, command captures and applicable safety fields. Expectations come from frozen job `124678675845` and existing instrumentation, never a newly observed candidate. | Complete synthetic controls reject erased assertions/measurements and changed runtime, loaded-binary and actor coordinates; unchanged actual 239-case receipt still passes. |
| B3 | Require saved observations to equal the complete validated in-memory measurements. | Missing, empty, altered and payload-mutated saved arrays reject. |
| B4 | Bind saved historical Builds, executor, diff hash and executor inputs to actual captured executor inputs, including the independently selected executor Builds and input-hash inventory. | Saved provenance erasure/mismatch and malformed actual capture reject; a valid historical Builds SHA substituted for executor Builds also rejects. |
| B5 | Resolve each old XML error to the implicated prepared consumer and unique actual completed command, including its operation/kind/serializer, interval, exit and retained error. Preserve only safe input-operation names, never raw credential-bearing stdin. | Foreign executable, foreign/missing command and wrong actual XML operation reject. All ten archived XML input hashes were independently reconstructed and verified. |
| B7, G1 | Add an independently stored complete 239-case synthetic fixture and builder, then run every receipt/aggregate guard through full validation. | All five aggregate mutations reject. Removing only the failed-total guard in a disposable mutation copy makes its specific committed control fail: 4 pass/1 fail, exit 2. |

The committed fixture is explicitly synthetic contract-test data, not live evidence, an operator receipt or package qualification. It contains symbolic paths/hashes and no retained runtime secrets. Actual archived evidence stays outside the repository. `ScenarioObservation.Passing`, all 239 observations, 74 execution failures, 49 incompatibilities, pending ownership/conformance and `qualified=false` remain unchanged. The future PR377 selected-99 characterization is a separate view; it does not replace this complete 239-case contract or qualify broader Works behavior.

### Review-correction verification checkpoint

- Isolated Release test-project build: 0 warnings/errors; `/tmp/es31301-review-fix-final-build.log`.
- Only the edited contract test class ran: 58 passed, 0 failed/skipped; `/tmp/es31301-review-fix-final-tests.log` and `/tmp/es31301-review-fix-closed-controls/closed-controls.trx`.
- The unchanged actual matrix SHA256 remains `b4a456c14f69eca54b6d93c9d36a8220227456c700161977fd9c75ceb36b3acd`. Positive validation retains 239/74 failed/49 incompatible/nonqualification. Eleven adverse actual-receipt controls reject; `/tmp/es31301-review-fix-actual-controls.json` records all twelve outcomes including the positive control.
- Actual receipt validation uses the original captured executor inputs, prepared builds and 197 completed command records. Ten legacy XML request inputs were reconstructed only from unchanged public fixture/writer material and accepted only after matching each original captured input hash.
- Disposable guard-deletion mutation builds with 0 warnings/errors and produces the required focused red result (4 pass/1 fail); `/tmp/es31301-review-aggregate-mutation-build.log` and `/tmp/es31301-review-aggregate-mutation-red.log`. The real branch's guard was never removed.
- Existing four Python workflow controls and exact pinned commitlint evidence remain unchanged. No broader local tests, new topology, SDK product build, original checkout/output mutation, commit, push or workflow dispatch occurred during these corrections.
