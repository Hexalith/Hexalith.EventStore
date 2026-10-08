# Story 8.3 Post-review Verification — 2026-10-08

The Story 8.3 corrections and independent review are complete for the exact
source inventory below. Final focused verification passes. **Story closure is
blocked:** the required broad Release build fails, normal restore-based packing
has no completed current result, and concurrent owner work changed the live
compilation graph after the successful focused build. The spec remains
`in-review`; the sprint entry remains `in-progress`. No successor, package,
provider, deployment, or G5 approval is inferred.

This record supersedes current execution, count, source, and review claims in
[the pre-review record](verification-2026-10-08.md). Its original 292-case Debug
runs and package fallback remain historical evidence. Original `preflight.md`,
`verification.md`, AR-20260914-01, AR-20260914-02, the approved authority,
46 frozen Security files, fixtures/verifiers, solution, and release manifest
retain their original bytes. No commit, stage, push, branch, dependency update,
Server integration, Parties edit, or external mutation was performed here.

## Exact source and execution identities

The [binding receipt](verification-2026-10-08-postreview/binding.json) records
path-sorted SHA-256 inventories, all command arguments/environment overrides/
exit codes, receipt hashes, 307 final TRX result rows, evaluated build inputs,
compiled/runtime outputs, dependency HEADs/dirty content, and scope exclusions.
Stream hashes use `sha256sum` format with repository-relative paths, LF, two
spaces after each digest, and a final newline. Generated `bin/obj` content is
excluded from source streams and separately bound as build inputs/outputs.

