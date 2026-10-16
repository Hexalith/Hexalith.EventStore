---
title: '6.1-P1R tooling hardening before successor qualification'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'dispatch'
baseline_commit: '247a192274d0ab01b81b8f5430d6293c3bdbda95'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user-authorized separate tooling package; preserve Story 6.1 draft">

## Intent

**Problem:** Recovery spec `spec-6-1-p1r-5.md` completed source recovery but recorded nine tooling gaps that prevent reliance on future P1R qualification.

**Approach:** Harden the isolated controller and proposed-successor importer, add regression controls through their real callers, retain fresh hash-bound verification and independent review, and deliver a canonical-source reconciliation/publication handoff.

## Boundaries & Constraints

**Always:** Work in EventStore alongside the Projects Story 6.1 draft. Preserve completed recovery records, historical evidence, production source, acceptance, package/runtime/backend pins and sprint state. Retain failed attempts. Keep P1R unusable and Story 6.1 blocked. Use explicit validation under optimized Python too. Remote Docker support is optional: this package chooses explicit refusal before provisioning; only local Unix endpoints qualify for tooling controls.

**Never:** Commit, stage, push, publish, switch branches, move HEAD/gitlinks, update dependencies, initialize nested submodules, select/reserve a release version or claim published-package qualification. Recovered historical manifests remain historical; create fresh source bindings.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior |
| --- | --- | --- |
| Images/creation | Missing or substituted image/create evidence or Python-tools role | Refuse import |
| Launch/input | Missing actual SDK launch, altered options or input bytes/hash | Refuse import |
| Mutation | Correctly hashed unrelated Docker mutation | Refuse import |
| Processes/tools | Missing supervisor/owned identities/cleanup or altered Docker/dotnet identities/versions | Refuse import |
| Cleanup | Duplicated/reordered observations | Refuse import |
| Remote endpoint | TCP/TLS/SSH controller | Refuse before Docker provisioning; retain reason |
| Slow daemon | Hung or failing startup probe | Short probe timeout within one overall monotonic deadline; retain failure/cleanup |
| Published run | Successful private execution and outer cleanup | Real controller attaches persisted receipts accepted by real importer |
| Failed run | Private/outer failure or finalizer failure | Nonpassing result; no successful qualification attachment |

</frozen-after-approval>

## Code Map

Paths are relative to EventStore.

- `tools/p1r_isolation.py`: `_run`, `docker`, `private_proof`, `_inner`, `cleanup`; retain image pins, exact ownership and signal handling. Add image/toolchain measurements, launch binding, ordered command IDs, timed process cleanup and bounded startup.
- `tools/p1r_published_qualification.py`: harden `validate_final_isolation` for proposed 3.122.1; retain historical version paths. `finalize_isolation_receipts` attaches evidence after cleanup. A dedicated `tools/p1r_isolation_evidence.py` may validate the journal.
- `tools/p1r_qualification.py`: reuse `regular`, `canonical`, `digest`, process identity helpers and `OwnedProcesses`. Identity includes PID/start ticks/PGID/session. Preserve shared schemas.
- `tools/tests/test_p1r_published_executor.py`: rebuild `attach_isolation` positive fixture to match producer; rehash negative evidence through real `receipt_outcome`.
- `tools/tests/test_p1r_isolation.py`: outer lifecycle and real process controls; add real `_run` caller coverage with finalizer/importer unmocked. Wire any new `test_p1r_isolation_hardening.py` suite into `.github/workflows/p1r-qualification.yml`.
- `tools/p1r-published-consumers/README.md`: document local controller profile.
- Projects recovery spec `spec-6-1-p1r-5.md`, final nine `deferred-work.md` entries, EventStore recovery and successor-enrollment handoffs: immutable context, already investigated by parent.

## Tasks & Acceptance

**Execution:**

- [x] Controller: measure pinned image OS/architecture/digest/image ID and bind exact role creation/inspection, copy chain and SDK `_inner` launch. Retain Docker client/server and dotnet executable hashes/versions, selected input path/hash, supervisor/root/owned identities and two timed cleanup observations.
- [x] Importer: validate every journal command with exact allowed forms/owned targets; bind literal image/create/launch/toolchain evidence and complete executor args/selected physical input hash; refuse missing process proof, duplicate or reordered cleanup observations. Require distinct ordered command IDs/timestamps and lifecycle containment.
- [x] Controller: explicitly refuse all nonlocal Unix endpoints, preserve TLS credentials from retained logs, and implement a bounded startup deadline with short probes.
- [x] Regression suites: positive and semantic mutation controls for all matrix rows; real caller success imports persisted receipts after teardown, and private/outer/finalization failures remain nonpassing. Include a control that fails when caller finalization is removed.
- [x] New `evidence/6-1-p1r-tooling-hardening-20261016/`: retain literal commands/results, exact source hashes, preservation proof, independent review and concrete reconciliation/publication handoff.

