---
title: '6.1-P1R published qualification harness'
type: 'bugfix'
created: '2026-10-06'
status: 'done'
approved: '2026-10-06'
approval_decision: 'approve-and-stop'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '12aa5d3a0b7b6b760e339cd9319e49123d4222a7'
tracking_scope: 'Projects P1R prerequisite; no EventStore sprint-status mutation'
context:
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/compatibility-matrix.md'
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/qualification-contract.md'
---

<frozen-after-approval reason="human-owned harness scope; actual qualification remains separately gated">

## Intent

**Problem:** Completed source remediation and preparation controls do not qualify actual published packages or lossless operational recovery.

**Approach:** Add a separate harness for the canonical seventeen scenarios/seven families, selected Reminder/evolution additions, bound receipts and recovery evidence. Require explicit owner inputs for execution and acceptance.

**Scope decision (2026-10-06):** The user selected option 1: build and test the harness first. Verify contracts with clearly identified synthetic tooling fixtures and real local process controls. No published tuple or operational profile is selected; actual package/database/container qualification and owner acceptance remain pending. Missing execution inputs produce refusal, not a default selection.

## Boundaries & Constraints

**Always:** Preserve concurrent work and evidence. Bind exact package/source/Builds/profile identities, signatures, graphs and loaded assemblies. Keep package consumers isolated in Release/package mode. Count executed checks independently of test totals. Retain freeze/forward recovery until capable rollback qualifies. Follow canonical authority, Dapr, tenant and single-writer boundaries.

**Never:** Guess authority/coordinates, republish versions, alter sealed evidence, fabricate counts, discard committed writes, mutate Projects/Builds/pins/readiness, or absorb P2/deferrals. Publication/deployment remain separately authorized.

## I/O & Edge-Case Matrix

| Input/state | Required behavior |
| --- | --- |
| Missing owner inputs or changed identities | Refuse dependent lanes; preserve unavailable/unverified status. |
| Invalid archive, graph, wire or runtime evidence | Retain the failure and compatibility disposition separately; no qualification. |
| Restore receipt contract | Require fresh-database restoration of floor 5/head 12/snapshot 9, append 13, restart, prior hashes and second-tenant preservation. Synthetic validator tests never establish this operational pass. |
| Startup/failure/timeout/cancellation/repeated cleanup | Verify bound receipts and end-state observations; local process controls prove only their own scope. |
| Missing/skipped/zero/unmeasured checks or decisions | Remain nonpassing; packet validity cannot establish usability. |

</frozen-after-approval>

## Code Map

- `tools/p1r_qualification.py` — reuse inventory, source bindings and process ownership; preserve preparation-only schema/validator.
- `tools/release_package_contract.py` — reuse archive/manifest validation.
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/run_verification.py` — immutable package/runtime/backup reference; preserve fixed historical coordinates.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Events/P1RRemediationPersistenceTests.cs` — source-only restore/append/restart reference.

## Tasks & Acceptance

**Execution:**

- [x] `tools/p1r_published_qualification.py` — implement strict input/receipt validation, inventory, counters and artifact/provenance/graph/loaded-binary checks.
- [x] `tools/p1r-published-qualification.py` — expose preparation and independent validation; refuse reused outputs, missing execution inputs and invented qualification.
- [x] `tools/p1r_qualification_runtime.py` — validate backup/restore/append/restart, tenant/inventory and cleanup receipt contracts; reuse bounded local process ownership controls. Actual backend execution requires separately selected inputs.
- [x] `tools/tests/test_p1r_published_qualification.py` — cover tampering, missing inputs, counters, substitutions, interruptions and cleanup failures; mocks cannot qualify operational lanes.
- [x] `.github/workflows/p1r-qualification.yml` and `_bmad-output/implementation-artifacts/evidence/6-1-p1r-published-qualification/README.md` — wire focused checks and index fresh receipts/decisions.

**Acceptance Criteria:**

