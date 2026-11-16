---
title: '3.19 Publication authority record and exact-subject validator'
type: 'feature'
created: '2026-11-16'
status: 'done'
route: 'dispatch'
story_key: '3-19-publication-authority-record-and-canonical-production-profile'
review_loop_iteration: 0
baseline_commit: '46936bf3d1091736762e5ed4bd02f721df8d8504'
planning_baseline: '46936bf3d1091736762e5ed4bd02f721df8d8504'
context:
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
  - '{project-root}/references/Hexalith.EventStore/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Profile construction is implemented, but 3.19 lacks its Publication Authority Record contract and lifecycle validator.

**Approach:** Build the schema, chain validator and assurance integration using the existing profile implementation. Missing prerequisites refuse; this construction does not complete the parent story.

## Boundaries & Constraints

**Always:** Work in `references/Hexalith.EventStore`; paths below are owner-relative. Enforce `built → evidence-candidate-published → evidence-validated → release-available → production-promoted`. Bind schema/version, immutable subject/source, package/OCI, candidate, validator/result/receipt identities, predecessor digest, issuer/authentication evidence, registry, outcome, validity and assurance. Promotion also binds environment, complete resolved profile and rendered deployment. Separate EventStore package and Platform composed-host subjects; changes restart at `built`.

Reuse 9.2's authenticated owner-written GitHub comments, numeric role registry, push-based 24-hour window and separate sealed-run consumption. Missing fixed prerequisite verifiers refuse. Beyond `evidence-validated`, require the Builds-owned `ReleaseEvidenceCodec` at the bound publisher/gitlink identity; absent or unverified codec always refuses. Do not invent its future API or substitute `ReleaseRecordCodec`.

**Never:** Issue records, post comments, publish the production profile, select targets, deploy, change GitHub settings/pins, or relabel historical subjects. Do not rebuild deleted 9.1/parity-packet machinery, duplicate profile/release codecs, introduce caller-executable adapters or accept pass flags as proof. Preserve approved Platform preparation bytes, sealed CI inputs and existing story status. Fixtures grant no authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Structural control | Complete synthetic linked records | Schema/chain validity only | No authority claim |
| Broken chain | Skips, duplicates, unknown states, wrong predecessor/subject | Refuse | Stable safe codes |
| Invalid authority | Wrong role, edited/missing comment, expiry, revocation, invalidation, overstated assurance | Refuse | Recheck on consumption |
| Wrong profile | Absent/noncanonical/under-declared slot, alternate file or digest-domain substitution | Refuse | Use owner codec and external coverage |
| Missing prerequisite | Missing/mismatched Builds codec, qualification, ratification, issuer approval or sealed predecessor | No release/promotion | Named cause; no inherited authority |
| Changed consumption | Head, workflow, bound inputs or deployment changed | Reject previous result | Subject change requires new chain |

</frozen-after-approval>

## Code Map

- `docs/architecture/production-profile-v1.md`, `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfile{Codec,Validator,ValidationContext}.cs` — structural contract; preserve both digest domains.
- `_bmad-output/planning-artifacts/{architecture,epics}.md` — AD-11/12/26, 3.19 and evidence-storage supersession.
- `tools/phase_4_transition.py`, `tools/phase-4-transition.schema.json`, `tools/validate-phase-4-gate-risk-matrix.py` — authentication, bounded execution, request binding and registry.

## Tasks & Acceptance

**Execution, ordered:**

- [x] `docs/architecture/publication-authority-v1.md`, `tools/publication-authority.schema.json` — specify closed records, canonical record-digest preimage, transition/role rules, separate subjects, lifecycle invalidation and structural-versus-authority results. Retain candidate references as identities; no committed proof packets.
- [x] `tools/production-profile.cs` — add a bounded read-only bridge to the Contracts validator, requiring separately bound composing-inventory coverage; no alternative canonicalization.
- [x] `tools/publication_authority.py`, `tools/validate-publication-authority.py` — implement schema/chain and fixed prerequisite checks with bounded inputs and safe 0/1/2 results. Validate the sole profile slot. Resolve publisher/gitlink identities from trusted repository inputs. Unavailable codec/proof verifiers refuse without a success override.
- [x] `tools/phase_4_transition.py`, `tools/phase-4-transition.schema.json` — integrate the fixed lifecycle business adapter and bind its code/schema/profile/inventory/record inputs. Reuse authenticated approval and consumption; retain unavailable business gates as failures, including G-RUNTIME-PARITY. Include exact 3.21, 5.7, 5.13, 7.22, AD-26, extension/issuer and assurance prerequisites.
- [x] `tools/tests/test_publication_authority.py`, `tools/tests/test_phase_4_transition.py`, `tests/Hexalith.EventStore.Contracts.Tests/Deployment/ProductionProfileTests.cs` — cover every matrix row beside positive structural and authenticated-transport controls; verify the genuine owner profile implementation and unchanged digest vectors. Fixture substitution cannot enable authority in production.
- [x] `.github/workflows/publication-authority.yml`, `.github/workflows/phase-4-transition-fixtures.yml` and the dedicated release/promotion workflows — add fixture checks and a non-promoting main evaluation reporting the real missing inputs. Prepare required fixture wiring without settings changes; keep seals confined to dedicated attempts. CI uses package/Release assets.
- [x] This spec and `_bmad-output/implementation-artifacts/sprint-status.yaml` — record commands/counts/full revision and available CI links; keep parent 3.19 in progress and historical 3.15/9.2 conclusions intact.

