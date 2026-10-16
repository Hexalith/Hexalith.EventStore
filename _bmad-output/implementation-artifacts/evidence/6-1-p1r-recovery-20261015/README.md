# P1R reviewed source and evidence recovery

This is a **source-only recovery**, performed on 2026-10-16 UTC in the directory
specified by the recovery spec. It grants no publication authority, package
qualification, P1R usability or prerequisite acceptance. P1R remains unusable and
Story 6.1 remains blocked. Sprint, acceptance, architecture and readiness state
were not changed.

## Exact scope and identities

The recovery source is local commit
`b7a1917cec2a59ffe80dc3ee977cf3aa31376b14`, parent
`210b90da172cc7ca946fbb06cfd44390bffa707b`, retained on
`fix/p1r-successor-enrollment`. EventStore remains on `main` at
`5b0df296abc86adf8d37a92fae02e26574d19ad5`; Projects remains on `main` at
`42c8e7bc3a58d78e7c6ea4273711d697e81175e7`. No branch, HEAD, history, staged
content or gitlink was moved. No dependency was updated, nested submodule
initialized, production `src/` changed, package published, or version selected
or reserved. The user's existing Story 6.1 edit remains protected. Parent-authorized workflow
metadata in the recovery spec and nine appended review deferrals are separately
bound by [exact authorization pins](authorized-workflow-changes-v2.json); the
ledger's original 115,260-byte prefix retains its starting hash.

The authoritative [reviewed source](../6-1-p1r-successor-enrollment-20261014/reviewed-source.json)
has 23 paths. Eleven differing paths and six absent paths were recovered from
exact Git blobs; six identical paths were left untouched. The recovered changes
restore the private runner, strict snapshot/proof checks, finite proposed
successor enrollment, consumer controls, snapshot regression test, preparation
workflow and required dated audit helpers. All 23 lengths and SHA-256 hashes,
and the Git blob identities, match the reviewed closure.

Only absent blobs were copied into the two historical bundles:

| Bundle | Missing files recovered |
| --- | ---: |
| `6-1-p1r-31210-blocker-repair-20261014` | 176 |
| `6-1-p1r-successor-enrollment-20261014` | 118 |
| Total | 294 |

Those files total **173,221,071 bytes**. All 2,348 existing files compared across
these two bundles and `6-1-p1r-31210-published-run` matched the recovery objects
and were left untouched. All 117 delivery-manifest entries and all four required
CI audit helpers match. Existing historical outputs, including
`compiler-enrollment.json` and `publication-readiness-handoff.md`, retain their
original bytes.

[Initial state](initial-source-state.json),
[per-path recovery inventory](recovery-inventory.json) and
[recovery result](recovery-result.json) record the exact inputs and actions.
The [protected-file inventory](protected-files-before.json.gz) is losslessly
compressed JSON; [encoding metadata](protected-files-encoding.json) binds both
compressed and original bytes. It covers 206,456 starting files, including
existing generated/untracked evidence and tracked architecture symlinks.
[Original final preservation](final-preservation.json) records the initial
post-verification hashes and Git boundaries before parent workflow updates.
The [revised verifier](verify-final-preservation.py) requires exact index-byte
equality in its verdict and emits a new `final-preservation-v3.json`, preserving
the original receipt. Its only allowed starting-byte differences are the exact
authorized workflow snapshots, independently checked against the original spec
and ledger prefix.

## Fresh verification

These are new executions against the recovered active checkout. Historical
passing outputs are retained solely as historical evidence.