- Checkout HEAD: `9542d3c9f48bf9ce1c57f2ef68904703eaba56cc` on `main`.
- Original workflow/approved baseline: `e8886ec4c277460de3d3208b3fc0b9c261c4967d`.
- Normative digest: `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.
- Complete authority: `542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82`.
- AR-20260914-01: `4b12f54fd24f62083760e9c6ef73ad57edb2c76d15f1fae07503bbe82ad9b068`.
- AR-20260914-02: `1d511941c09d12e1d3a09a82968fc82737dcd786b0b35082c75584b6e7358537`.

| Bound inventory | Files | SHA-256 stream |
| --- | ---: | --- |
| Tested Story 8.3 core/project | 35 | `3a657ad349bc2f738cd0c596d9c3829e53b690ed4ed5bf75ca2aa091563e9c5e` |
| Focused tests/project/vector manifest | 12 | `d22365a5ec94a87975d2069241eebc5cff864a4c98f694f801d6429f732e2b55` |
| Original approved Security inventory | 46 | `a01dc5576702f08dc0a95caaa8a4158e457c2f1d85dd4337fc145d126a02fa2e` |
| Security inventory at successful focused build | 53 | `53cdc3bf1bed8594e8d63fdc529975060fed3db96d7536f78048eea8ebe4e340` |

Every Story 8.3-owned source byte in the final binding matches the captured
before-build source input. The 72 focused compiled/runtime artifacts and 104
Contracts-test artifacts were unchanged between capture and final execution.
The source graph was captured before/after compilation and after testing;
`AggregateIdentity.cs`, root build/target/package/global configuration, imported
SDK/NuGet/MSBuild files, restored library identities, and root-declared
submodule revisions/dirty content are included. The later Contracts-test
capture follows its transitive project references as well. Query receipts
state each project, evaluated properties/items, and imported file hashes.
These are source/assembly bindings, not retrospective approval records.

The workflow hash is `c34ed9b0699fba6911f539c2ac5f0ce7c5d74a13104d15a765f849a15d05a4f3`;
the local-lane hash is `ed530650f4a9294392f27deb3d94dda3052db71a0524b76ea7468ec7ed46f036`; the required-lane/packaging
guard hash is `4109c0a277cb7e1b956fc2ee9321caac9f5aed7d761b3ee35d7604b378436811`.
Both focused lanes require **306** cases, fail on skips, and separately require
the invariant-globalization regression. The workflow guard validates existing
push-to-main and pull-request-to-main triggers and rejects four trigger mutations.

## Implementation and review disposition

Nine scoped source/test/CI files changed; the two evidence attribute rules are
the additional receipt configuration change. The writer computes the complete
selected-subtree node plan before copying selected plaintext or requesting
material, so a scalar's temporary expansion cannot reject a valid later
container contraction. Both readers recheck cancellation before existing
bounded authentication/cryptographic/format failure mappings and preserve
cancelled diagnostics. Manifest enumeration also checks cancellation after a
successful disposing iterator cancels the caller.

Diagnostics retain the activity created before starting it, preserve explicit
trace-parent identity without inherited baggage, retain the successful running
child until cleanup, and restore the prior ambient activity despite throwing
start, stop, or `CurrentChanged` callbacks. Metric callbacks remain independent
best-effort observers. No wire bytes, AAD fields, crypto primitive, public API,
provider, or registration surface changed.

All three independent layers reviewed the scoped baseline diff
`7dbd06b1c115fdb5d38abada33c98b50167a973173d485e5a6de087c16bcb766`.
It contained 687,868 bytes: 687.868 kB; the blind finding floor was
`min(floor(sqrt(687.868) + 1), 10) = 10`. Every finding received a separate
verdict before grouping in the spec's Review Triage Log: 10 blind, 2 edge,
2 verification-gap findings, all 14 resolved. No new deferral was added;
historical deferrals retain their existing owners. The independent pre-patch
[probe source](verification-2026-10-08-postreview/independent-review-probe.cs.txt)
is historical reproduction evidence, not a final-API executable.

Fourteen added core cases cover the exact node maximum, malformed-path disposal
cancellation, alternate snapshot reference/version 2, genuinely pending event
and snapshot resolvers, start/stop callback cleanup, ambient-restoration faults,
closed baggage/correlation, and authentication/format cleanup cancellation with
key clearing and cancelled metrics. Four additional Contracts cases mutate the
workflow triggers. The complete assigned vector set remains 51 identifiers
V001–V048/V135/V136/V138. V038/V039 policy work remains Story 8.5-owned; V138
remains observational. No independent snapshot golden or CAVP certification is
claimed beyond the existing frozen fixture ownership.

## Final verification

Exact commands, UTC times, environment overrides, and exits are in
[commands.json](verification-2026-10-08-postreview/commands.json). Raw outputs and
result rows are durable reviewable receipts; TRX byte streams use `.trx.xml`
names to avoid repository `*.trx` ignores. Two scoped receipt-directory rules
in `.gitattributes` disable Git text conversion, preserving raw bytes and
binding hashes across checkouts without changing source-file line rules.

| Check | Final result | Receipt |
| --- | --- | --- |
| Package-mode focused Release restore/build, XML docs and project-scoped AOT/trim analyzers | Exit 0, zero warnings/errors | [restore](verification-2026-10-08-postreview/release-restore.log), [build](verification-2026-10-08-postreview/release-build.log) |
| Focused Release full suite, floor 306 and fail-skips | 306/306; zero failure/error/skip/unrun | [log](verification-2026-10-08-postreview/release-tests.log), [TRX](verification-2026-10-08-postreview/release-results.trx.xml) |
| Invariant-globalization V029 | 1/1; zero failure/skip/unrun | [log](verification-2026-10-08-postreview/invariant-tests.log), [TRX](verification-2026-10-08-postreview/invariant-results.trx.xml) |
| Frozen v1/additive contract compatibility/API class | 31/31; zero errors/failures/skips/unrun | [log](verification-2026-10-08-postreview/contracts-compatibility-tests.log) |
| Required-lane and package guard class | 119/119; zero errors/failures/skips/unrun | [build](verification-2026-10-08-postreview/contracts-build.log), [log](verification-2026-10-08-postreview/contracts-packaging-tests.log) |
| Immutable Node and Python V001–V003 verifiers | Both PASS, exact frozen envelope/wrapper hashes | [Node](verification-2026-10-08-postreview/node-vectors.log), [Python](verification-2026-10-08-postreview/python-vectors.log) |
| Core/focused-test style | Both exit 0 | [core](verification-2026-10-08-postreview/core-style.log), [tests](verification-2026-10-08-postreview/tests-style.log) |
| Owned source structural/dependency/whitespace scan | PASS; 34 production and 10 test types; Allman statement braces, LF, docs, citations, no public core types/packages, non-packable, excluded from solution/14-package manifest | [scan](verification-2026-10-08-postreview/structural-check.log) |
| Actionlint and local shell syntax | Exit 0 | [actionlint](verification-2026-10-08-postreview/actionlint.log), [shell](verification-2026-10-08-postreview/shell-syntax.log) |

Targeted implementation verification passed 147/147 and the five trigger guard
cases passed 5/5. Its initial 147-case run exposed a pre-copy plan regression and
an overly exact callback-count assertion; both were corrected before final
source binding. Initial failures and final passing receipts remain saved and
are explicitly separate from final acceptance counts.

## Unmet gates and concurrent source boundaries

The required current broad Release command exited 1 with zero warnings and six
errors for absent `Hexalith.Tenants`, `TenantRole`, and `TenantStatus` in
`tests/Hexalith.EventStore.Server.Tests/Authorization/TenantsAuthorizationContractMappingTests.cs`:

```bash
dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1 -nodeReuse:false -p:UseHexalithProjectReferences=false
```

See [the exact final failure](verification-2026-10-08-postreview/solution-release-build.log).
It belongs outside this run's authorized Story 8.3 scope. The pre-review source
Debug solution failure and supplemental mixed Commons.UniqueIds build failure
remain disclosed in the pre-review record; no post-patch Source/Debug pass is
claimed. A successful focused build does not establish a successful solution
build or fresh dependency audit.

The optional whole-file Contracts guard style command exited 2:

```bash
dotnet format style tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --verify-no-changes --no-restore --severity info --include tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs
```

All 34 reported IDE1006/IDE0305 diagnostics are on unchanged existing lines;
[the comparison](verification-2026-10-08-postreview/guard-style-comparison.json)
verifies zero diagnostics in changed lines. The [raw failure](verification-2026-10-08-postreview/guard-style.log)
is preserved. Existing unrelated fields and release-test expressions were not
renamed or reformatted to hide it.

The normal shared restore-based pack attempts were interrupted before review;
no current full pack gate is established. The earlier cached-restore fallback
produced exactly 14 archives and passed both validators, with exact archive
hashes retained in the pre-review binding. This is inventory/exclusion evidence,
not package availability, provider readiness, or the Story 8.8 release transition.
The manifest still has SHA-256 `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae`.

Concurrent owner work added four core candidate helpers (including
`InteractionOccurrenceKeyDeriver.cs`) and 21 Security contracts after the successful focused compilation. At the final recorded
snapshot the complete core directory has 39 files (stream
`2e4617b537ae10d519710157e6adce4aa2f01466d8bca20d536dbedb39e9977f`) and Security has
74 files (stream `1ae09a59bcb2534ab3c629798690dd656647147907dc73eb1ee43538c89b6019`).
The binding lists every extra file separately. The 35-file Story 8.3 inventory, all 46 approved Security files, ten
Story 8.3 test C# files, and the captured executed binaries remain byte-stable.
Subsequently, another owner also added prototype test files and an NSubstitute
reference to the focused test project. The binding records this project drift
and preserves the exact tested project as `tested-focused-project.csproj.txt`.
The later complete test directory has 16 files, stream
`78a429288cf537befeceae352c80ff6ca644926a30a91721a6778e679fd70113`; four added prototype files
and the new dependency are excluded from this 306-case execution;
no post-change restore, build, or full-current-graph pass is claimed. Later additions and a subsequent restored-assets change are
preserved and excluded from Story 8.3 review/approval; the successful earlier
build is not represented as execution of the enlarged live graph. Other
concurrent work outside these directories is also preserved and unclaimed.

The `bmad-build` rendered review step requires: “if verification fails and the
failure cannot be fixed, HALT and escalate to the human.” The scope instruction
for this run excludes fixing these owner gates or approving concurrent work.
Accordingly the workflow stops before marking the spec done or advancing the
sprint to review. The next action is owner resolution of the exact verification
blockers and approval of a reproducible, content-bound Story 8.3 closure.

## Successor and G5 disposition

The next EventStore implementation owner is **Story 8.4, Compatibility Readers
and Mixed-History Routing**, executable only after approved Story 8.3 closure
and exact successor authorization. Story 8.5 is the parallel policy/key-lifecycle
owner under the same predecessor gate. No successor starts in this run.

**G5 remains CLOSED. Parties Story 8.7 remains BLOCKED.** Required later evidence
includes lifecycle/state/backend mechanics (8.5), the qualified production
provider and real isolated backend conformance (8.6), persisted Server behavior
(8.7), package-only provenance and release transition (8.8), Parties
dual-provider parity (8.9), rollback after real persisted v2 event/snapshot
writes (8.10), and the exact content/source/package/digest-bound Story 8.11
closure packet with named approvals. Earlier approval packets do not approve
new source bytes, concurrent candidate helpers, later packages, or G5.

The living-source recheck from the pre-review record still applies. For the
trace repair, the runtime-specific [.NET 10.0.12 Activity source](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Activity.cs)
and [ActivitySource source](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/ActivitySource.cs)
were checked: explicit parent identity avoids parent-object baggage inheritance,
and listener callbacks can interrupt the normal ambient restoration sequence.
The final regressions execute those repaired boundaries on runtime 10.0.12.
