---
title: '3.19 Isolate the production-profile bridge build'
type: 'refactor'
created: '2026-11-16'
status: 'done'
baseline_commit: '107e50e39cf2545cbc6546e60e02252251aca274'
route: 'dispatch'
review_loop_iteration: 0
planning_baseline: '107e50e39cf2545cbc6546e60e02252251aca274'
context:
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The profile bridge builds all Contracts sources, Builds imports and Commons dependencies. Authenticated publication refuses because its bindings do not cover that executable graph.

**Approach:** Compile unchanged owner profile sources in a private SDK-only bridge from a verified disposable snapshot. Reduce the application build graph while retaining the authenticated build-proof refusal.

## Boundaries & Constraints

**Always:** Work in `references/Hexalith.EventStore`; paths below are owner-relative. Preserve owner codecs, canonical bytes/digests, strict versions, sole profile slot, external coverage, safe diagnostics and closed results. Authorization/qualification stay unevaluated. Keep `PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE` before authenticated .NET execution and all other fixed refusals.

Verify complete bridge/project/configuration/source membership, bounded regular files, safe paths, no symbolic ancestors and SHA-256 before building. Bind new controls/sources in authenticated inputs. Build only verified snapshots with explicit compile items, no project/package references, private outputs, cleared package sources, disabled ancestor build/central-package imports and no build servers. Keep SDK generators. Use Debug locally, Release in CI. Preserve the existing installed-toolchain trust boundary; local hashes authenticate nothing.

**Never:** Fork profile semantics, duplicate codecs, follow nested submodules, fetch fallback packages, accept caller commands/properties or compile mutable checkout bytes. Change pins, issue authority, publish profiles/inventory, select infrastructure, implement a currency checker or complete parent stories.

## I/O & Edge-Case Matrix

| Scenario | Expected behavior |
| --- | --- |
| Complete v1/v2 synthetic inputs | Genuine unchanged owner results and digest domains; no authority |
| Missing/changed snapshot input or unbound authenticated source | Safe refusal before build; no partial snapshot accepted |
| Unrelated Contracts source or ancestor build/package configuration | Never compiled, imported or executed |
| Unavailable SDK/targeting pack, package fallback or no-op child | Refuse safely; no cached result or success from exit zero alone |
| Checkout changes after snapshot | Build only verified bytes; current-head binding checks still apply |
| Authenticated lifecycle evaluation | Existing build-proof refusal before .NET execution; no seal |

</frozen-after-approval>

## Code Map

- `tools/production-profile.cs` — move existing bridge behavior.
- `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfile*.cs` — 38 self-contained System/profile-only sources; reuse exact bytes.
- `tools/publication_authority.py` — `transition_inputs`, `evaluate_transition`, `profile_check`, `validate_owner_result` own bindings/refusals/results.
- `tools/phase_4_transition.py` — reuse `Control.bounded_process` filtering, bounds and descendant reaping.
- Completed publication-validator spec BH2/BH3/EH2 — trusted installed toolchain; incomplete executable inputs.

## Tasks & Acceptance

**Execution:**
- [x] `tools/ProductionProfileBridge/Program.cs`, `ProductionProfileBridge.csproj`, `NuGet.Config` — move bridge body, add explicit SDK-only project/config; remove obsolete launcher after updating callers.
- [x] `tools/profile_bridge_build.py` — capture bounded owner inputs and hashes, verify copied snapshot bytes/membership before execution, and use the bounded child. Honor owner `global.json`; refuse package fallback. Structural capture grants no source authentication.
- [x] `tools/publication_authority.py` — route structural checks through snapshots; bind new controls/sources in inputs/subjects; retain authenticated refusal and head/seal rules.
- [x] `tools/tests/test_profile_bridge_build.py`, `test_publication_authority.py`, `test_phase_4_transition.py` — cover matrix, pre-launch sentinels, poisoned imports/environment, changed/missing/extra sources and snapshot mutation.
- [x] `tools/tests/check_production_profile_bridge.py` — retain both codecs and all existing controls; compare Contracts golden results and prove unrelated Contracts/Builds/Commons are unnecessary.
- [x] `.github/workflows/publication-authority.yml`, `docs/architecture/publication-authority-v1.md` — update bridge commands, require controls, document reduced graph/residual proof; preserve test coverage and workflow pins.

