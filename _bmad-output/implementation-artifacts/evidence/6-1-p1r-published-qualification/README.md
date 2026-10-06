# 6.1-P1R published qualification harness

This directory indexes the fresh receipts for the
[published qualification harness spec](../../spec-6-1-p1r-published-qualification.md).
The scope is **the harness only** (owner scope decision of 2026-10-06, option 1).
No published tuple, rollback, operational profile, assertion instrumentation or
owner decision has been selected. Actual package, database and container
qualification and owner acceptance are still pending. Every packet and every
validation keeps `qualified=false` and `p1r_usable=false`, and recovery stays
at `mutation-freeze-and-forward-recovery`.

## What the harness is

| Path | Role |
| --- | --- |
| `tools/p1r_published_qualification.py` | Validates owner inputs and decisions, the canonical inventory (17 scenarios, 7 families mirrored from their scenarios, 2 additions, candidate/rollback package lanes, candidate/rollback restore and container-cleanup recovery lanes), assertion counters, and the archive, provenance, restore-graph and loaded-binary checks. Also creates packets and validates them independently. |
| `tools/p1r-published-qualification.py` | CLI with two commands: `prepare` (new output only) and the read-only `validate`. |
| `tools/p1r_qualification_runtime.py` | Contracts for restore/append/restart, tenant/inventory and cleanup receipts. Runs the bounded local process controls from `p1r_qualification.py`. It executes no database, container, Dapr or actor backend. |
| `tools/p1r_qualification.py` | Extract-function refactor only. The process-control receipt and sentinel checks moved into `validate_control_receipt`/`validate_sentinel`. The preparation schema, checks and messages are unchanged. |
| `tools/tests/test_p1r_published_qualification.py` | 46 contract tests. They use clearly marked synthetic fixtures (`hexalith-p1r-synthetic-fixture`) and real local processes. |
| `.github/workflows/p1r-qualification.yml` | Adds a step that runs the focused suite. The sealed `ci.yml` is unchanged. |

```bash
python3 tools/p1r-published-qualification.py prepare --out <new-dir> \
  [--inputs owner-inputs.json] [--decisions owner-decisions.json] \
  [--candidate-evidence <dir>] [--rollback-evidence <dir>] \
  [--receipt <lane|restore|cleanup receipt>]... [--process-controls]
python3 tools/p1r-published-qualification.py validate <dir>
python3 -m unittest discover -s tools/tests -p 'test_p1r_published_qualification.py'
```

The exit codes mean:

- **Exit 0:** the packet is structurally valid. That says nothing about qualification.
- **Exit 2:** the invocation was refused, is incomplete, or the packet failed validation.
  - Package evidence, decisions and receipts all depend on owner inputs. Without `--inputs` they are refused, never defaulted.
  - Rollback evidence is refused unless a rollback is selected.
  - Any existing output path is refused, including a symlink.

## Contracts

- **Owner inputs (`hexalith.p1r.owner-inputs.v1`)**
  - The execution authority, plus the candidate and an optional rollback. Each of those carries its version, `v<version>` tag, tag commit, Builds version/commit, https feed, and per-package archive SHA-256, NuGet content hash and repository commit.
  - The operational profile and the Test-owner-accepted assertion instrumentation. Both may be `null`, and a `null` stays a recorded refusal.
  - The selected additions.
  - Synthetic inputs select no published tuple.
- **Owner decisions (`hexalith.p1r.owner-decisions.v1`)**
  - The EventStore, Builds, Solution and Test decisions, in that order, plus same-baseline conformance, all bound to the exact inputs hash.
  - Decisions are complete only when all four are approved, conformance holds, and neither document is synthetic. Even complete decisions leave `p1r_usable=false`: usability belongs to the separately validated coordinated transition.
- **Package evidence (`hexalith.p1r.package-evidence.v1`)**
  - Covers the isolated packages folder, the Release consumer projects, their `project.assets.json`/`packages.lock.json`, and the inventory of physically loaded assemblies.
  - The harness checks the following, and the validator recomputes every check from the observations and copies it retained:
    - archive bytes, plus a nuspec that satisfies the release contract
    - `.signature.p7s` and the actual `dotnet nuget verify --all` output
    - repository commit, `.nupkg.metadata` content hash and source
    - assets/lock agreement
    - no EventStore project dependencies, and the exact versions/content hashes
    - isolated package folder and Release outputs
    - loaded DLL bytes equal to the verified archive entries.
  - Archive-level substitutions and defects are retained as failed checks. An unreadable evidence manifest refuses the invocation.