- Given the complete canonical inventory, when validated, then missing required scenarios, families, additions or bound evidence reject qualification.
- Given a required unsupported operation, when exercised, then honest refusal and unchanged persisted inventory are retained without claiming compatible execution.
- Given incomplete owner/conformance decisions, when technical checks pass, then usability remains false and downstream gates retain their decisions.
- Given synthetic fixtures or local process controls, when checks pass, then evidence retains tooling-only scope and cannot satisfy published or operational lanes.

## Implementation Notes

- **Inventory and scopes.** The packet keeps the canonical 17 scenarios, the 7 families (mirrored from their scenario rows), both additions, the candidate/rollback package lanes and the candidate/rollback restore and container-cleanup recovery lanes.
  - Required lanes follow the owner inputs. Additions are required only when selected. Rollback lanes are required only when a rollback is selected; otherwise the freeze/forward refusal is recorded.
  - Lane scopes are `tooling-synthetic`, `local-process-control`, `published-package` and `operational`. The two tooling scopes always carry `compatibility=unverified` and evaluation never accepts them.
  - Removing fixture markers cannot promote synthetic evidence, because an injected signature verifier still keeps the lane tooling-only.
- **Evaluation.** `technically_qualified`, `decisions_complete` and `qualified` are recomputed independently. `p1r_usable` is always false and `recovery` is always `mutation-freeze-and-forward-recovery`, because usability and the recovery envelope belong to the separate coordinated transition. Downstream gate decisions are a fixed contract.
- **Package lanes.** Each lane records physical observations: archive bytes, the release nuspec contract, the signature entry, the actual `dotnet nuget verify` output, the repository commit, the `.nupkg.metadata` content hash and source, and the loaded file hashes. It also keeps exact copies of the evidence manifest, the assets and lock files, and the loaded inventory.
  - Validation recomputes every check from those copies.
  - Archive-level substitutions and defects are retained as failed checks. An unreadable manifest refuses the invocation.
- **Imported receipts.** Lane receipts and the restore/cleanup recovery receipts are validated when imported and again on validation. Their outcomes are derived from their data, so a receipt can record a failure but can never claim an outcome its data contradicts.
- **Process controls.** These are the existing bounded local controls, run unchanged. The preparation module only had its receipt and sentinel validators extracted (`validate_control_receipt`, `validate_sentinel`); its schema and messages are unchanged.
- **Evidence and blockers.** Fresh receipts are indexed in `evidence/6-1-p1r-published-qualification/README.md`. That README also records two blockers:
  - The Aspire baseline build failed during the concurrent Story 5.5 edits.
  - The live source binding refuses, because the same work left a tracked file deleted but unstaged. The real CLI packets therefore retain that refusal; the contract tests use a marked synthetic binding snapshot.

- **Review pass 1 patches (2026-10-07).**
  - The lane-receipt contract changed: `identities.packages` is now a list of `{id, sha256}` pairs (duplicates refused), so cross-version lanes can bind the candidate and rollback archives of the same package id. Every published/operational lane must include all of the candidate's verified archives and at least one candidate DLL; verified rollback archives may appear alongside them.
  - Restore receipts additionally require an exit-0 `stop-writer` between append and restart, and an exit-0 `replay` between restart and the restarted inventory. The three replay integers remain executor-reported values compared with the contract.
  - Any exception while preparing is retained in `errors` before sealing. Corrupt, encrypted or unsupported archive entries are retained as failed checks. The CLI keeps the 0/2 exit contract and reports exact refusal errors. The harness labels the signature verifier itself.
  - Post-review evidence: clean-clone invocation `285786297665409ca69577afa95a19c4` (29/29 preparation, 46/46 focused, actionlint clean, real CLI prepare/validate).

## Spec Change Log

## Review Triage Log