| Check | Fresh result | Evidence |
| --- | --- | --- |
| Published executor suite | 104 passed | [Log](python-published_executor.log) |
| Isolation suite | 28 passed | [Log](python-isolation.log) |
| Published qualification suite | 61 passed | [Log](python-published_qualification.log) |
| Preparation suite | 35 passed | [Log](python-qualification.log) |
| Server.Tests Debug/source build | Passed | [Log](server-build.log) |
| Four requested snapshot classes | 178 passed; none skipped | [Log](server-controls.log) |
| Compiler enrollment, without restore | All 24 profiles passed | [Profiles](compiler-enrollment.json) |
| Existing evidence conflict refusal | Passed; no bytes overwritten | [Revised controls](recovery-refusal-controls-v2.json) |
| Concurrent destination edit refusal | Passed; concurrent bytes retained | [Revised controls](recovery-refusal-controls-v2.json) |
| Optimized Python refusal | `-O` and `PYTHONOPTIMIZE=1/2` refused before filesystem actions | [Controls](recovery-refusal-controls-v2.json) |
| Index-byte verdict | Equal index passes; changed index fails with every other check passing | [Controls](final-preservation-index-controls-v2.json) |

[Fresh command receipts](fresh-verification.json) retain literal argument arrays,
working directories, exit codes, timing and log hashes. The
[fresh source binding](fresh-source-binding.json) binds production, tests,
tooling, build configuration and imported central versions. The original
compiler evaluator was copied byte-for-byte into this new directory and its
24 evaluations wrote only the new output. The
[recovery matrix](recovery-matrix-coverage.json) maps each spec scenario to its
executed evidence. [Tooling attempts](recovery-tooling-attempts.json) retain the
two corrected helper/fixture setup failures separately from the actual recovery
and passing controls.

The compiler profiles include proposed `3.122.1`, existing admitted versions,
and separate Debug/source mode; arbitrary `3.122.0` and `3.123.0` remain excluded.
Executor controls retain genuine nine-event/manual-version-one/three-tail
snapshot creation, strict physical-writer proof, complete nonempty shared
inventory equality, private isolation and exact owned cleanup. This recovery
does not rerun published package scenarios or substitute unit/fixture results
for operational evidence.

[Source API applicability](source-api-applicability.json) confirms the snapshot
registration/manual APIs and original snapshot controls equal the reviewed
parent, and the current snapshot tests compile and pass. The complete active
production baseline differs from that parent in unrelated areas; no unrelated
source was imported. [Canonical applicability](canonical-applicability.json)
records a read-only comparison with the fixed canonical commit from the spec,
including both runtime-generated credential fixture changes. One affects the
hash-bound executor test; the other is outside this closure. Their later landing
must retain the canonical security/test behavior and rebind the reviewed
closure. This recovery cannot silently amend those exact reviewed hashes.

The required pre-change Aspire attempt built successfully but could not start:
its nested Tenants and Tenants.Api host projects are absent. The exact command,
exit code and full logs are retained in [the baseline receipt](aspire-baseline.json).
No nested initialization or configuration workaround was performed. A later
exact-target describe confirmed EventStore was not running; an unrelated
AppHost was left untouched. [Toolchain and state](toolchain-and-apphost-state.json)
retains actual observations with dashboard authentication URLs omitted. The
local Dapr CLI is 1.18.2 and installed runtime is 1.18.7; no live Dapr profile
qualification was attempted. The selected qualification profile remains pinned
to Dapr **1.18.2** in the preserved evidence.

## Recovery helper review fixes

The recovery helper now refuses optimized interpreters before any filesystem-capable
import or action. Its isolated controls exercise normal collision/concurrency
refusals plus `-O` and `PYTHONOPTIMIZE=1/2`, with zero filesystem/process audit
events after helper entry and unchanged fixture bytes. The revised preservation
verdict includes `index_bytes_unchanged`; isolated equal/changed index cases
exercise the actual verifier, writing only fresh fixture outputs.

[Original version bindings](review-fix-originals.json) preserve the original
helper, verifier, README and bundle manifest under `-v1` names. Original control
sources and receipts, including `final-preservation.json`, are untouched. New
controls and results use `-v2` paths. [Focused preservation controls](review-fix-preservation-controls-v2.json)
verify all 23 reviewed source hashes, original helper/control/receipt bytes, and
the two exact authorized workflow differences. Only these focused controls were
rerun for the helper fixes; the parent runs the full verification independently.

