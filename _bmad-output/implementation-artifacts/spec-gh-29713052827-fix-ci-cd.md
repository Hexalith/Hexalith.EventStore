---
title: 'Authorize and complete EventStore release 3.78.0'
type: 'bugfix'
created: '2026-07-20'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: 'bccc25601ae8226290324bf2adfbce69bcfc40cf'
continuation_baseline_commit: '409731baef9ed974f715f00a2f048f9ba486cb3f'
context:
  - '{project-root}/docs/ci.md'
  - '{project-root}/docs/ci-secrets-checklist.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Release run `29713052827` correctly failed closed because its authority covered only `3.77.2`. Before authorization could be renewed, `main` advanced to `409731ba...`, whose CI run `29719612077` fails because `commitlint.config.mjs` reintroduced policy relaxations forbidden by the repository contract; the old release run can no longer publish from current `main`.

**Approach:** Restore only the strict three-line commitlint configuration, verify it, deliver it through the protected-main pull-request flow, and wait for green CI. Then create a four-hour durable authority for `3.78.0` at that exact CI-approved merge SHA and Builds `ffa1662829b28d1d90554980c87f23bd9d4e25e7`, rotate the authority variable, add the missing failure-reporting label, and rerun the corresponding failed release once.

## Boundaries & Constraints

**Always:** Keep `commitlint.config.mjs` byte-equivalent to the approved LF-only three-line policy; validate through repository-pinned commitlint and Contracts tests; use a Conventional Commit branch/commit/PR with protected-main checks. After merge, reconfirm exact `origin/main`, successful push CI, semantic-release version, authenticated allowlisted owner, Builds/helper identities, four-hour UTC window, and absence of `3.78.0` from GitHub Releases, all 14 NuGet packages, and `registry.hexalith.com/eventstore`. Use rationale `Authorize release 3.78.0 from the latest CI-approved main source.` Preserve PR, authority, release, and evidence URLs.

**Ask First:** Any change beyond `commitlint.config.mjs` and this spec, unexpected test/check failure, source drift after the corrective merge, Builds SHA/version change, destination collision or partial publication, inability to prove the authority body, or release-governance change outside this authorized flow.

**Never:** Modify the contract test to accept the relaxations, weaken commitlint or authority validation, reuse/edit comment `5016454096`, extend the four-hour window silently, rerun after identity drift, overwrite an existing package/tag/image, expose credentials, force Git history, or alter the user's existing planning changes or submodule checkout.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Commit policy repair | Relaxed 13-line config at `409731ba...` | Exact strict three-line config; lowercase messages pass and uppercase/oversized headers fail | Any broader diff or changed test contract halts delivery |
| Authorized release | Exact `3.78.0`/green merge SHA/`ffa16628...` identity, absent destinations, unexpired owner record | Release publishes 14 packages, GitHub release/tag, two-platform OCI image, and evidence | Watch the exact rerun through completion and bind outputs to the candidate identity |
| Main or release identity drifts | Remote head, computed version, Builds SHA, or helper bytes differ | No authority is created or consumed | Halt for renewed human authorization |
| Destination exists or publication becomes partial | Any package, tag, release, or OCI identity collides or only some outputs appear | No overwrite or blind retry | Halt, preserve evidence, and report exact external state |
| Release fails | Authority/preflight/publish/smoke step fails | First actionable error remains visible; `semantic-release` failure issue can be created | Do not rerun again until the new cause is diagnosed |

</frozen-after-approval>

## Code Map

