---
title: '9.1 Corrective-work authorization record and validator'
type: 'feature'
created: '2026-10-17'
status: 'done'
route: 'dispatch'
story_key: '9-1-corrective-work-authorization-record-and-validator'
review_loop_iteration: 1
planning_baseline: 'bb986902d3c1b2f3b1c209fe8eb0ffa0292153d9'
bootstrap_authorization: 'owner-approved-2026-10-17'
baseline_commit: 'bb986902d3c1b2f3b1c209fe8eb0ffa0292153d9'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** PRD §0 requires corrective-work authorization, but its record, validator, and CI controls do not exist.

**Approach:** Add a versioned contract, preflight/postflight validator, rejection fixtures, and blocking CI controls.

## Boundaries & Constraints

**Always:** Bind schema/version, failed gate, PRD digest, baseline commit, input subject, output derivation, owner/role registry, allowed paths/mutations, prohibited paths/actions, expected evidence/exact validator, issuance, expiry, and revocation. Require literal `corrective-work-only: true` and false readiness/release/deployment/production/migration/removal flags. Reject unknown fields/types.

Before development, record dated owner bootstrap approval covering Execution paths plus this spec, `epic-9-context.md`, and Epic 9/Story 9.1 entries in `sprint-status.yaml` under `_bmad-output/implementation-artifacts/`. Preserve actual approval identity/date/scope.