## Remaining handoff

The recovered [publication readiness handoff](../6-1-p1r-successor-enrollment-20261014/publication-readiness-handoff.md)
remains authoritative for its qualification and authority boundaries. Its old
upstream observations, release checks, CI passes and registry observations are
historical; they grant nothing to a future source or package tuple. Proposed
`3.122.1` remains an enrollment proposal, with no future frozen publication
source. Landing and any version selection require fresh analysis.

1. Separately authorize landing and reconcile with then-current canonical main,
   including the generated credential fixtures and canonical security/test
   additions. Freeze the exact landed source, all 23 reviewed paths and four CI
   helpers, tag Builds gitlink, construction inputs and approved publisher Builds
   identity. Every changed byte requires fresh binding and verification.
2. Obtain successful push CI, P1R preparation/isolation controls and Commitlint
   for that exact frozen current-main source. Local source checks and old green
   CI do not satisfy this gate.
3. Separately implement and approve publication-authority workflow wiring.
   The active release workflow still sets
   `HEXALITH_RELEASE_REQUIRE_AUTHORITY=false`. Require the reserved-version,
   authority issue/owner and one-use identity-bound authority gates, including
   refusal of missing, mismatched, expired and replayed authority. Then obtain
   protected production approval, publisher credentials/OIDC, exact-source CI,
   authenticated registry/version-floor checks and the guarded release workflow.
4. After authorized publication, independently observe the real release/tag,
   all package/archive hashes and repository commits, signatures and OCI digest
   provenance. Obtain explicit owner selection of the new observed tuple before
   executing qualification in a new invocation with all seventeen lanes and the
   adopted Reminder recovery/logical-event evolution additions.
5. Retain rollback `null`, comparisons `3.70.1`/`3.110.0`, Dapr `1.18.2`, and the
   complete recorded PostgreSQL v1 profile with
   `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
   Preserve single-writer drain, mutation freeze, forward recovery, every failed
   attempt and every exact owned cleanup receipt. Historical failed package
   cases remain failed until actual new selected-package evidence resolves them.
6. EventStore Owner, Builds Owner, Solution Architect and Test Architect must
   decide and provide conformance for the same exact tuple; the Solution
   Architect's support-boundary disposition remains pending. Separate Projects
   acceptance, P0/P2/P3, same-baseline sign-off, P4 clean checkout and independent
   readiness exactly `READY` continue to govern Story 6.1.

## Final parent verification

The [parent verification](parent-verification/verification.json) repeated all
228 Python tests, 178 snapshot tests (zero skipped), the Debug/source build,
24 compiler evaluations without restore, the root production-authority check
and both `git diff --check` commands after the helper fixes; every command passed.
All [seven helper regression controls](parent-verification/helper-regressions.json)
also passed independently. The final preservation verifier accepts the exact
final `done` workflow metadata snapshot and the nine-entry append-only ledger;
its [v2 implementation](verify-final-preservation-v2.py) and receipt are retained.
The [v3 index controls](final-preservation-index-controls-v3.json) cover its unchanged
and changed index verdicts. [Final preservation](final-preservation-v3.json) binds
the final authorized metadata and all original preservation checks.

The three independent reviewers identified nine pre-existing hardening/test gaps
in the exact recovered tooling. Their individual verdicts are in the recovery
spec; each is appended to the [deferred-work ledger](../../../../../../_bmad-output/implementation-artifacts/deferred-work.md).
They cover image and launch provenance, mutation targets, private process proof,
full toolchain identities, distinct cleanup observations, remote TLS, startup
deadlines and caller-level receipt finalization tests. They remain outstanding
before relying on future published qualification. Neither this deferral nor the
source-only recovery grants publication, P1R usability or Story 6.1 readiness.