**Acceptance Criteria:**

- Given repository-only inputs today, when live evaluation runs, then it reports actual missing prerequisites and cannot establish release or promotion.
- Given structural success, when an authority consumer runs, then authenticated roles, exact subjects, prerequisite reruns and sealed consumption remain mandatory.
- Given a Platform-composed host, when evaluated with EventStore package receipts, then it cannot inherit the package subject's lifecycle.

## Implementation Notes

- Closed synthetic record chains are structural declarations only. Record digests and
  comment-claim digests have separate versioned preimages; the latter removes the two
  authentication-body hashes and the not-yet-created comment ID to avoid issuance
  cycles. The full record digest retains all three values.
- The lifecycle adapter reuses 9.2's authenticated transport, numeric registry,
  push-based window and separately sealed consumption. Required profile, inventory,
  source composition, records, owner implementation and rendered inputs also belong
  to the lifecycle subject so evidence-only bindings cannot bypass that window.
- The read-only bridge delegates canonical-byte validation and both digest domains
  to Contracts. No production profile, inventory, authority record or proof packet
  is published. The separate composing-inventory contract is documented in
  `../../docs/architecture/publication-authority-v1.md`.
- Builds publisher and gitlink revisions are bound independently from trusted
  workflow/Git inputs. Different whole commits are not assumed to imply different
  codec implementations. No approved `ReleaseEvidenceCodec` integration exists;
  its absence and every missing fixed proof verifier remain named refusals.
- Main evaluations report a retrievable truthful FAIL with no seal. Dedicated
  release/promotion workflows retain the existing authenticated consume protocol;
  no GitHub settings, publisher pins or historical authority subjects were changed.

## Spec Change Log

## Review Triage Log

Review pass 1 (2026-11-16): all three layers completed before triage. Direct CLI
probes reproduced BH1/BH2/BH5/BH6 in private scratch. Source tracing confirmed
remaining implementation findings; VG findings include reviewer-run mutation
proofs. No current record can confer authority because fixed prerequisites refuse.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH1: output traversal | medium | patch | An outside/../repository result path creates the protected profile and exits 0. Normalize before containment and retain symlink refusal. |
| BH2: unchecked child result | medium | patch | A no-op executable selected as the installed SDK returns 0; profile_check then accepts invalid {} bytes without an owner result. Require the closed structured owner result and matching digests. The existing installed toolchain remains a trusted dependency; no new binary-attestation service is implied. |
| BH3: incomplete executable-input closure | medium | patch | Contracts compiles sources beyond the bound ProductionProfile prefix and imports Builds. The complete executable closure is unverified. Refuse authenticated evaluation before launching that build while this fixed proof is unavailable; retain structural bridge results as unevaluated. |
| BH4: release/profile associations | medium | patch | The final association block compares only environment/rendered/OCI fields. Different package/server, extension, catalog-content or runtime-image identities can pass this preflight. Check those existing identity fields against the owner-decoded release. |
| BH5: malformed live exit status | medium | patch | Duplicate-key records produce SCHEMA_INVALID but exit 1; the documented malformed-input status is 2. Preserve collected refusal status. |
| BH6: symbolic input parent | medium | patch | Structural mode reads link/records.json and returns PASS because read checks only below its supplied symlinked root. Validate root ancestors too. |
| BH7: live job toolchain setup | medium | patch | The new live job lacks the owner SDK and pinned Builds setup needed by its genuine bridge once inputs exist; mirror the existing fixed setup. |
| BH8: fixture tied to absent production files | medium | patch | The required suite asserts actual repository files are absent; legitimate supplied inputs would fail the fixture job before its transition. Isolate missing-input assertions and retain a non-authorizing real smoke check. |
| BH9: fully bound lifecycle coverage | medium | patch | The existing transport test stops at missing bindings and checks only the outer refusal. Add a fully bound/authenticated control reaching the explicit fixed prerequisite and individual binding mutations. |
| BH10: lost epic-context guardrails | medium | defer | Context regeneration removed the explicit sealed/original-input preservation and separate-reseal boundary. Current planning treats the 1.19.1 refresh as historical, so it must not be restored as new target authority. Context-file corrections route to defer under this workflow. |
| EH1: output traversal | medium | patch | Independently reproduced the same protected profile creation as BH1; same root cause. |
| EH2: incomplete executable-input closure | medium | patch | The Contracts project and Builds imports execute inputs omitted from current exact bindings; same root cause as BH3. No complete build-input proof exists, so authenticated construction must refuse before execution. |
| EH3: deleted context constraints | medium | defer | Same deletion as BH10; future 3.21 context consumers lose the explicit reseal boundary. Agent-context changes route to defer. |
| VG1: wrong semantic approval claim | medium | patch | Pre-verified: removing semantic comment equality leaves all 16 tests green; hash-consistent approval of another claim is untested. Add direct negative controls. |
| VG2: promotion association coverage | medium | patch | Pre-verified: removing the final association block leaves publication and lifecycle tests green because bridge checks pass record=None. Add a genuine complete promotion and independent association mutations. |
| VG3: actual caller window coverage | medium | patch | Pre-verified: removing Control.subject_at delegation leaves tests green. Exercise the real caller with receipt-only and authored-claim histories and retain raw seal binding. |