Review pass 1 (2026-10-07): Blind Hunter (BH), Edge Case Hunter (ECH), Verification Gap (VG). Groups G1–G19 share a root cause; `reject` rows carry their refutation or low-severity rationale.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH1 | medium | patch G1 | `create_packet` catches only InvalidPacket/OSError/ValueError/SubprocessError/KeyboardInterrupt; a TypeError from a malformed receipt escapes after `finally` seals `errors: []`, and `validate_packet` accepts the packet with the receipt silently dropped (reproduced by BH/ECH/VG). |
| BH2 | medium | patch G2 | `bind_lane` unions `verified` archives/DLLs of both roles and lane receipts have no role, so a candidate scenario/addition lane bound only to rollback identities is accepted. |
| BH3 | medium | patch G6 | Same gap as VG1: no test reaches the accept or subset branches of `bind_lane`/`bind_recovery` with a populated verified map. |
| BH4 | low | reject | `read_package_metadata` covers archive + nuspec manifest validation; graph checks already bind every resolved EventStore package to the selected id/version/content hash. Full release-set validation needs a subset-aware adapter (more than a direct fix). |
| BH5 | low | reject | Selecting the `Admin.Cli` DotnetTool fails `selected-packages-consumed` — fail-closed, never a false pass. The P1R shared scope is owner-selected (consumed libraries); tool/analyzer lanes would need new package-type branches. |
| BH6 | medium | patch G3 | Verified: `restore_observations` never requires an exit-0 `stop-writer` between append and restart or a `replay` after restart, so the single-writer restart is unproven. Sub-claim (free observation integers) → G9 docstring; timestamp order → as ECH13. |
| BH7 | low | reject | Final-attempt `remaining == []` is the end-state the contract needs; `removed` is bookkeeping. Consistency guards would be added complexity for an unlikely executor defect. |
| BH8 | low | reject | qualification-contract requires per-case output/inventory *hashes*; published/operational receipts are bound to `inputs_sha256`. Retaining bytes or freshness windows is a contract change. |
| BH9 | false | reject | Same-checkout revalidation is the documented, intended contract reused from the preparation packet ("a later change requires a new invocation"); not a defect. |
| BH10 | medium | patch G5 | Verified: on refusal the CLI prints only `refused or incomplete invocation` and drops `packet["errors"]`. Attribution sub-claim is false: `DaprInternalAuthenticationHandler.cs` is the only tracked-but-missing file (checked 2026-10-07). |
| BH11 | low | reject | Cosmetic index traceability; clone invocation `ac68c128…` is indexed in the README table and check-0 retains clone identity/overlay hashes. |
| BH12 | medium | patch G8 | Verified: Aspire receipts have another recorder's schema (`timeout_secs`, scratch `log_path`), and no retained receipt supports "appeared 12 s after the build failed". README overclaims. |
| BH13 | low | patch G7 | `package_scope` trusts the verifier callable's own `verifier` label; only reachable through the Python API (CLI always uses `default_verifier`), but the README guarantee is broader than the code. |
| BH14 | low | reject | Decisions are bound to the exact `inputs_sha256`; `conformance.baseline` is the owner's attestation field. Choosing a comparison identity is not settled and adds a guard. |
| BH15 | low | reject | Rollback is an owner selection bound exactly; rollback lanes bind `verified["rollback"]`. Ordering/package-set rules would add unsettled guards. |
| BH16 | low | reject | SIGTERM skips `finally`, but the unsealed packet fails validation (fail-closed) and every control self-exits within 30 s (`control_command`). Same exposure pre-exists in the reused preparation module. |
| BH17 | low | reject | Mutation evidence scope/labels are cosmetic; the concrete untested guards are carried by VG1–VG6. |
| BH18 | low | patch G10 | `EvaluationTests` leak `mkdtemp` trees on every run (direct fix). NU3005 skip and missing `if: always()` sub-claims rejected: ubuntu-latest ships .NET and the job is already red. |
| BH19 | low | reject | Fix edits this build's spec metadata. |
| ECH1 | medium | patch G1 | Same defect as BH1. |
| ECH2 | medium | patch G1 | Non-scalar command refs / list ids raise raw TypeError in runtime validators and feed the BH1 sealed-valid path. |
| ECH3 | medium | patch G11 | Verified: `zlib.error`, `RuntimeError` (encrypted), `EOFError`, `NotImplementedError` are outside both `inspect_archive` except tuples; a corrupt archive crashes `prepare` instead of being retained as a failed check (I/O matrix row 2). |
| ECH4 | low | patch G12 | CLI catches a fixed tuple; `RecursionError`/`zlib.error` give a traceback and exit 1 outside the documented 0/2 contract. Direct fix. |
| ECH5 | low | reject | TOCTOU swap inside the isolated evidence folder during one run; guard adds complexity, unlikely. |
| ECH6 | low | reject | Memory bound for huge DLL entries; size-limit guard, unlikely. |
| ECH7 | low | reject | The owner-selected `archive_sha256` pins the exact `.signature.p7s` bytes; signer-fingerprint binding needs a new owner input field. |
| ECH8 | low | reject | Only reachable by forging a resealed receipt; consistency, not authenticity, is the threat model. |
| ECH9 | low | reject | Project names `.`/`..`/`evidence.json` refuse with an opaque OSError (fail-closed); guard, unlikely. |
| ECH10 | medium | patch G2 | Same defect as BH2. |
| ECH11 | low | reject | qualification-contract requires per-case persisted inventory *hashes*; recomputing from rows is a contract change. |
| ECH12 | medium | patch G3 | Same defect as BH6. |
| ECH13 | low | reject | Timestamp-vs-id ordering guard for an unlikely executor defect. |
| ECH14 | low | reject | As BH7. |
| ECH15 | false | reject | A pending row can never count as approved (`decisions_complete` requires all `approved`), so no qualification outcome changes. |
| ECH16 | low | reject | As BH14. |
| ECH17 | medium | patch G5 | Same defect as BH10. |
| ECH18 | low | patch G13 | `run_cli(timeout=120)` < 2 × 180 s verifier bound; direct numeric correction. |
| ECH19 | low | patch G10 | Same defect as BH18. |
| ECH20 | low | reject | Receipts already record `exit_code` and `expected_exit_code`; editing the recorder after it produced retained receipts would misdescribe that evidence. |
| ECH21 | medium | patch G14 | Verified in check-4: mutation 18 was killed only by ERRORs in unrelated tests, so the README's "killed by their targeted tests" is inaccurate. |
| ECH22 | medium | patch G1 | Same defect as BH1. |
| ECH23 | low | patch G7 | Same defect as BH13. |
| VG1 | medium | patch G6 | Pre-verified: 7 binding mutations in `bind_lane`/`bind_recovery` survive all 41 tests. |
| VG2 | medium | patch G15 | Pre-verified: `retained-source-shape`, `reconstructed-head-twelve`, `restart-reconstructs-thirteen`, embedded-cleanup binding, `input_bytes`, inventory ordering and create-before-use mutations survive. |
| VG3 | medium | patch G16 | Pre-verified: dropping the decision role check or the decision-scope clause survives; a missing `test-owner` row yields `qualified`. |
| VG4 | medium | patch G17 | Pre-verified: 12 package-lane checks forced to `True` survive. |
| VG5 | medium | patch G18 | Pre-verified: each `package_scope` signal and the published-package compatibility mapping are untested. |
| VG6 | low | patch G19 | Pre-verified: three validate-time-only guards untested; filed `defer`, but the fix is trivial tests in the same file. |
| VG-O1 | medium | patch G1 | Same defect as BH1. |
| VG-O2 | low | patch G7 | Same defect as BH13 (validate-time scope of the marker claim). |
| BH6/G9 | low | patch G9 | Runtime module docstring claims every observation is recomputed; the three replay integers are taken as reported. Direct wording fix. |

## Verification

- Run focused Python contract/process tests and actionlint on the affected workflow.
- Observe Aspire before edits; build source verification individually in Debug/project-reference mode.
- Verify archive/graph/loaded-identity contracts with marked synthetic fixtures; real published execution remains pending input selection.
- Retain commands/results/blockers in fresh evidence; tooling tests grant no operational or acceptance pass.
