---
title: 'Track Story 3.13 v3.94.1 package archives so CI contracts pass'
type: 'bugfix'
created: '2026-08-15'
status: 'done'
review_loop_iteration: 0
baseline_commit: '3f33a5f4b9bf7342ae6e1bf7d97b9cdc9902d3f1'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Push CI [31841848349](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31841848349) fails `ci / contracts` on `SelectedV3941PacketIsPresentAndDoesNotSpliceHistoricalProofBytes` (`DeployedRuntimeParityClosureTests.cs:214`). `ValidatePackageBytes` needs the 14 recovered `v3.94.1` archives under the selected packet `packages/`, but `.gitignore` (`*.nupkg` and `**/[Pp]ackages/*`) leaves them untracked. Local leftovers hide the gap; a clean CI checkout has zero `.nupkg` files. The same fail already existed on parent `247a2257`; the Builds/FrontComposer pointer bump only re-ran it. Other push workflows on that SHA (Commitlint, Integration Tests, Advisory Tests, CodeQL) succeeded.

**Approach:** Re-include Story evidence `packages/*.nupkg` with last-matching gitignore negations (same class as the Story 2.12 evidence-log exception), track the already-captured 14 archives whose SHA-256 values match `evidence-core-sha256.txt`, lock that ignore exception, then push `main` and dispatch Release only after every push workflow on the new SHA is green.

## Boundaries & Constraints

**Always:** Keep `ValidatePackageBytes` requiring on-disk archives when `byte_verification.result == pass` and `recovered_count == 14`. Track exactly the 14 existing local archives after hash-checking them against `evidence-core-sha256.txt`. Place gitignore negations LAST (directory first, then `*.nupkg`). Work on `main` as requested. Validate the commit subject with the repo-pinned commitlint CLI. After push, wait for every workflow that ran on `3f33a5f4` (CI, Commitlint, Integration Tests, Advisory Tests, CodeQL) to succeed on the new SHA before dispatching Release from `refs/heads/main`.

**Ask First:** Hashes-only / Git LFS / dropping `byte_verification` to unavailable; changing Builds or FrontComposer; force-push; dispatching Release while any of those push workflows is red, pending, or skipped-as-failed.