Survivors group by shared roots: BH1/EH1; BH3/EH2; BH10/EH3. All
other findings retain separate roots. The grouped code corrections are direct
checks/test corrections on demonstrated inputs, add no public request/schema
surface, and route to patch. Full build-input qualification remains an explicit
unavailable prerequisite, consistent with the frozen fail-closed construction.

## Verification

- Use `run-with-temp`; copy owner/root-declared sibling sources and outputs under `$TMPDIR`. Preserve logs outside scratch; await children.
- In scratch: `python3 -B -m unittest discover -s tools/tests -p 'test_publication_authority.py'` and the existing transition, gate-decision, story-completion and matrix suites. Require nonzero passing counts and observed negative refusals; no network mutation.
- Build Contracts tests and the profile bridge in Debug with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 --disable-build-servers`; invoke the built xUnit assembly with `-class '*ProductionProfileTests'`, then run Contracts regressions. Preserve existing unrelated blockers explicitly.
- Assert the evaluator's named truthful FAIL; check workflow syntax and `git diff --check`. Local tests provide no CI or qualification claim.

### Local implementation verification (2026-11-16)

- Full owner revision: `46936bf3d1091736762e5ed4bd02f721df8d8504` plus this
  uncommitted implementation. No CI execution or qualification is claimed; CI links
  are unavailable until the owner runs the prepared workflows.
- All Python jobs used `run-with-temp`; their fixtures and bounded child workspaces
  were private and cleaned after completion. Full output is retained locally under
  `/home/administrator/.cache/codex-verification/publication-authority-20261116-v7_pk387/` outside disposable scratch and the repository.
- `python3 -B -m unittest discover -s tools/tests -p 'test_publication_authority.py'`:
  **16 passed** (final rerun 0.398s), including observed CLI 0/1/2 controls, both
  subject kinds, authenticated comment refusals, receipt attachment and historical
  predecessor-window controls, fake-codec refusal and the real repository FAIL.
- `python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_transition.py'`:
  **41 passed** (637.891s), including genuine OQ8/authenticated transport controls,
  actual fixed lifecycle binding refusal and unchanged runtime-parity refusal.
- `python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_decision.py'`:
  **16 passed** (125.833s).
- `python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_story_completion.py'`:
  **32 passed** (35.775s). Its schema-boundary assertions now preserve the 215-input
  story and 209-input ordinary limits beside the separate lifecycle allowance.
- `python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_risk_matrix.py'`:
  **147 passed** (15.512s). Total across the five suites: **252 passed**.
- `python3 -B tools/validate-publication-authority.py evaluate`: observed exit **1**,
  truthful `FAIL`, `seal: null`, named missing profile/records/composing coverage,
  unavailable Builds codec and each fixed prerequisite verifier. Trusted publisher
  and gitlink identities were resolved independently from the immutable Git tree.
- `actionlint` on publication-authority, transition-fixtures, release-available and
  production-promoted workflows: **passed**. `git diff --check`: **passed**.
- Outer Debug Contracts verification succeeded with zero warnings/errors, **130/130**
  focused profile cases passing, and **1,704** Contracts cases with zero failures and
  two existing package-inventory skips. Broad Contracts checks were not repeated for
  the subsequent bridge-only correction. Historical 3.15/9.2 conclusions and parent
  3.19's in-progress status are preserved.

