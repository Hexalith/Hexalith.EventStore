---
title: '9.1 Corrective-work authorization record and validator'
type: 'feature'
created: '2026-10-17'
status: 'in-progress'
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

### Review Findings

Code review 2026-10-18 of `80b0cd9c` (`9ed9b974..80b0cd9c`), code-first chunk: validator, schema, workflow, `authority_cli.py`, test module, fixture README, tracking/spec edits. The JSON fixtures (`rejections.json`, `observed-rejections.json`, `subject-vectors.json`, `verification.json`, `malformed-records.json`) were read as reference, not reviewed as a diff chunk. Layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor; none failed.

- [ ] [Review][Patch] (D1 resolved 2026-10-18 — owner: detect mutations against Git's text/eol clean conversion, keep raw bytes bound in the output subject, add an `eol=crlf` positive control) Raw-byte scope comparison treats Git checkout conversions as mutations, so no real preflight can PASS — `.gitattributes:40` sets `global.json text eol=crlf`; a fresh `--local` clone at `ab8fa115` with clean `git status` yields `snapshot(HEAD)` mutations `{'global.json': ['modify']}`, so every real record fails `out-of-scope` unless it authorizes modifying the sealed OQ8 input `global.json`. The maintainer workspace shows 287 such mutations (273 `i/lf w/crlf` files). `core.symlinks=false`/`core.filemode=false` checkouts behave the same way. Fixtures use attribute-free repositories, so every positive control is green by construction. Options: compare worktree bytes after Git's eol/text clean conversion for mutation detection while still binding raw bytes in the output subject; or require/repair a normalized checkout; or another rule. Violates I/O row "Preflight → exit 0" and ACs 1-2. [tools/validate-corrective-work-authorization.py:284]
- [ ] [Review][Patch] (D2 resolved 2026-10-18 — owner: drop the association gate, rely on the baseline-pinned numeric id/login, keep `type == User` and no-GitHub-App checks, remove dead `owner_only`) `author_association` allow-list rejects the sole owner's genuine receipts — `authenticated()` requires `OWNER`/`MEMBER`/`COLLABORATOR`, but `jpiquot` is a private Hexalith member (`orgs/Hexalith/public_members/jpiquot` → 404): the same comments read `MEMBER` with the user token and `CONTRIBUTOR` unauthenticated. A token that cannot see private membership (unauthenticated local runs; likely the Actions `GITHUB_TOKEN`) rejects every live registry/authorization/completion receipt. The x-contract says association creates no authority beyond the pinned numeric identity. Options: drop the association gate (and the dead `owner_only` parameter); make org membership public; or accept `CONTRIBUTOR`. [tools/validate-corrective-work-authorization.py:371]
- [ ] [Review][Patch] (D3 resolved 2026-10-18 — owner: add "Corrective authorization fixture controls" to the `main` ruleset required status checks; outward-facing settings change, executed only on explicit go-ahead) Fixture controls are not a blocking check — the `main` ruleset requires `advisory`, `ci / build-and-test`, `ci / tenants-source-mode`, `codeql / analyze`, `commitlint / commitlint`, `dependency-review / dependency-review`, `live-sidecar`; "Corrective authorization fixture controls" is absent, so a red fixture run cannot stop a merge (AC4; epic-9 "Required fixture jobs block merges"). Options: owner adds the job to the ruleset's required checks; or accept advisory status and renegotiate AC4. [.github/workflows/corrective-work-authorization.yml:14]
- [ ] [Review][Patch] (D4 resolved 2026-10-18 — owner: keep stdout/stderr binding and document that approved evidence validators must emit deterministic output) Evidence replay requires deterministic validator output, which is neither documented nor enforceable — postflight binds `stdout_sha256`/`stderr_sha256` and every later `--postflight`/registry evaluation re-executes and compares them; timing output (e.g. unittest "Ran N tests in Xs") or hash-order-dependent output makes a completed authorization fail `postflight` forever. `PYTHONHASHSEED` cannot be pinned because `-I` ignores `PYTHON*` variables. Options: document a deterministic-output requirement for approved validators; or bind only exit code plus file digests. [tools/validate-corrective-work-authorization.py:494]
- [ ] [Review][Patch] Hidden index flags inside an initialized submodule bypass scope — on the unmodified validator, `git -C vendor/linked update-index --assume-unchanged|--skip-worktree tracked.txt` plus an overwrite PASSes preflight (exit 0), while a plain dirty edit fails `gitlink`. Apply the superproject hidden-flag rule to initialized Gitlinks' own index via their verified gitdir. [tools/validate-corrective-work-authorization.py:262]
- [ ] [Review][Patch] Many refusal branches survive deletion with all 18 tests green (contradicts "every rejection path needs a checked-in negative") — add negatives for: registry file with zero records (mutant gives vacuous registry PASS, V:603); initialized-Gitlink dirty content, HEAD drift and foreign gitdir (V:264/217/215); new in-repo symlink (V:295); registry/validator/contract path pins (V:509-510; mutant lets a collaborator's baseline JSON act as registry); receipt time ordering, identity URLs, reused ids and duplicate registry approvals (V:373-374/414/416/424/439/440); redirect/proxy opener construction (V:336-344, mocked away by `authority_cli.py`); envelope normalization location (V:180); evidence env scrub (V:490); baseline ancestry (V:507); duplicate/self evidence (V:524/528); running-script identity (V:513); authority-drift comparison (V:555); record/contract drift (V:557-558); unexpected registry entry (V:601). The size, page and usage bounds (V:76/358/559/632) are low value. [tools/tests/fixtures/corrective-work-authorization/rejections.json]
- [ ] [Review][Patch] Negatives assert only coarse reason codes, so some fail for the wrong reason — `registry-duplicate-human` appends an identical dict and stops at schema `uniqueItems`; the runtime duplicate-identity guard (same id/different login, casefolded login) is never reached, and Story 9.2 counts humans from this registry. `passed-gate` stops at the schema `const`; `wrong-human` is filtered before `authenticated()`. Add targeted negatives and assert a distinguishing detail where reason codes are shared. [tools/tests/test_corrective_work_authorization.py:297]
- [ ] [Review][Patch] Live workflow publishes an unvalidated verdict — with `continue-on-error`, a crash leaves an empty or partial `$RUNNER_TEMP/corrective-work-authorization.json` that `if-no-files-found: error` still uploads as "content-bound"; run `39104648870` concluded `success` while its artifact says `FAIL / missing-authority`, and there is no step summary. Validate that the verdict parses (fail the job only on an invalid verdict) and write PASS/FAIL plus the reason to `$GITHUB_STEP_SUMMARY`. [.github/workflows/corrective-work-authorization.yml:46]
- [ ] [Review][Patch] Unmapped exceptions emit tracebacks instead of a FAIL verdict — `http.client.HTTPException` (`IncompleteRead`/`BadStatusLine`) from `comments()`; `subprocess.TimeoutExpired` inside the per-record registry loop (replaces every outcome with one FAIL); `ValueError` from the integer-digit limit in `load()`; `RuntimeError` from symlink-loop `resolve()` on Python ≤ 3.12 (reproduced on 3.11; CI runs 3.12); `UnicodeEncodeError` printing a lone-surrogate detail. Map every failure to a structured FAIL in `main()` and the per-record loop, and print with ASCII escaping. [tools/validate-corrective-work-authorization.py:637]
- [ ] [Review][Patch] Windows re-exec reports exit 0 before validation — `nt.execv` spawns a detached child and the parent exits 0 (CPython bpo-19124), so an exit-code consumer sees success for any verdict. On win32, use builtin `nt.spawnv(P_WAIT, …)` and exit with the child's status. [tools/validate-corrective-work-authorization.py:13]
- [ ] [Review][Patch] Registry evaluation aborts on a stray directory entry instead of reporting it and continuing — the `gate_dir.is_dir()` and `.json` file checks sit outside the per-record `try`, so a `README.md`, `.gitkeep` or `.json.bak` replaces all per-record outcomes with one top-level FAIL (Design Notes: "reports every malformed record and continues"). [tools/validate-corrective-work-authorization.py:595]
- [ ] [Review][Patch] Worktree executable bit uses `0o111` instead of Git's owner-execute bit — a file with only group/other execute bits gets a spurious `mode` mutation; use `stat.S_IXUSR`. [tools/validate-corrective-work-authorization.py:285]
- [ ] [Review][Patch] The schema accepts records the runtime rejects — `prohibited_actions` allows any order, but `validate_scope` requires the exact `ACTIONS` order (pin it with `const`); a 511-512-character evidence path makes the recorded `./`+path command element exceed `evidence_result.command` `maxLength` 512, so a written postflight fails the schema forever. [tools/corrective-work-authorization.schema.json]
- [ ] [Review][Patch] Undocumented requirement that each record list its own path in `scope.allowed` with `add`/`modify`; otherwise it fails `out-of-scope`. State this in the x-contract/README. [tools/tests/fixtures/corrective-work-authorization/README.md]
- [ ] [Review][Patch] `sprint-status.yaml` `last_updated` was edited outside the bootstrap scope and into a new format (`'2026-10-17'`); the tracker tool writes `%m-%d-%Y %H:%M`. [_bmad-output/implementation-artifacts/sprint-status.yaml:59]
- [x] [Review][Defer] No story owns minting the first live owner-role registry and its authority/approvals issues [_bmad-output/planning-artifacts/epics.md:7686] — deferred: planning gap, not a code defect. Story 9.3 depends on a 9.1 authorization record for G-BASELINE, and live evaluation stays `missing-authority` until a baseline-committed registry and issue numbers exist; the fix edits epics/planning (owner action or correct-course).

**Rejected:**

- `low` — file↔directory type transitions fail as `filesystem`: fail-closed, rare in corrective work, fix adds branches.
- `low` — an untracked nested repository fails with a misleading `noncanonical path` reason: fail-closed, none in the workspace.
- `low` — comment floods (> 4 MiB page or > 1,000 comments) deny authority: unlikely; operational mitigation is locking the two authority issues.
- `low` — an approved validator could load mutable helper modules: the owner approves the validator bytes and scopes any helper edit explicitly.
- `low` — validator swap between digest check and execution: needs a concurrent local attacker who already controls the process; CI fresh-checkout re-evaluation is the trust boundary.
- `false` — `GIT_*` environment overrides substitute scope: the baseline→HEAD→index→raw-worktree chain still exposes any difference from the baseline blob, and a local runner can always choose which checkout to evaluate.
- `false` — `--help` exits 0: requesting help is not a validation; unknown options exit 2 (fail-closed).
- `false` — "enforcement is voluntary" because no job validates PR diffs: the spec requires blocking fixture controls plus live publication, and handoff preflight is invoked by the handoff procedure.
- `low` — live evaluation runs only on push to main: the spec prescribes push; handoff mode re-reads authority live; no live authority exists yet.
- `low` — registry-issuer/owner separation is untested and only the owner can revoke: the roster has one human, so issuer equals owner, and deleting a registry approval withdraws it.
- `low` — no record-authoring tool, and missing approvals return no proposed payload: outside the spec's scope; fix adds surface.
- Rejected (fix edits the spec under review) — the live aggregate cannot tell historical records from violations: already adjudicated as R1-BH7; it is a current-state diagnostic and Story 9.3 owns reconciliation.
- Rejected (fix edits the spec under review) — the spec's lifecycle narrative has stacked verification counts, `review_loop_iteration`, a `/tmp` reference and `actionlint` that is not in the workflow.
- `low` — `feat(governance)` type on tooling-only commit `80b0cd9c`: already on `origin/main`; `db23b193 feat(queries)` already makes the next release minor, so only the CHANGELOG category is affected; no history rewrite.
- `low` — glob characters (`*?[]{}!`) in any tracked/untracked name fail every evaluation with `scope`: zero such paths today; fix restructures path validation.
- `low` — tests use `mkfifo`/`symlink_to`/`chmod` without platform skips: CI is Linux-only.

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
