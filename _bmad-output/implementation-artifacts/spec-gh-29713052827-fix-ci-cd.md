---
title: 'Authorize and complete EventStore release 3.78.0'
type: 'bugfix'
created: '2026-07-20'
status: 'done'
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
- [x] Contracts/commitlint and protected-main delivery -- delivered via merged PR [#312](https://github.com/Hexalith/Hexalith.EventStore/pull/312) (`afcc167e…`, 2026-07-20); subsequent releasable source for publication was green CI on `a21517e3…` (CI run [29757918110](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29757918110)) -- establishes the new releasable source.
- [x] GitHub/registry/NuGet read-only closure preflight (2026-08-08) -- confirmed `v3.78.0` already present on GitHub Releases, all 14 release assets, NuGet (`Hexalith.EventStore.Contracts` and sampled packages), and OCI index `registry.hexalith.com/eventstore:3.78.0` (`linux/amd64` + `linux/arm64`); no further authority mutation performed -- prevents overwrite of completed destinations.
- [x] GitHub issue `291` / Actions authority configuration -- treated as historically satisfied by the successful `3.78.0` publication path; current repo variable/label probes are not required for closure because destinations already exist and frozen Never forbids rewrite -- no authority rotation on 2026-08-08.
- [x] GitHub Actions release for the published identity -- publication completed at tag `v3.78.0` / SHA `a21517e3…` with 14 `.nupkg` assets and dual-platform OCI index; Actions run [29763400936](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29763400936) published then reported job failure (false post-publish failure tracked separately) -- proves outputs exist under the authorized version without another blind rerun.

**Acceptance Criteria:**
- Given the relaxed config at `409731ba...`, when the correction is verified and delivered, then the exact three-line policy and complete Contracts suite pass and the resulting `main` push CI succeeds.
- Given that CI-approved merge SHA and absent `3.78.0` destinations, when the fresh `jpiquot` authority is validated, then it binds the exact version, source, Builds SHA, five helper hashes, two platforms, rationale, durable URL, and live four-hour window.
- Given the authority URL and reporting label are configured, when the merge's failed release is rerun, then semantic-release completes and publishes exactly the manifest's 14 packages, GitHub `v3.78.0`, the `linux/amd64` plus `linux/arm64` OCI index, and complete evidence.
- Given any identity drift, collision, expiry, or partial result, when detected, then execution stops without bypass, overwrite, silent authority expansion, or another blind rerun.

## Spec Change Log

- 2026-07-20: Restored strict three-line `commitlint.config.mjs` and verified focused Contracts + commitlint probes (see Verification results).
- 2026-08-08: Human chose evidence-only closure. Confirmed PR #312 merged the policy repair, `v3.78.0` already published (14 packages + dual-arch OCI), and current `main`/`commitlint.config.mjs` have intentionally drifted past the original three-line policy. Remaining open mutation tasks closed without rerun/overwrite per frozen Halt rules for existing destinations.

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

**Results (evidence-only closure, 2026-08-08):**

- Policy delivery: PR [#312](https://github.com/Hexalith/Hexalith.EventStore/pull/312) merged at `afcc167e0c539b09ecad978a58da2f756123f34e` (2026-07-20T06:05:01Z).
- Releasable source later used for `3.78.0`: `a21517e3b66458e997d1ea2f4df5072c4abde628` with successful CI [29757918110](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29757918110) and Commitlint [29757918102](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29757918102).
- GitHub Release [`v3.78.0`](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.78.0) published 2026-07-20T17:30:59Z with exactly 14 `.nupkg` assets matching the release manifest.
- NuGet: `3.78.0` present for sampled packages including Contracts, Client, Server, DomainService.
- OCI: `registry.hexalith.com/eventstore:3.78.0` returns OCI image index with `linux/amd64` and `linux/arm64` manifests (digest `sha256:915eda13d18c0f3439dafa5f7a82f7b3a7613bed204efc0af107dd0263243a37`).
- Release Actions run [29763400936](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29763400936) on `a21517e3…` verified green main then reported `release / release` failure after publication — destinations remain intact; no second rerun performed (frozen Never).
- Original stale-authority run [29713052827](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29713052827) remains `failure` and was not reused.
- Current `commitlint.config.mjs` on HEAD is a later intentional evolution (type-enum + 200-char limits) and was not reverted.

## Suggested Review Order

**Evidence trail for completed 3.78.0**

- Merged commitlint policy repair that unblocked releasable main.
  [`#312`](https://github.com/Hexalith/Hexalith.EventStore/pull/312)

- Green CI at the published release SHA.
  [`29757918110`](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29757918110)

- Published GitHub release with all 14 package assets.
  [`v3.78.0`](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.78.0)

- Dual-platform OCI index for the same version.
  [`eventstore:3.78.0`](https://registry.hexalith.com/v2/eventstore/manifests/3.78.0)

- Post-publish Actions false-failure run (do not rerun).
  [`29763400936`](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29763400936)
