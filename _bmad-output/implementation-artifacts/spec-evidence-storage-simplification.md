---
title: 'Simplify evidence: story file + CI only, remove committed evidence and its machinery'
type: 'chore'
created: '2026-10-23'
status: 'done'
baseline_commit: 'b7ba9bb1fdff85fb47817d35506bd6ca81b23d70'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Committed evidence (`_bmad-output/implementation-artifacts/evidence/`, 56,916 files, 7.4 GB; `docs/evidence/`) dominates checkout size and Git history. About 30k lines of validators, seals and closure tests exist only to re-verify it. The 2026-10-21 Story 3.22/3.23 plan (external store, manifests, materialization lanes) adds more machinery instead of removing it.

**Approach:** Adopt one rule: proof = the repo's tests + CI; a story/spec records the commands run, pass/fail counts, commit SHA and CI run link. `_bmad-output/**/evidence/` and `docs/evidence/` become ignored local scratch. A pre-commit hook and a CI step reject any tracked/staged path under those folders and any file over 1 MiB (small explicit allowlist). Delete all committed evidence and the tools, tests, CI steps and docs that only produce or re-verify it.

**Owner decisions (2026-10-23):** story-file-only policy; remove all evidence + its machinery; rewrite `main` history afterwards as a separate, explicitly confirmed step; FrontComposer gets the same treatment later as its own change. Supersedes Stories 3.22/3.23 and the architecture/PRD "Evidence storage (approved 2026-10-21)" paragraphs.

## Boundaries & Constraints

**Always:** Work in the isolated worktree `../es-evidence-simplify` on branch `fix/evidence-storage-simplification` (a concurrent session commits to `main`). Keep runtime tests that write captures only to temp/env dirs. Keep CI artifact uploads (GitHub artifacts are fine). Keep required status-check job names (`advisory`, `ci / build-and-test`, `ci / tenants-source-mode`, `live-sidecar`, …). Conventional Commits validated with the repo commitlint; tooling-only commit types (`build`/`ci`/`test`/`docs`) so no release is triggered.