**Acceptance Criteria:**

- Given the full producer-shaped evidence, when imported through the real receipt caller, then valid evidence passes and each of the nine recorded gaps has an executed regression control.
- Given the user's starting checkout, when verification completes, then historical evidence, Story 6.1 draft, acceptance, pins, sprint, both HEADs and indexes retain their starting bytes.
- Given source-only hardening, when handed off, then outstanding canonical reconciliation, exact-source CI, authority wiring, publication and independent published qualification remain explicit gates.

## Implementation Notes

User authorization covers the existing dirty Projects draft and continuation through implementation; no sprint key is set. No intent gaps or irreversible actions. Parent captured starting tracked bytes. Required Aspire baseline builds but exits 134 on absent nested Tenants hosts; nested initialization is forbidden.

Completed all nine source-only hardening items. The new 26-path closure retains
seventeen recovered paths unchanged, hardens six and adds three Python files.
Final parent verification passed 249 tests plus nineteen optimized hardening
controls, whitespace checks and production-authority validation. Source hashes
remain equal before/after each final run. The successful post-review live Python
smoke uses identical runtime/fixture bytes; the last change only strengthened
coherent identity test mutations. All twenty classified review rows are resolved
without deferral.

The [evidence index](evidence/6-1-p1r-tooling-hardening-20261016/README.md),
[final verification](evidence/6-1-p1r-tooling-hardening-20261016/parent-verification-v4/verification.json),
[reviewed closure](evidence/6-1-p1r-tooling-hardening-20261016/reviewed-source-hardening.json)
and [publication handoff](evidence/6-1-p1r-tooling-hardening-20261016/publication-readiness-handoff.md)
retain exact bindings and outstanding canonical-source/authority/qualification
gates. Both repositories' HEADs, branches and index bytes are preserved; Projects
tracked bytes and all protected historical evidence remain unchanged. No commit,
push or publication was performed. `done` applies only to this tooling package;
P1R remains unusable and Story 6.1 remains blocked.

## Spec Change Log

## Review Triage Log

Three independent context-free reviewers examined `review-v1.diff`; all fourteen
findings received a verdict before grouping. Each routes to a direct patch with
focused regression controls. [Full findings and evidence](evidence/6-1-p1r-tooling-hardening-20261016/independent-review.md)
retain the reviewed diff binding and reproductions.

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| B1 | medium | Producer deliberately permits equal timestamps; strict comparisons reject valid runs. Patch ordering without accepting duplicate IDs. |
| B2 | medium | A 120-second start imports; include start in the existing deadline and remove slack. |
| B3 | medium | Actual role inspections can contradict safe creation options. Validate all observed role settings. |
| B4 | medium | Coordinated executable substitution imports. Bind fixed binary identities from unchanged pinned images. |
| B5 | medium | Root session can differ from PID despite a new-session launch. Enforce the actual relationship. |
| B6 | medium | Protected PIDs with altered ticks enter owned lists. Reject aliases by PID. |
| B7 | medium | A process reappears after established absence. Refuse contradictory second observations. |
| B8 | medium | Incomplete mount/glibc measurements import. Require populated measurements. |
| B9 | medium | Rejected Unix query credentials are retained. Delay endpoint retention until validation. |
| E1 | medium | Independent reproduction of B9; same patch. |
| E2 | medium | Independent confirmation of B1; same patch. |
| E3 | medium | Independent sixty-second startup reproduction; same patch as B2. |
| V1 | medium | Removing root/timed-cleanup production leaves tests passing. Test actual supervisor evidence through import. |
| V2 | medium | Independently rehashed overlong start imports; same patch as B2. |
| P1 | medium | Live Docker Desktop uses a translated physical bind source. Bind literal requested workspace, in-SDK source hashes and consistent nonsocket mount observations; test mapped-source compatibility. |
| B10 | medium | Coordinated noncanonical engine sources import; add lexical POSIX guards without resolving host engine paths locally. |
| B11 | medium | Real late-discovered registered processes are rejected despite successful removal; distinguish new identities from previously absent identities. |
| E4 | medium | Independent noncanonical engine-source reproduction; same patch as B10. |
| V3 | medium | Removing real Docker-version production leaves tests green; directly observe collector output and prove mutation sensitivity. |
| V4 | medium | New snapshots mask older identity negatives; update all identity references coherently and prove guard-regression sensitivity. Test-only patch. |

## Verification

Run each existing P1R Python suite individually, including any new suite; run a focused controller-finalization mutation control. Retain exact commands, test counts and source hashes before/after. Run `git diff --check` in both repositories, Projects `python3 tools/planning/validate_production_authority.py --story-id 6.1`, and the new preservation verifier. Package/runtime behavior is unchanged; no live successor qualification is authorized. Parent independently reviews/tests before final handoff.