**Acceptance Criteria:**
- Given the fixed project/configuration, bridge, profile sources and installed SDK, when synthetic v1/v2 profiles validate, then results match unchanged golden vectors without external packages.
- Given poisoned unrelated build inputs, when structural validation runs, then only verified snapshot application code executes.
- Given source isolation, when authenticated publication evaluates, then fixed barriers still refuse authority before .NET execution.

## Implementation Notes

Intent gaps/irreversibles: none. Footprint: private bridge/helper, tests, workflow/docs; no public API. Complete SDK/import-resolution proof and genuine Builds ReleaseEvidenceCodec remain later prerequisites. Platform 4.9/9.2 stay draft/backlog; EventStore 3.19 stays in-progress.

- 2026-11-16: User approved this focused specification and requested continued implementation. Its frozen intent is unchanged. Parent Story 9.2 remains draft/backlog; this approval covers the bridge-isolation prerequisite only.
- Implemented fixed project/configuration and a private snapshot helper. The bridge body is byte-identical after removing launcher directives; all 38 owner profile sources and existing SDK/dependency/publisher pins are unchanged. Structural checks capture data as well as code, verify source membership and snapshot hashes before/after children, clear package fallback and reject SDK redirection. Authenticated evaluation retains its unconditional build-proof refusal.
- Lifecycle bindings now include the helper/project/configuration/body and fixed source set. `tools/phase-4-transition.schema.json` increases publication binding maxima from 260 to 265 (209 base + 18 controls + 38 sources), with a consistency assertion. Publication fixtures materialize sources lazily so unrelated lifecycle cases avoid unnecessary serialized input overhead.
- Read the complete unified tracked/untracked implementation diff against the preserved baseline and checked every task and matrix row. Parent Story 3.19 remains in-progress; Platform 4.9/9.2 remain draft/backlog. No canonical profile, composing inventory or authority records were created, and no remote operation or commit was performed.

## Spec Change Log

## Review Triage Log

Review pass 1: all three independent layers completed before triage. Edge-case hunter returned no findings. The following rows classify every blind-hunter and verification-gap finding before grouping.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH1: implicit SDK library-pack source | medium | patch | The installed SDK marks `RestoreAdditionalProjectSources` local and can append its library-packs folder despite the empty global property. This breaks the private restore configuration's explicit source closure on SDK installations with that folder. Set the existing SDK opt-out and verify evaluated sources; no successful fallback restoration was demonstrated. |
| BH2: SDK workload advertising | maybe-false | defer | Actual background download under the filtered installed SDK was not demonstrated. The previous dotnet-run bridge also lacked this opt-out; complete SDK behavior remains unproven. A controlled network/process trace under the exact filtered environment would settle this medium-severity claim if true. |
| BH3: populated caller-cache fixture claim | low | patch | The missing-pack fixture does not construct a known usable caller cache. The private-cache override is separately tested, but the integration comment overstates this fixture's evidence. Remove that unsupported comment claim rather than inventing an availability proof. |
| BH4: owner-result call-site gap | medium | patch | An in-memory removal of only `profile_check`'s result-validation call leaves all 25 publication tests passing. The existing no-op case refuses first because no assembly exists. Exercise empty/malformed/authorizing output after fresh assembly production through the real caller. |
| BH5: process tests excluded from new filter | false | reject | `phase-4-transition-fixtures.yml` runs the complete lifecycle suite on every main push/PR without path filters, including token stripping, bounds, timeout and descendant reaping. All 43 cases executed and passed locally; shared process code is unchanged. Adding duplicate execution to another workflow would not close a coverage gap. |
| BH6: scratch SDK-pin mutation gap | medium | patch | The helper checks the separate scratch `global.json` before/after each child, but existing tests mutate only snapshot members. Add a deterministic mutation after restore and prove no build starts. |
| BH7: aggregate/data budget gap | medium | patch | Existing size tests cover an oversized program only. Aggregate capture and larger data-file budgets have no executed boundary controls. Add valid individual-size inputs exceeding the total and oversized data, both refused before launch. |
| BH8: descriptor traversal race gap | medium | patch | Static symlink cases do not exercise replacement after preliminary checks. Add deterministic intermediate-directory and leaf substitution at descriptor open, proving the no-follow traversal refuses before any child. |
| BH9: observer finally diagnostic | low | reject | A rare launch failure can be followed by a missing observation object; exception chaining still preserves the original failure and the integration run fails. Additional observer guards for this uncommon diagnostic path exceed a direct correction, so reject under the workflow's low-severity rule. |
| BH10: generic stage diagnostics | medium | defer | The former single dotnet-run bridge already returned `PROFILE_OWNER_VALIDATION_FAILED` and discarded private diagnostics for build/runtime failures. Safe stage identification could help developers, but this is pre-existing behavior and requires no change to this slice. |
| VG1: actual response-file ancestry | medium | patch | Pre-verified: removing `-noAutoResponse` leaves all ten snapshot tests passing. Real execution with poison in its actual ancestor succeeds with the flag and fails NETSDK1045 without it; current integration poison is on a sibling checkout. Put poison on the real execution ancestry and assert golden results. |