**Never:** No history rewrite, force-push, tag change or GitHub ruleset change in this spec. No edits to FrontComposer or any other submodule, or to the shared `AGENTS.md`/`CLAUDE.md`/copilot baseline. No change to `src/` behaviour. Not in scope (kept): `tools/ext_protection_1*` (writes only to an external `--evidence-root`), `tools/qualify-internal-actor-admission.py` (live HTTP/Dapr/Redis regression), `tools/bounded_worker_observer.py`, Dapr broker qualification tooling, and PRD gate redesign beyond one supersession sentence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Normal commit | staged small source/doc files | hook passes | N/A |
| Evidence path | staged `_bmad-output/x/evidence/a.json` or `docs/evidence/a.md` (even via `git add -f`) | hook/CI fail, path listed | exit 1 with fix hint |
| Large file | staged/tracked file > 1,048,576 bytes not allowlisted | fail, path + size listed | exit 1 |
| Allowlisted large file | `docs/assets/quickstart-demo.gif`, `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` | pass | N/A |
| Gitlink / deletion | submodule pointer bump, or a deleted file | ignored, pass | N/A |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/evidence/`, `docs/evidence/`, `…/6-5-integration/`, `…/6-5d-simplification/`, `…/1-20-owner-approved-parity-closure-proof-packet.md` -- committed evidence; already `git rm --cached` in the worktree index (56,993 paths).
- `deploy/dapr/qualification/runtime-lock.json` L33-89, L122-145 -- points at `evidence/story-3-21/qualification-2026-10-20/public-identities/*.manifest.json`; read by `tools/qualify-dapr-brokers.py:69-74`, `tools/dapr_broker_qualification.py:65-70`, `tools/tests/test_dapr_broker_qualification.py:22-28`, `tests/…IntegrationTests/Fixtures/DurableBrokerQualificationFixture.cs:25`. Relocate those files; recompute self-referencing sha256 in `inputs`.
- Delete-tools: OQ8 (`tools/validate-oq8-platform-evidence.py`, `tools/oq8-v5-packet.*`, `tools/prepare-oq8-v5-candidate.py`, `scripts/verify-oq8-v5-candidate.sh`, `requirements-oq8.txt`); P1R (`tools/p1r[-_]*`, `tools/p1r-published-consumers/`, `tools/tests/test_p1r_*`, `tools/tests/p1r_isolation_fixture.py`, `tools/tests/fixtures/p1r-source-proof/`); release evidence (`tools/release_evidence_codec.py`, `tools/release_evidence_handlers/`, `tools/validate-corrective-release-evidence.py`); `tools/deployed_runtime_parity_handlers/` + `tools/{assemble,capture,validate}-corrected-deployed-runtime-parity*.py`; corrective work (`tools/validate-corrective-work-authorization.py`, `tools/corrective-work-authorization.schema.json`, test + fixtures); ES-DIAG (`tools/validate-es-diag-capture.py` + test); `tools/evidence-provider-adapters/`; `tools/qualify-bounded-owner-identity.py`; operational evidence (`scripts/validate-operational-evidence.py`, `scripts/validate-evidence.{sh,ps1}`, `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/` + `Hexalith.EventStore.slnx:60`, `scripts/validate-docs.sh:64-69`, `scripts/validate-docs.ps1:50-52`).
- Delete-tests (`tests/Hexalith.EventStore.Contracts.Tests/Packaging/`): `ContractsPackageDependencyTests.{FrozenEvidence,InheritedOwnerEvidence}.cs`, `DeployedRuntimeParityClosureTests.cs`, `CorrectedDeployedRuntimeParity{Closure,SmokeCapture}Tests.cs`, `Oq8PlatformClosureTests.cs`, `Oq8V5CandidateTests.cs`, `Fixtures/oq8-v5-synthetic-fixture.py`, `EvidenceProviderAdapterTests.cs`, `FrozenStory120EvidenceIntegrityRepairTests.cs`, `FrontComposerRuntimeIdentitySuccessorTests.cs`, `ProofPacket{ValidatorIntegrity,DaprConflictProcessContract}Tests.cs`; `tests/…Server.Tests/Security/SecretsProtectionTests.{Story64Retirement,EvidenceIngestion,EvidenceSemanticIngestion,Reconciliation,BoundedEvidence}.cs`; `tests/…Admin.Server.Tests/Evidence/Dw2EvidenceIndexAtddTests.cs`.
- Trim-tests (keep the real regression, drop evidence cases/helpers): `ContractsPackageDependencyTests.cs`, `SecretsProtectionTests.cs` (+ `HistoricalLines`, `XunitEvidence`, `NuGetEvidence`, `SourceContexts{,.Tests}` partials; move `WithEvidenceRepository`/`WriteEvidenceFile`/`EncodeEvidenceText` used by `ScanPerformance.cs`; remove `IsRetiredEvidenceCapture` which reads a deleted file), `CorrectiveOciProvenanceReleaseTests.cs`, `ReleasePackageManifestTests.cs` (675-802; `typeof(Oq8PlatformClosureTests)` 798; workflow-text guards 854-855, 968, 2019-2021), `PostgreSqlImageGovernanceTests.cs` (validator leg), `ContainerPublishingGovernanceTests.cs` (917-954 raw/nupkg; 674 loop text), `TrustedPublishingReleaseTests.cs:52`, `ProviderVerification.Tests/RuntimeIdentityValidatorTests.cs` (evidence-receipt cases).
- CI: delete `.github/workflows/{p1r-qualification,corrective-work-authorization}.yml` (move `p1r-qualification.yml` L65-78 release-governance step into `ci.yml` `semantic-release-governance`); `release.yml` L4-7, L59, L176 required-workflow loop; `integration.yml` L53-57, L111-161 (OQ8 venv/capture/validate/upload); `ci.yml` L112-116 (OQ8 venv); `payload-protection.yml` L5-8 comment; `advisory-tests.yml` L25; `tools/release-publication-identity.py:34`, `tools/tests/test_release_publication_authority.py:52,126` name `p1r-qualification.yml`.
- Config/docs: `.gitignore` L473-569 (keep L493 `_bmad/render/`, L496-498 `.oq8-python/`); `.gitattributes` evidence lines 8-10,12 (comments), 15-16, 21-44, 46-157 (keep 1-7, 11, 13-14, 17-20, 45; `CommitMessagePolicyTests:475-477`); `.husky/` has only `commit-msg`; `docs/ci.md` (53, 62, 71-80, 103-109, ~120-172, 393-450, 451-583, 644-646); delete `docs/oq8-v5-activation.md`, `docs/architecture/evidence-secret-scanning.md`, `docs/operations/{projection-delivery-v2-evidence,signalr-operational-evidence,query-operational-evidence}.md` and fix inbound links; trim pages listed by the docs inventory (security/workload-binding-authority, concepts/dapr-infrastructure-boundary, guides/named-projection-authoring, operations/redis-signalr-channel-isolation, guides/sample-blazor-ui:52, brownfield/development-guide:51, 4× OQ8 packet paragraph); `_bmad-output/project-context.md`.
- Planning: `architecture.md:223,426`; `prd.md` §8.1 paragraph (~440/451) + one supersession sentence; `epics.md` 3.22/3.23 reconciliation; `sprint-status.yaml` L177-179 and the GUARDED header/fences (their asserting tests are deleted).

## Tasks & Acceptance

**Execution:**
- [x] `scripts/check-committed-files.sh` -- new bash guard: `--staged` (index adds/modifies) or default (all tracked); evidence-path + 1 MiB rules, allowlist, skip gitlinks -- single enforcement point.
- [x] `tools/tests/test_check_committed_files.py` -- unittest over a temp repo covering every matrix row.
- [x] `.husky/pre-commit`, `.husky/pre-merge-commit`, `.github/workflows/integration.yml`, `.github/workflows/ci.yml` -- run the guard (hooks: `--staged`; CI: full tree + guard and broker unittests in the required `live-sidecar` job, since required `ci / build-and-test` is a reusable workflow; the moved release-governance step stays in `ci.yml` `semantic-release-governance`).
- [x] `deploy/dapr/qualification/` -- relocate `public-identities/` manifests, repoint `runtime-lock.json`, recompute hashes; broker tests stay green.
- [x] Delete-tools, delete-tests, trim-tests, CI edits per Code Map; `Hexalith.EventStore.slnx` entry removed.
- [x] `.gitignore`, `.gitattributes` -- replace evidence blocks with `_bmad-output/**/evidence/` and `docs/evidence/` ignores; keep non-evidence rules.
- [x] Docs + planning + `project-context.md` -- state the policy once (architecture), point others at it, mark 3.22/3.23 `done` (superseded by this spec), remove stale GUARDED notices.

**Acceptance Criteria:**
- Given the branch, when `git ls-files` runs, then no path is under `_bmad-output/**/evidence/` or `docs/evidence/` and no non-allowlisted file exceeds 1 MiB.
- Given the branch, when the solution builds (Debug and Release), then 0 errors/warnings-as-errors, and the trimmed test projects and Python `tools/tests` pass.
- Given the repo, when `git grep` searches tracked non-history files for deleted tool names (`validate-oq8-platform-evidence`, `p1r_`, `release_evidence_`, `validate-corrective-`, `oq8-v5-packet`), then only historical story/spec prose matches.

## Implementation Notes

- Review pass 1 patches applied (triage rows 1-3, 5, 8-10, 13-14, 18, 22-23). Guard CI steps live in `integration.yml` `live-sidecar` (required, runs on every push/PR to `main`): `ci / build-and-test` calls the shared Builds `domain-ci.yml`, which accepts no caller steps.
- Coverage intentionally dropped with the owner's OQ8 decision: the `integration.yml` fresh-capture validation (runtime-version/image pin, diagnostics secret scan, exact output set). The OQ8 PostgreSQL production matrix still runs inside the live-sidecar suite.
- ProviderVerification `historical-authorization` mode keeps its code but no in-repo inputs or passing-path tests; handle with the FrontComposer follow-up.

## Spec Change Log

## Review Triage Log

Pass 1 (2026-10-23; Blind Hunter, Edge Case Hunter, Verification Gap). Diff: changes since baseline, deletion bodies omitted, evidence-folder deletions excluded.

| # | Source | Finding | Verdict | Evidence / route |
|---|--------|---------|---------|------------------|
| 1 | VG, BH | CI guard runs in `ci / semantic-release-governance`, not a required check; docs call it blocking | medium | Ruleset required checks exclude it. Patch: run guard + its tests in required `ci / build-and-test`; fix wording. |
| 2 | VG | Staged rename into an evidence path untested (`--no-renames` load-bearing) | low | Reviewer showed dropping the flag keeps all tests green. Patch: add `git mv` test. |
| 3 | VG, BH | Relocated broker identities only checked by a manual-dispatch workflow | medium | `dapr-broker-qualification.yml` is `workflow_dispatch` only. Patch: run `test_dapr_broker_qualification.py` in push/PR CI. |
| 4 | VG, BH | ProviderVerification HistoricalAuthorization passing path lost its tests | low | Mode never runs in CI (only the test project does) and its in-repo inputs are deleted; tests or mode removal exceed a direct correction. Rejected; revisit with the FrontComposer follow-up. |
| 5 | VG, BH | ProviderVerification README still points at the deleted Story 11.24 lane / an evidence output path | low | Patch: correct the README text. |
| 6 | BH | Guard checks only the final tree, not every commit in a range | low | Ruleset allows squash merges only; hook covers local commits. Range scan adds complexity. Rejected. |
| 7 | BH | `deferred-work.md` (77%) / `epics.md` (73%) will hit the 1 MiB cap | false | Loud failure with a fix hint is the specified "Large file" behaviour; remedy is an owner choice when reached. |
| 8 | BH, ECH | `SignalRRedisBackplaneRuntimeProofTests` writes `evidence-*.md` into tracked `_bmad-output/test-artifacts/…`; 22 captures tracked | medium | Confirmed at `SignalRRedisBackplaneRuntimeProofTests.cs:598-605`. Patch: default under an ignored `evidence/` folder; `git rm` the 22 captures. |
| 9 | BH | Evidence-path match is case-sensitive (`Evidence/`) | low | One-line fix. Patch. Broader `runs/`/`captures/` patterns are outside the chosen rule: rejected. |
| 10 | BH, ECH | In-flight planning (Story 9.1, `spec-9-1-es-diag…`, retro items 24–29/34, Stories 2.13/3.17/3.21 tasks, epics ACs, architecture gate paragraphs) still direct work at deleted tools/evidence paths | medium | Confirmed (e.g. epics 9.1 ACs name the deleted validator). Patch: global supersession note in epics + architecture, supersede 9.1/retro items, one-line policy note in in-flight story files. |
| 11 | BH | 49 open DW entries cite deleted tools | low | Sweep verifies entries against code. Rejected. |
| 12 | BH | 258 links in historical prose now point at untracked evidence | low | Inherent in the owner's delete-all decision; rewriting them exceeds a direct correction. Rejected. |
| 13 | BH | `dapr-infrastructure-boundary.md` claims the story record keeps run results; `projection-delivery.md` points at a spec whose links are dead | low | New wording introduced by this diff. Patch: correct both sentences. |
| 14 | ECH | `architecture.md:230` links deleted `6-5-integration/metadata-adapter-contract.md` | low | Confirmed. Patch: drop the link, keep the text. |
| 15 | BH | Deleted OQ8 integration lane validated fresh captures | low | Owner explicitly chose to delete the OQ8 validators and their CI lanes. Rejected (intent); coverage change noted below. |
| 16 | BH | Deleted manifest test leaves case-variant package IDs unchecked | false | `tools/release_package_contract.py:148` rejects casefold duplicates in the kept release path. |
| 17 | BH | Spec lacks recorded counts/SHA/CI link; count mismatch | — | Fix edits this spec. Rejected per workflow; results are recorded at presentation. |
| 18 | BH | `.oq8-python/` ignore comment cites deleted docs | low | Patch: reword comment, keep rule. |
| 19 | BH | Allowlist has no per-entry size ceiling | low | Two fixed entries; ceiling adds parameters. Rejected. |
| 20 | BH | Guard tests miss spaces/newlines, symlinks, temporary index | false | `-z` parsing handles names; only mode 160000 is skipped; Git honours `GIT_INDEX_FILE`. No bad outcome. |
| 21 | BH | Moved Builds checkout pins `actions/checkout` v7.0.1 vs v7.0.0 elsewhere | false | Both SHA-pinned and valid; no caller diverges. |
| 22 | ECH | Guard passes `0 checked` if `git ls-files`/`git diff` fails inside process substitution | low | Confirmed: process-substitution status is ignored under `set -e`. Patch: fail with exit 2. |
| 23 | ECH | Merge commits run `pre-merge-commit`, not `pre-commit` | low | Confirmed Git behaviour. Patch: add `.husky/pre-merge-commit`. |

## Verification

**Commands:**
- `bash scripts/check-committed-files.sh` -- expected: exit 0
- `python3 -m unittest discover -s tools/tests` -- expected: all pass
- `dotnet build Hexalith.EventStore.slnx -c Release -p:NuGetAudit=false` -- expected: 0 errors
- built test DLLs in place: `Contracts.Tests`, `Server.Tests` (`-class …SecretsProtectionTests`), `ProviderVerification.Tests`, `Admin.Server.Tests`, `IntegrationTests` broker fixture -- expected: pass (Tier-3/live tests: build only)