**Never:** Change planning approval digests, sealed artifacts, `.github/workflows/ci.yml`, `docs/ci.md`, dependencies, submodules, or runtime. Mint no live authorization/roster. Story 9.2 owns Assurance Control; 9.3 owns readiness reconciliation. No failed-gate or external-authority transition is granted.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior |
|---|---|---|
| Preflight | Valid record, trusted inputs, scoped diff | Exit 0; identify phase/authorization |
| Authority rejection | Missing/revoked/expired record, owner/receipt mismatch, broad scope, true authority flag | Exit nonzero with reason |
| Diff rejection | Baseline/input drift; prohibited path, mutation, or unsafe filesystem entry | Exit nonzero |
| Postflight | Expected evidence validates and derived subject matches recorded digest | Exit 0 with bound output digest |
| Drift | Changed source/evidence/validator/registry/scope/derivation | Reject; require new authorization |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/prd.md:112`, `epics.md` Story 9.1: contract/bootstrap.
- `tools/p1r_qualification.py:repository_binding`: Git inventory patterns; replace exclusions/deletion refusal.
- `tools/deployed_runtime_parity_handlers/v1.py`: receipt patterns; its roster is Story-3.15-only.
- `tools/validate-corrected-deployed-runtime-parity.py`: isolated/strict parsing patterns; preserve sealed code.
- `.github/workflows/p1r-qualification.yml`: pinned actions/unittest conventions.

## Tasks & Acceptance

**Execution:**

- [x] `tools/corrective-work-authorization.schema.json` — define closed authorization, registry-binding, postflight, canonicalization, and invalidation contracts.
- [x] `tools/validate-corrective-work-authorization.py` — implement the required `<record> --changed-paths-from <baseline-sha>` command, explicit postflight validation, machine-readable verdicts, and registry evaluation for CI.
- [x] `tools/tests/test_corrective_work_authorization.py` and `tools/tests/fixtures/corrective-work-authorization/` — exercise actual CLI in disposable Git repositories; retain positive controls, per-rejection negatives, and observed outcomes.
- [x] `.github/workflows/corrective-work-authorization.yml` — block on fixture failures for PR/main; publish retrievable live evaluations on every main push without blocking merges. Missing authority yields FAIL, never vacuous PASS.
- [x] This spec and `sprint-status.yaml` — retain bootstrap/review/verification and bounded lifecycle.

**Acceptance Criteria:**

- Given a record in the required gate/authorization directory, when preflight runs, then all PRD §0 bindings/restrictions are checked before handoff.
- Given completed work, when postflight runs, then actual diff/passing evidence bind the output digest; later mutation fails.
- Given checked-in negatives and positive controls, when tests run, then each negative fails for its intended reason and controls pass, including optimized Python.
- Given main CI, when controls run, then fixture failures block and published live failures preserve readiness/sealed inputs.

## Implementation Notes

- Bootstrap authorization, 2026-10-17: the interactive owner (workspace identity `Administrator`) explicitly answered `approve and continue` after reviewing this spec and its bootstrap scope. Authorized paths: `tools/validate-corrective-work-authorization.py`, `tools/corrective-work-authorization.schema.json`, `tools/tests/test_corrective_work_authorization.py`, `tools/tests/fixtures/corrective-work-authorization/`, `.github/workflows/corrective-work-authorization.yml`, this spec, `epic-9-context.md`, and only Epic 9/Story 9.1 entries in `sprint-status.yaml` under this directory. This is the Story 9.1 bootstrap exception, not a GitHub receipt or any live gate authorization. No external owner roster was minted.
- Implementation, 2026-10-17: v1 uses a closed record and baseline-bound owner registry with an explicit `authority_owner` numeric GitHub ID/login trust anchor. Registry and authorization approvals are exact canonical hash payloads retrieved from fixed read-only repository issue-comment endpoints. An authenticated `MEMBER` association is permitted only when that exact human identity matches the baseline trust anchor; association alone creates no authority. Historical Story 3.15 registry bytes and scope remain untouched.
- Operating constraint: expected evidence may use `sha256: null` for an explicitly scoped output, whose actual digest is bound by the first successful postflight. A future corrective lane must identify its evidence validator bytes before handoff (or use an existing baseline validator). An absent validator is permitted only as an explicitly scoped addition with its future exact SHA-256 preapproved. Unknown future executable bytes are never authorized.
- Postflight commands: add `--postflight --write-postflight` once to capture the derived subject and executed evidence results; later `--postflight` reexecutes and compares them. Only the fixed envelope fields `authorization_sha256` and `postflight` are excluded from the record's recursive digest. Committing identical final bytes preserves the subject; source, evidence, command/result, registry, scope or validator drift rejects it.
- Verification scope: synthetic authority transport exercises the actual isolated CLI and every identity/payload check in disposable Git repositories. It does not establish GitHub service availability or issue a production receipt. Fixture history messages passed the repository-pinned commitlint 21.1.0 before use; no workspace commit was created. No runtime, dependency, submodule, sealed artifact, `.github/workflows/ci.yml`, or `docs/ci.md` changes were made.

- Parent audit: read the complete baseline diff including untracked files using a temporary index (real index unchanged). All 12 paths match the bootstrap scope. Verified retained source/fixture SHA-256 pins. Matrix coverage: preflight uses `test_preflight_positive_control_across_git_layers`; authority/diff rejection uses all 81 cases in `test_checked_in_rejection_fixtures`; postflight uses `test_postflight_positive_control_and_commit_stability` and staged/deferred controls; drift uses the postflight/deferred negative fixtures. All covering methods ran in normal and optimized verification. Lifecycle remains in progress until independent review closes.

- Parent loop-1 audit: read the re-derived source/schema/test deltas and retained fixture/transport evidence against the complete prior diff. Source pins match. Matrix rows run in 14 methods and 115 negatives in both normal and optimized Python, now including Git-hidden content/modes, both shadows, baseline symlink, completion reset, pagination and malformed-registry aggregation. All reviewed implementation paths remain bootstrap-scoped. Independent re-review follows.

## Spec Change Log

- Review loop 1 (2026-10-17): multiple independent reviews exposed Git-filtered scope enforcement, omitted committed shadows, a baseline symlink incompatibility, mutually recursive record envelopes, and resettable postflight completion. Revised the non-frozen derivation/trust design below. Avoid accepting unauthorized bytes/modes, blocking valid baseline files, or rebinding later output under old authority. KEEP: isolated CLI/strict closed schema, numeric baseline registry issuer and authenticated read-only receipts, exact scopes and literal false authority flags, deferred evidence/preapproved validator bytes, actual CLI fixture transport, normal/-O controls, truthful nonblocking live workflow, commit stability and all existing negative controls. The prior implementation is retained only as a re-derivation reference at `/tmp/story-9-1-pre-review-r1/`; it is not a passing implementation.

## Review Triage Log

| Finding | Verdict | Evidence | Route |
|---|---|---|---|
| BH1 | high | The real baseline tracks ARCHITECTURE-SPINE.md as mode 120000 targeting ../../architecture.md; snapshot rejects all such entries, preventing actual use. | bad_spec |
| BH2 | high | Reproduced committed postflight reset: clearing only postflight and changing allowed source produces PASS with the same authority and a different output digest. | bad_spec |
| BH3 | high | Parent and reviewer reproduced PASS for unauthorized raw bytes hidden by assume-unchanged or skip-worktree; mutations are gated on Git diff output. | bad_spec |
| BH4 | high | Actual mode is inventoried, but core.filemode=false suppresses changed-path inclusion, allowing an unauthorized executable-bit change. | bad_spec |
| BH5 | medium | Snapshot retains current files and divergent index bytes only; a divergent HEAD blob can disappear from the output subject when index/worktree are restored. | bad_spec |
| BH6 | high | Each selected record normalizes only itself; another record postflight is hashed raw, so two completed records recursively invalidate one another. | bad_spec |
| BH7 | medium | Matching comments are counted before author verification; copying a real payload from an unrelated user can deny valid authority. | patch |
| BH8 | high | Parent reproduced PASS with prohibited src///; rstrip canonicalizes the check but matching uses the unnormalized original. | patch |
| BH9 | medium | Registry evaluation indexes an unvalidated root and catches no TypeError per record, so [] aborts evaluation and discards other record outcomes. | patch |
| BH10 | high | Authority is read before evidence; expiry/local drift are rechecked afterwards but a revocation published during evidence is not. | bad_spec |
| EC1 | high | Duplicate confirmed Git-hidden content/mode scope bypass; preserve its demonstrated cases. | bad_spec |
| EC2 | high | Duplicate confirmed postflight reset using a committed completed record; local null guard provides no durable first-output commitment. | bad_spec |
| EC3 | high | Fetched revocation newer than the pre-read now variable is ignored despite existing by verdict time. | patch |
| EC4 | high | Claim finding independently confirms preflight scope is not enforced for Git-hidden raw changes. | bad_spec |
| EC5 | high | Claim finding independently confirms later output can be rebound under the original approval by clearing the envelope. | bad_spec |
| VG1 | medium | Preverified gap: removing index_shadows leaves all existing controls green; an allowed-source staged-shadow regression needs a retained negative. | patch |
| VG2 | medium | Preverified gap: first-page early-return mutation leaves all controls green; page-two authenticated revocation needs a retained negative and paginated positive. | patch |
| VG3 | high | Other finding repeats the postflight reset; parent reproduced identical authority and changed subject after a committed completion. | bad_spec |


Second review (re-derived implementation):

| Finding | Verdict | Evidence | Route |
|---|---|---|---|
| R1-BH1 | medium | Fixed expected evidence is never read preflight; known SHA and missing file both reach PASS. Deferred null outputs have a separate explicit contract, so fixed inputs must be checked. | patch |
| R1-BH2 | low | Rebinding after remote deletion additionally requires the exact trusted owner to issue a fresh authenticated commitment for the new digest; old receipts alone never authorize it. This is deliberate trusted-authority administration, not an untrusted reset. Append-only protection against a trusted issuer rewriting its own service records would require a separate authority-history system; reject this rare low-impact extension as disproportionate. | rejected-low |
| R1-BH3 | high | Normalized record identities are reused for mutation detection; changing another unscoped record derived field is invisible. Scope must compare raw identities independently of recursive subject projection. | patch |
| R1-BH4 | high | A raw evidence path such as --help reaches argv as an option and can obtain argparse exit 0 without validation. Prefix the validated relative positional path with ./ and retain a rejecting option-name control. | patch |
| R1-BH5 | high | An ignored executable is not inventoried and can execute despite having no allowed-path entry; digest approval does not replace path/mutation scope. Reject ignored validator inputs. | patch |
| R1-BH6 | medium | Canonical src without slash is accepted as an exact prohibition but matches no children. Enforce path-or-descendant semantics for every canonical prohibited path. | patch |
| R1-BH7 | false | The prescribed single-record handoff evaluates its selected authorization and can pass after later work; the live whole-registry aggregate is explicitly a current-state diagnostic, never readiness or handoff authority. Story 9.3 owns closed/revoked/historical reconciliation. A truthful historical-row FAIL does not permanently block a later individual corrective lane. | rejected-false |
| R1-BH8 | medium | Parent reproduced 1 MiB nested arrays causing uncaught RecursionError, traceback, and empty stdout. Map decoder depth failures to JSON rejection and continue registry aggregation. | patch |
| R1-BH9 | low | JSON Schema patterns are substring-based while runtime fullmatches; direct anchored literals and exact SHA lengths align authoring tooling with the enforced contract. | patch |
| R1-BH10 | medium | Expected subject/core formulas reuse production helpers; independent checked-in known-answer vectors are missing for the versioned derivation contract. Add deterministic canonical/core/input/output/projection vectors. | patch |
| R1-EC1 | high | Uninitialized Gitlink directories are accepted based on superproject diff, which omits arbitrary files inside them. Reject nonempty uninitialized directories while preserving empty/missing and verified initialized Gitlinks. | patch |
| R1-EC2 | medium | Evidence argument control-character validation occurs only during execution; malformed approved commands pass handoff. Apply the same guard before handoff. | patch |
| R1-EC3 | low | Same trusted-owner deletion-and-replacement outcome as R1-BH2: a fresh exact-owner output approval is required. Retain the current-service trust boundary accurately; reject an append-only authority-system extension as rare/disproportionate. | rejected-low |
| R1-VG1 | medium | Preverified mutation evidence: removing file modes preserves all 14 methods/115 negatives; an authorized-mode postflight change needs an independent retained negative. | patch |
| R1-VG2 | medium | Preverified mutation evidence: dropping evidence arguments preserves all tests. Exercise nonempty arguments whose actual presence controls validator acceptance. | patch |

## Design Notes

Review-loop-1 requirements for re-derivation:

- Compute mutations from raw baseline/HEAD/index/worktree states, independent of Git diff filters, assume-unchanged, skip-worktree, and core.filemode. Bind divergent committed shadows as well as divergent index shadows, while preserving identical-byte commit stability. Bind unchanged safe tracked symlink target bytes without following them outside the repository; authority/evidence/executable inputs remain regular files. Reject new/changed unsafe symlinks. Include a positive shaped like the actual tracked ARCHITECTURE-SPINE.md link.
- Normalize the same two recursive envelope fields for every correctly located/schema-recognized corrective authorization in the registry inventory, not only the selected record. Bind every immutable core and unrelated file. Precreate all authorization cores before completion; prove two scoped records can both complete and registry evaluation can pass without recursive hashes.
- Local postflight is not a first-output trust anchor. Require an authenticated owner completion commitment retrieved from the same fixed approvals issue, binding authorization_sha256 plus the derived output_subject_sha256. At most one eligible completion commitment may exist for an authorization; a different output requires new authorization. A candidate command must emit the exact proposed payload/digest as non-authorizing FAIL when the commitment is missing, so a human can approve concrete output. Never post a receipt automatically. A cleared, absent, replaced, or committed/reset envelope must not bypass the existing external commitment, including through default preflight. No live completion receipt is created in this story; fixtures model this read-only protocol.
- Filter receipt candidates by the exact baseline-bound eligible human identity before counting duplicates/revocations. Ignore unrelated accounts copying authority payloads. Retain strict payload/endpoint/time checks for eligible receipts. Use current time when considering fetched revocations and recheck authenticated authority after evidence execution before the final local drift/expiry check.
- Validate canonical prohibited directory spelling (exactly one trailing slash); malformed forms reject. Registry evaluation reports every malformed record and continues to the rest. Add per-record structural error handling and malformed-input fixtures.
- Retain negatives for staged allowed-source shadow drift and page-two revocation, with a paginated positive. Every demonstrated new refusal requires a checked-in negative beside positive controls. Refresh verification evidence after re-derivation; historical results above do not establish the revised code passes.

Use standard-library Python, isolated imports, strict JSON, bounded subprocess argv, and no shell evaluation. Execute only approved digest-bound repository validators. Retrieve receipts read-only from fixed GitHub API endpoints; verify human numeric identity, role, unchanged timestamp, and approved payload. Registry bytes must match the baseline; reject a roster supplied by the corrective diff. Fixtures use isolated authority without production CLI bypasses.

Canonical subjects bind baseline, immutable authority inputs, changed paths/modes/raw bytes, and evidence. Cover committed/staged/unstaged/nonignored untracked changes; renames check both sides. Check Gitlinks without entering submodules. Reject traversal, broad globs, symlink escapes, prohibited mutations, and ignored evidence. Fix recursion exclusions to envelope digest/postflight fields in the versioned rule; no caller-selected exclusions. Detect concurrent input drift. Evidence retains exact command/outcome/digest; authorization alone cannot pass its gate.

## Verification

- `python3 -m unittest discover -s tools/tests -p 'test_corrective_work_authorization.py'` — positive controls pass and all checked-in negatives are observed rejected.
- `python3 -O -m unittest discover -s tools/tests -p 'test_corrective_work_authorization.py'` — enforcement survives disabled assertions.
- `actionlint .github/workflows/corrective-work-authorization.yml` — workflow is valid.
- `git diff --check` — no whitespace errors; inspect scope against bootstrap paths.

Implementation verification, 2026-10-17: normal Python passed 9 test methods in 14.106s; optimized Python passed the same 9 in 39.023s. Each run observed all 81 retained negatives rejected for its intended reason alongside passing controls. `actionlint` and `git diff --check` exited 0. Actual workspace `--evaluate-registry` exited 1 with `FAIL` / `missing-authority`, bound to the current source commit and validator/contract digests, with all external-authority flags false. Retained command outcomes and source pins are in `tools/tests/fixtures/corrective-work-authorization/verification.json`. Review and final lifecycle synchronization remain the parent workflow's next step.


Review-loop-1 re-derivation verification, 2026-10-17: the revised normal Python suite passed 14 test methods in 26.743s; optimized Python passed the same 14 in 76.896s. Both observed all 115 retained negatives rejected for their intended reasons. Positive controls include the actual architecture-link shape, unchanged uninitialized Gitlinks, paginated authority, unrelated copied receipts, deferred evidence/validator bytes, identical-byte commit stability, and two precreated authorization cores completing without recursive subject hashes. Registry evaluation reports all six retained malformed roots and continues to a passing record. Source and catalog pins and current command outcomes are refreshed in `tools/tests/fixtures/corrective-work-authorization/verification.json`; the earlier 9-method/81-negative results above are historical only.

The revised first-output trust anchor is an unedited, numeric-human-owner completion commitment read from the fixed approvals issue. Missing commitment produces non-authorizing `FAIL / missing-completion` with the exact canonical payload and digest; this story posts no receipt. Default preflight enforces an existing external commitment even when local postflight is null or has been committed/reset. Raw mutation derivation ignores Git change filters, binds divergent HEAD and index shadows, rejects hidden Gitlink flags, and normalizes the same two envelope fields for every correctly located/schema-recognized record. Safe tracked symlink target bytes are inventoried without following them; escaping, new or changed links reject. Authority is re-read after evidence execution before the final local drift/expiry check.

`actionlint .github/workflows/corrective-work-authorization.yml` and `git diff --check` exited 0. Actual workspace `--evaluate-registry` exited 1 with `FAIL / missing-authority`, exact source/validator/contract pins, and every external-authority flag false. A read-only raw inventory succeeded over 22,834 workspace entries including the real tracked architecture symlink; it also reported pre-existing raw-byte worktree differences hidden by Git normalization. Those existing bytes were preserved. No live authorization, owner registry, completion receipt, external gate transition, runtime/dependency/submodule change, or workspace commit was created. Lifecycle remains in progress pending the parent workflow's independent review.

Final parent verification and review closure, 2026-10-17: both independent review rounds are triaged above; every surviving finding is resolved, and no work is deferred. The final suite passed 18 methods in normal Python (28.962s) and optimized Python (82.391s), including all 129 retained rejection scenarios, seven malformed-registry cases, independent known-answer subject vectors, and argument-dependent evidence controls. The final patches enforce fixed evidence before handoff, raw authorization-envelope mutation scope, positional evidence paths, ignored-validator refusal, prohibited descendants, structured deep-JSON failure, safe Gitlink state, preflight argument validation, and exact schema formats. Normalized command subjects remain stable across interpreter paths while preserving every approved evidence argument and executable digest.

`actionlint .github/workflows/corrective-work-authorization.yml` and `git diff --check` exited 0 on the final implementation. Repeated live registry evaluation at source `9ed9b9742bd006e568f026cc1c588275cf9db097` truthfully exited 1 with `FAIL / missing-authority` and all six external-authority flags false. The intervening query-only commit was preserved; the approved planning baseline remains unchanged. Final command results, source pins, and successful exact-message commitlint 21.1.0 preflight are retained in the verification fixture. No live authority or sealed transition was created. The implementation spec is done and sprint Story 9.1 is in review; Epic 9 remains in progress.