Each surviving patch has a distinct demonstrated configuration/test root and adds no public surface. BH2 and BH10 are recorded separately in the Platform deferred-work ledger; no intent or specification loopback is required.

- 2026-11-17 resolution: BH1, BH3, BH4, BH6, BH7, BH8 and VG1 are fixed. The production correction is the fixed `DisableImplicitLibraryPacksFolder=true` argument. Actual execution ancestry is now poisoned, unsupported cache wording is removed, and caller-result/SDK-pin/budget/descriptor-race controls cover the demonstrated gaps. The owner-result call-removal mutant now fails all four invalid-output subcases. No frozen intent, profile semantics, pins or authority barriers changed.

## Verification

Use `run-with-temp`; retain logs outside scratch and wait for children. Run `python3 -B -m unittest discover -s tools/tests -p 'test_profile_bridge_build.py'`, publication/lifecycle suites, and `python3 -B tools/tests/check_production_profile_bridge.py` with real .NET. Require both profile classes/golden vectors, matrix controls and no production files created. Run workflow `actionlint` and `git diff --check`. Fixtures satisfy no parent acceptance.

Executed results and exact commands are retained in the [verification summary](evidence/profile-bridge-isolation-20261116/verification-summary.json): snapshot 10/10, publication 25/25, lifecycle 43/43, original profile classes 130/130 and 105/105, and genuine bridge 51/51. Workflow actionlint and whitespace checks pass. All child work was awaited and disposable jobs used `run-with-temp`.

| Matrix row | Executed passing coverage |
| --- | --- |
| Complete v1/v2 | Both original test classes; real `complete-synthetic` and golden digest controls |
| Missing/changed/unbound | Snapshot membership/mutation/pre-launch sentinels; authenticated control and all 38 source-binding mutations |
| Unrelated inputs | Real poisoned Contracts/ancestor/environment controls without Builds/Commons/Tenants; closed project/configuration tests |
| SDK/pack/fallback/no-op | SDK-redirection/unavailable/no-op unit controls; real owner SDK pin and missing reference-pack NETSDK1226 refusal |
| Checkout changes | Real v1/v2 `verified-snapshot-survives-checkout-mutation`; snapshot byte/hash controls and lifecycle head binding |
| Authenticated lifecycle | Actual authenticated CLI reaches retained build-proof refusal with no child/seal; full 43-case lifecycle regression suite |

Earlier timing failures and the initial inadequate missing-pack fixture remain retained. The exact baseline and sequential modified-tree retries both passed; the final full lifecycle suite passed after lazy fixture setup. The corrected missing-pack fixture copies the selected SDK privately and proves actual NETSDK1226 refusal. No production deadline/property was weakened.

Post-review parent verification is complete: **15 snapshot tests, 25 publication tests, all 43 lifecycle tests and 52 genuine bridge controls pass**, as do actionlint and whitespace checks. The [final verification summary](evidence/profile-bridge-isolation-20261116/review-final-verification-summary.json) retains commands, timings, logs and all 38 source hashes. The original 130 + 105 passing profile cases remain applicable to unchanged source/fixture bytes. All six matrix rows have executed passing controls, including real poisoned build ancestry and the SDK source-evaluation probe; the latter establishes evaluated sources only and claims no package-restoration proof.

The [preservation check](evidence/profile-bridge-isolation-20261116/review-final-preservation-check.log) confirms exact owner source/body bytes, unchanged pins and absent production files. Existing configuration is compared with `git cat-file --filters` because `global.json` intentionally has a CRLF checkout and LF Git blob. The initial raw-byte comparison of those two formats was corrected without editing configuration. Disposable verification jobs and all child work have finished.

Completion: three independent review layers and all final checks are complete. The workflow records the owner change in a local Conventional Commit; its exact revision is reported in the Platform parent completion record. No push or remote CI run is performed. Production authority, complete SDK proof, ratified inventory and parent-story acceptance remain outstanding.