- `.github/workflows/release.yml` -- binds the CI-approved source to exact Builds execution SHA and the repository authority variable.
- `commitlint.config.mjs` -- sole implementation edit; must restore the approved strict three-line configuration.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` -- unchanged regression contract that detected the policy drift.
- `.releaserc.json` -- computes `3.78.0` and runs authority verification before prepare, NuGet, container, tag, and GitHub Release mutation.
- `scripts/validate-release-authority.sh` -- EventStore fail-closed wrapper for exact release identity and owner allowlist validation.
- `_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` -- authorizes GitHub login `jpiquot` as EventStore release owner.
- GitHub issue `291`, Actions variable `HEXALITH_RELEASE_AUTHORITY_URL`, and label `semantic-release` -- external durable authority, active pointer, and failure-reporting surfaces.

## Tasks & Acceptance

**Execution:**
- [x] `commitlint.config.mjs` -- remove the ten policy-relaxation lines and restore exact LF-only approved content -- repairs CI without weakening its contract.
- [ ] Contracts/commitlint and protected-main delivery -- run focused and full verification, commit only the scoped repair on a fix branch, push, open a validated PR, wait for checks, squash-merge, and bind the successful push CI plus failed stale-authority release to the resulting merge SHA -- establishes the new releasable source.
- [ ] GitHub/registry/NuGet read-only preflight -- bind resulting `origin/main`, triggering CI, proposed version, Builds/helper hashes, owner identity, and destination absence before authority mutation -- prevents stale or colliding authorization.
- [ ] GitHub issue `291` and Actions configuration -- create the missing `semantic-release` label, create then finalize a self-referential durable authority comment with exact identity and four-hour timestamps, and set `HEXALITH_RELEASE_AUTHORITY_URL` to its API URL -- enables the approved release without changing source.
- [ ] GitHub Actions release run for the corrective merge -- rerun that failed stale-authority workflow once, watch it to completion, and inspect release/evidence identities -- proves the gate and publication complete under the authorized scope.

**Acceptance Criteria:**
- Given the relaxed config at `409731ba...`, when the correction is verified and delivered, then the exact three-line policy and complete Contracts suite pass and the resulting `main` push CI succeeds.
- Given that CI-approved merge SHA and absent `3.78.0` destinations, when the fresh `jpiquot` authority is validated, then it binds the exact version, source, Builds SHA, five helper hashes, two platforms, rationale, durable URL, and live four-hour window.
- Given the authority URL and reporting label are configured, when the merge's failed release is rerun, then semantic-release completes and publishes exactly the manifest's 14 packages, GitHub `v3.78.0`, the `linux/amd64` plus `linux/arm64` OCI index, and complete evidence.
- Given any identity drift, collision, expiry, or partial result, when detected, then execution stops without bypass, overwrite, silent authority expansion, or another blind rerun.

## Spec Change Log

## Design Notes

The policy regression is fixed before authority issuance because release authority must bind a green immutable source. The authority comment must then be created first to obtain its GitHub URL and finalized by updating only that new comment so `durable_source` equals the fetched URL. The variable stays on the old authority until the new body is complete and independently read back. Adding the label is diagnostic only.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -warnaserror -m:1` and direct xUnit execution -- expected: zero build errors and complete Contracts pass.
- Repository-pinned commitlint valid/invalid probes -- expected: lowercase valid message passes; uppercase subject and 147-character header fail with their named rules.
- `gh pr checks` plus merge-head and subsequent push-CI inspection -- expected: validated PR merges without drift and CI succeeds at the exact merge SHA.
- `npx semantic-release --dry-run` with publication credentials unset is not used because authority and remote publication probes are part of verify-release; inspect the immutable run log's computed `3.78.0` instead.
- `gh api` readbacks for the new comment, repository variable, label, tag/release, Actions run/job/artifact, and NuGet/OCI probes -- expected: exact identity, successful run, 14 packages, two-platform OCI index, and retained evidence.
- `git status --short --branch && git diff --check` -- expected: no implementation/code change and all pre-existing user planning changes remain preserved; only this spec is added locally.

**Results (policy repair, 2026-07-20):**

- `commitlint.config.mjs` is byte-equivalent to the approved three-line LF-only policy at `d046120f`;
  the focused regression passed 1/1 from a fresh isolated Release build with zero warnings and errors.
- The complete freshly built Contracts assembly passed 746/746 in an isolated runtime mirror bound to
  workflow-pinned Builds `ffa1662829b28d1d90554980c87f23bd9d4e25e7`; the preserved local Builds checkout
  remained untouched at `ed7cea8e1f943b4c47a454a0e8f462f0fae9891d`.
- Repository-pinned `@commitlint/cli@21.1.0` accepted the valid lowercase repair message, rejected an
  uppercase description with `subject-case`, and rejected a 147-character header with
  `header-max-length`.