- **Lane receipts (`hexalith.p1r.lane-receipt.v1`)**
  - Outcomes are derived from the cases.
  - An unsupported operation must expect an honest refusal, must leave the persisted inventory unchanged, and is never compatible.
  - Unmeasured, zero, skipped or invented counters remain nonpassing or are refused.
  - Published or operational scope needs:
    - owner-selected inputs and the accepted instrumentation
    - Release identities (a list of `{id, sha256}` packages) bound to verified published archives and DLLs. The candidate's verified archives must all be included and at least one loaded DLL must be a candidate DLL. Verified rollback archives may appear alongside them for cross-version cases.
    - for operational scope, the selected profile.
- **Restore receipts (`hexalith.p1r.restore-receipt.v1`)**
  - Require a fresh restored database and backup/restore hash binding.
  - The restored inventory must equal the source: floor 5, head 12, snapshot 9.
  - Reconstruction must reach state 12. Append 13 must preserve floor 5, the prior event hashes and the snapshot, and leave the second tenant unchanged.
  - Requires a successful stop of the appending writer on the restored database before a fresh writer restart, a successful replay after that restart, reconstruction of state 13, and complete owned cleanup.
  - The replay observations (state 12 before the append, appended sequence 13, state 13 after the restart) are executor-reported values compared with the contract; they are not recomputed.
  - Bookkeeping rows are counted separately.
- **Cleanup receipts (`hexalith.p1r.cleanup-receipt.v1`)**
  - Resources are owned only through the exact invocation label.
  - The first attempt must target every owned resource. Later attempts must continue after failures, and there must be at least two attempts.
  - Requires no remaining resources and no errors, complete shared discovery, and unchanged shared resources.

**Scope rules:**

- `tooling-synthetic` and `local-process-control` evidence always carries `compatibility=unverified`, and evaluation never accepts it.
- Package lanes need `published-package` scope.
- Restore and cleanup lanes need `operational` scope.
- Scenario, family and addition lanes need `published-package` or `operational` scope.
- At prepare time, removing a marker cannot promote synthetic evidence. The harness itself labels the signature verifier: only the actual `default_verifier` is recorded as `dotnet-nuget-verify`, so an injected verifier keeps the lane tooling-only even if it labels itself otherwise.
- Checksums and recomputation prove that a packet is internally consistent, not that it is authentic. A receipt deliberately relabelled and resealed with consistent data is not detected by validation; that limit is why owner-selected inputs and owner decisions are separate gates.

## Fresh receipts: invocation `00896363793442269b6f2180e8946199`

Pre-review receipts: they predate the review pass 1 patches of 2026-10-07. The current tools are covered by the
post-review invocation `285786297665409ca69577afa95a19c4` below.

[Index](verification/00896363793442269b6f2180e8946199-index.json). Each
receipt records the exact argv, cwd, UTC times, exit status and output hash.
They were recorded with
[`record_command.py`](verification/record_command.py).

| Check | Receipt | Exit | Result |
| --- | --- | --- | --- |
| Focused harness suite | [check-0](verification/00896363793442269b6f2180e8946199-checks/check-0-focused-suite.json) | 0 | 41 tests pass |
| Preparation suite, live source binding | [check-1](verification/00896363793442269b6f2180e8946199-checks/check-1-preparation-suite-live-binding.json) | 1 | **Blocked** (see below) |
| Preparation suite, synthetic binding ([runner](verification/run_preparation_suite.py)) | [check-2](verification/00896363793442269b6f2180e8946199-checks/check-2-preparation-suite-synthetic-binding.json) | 0 | 29 tests pass. The refactor preserves behaviour. |
| actionlint `p1r-qualification.yml` | [check-3](verification/00896363793442269b6f2180e8946199-checks/check-3-actionlint.json) | 0 | Clean |
| Guard mutation checks ([runner](verification/run_mutation_checks.py)) | [check-4](verification/00896363793442269b6f2180e8946199-checks/check-4-mutation-checks.json) | 0 | The unmutated control passes and all 27 guard-weakening mutations are killed. The output lists at most three killing tests per mutation. For 26 mutations a listed test targets the weakened guard (by a failure, or for mutation 4 by an error). Mutation 18 (tooling-scope disposition in the runtime module) lists only errors in fixture-dependent tests: the synthetic fixtures declare `unverified`, so the mutation breaks fixture validation rather than failing a targeted assertion. |
| LiveSidecar tests, `-c Debug -p:UseHexalithProjectReferences=true -m:1` | [check-5](verification/00896363793442269b6f2180e8946199-checks/check-5-livesidecar-debug-build.json) | 0 | Build succeeds with 0 warnings and 0 errors. This is the source-only restore/append/restart reference, built on the concurrent tree. Tests not run. |
| CLI `prepare` (no inputs) | [receipt](verification/00896363793442269b6f2180e8946199-prepare.json); [packet](invocations/00896363793442269b6f2180e8946199-prepare/packet.json) | 2 | **Refused**: live source binding |
| CLI `prepare --process-controls` | [receipt](verification/00896363793442269b6f2180e8946199-run.json); [packet](invocations/00896363793442269b6f2180e8946199-run/packet.json) | 2 | **Refused** before any control ran |
| CLI `validate` of both packets | [prepare](verification/00896363793442269b6f2180e8946199-validate-prepare.json), [run](verification/00896363793442269b6f2180e8946199-validate-run.json) | 2 | `refused or incomplete invocation` |