### Bridge integration correction (2026-11-16)

- Fixed controlled refusal for `InvalidDataException`, including malformed,
  self-derived and wrong-schema inventory inputs, with no stack trace or input echo.
- The real fixed child resolves the installed `dotnet` executable before environment
  filtering, provides its runtime root and an existing package cache, copies the
  owner's exact `global.json` into scratch, and forces build-server reuse off.
  SDK/project/NuGet/build inputs now belong to exact transition control bindings.
- File-based SDK 11 builds use `-p:BuildInParallel=false`; `-m:1` remains on ordinary
  project builds, where it is supported.
- Added `python3 -B tools/tests/check_production_profile_bridge.py` to the required
  publication fixture job. It copies owner/root-declared sources into disposable
  workspaces and builds the genuine bridge in Debug/source mode locally or
  Release/package mode in CI. Its **12/12** cases passed: structural success,
  noncanonical bytes, under-declared coverage, wrong digest domain, absent sole slot,
  self-derived source, wrong/duplicate/malformed inventory without input echo,
  genuine fixed Python-child success/refusal, and refusal of an unavailable owner
  SDK with roll-forward disabled. Fixture controls confer no authority.
- Affected Python reruns: publication authority **16 passed** (0.455s); focused
  lifecycle control/input binding **1 passed** (24.066s). `actionlint` and
  `git diff --check` passed. Complete output is retained beside the earlier logs as
  `bridge-integration.log`, `publication-bridge-fix-python.log` and
  `publication-bridge-fix-transition.log`.

### Outer task and matrix audit (2026-11-16)

- Read the full baseline diff, including untracked files, through a disposable private
  Git index; preserved the real index and all pre-existing work. Every execution task
  and acceptance criterion is implemented. The frozen intent remains unchanged.
- Structural-control and broken-chain rows: `test_complete_synthetic_chain_and_all_prefixes_grant_only_structural_validity`,
  `test_skips_duplicates_unknown_states_and_wrong_predecessors_refuse`, subject/source
  mutation and Platform receipt-separation tests.
- Invalid-authority row: role/expiry/revocation/invalidation/assurance tests plus
  authenticated-comment and per-record historical-window controls.
- Wrong-profile row: all 12 genuine bridge cases, the 130 executed profile cases and
  independent canonical byte/digest vectors; no new case skipped.
- Missing-prerequisite row: fake/missing codec, all fixed verifier codes, real live
  evaluation and explicit `BUILDS_RECORD_IDENTITY_MISMATCH` assertion.
- Changed-consumption row: raw-byte receipt binding and deployment identity tests,
  plus shared transition head/workflow/input/consumption rejection controls in the
  passing 41-case suite. All matrix rows have executed passing coverage.
- Final publication audit rerun: 16/16 passed (0.489s), retained as
  `publication-audit.log` beside the other verification logs. The full Contracts
  suite executed 1,704 cases: 1,702 passed and two known release-package inventory
  skips; zero failures. No remote CI or issue-step qualification is claimed.

### Review resolution and independent final verification (2026-11-16)

- All 14 code/test findings were addressed and independently checked. BH10/EH3
  share one deferred agent-context correction recorded in the Platform
  `deferred-work.md`; no frozen intent or historical authority record changed.
- The authenticated adapter now reports `PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE`
  before any .NET launch until complete execution-input proof is available.
  Standalone profile checks remain structural and non-authorizing.
- Independent final commands, each under `run-with-temp`: publication suite
  **23/23 passed** (0.726s); the three actual lifecycle binding/history/transport
  tests **3/3 passed** (24.711s); genuine bridge/child integration **23/23 passed**.
  All four edited workflows pass `actionlint`; `git diff --check` passes. Logs:
  `final-publication.log`, `final-transition.log`, `final-bridge.log`,
  `final-actionlint.log`, `final-diff-check.log` in the retained verification directory.
- Earlier broad verification remains applicable to the unchanged Contracts
  implementation: 252 Python executions across the five original suites;
  **130/130** focused profile cases; **1,704** full Contracts cases with
  **1,702 passed, two known package-inventory skips, zero failures**; independent
  canonical vectors and both digests passed. Targeted checks were repeated for
  the reviewed changes; unaffected broad suites were not repeated.
- This construction spec is complete. Parent Story 3.19 stays **in-progress**:
  full build-input proof, qualified inventory, codec integration, ratification
  and authenticated authority issuance remain outstanding. No CI run, remote
  publication, accepted production profile or release/promotion authority is claimed.