**Never:** Broadly re-include evidence `logs/**` nupkgs (the 2026-07-28 defect). Change selected identity `80d12ef5…` / `3.94.1` / `sha256:ab8784c8…`. Edit `ValidatePackageBytes` to pass without archives. Mutate the historical `fa2d1c99` packet, production runtime, submodule pointers, or nested submodules. Bypass commit hooks.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Clean checkout | Selected packet `packages/` present in git | `SelectedV3941PacketIsPresentAndDoesNotSpliceHistoricalProofBytes` passes; 14 SHA-256 matches | Missing/mismatched archive fails closed |
| Ignore regression | Last gitignore patterns omit evidence `packages/` re-include | Guardrail fails closed | Must keep failing |
| Accidental build nupkg | `*.nupkg` under `logs/` or restore `packages/` | Still ignored | Must keep ignored |
| Release preflight | New main SHA with successful push CI | `workflow_dispatch` Release from that SHA | Do not dispatch if CI is not success on that exact SHA |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:17-26,198-219,2912-2970,4692-4710` -- failing fact L214; `ValidatePackageBytes` requires exact 14 top-level archives + SHA-256; `ExpectedCoreFilesFor` already lists `packages/*.nupkg` when byte verification is `pass`.
- `.gitignore:219-223,473-490` -- `*.nupkg` + `**/[Pp]ackages/*` exclude the evidence dir; logs exception is the last-pattern precedent. Append evidence `packages/` negations after the log block.
- `_bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/packages/` -- 14 local archives (~2.4 MB); hashes already match `evidence-core-sha256.txt:11-24`. `git ls-files` is empty; `git check-ignore -v` cites `.gitignore:223`.
- `_bmad-output/.../identity-crosswalk.json:69-87` -- `byte_verification.result=pass`, `recovered_count=14`, `archive_root=packages`. Read-only.
- `docs/ci.md:273-287` -- selected identity already documented; add that the 14 archives are tracked evidence, not restore output.
- `.github/workflows/release.yml:1-70` -- `workflow_dispatch` from current `main` only after successful push CI on that SHA.
- `references/Hexalith.Builds` / `Hexalith.FrontComposer` gitlinks -- red herring; read-only.

## Tasks & Acceptance

**Execution:**
- [x] `.gitignore` -- LAST: `!_bmad-output/**/evidence/**/packages/` then `!_bmad-output/**/evidence/**/packages/*.nupkg`; keep log negations; do not re-include `logs/**` nupkgs.
- [x] Selected packet `packages/*.nupkg` -- hash-check then `git add` the 14 archives; do not retouch JSON/manifests.
- [x] `DeployedRuntimeParityClosureTests.cs` -- assert `.gitignore` last patterns re-include evidence `packages/*.nupkg` and still exclude generic `*.nupkg`.
- [x] `docs/ci.md` -- note that the selected `v3.94.1` archives are tracked because `ValidatePackageBytes` rehashes them.
- [ ] Push `main` after commitlint; wait for CI + Commitlint + Integration Tests + Advisory Tests + CodeQL on the new SHA; then `gh workflow run` Release on that SHA. Skipped by operator: no commit, push, or remote GitHub operations.

**Acceptance Criteria:**
- Given a clean checkout of the new SHA, when Contracts.Tests run in Release/package mode, then `SelectedV3941PacketIsPresentAndDoesNotSpliceHistoricalProofBytes` passes and `git ls-files` lists exactly those 14 archives.
- Given `.gitignore` without the evidence `packages/` negations, when the new guardrail runs, then it fails closed.
- Given restore/`logs/` nupkgs, when gitignore is applied, then they stay ignored.
- Given every listed push workflow is green on the new `main` SHA, when Release is dispatched, then `verify-source` accepts that SHA.

## Spec Change Log

- 2026-08-15: Local implementation completed gitignore re-include, hash-checked staging of the 14 archives, guardrail test, and `docs/ci.md` note. Push/`workflow_dispatch` Release left incomplete per operator instruction.

## Design Notes

Git last-matching-pattern wins, and a file cannot be re-included while its parent directory is excluded:

```gitignore
!_bmad-output/**/evidence/**/packages/
!_bmad-output/**/evidence/**/packages/*.nupkg
```

Do not use `!…/logs/**` — that re-included build nupkgs in 2026-07-28.

## Verification

**Commands:**
- `git check-ignore -v _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/packages/Hexalith.EventStore.Contracts.3.94.1.nupkg; echo $?` -- expected: not ignored (nonzero `check-ignore`, no `.gitignore:223` hit)
- `git ls-files _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/packages | wc -l` -- expected: 14
- `dotnet restore tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false && dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false && dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-build --configuration Release --filter FullyQualifiedName~SelectedV3941PacketIsPresentAndDoesNotSpliceHistoricalProofBytes -p:UseHexalithProjectReferences=false` -- expected: pass
- After push: `gh run list --commit <new-sha>` -- expected: CI, Commitlint, Integration Tests, Advisory Tests, CodeQL all `success`
- Then: `gh workflow run release.yml --ref main` and watch the run -- expected: `verify-source` plus release jobs succeed

## Suggested Review Order

**Ignore re-include**

- Last-matching gitignore re-includes evidence `packages/*.nupkg` after log exceptions.
  [`.gitignore:502`](../../.gitignore#L502)

- `**/[Pp]ackages/*` ignores children; directory is still re-included first.
  [`.gitignore:495`](../../.gitignore#L495)

- Log block no longer claims to be last, so later package rules stay effective.
  [`.gitignore:476`](../../.gitignore#L476)

**Tracked archives**

- Fourteen recovered `v3.94.1` archives are now git-tracked evidence, not leftovers.
  [`Hexalith.EventStore.Contracts.3.94.1.nupkg`](evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/packages/Hexalith.EventStore.Contracts.3.94.1.nupkg)

- ZIP archives stay binary so `* text=auto` cannot rewrite hashed bytes.
  [`.gitattributes:11`](../../.gitattributes#L11)

**Guardrail**

- Existing fact still rehashes all 14 on-disk archives against the crosswalk.
  [`DeployedRuntimeParityClosureTests.cs:214`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L214)

- New fact requires last gitignore patterns, ignore negatives, and `git ls-files` of exactly 14.
  [`DeployedRuntimeParityClosureTests.cs:225`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L225)

- `check-ignore` uses timeout, concurrent drain, `--`, and fails closed on empty ignore output.
  [`DeployedRuntimeParityClosureTests.cs:6801`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L6801)

**Docs**

- Selected packet archives are documented as tracked because `ValidatePackageBytes` rehashes them.
  [`ci.md:276`](../../docs/ci.md#L276)