## Fresh receipts: clean-clone invocation `ac68c12822964598af395a0ad0d489c2`

Pre-review receipts, superseded for the current tools by invocation `285786297665409ca69577afa95a19c4` below.

Recorded after implementation to work around blocker 2 below, without touching the shared worktree. A local scratch
clone of EventStore HEAD `253980f9eb63cc5a7d96be7c8676197e78f39d67` was used. Its root-declared submodules were
initialized non-recursively from the local checkouts. Only this harness's six `tools/`/workflow files were overlaid
on it; [check-0](verification/ac68c12822964598af395a0ad0d489c2-clone-head/check-0-clone-identity.json) retains the
clone HEAD, gitlinks, status and overlay hashes. The source binding is the clone's own real binding, not a synthetic
snapshot. The packets bind the scratch clone path, so they validate only there and are not retained here.

| Check | Receipt | Exit | Result |
| --- | --- | --- | --- |
| Preparation suite, real binding | [check-1](verification/ac68c12822964598af395a0ad0d489c2-clone-head/check-1-preparation-suite-real-binding.json) | 0 | 29 tests pass. The refactor preserves behaviour. |
| Focused harness suite, real binding | [check-2](verification/ac68c12822964598af395a0ad0d489c2-clone-head/check-2-focused-suite-real-binding.json) | 0 | 41 tests pass |
| CLI `prepare` (no inputs) and `validate` | [prepare](verification/ac68c12822964598af395a0ad0d489c2-clone-head/check-3-cli-prepare.json), [validate](verification/ac68c12822964598af395a0ad0d489c2-clone-head/check-4-cli-validate-prepare.json) | 0 | Valid packet; all 31 required lanes unsatisfied; `qualified=false`, `p1r_usable=false` |
| CLI `prepare --process-controls` and `validate` | [prepare](verification/ac68c12822964598af395a0ad0d489c2-clone-head/check-5-cli-prepare-process-controls.json), [validate](verification/ac68c12822964598af395a0ad0d489c2-clone-head/check-6-cli-validate-process-controls.json) | 0 | 38 local-process-control assertions; scope stays `local-process-control`; `qualified=false` |
| CLI reused output | [check-7](verification/ac68c12822964598af395a0ad0d489c2-clone-head/check-7-cli-reused-output.json) | 2 | `output path already exists` |
| CLI dependent evidence without owner inputs | [check-8](verification/ac68c12822964598af395a0ad0d489c2-clone-head/check-8-cli-dependent-without-inputs.json) | 2 | `refused or incomplete invocation` |

These receipts prove the harness only, at that clone's source scope. They grant no package, operational or
acceptance pass.

## Fresh receipts: post-review clean-clone invocation `285786297665409ca69577afa95a19c4`

Recorded on 2026-10-07 after the review pass 1 patches, the same way as `ac68c128…`: the same scratch clone of
HEAD `253980f9eb63cc5a7d96be7c8676197e78f39d67` with non-recursive local submodules, and the six patched
`tools/`/workflow files overlaid. [check-0](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-0-clone-identity.json) retains the clone HEAD, gitlinks,
status and overlay hashes. The packets bind the scratch clone path, so they validate only there and are not
retained here. `tools/p1r_qualification.py` is unchanged since `ac68c128…`.

| Check | Receipt | Exit | Result |
| --- | --- | --- | --- |
| Preparation suite, real binding | [check-1](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-1-preparation-suite-real-binding.json) | 0 | 29 tests pass |
| Focused harness suite, real binding | [check-2](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-2-focused-suite-real-binding.json) | 0 | 46 tests pass |
| actionlint `p1r-qualification.yml` | [check-3](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-3-actionlint.json) | 0 | Clean |
| CLI `prepare` (no inputs) and `validate` | [prepare](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-4-cli-prepare.json), [validate](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-5-cli-validate-prepare.json) | 0 | Valid packet; all 31 required lanes unsatisfied; `qualified=false`, `p1r_usable=false` |
| CLI `prepare --process-controls` and `validate` | [prepare](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-6-cli-prepare-process-controls.json), [validate](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-7-cli-validate-process-controls.json) | 0 | 38 local-process-control assertions; scope stays `local-process-control`; `qualified=false` |
| CLI reused output | [check-8](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-8-cli-reused-output.json) | 2 | `output path already exists` |
| CLI dependent evidence without owner inputs | [check-9](verification/285786297665409ca69577afa95a19c4-post-review-clone-head/check-9-cli-dependent-without-inputs.json) | 2 | Refusal now reports the exact cause: `dependent execution refused: owner execution inputs missing` |

The implementer's post-patch revert/mutation check (22 targets, all caught) ran only in its session scratchpad and
has no retained receipt.

The focused suite covers the following:

- missing, malformed and partial owner inputs, which are refused and not defaulted
- reused outputs
- the inventory: missing, duplicate or reordered lanes, and diverging families
- invented qualification, usability or recovery, and changed downstream gates or refusals
- 14 archive, graph and loaded-binary substitutions, each retained as a failed check, plus the actual `dotnet nuget verify` refusing a synthetic signature (NU3005)
- resealed and unresealed tampering, including a fully consistent forgery that drops a consumer project
- unsupported-operation honesty, and zero, skipped or unmeasured counters
- restore invariant violations, retained as failures, and restore binding tampering
- cleanup failures, plus real cleanup with an injected signal failure
- a real external SIGINT
- an interrupted package observation
- source drift
- review pass 1 additions: malformed imported receipts and a corrupt-deflate archive retained as refusals or failed
  checks; CLI refusal causes and the 0/2 exit contract; one failing case per package-lane check; each package-scope
  signal alone; populated verified-identity bindings (accept and refuse, including rollback-only and partial
  candidate bindings); restore shape, replay, stop-writer and cleanup-binding invariants; owner-decision role
  completeness; and validate-time-only guards.

## Blockers retained

1. **Aspire baseline: blocked.**
   - The pre-edit `aspire start` of `src/Hexalith.EventStore.AppHost` exited 2 after about 8 s.
   - The cause was `CS0246 ProjectionNotificationProvenanceVerifier` in `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs(71,35)`, raised while concurrent Story 5.5 work was editing `src/Hexalith.EventStore/**`.
   - Unreceipted observation reported by the observing agent: the file defining that type appeared about 12 s after the build failed. No retained receipt supports this timing.
   - No AppHost or resource ever started. `aspire stop` and the final `aspire ps` report none.
   - The four shared `dapr_*` containers kept their IDs and stayed running.
   - Receipts: [`verification/aspire-baseline/`](verification/aspire-baseline/). They were written by the Aspire observation agent's own receipt script, not by `record_command.py`, and that script was not retained. Their schema differs (separate stdout/stderr byte counts and hashes, timeout fields), and each `log_path` points into that session's scratchpad rather than to the retained `.log` copy next to the receipt.
   - This harness changes no C# or AppHost code.
2. **Live source binding: refused.**
   - The concurrent work left the tracked file `src/Hexalith.EventStore/Authentication/DaprInternalAuthenticationHandler.cs` deleted but not staged.
   - The reused `p1r_qualification.source_binding` correctly refuses with `missing or substituted input`.
   - The real CLI packets above and the live-binding preparation suite (check-1) therefore retain that refusal.
   - Packet contract tests use a marked synthetic binding snapshot instead. Real CLI evidence needs a new invocation after that work stages or restores the file. Earlier packets are never resealed.
   - The clean-clone invocation `ac68c12822964598af395a0ad0d489c2` above shows that both suites and the real CLI pass with a real binding at HEAD plus this harness. It does not replace a shared-worktree invocation. The post-review invocation `285786297665409ca69577afa95a19c4` repeats this for the patched tools.

## Decisions and pending gates

- No owner inputs or decisions were supplied, and none is inferred.
- The following remain unavailable or unverified until a separately selected tuple, profile and executor exist:
  - the exact published candidate and any capable rollback, with their archives, signatures, assets/lock graphs and loaded assemblies
  - the seventeen-scenario matrix, the seven families and the selected Reminder/evolution additions
  - fresh-database restore/append/restart with tenant proof
  - container cleanup
  - Test Architect acceptance of the assertion instrumentation
  - the exact EventStore/Builds/Solution/Test decisions and same-baseline conformance.
- No publication, deployment, pin, Projects/Builds or readiness change was made, and no acceptance transition. The following keep their decisions:
  - the fixed October 1 acceptance
  - P0/P2/P3/P4, G-6, DW-35/DW-68
  - independent readiness (`NOT_READY`)
  - Story 6.1 (blocked) and Story 8.11.
