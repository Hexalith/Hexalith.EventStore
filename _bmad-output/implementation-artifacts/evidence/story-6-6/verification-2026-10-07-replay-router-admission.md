# Local replay router admission — 2026-10-07

This run implements a local M4 prerequisite. Independently registered synchronous
and asynchronous legacy replay handlers now receive a bounded, privately copied,
contiguous prefix before service resolution or ownership callbacks. Replay
selection is independent of command processor registration. The built-in route
borrows the admitted input; derived aggregates' explicit public replay interface
implementations retain their existing dispatch. Original cancellation tokens are
checked after source count/index access, admission, ownership and handler calls.

The private input exposes a read-only list view. A handler can mutate its private
payload bytes, but cannot replace a list slot with a caller-owned payload and
change which bytes disposal clears. Actual-router tests reproduce this ownership
boundary, explicit derived-interface dispatch and cancellation from the caller's
`Count` getter before an oversized-count refusal.

The spec's canonical baseline remains
`1329b35e52852952ecb2c94aabf100674e9691e3`. The observed repository HEAD is
`40c92e085d8a6463d469c1b340c410fec84a690f`. Concurrent UX changes and the
`references/Hexalith.Memories` and `references/Hexalith.Platform` worktrees were
preserved. No staging, commit, branch, dependency, deployment or publication
mutation was performed. The 14 owned runtime/test/script hashes, exact commands,
original `/tmp` log paths and retained copies are in
[commands-and-source.json](replay-router-admission-2026-10-07/commands-and-source.json).

## Observed verification

All commands below exited zero. Test counts are from the built xUnit assembly
runner, with zero failures, errors, skips or not-run tests.

| Lane | Result | Retained log |
| --- | --- | --- |
| DomainService Debug source build and full assembly | 0 warnings/errors; 517/517 | [build](replay-router-admission-2026-10-07/domainservice-build.log), [tests](replay-router-admission-2026-10-07/domainservice-full.log) |
| Client Debug source build and full assembly | 0 warnings/errors; 1,285/1,285 | [build](replay-router-admission-2026-10-07/client-build.log), [tests](replay-router-admission-2026-10-07/client-full.log) |
| Sample Debug source build and full assembly | 0 warnings/errors; 175/175 | [build](replay-router-admission-2026-10-07/sample-build.log), [tests](replay-router-admission-2026-10-07/sample-full.log) |
| Required Release package-mode solution build | 0 warnings/errors | [build](replay-router-admission-2026-10-07/release-build.log) |
| Standard local package build | All 14 manifest-owned packages produced | [pack](replay-router-admission-2026-10-07/local-package-pack.log) |
| Isolated package-only consumers | Contracts, Client and DomainService passed | [consumers](replay-router-admission-2026-10-07/package-consumers.log) |
| Historical approval preflight with mutations | Passed; prior approved digest preserved | [result](replay-router-admission-2026-10-07/historical-preflight.json) |
| Current-amendment preflight with mutations | Passed; 22 timed controls; O-01–O-20 open | [result](replay-router-admission-2026-10-07/current-preflight.json) |
| Deferred-work checker | Exit 0; 406 existing unclassified advisory entries | [result](replay-router-admission-2026-10-07/deferred.json) |

The Release command was exactly
`dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false`.
Packages used the existing local CI fixture version `999.0.0-ci-test`; none were
published. Package consumer checks are current package-only source consumers,
not qualification of every historical already-compiled consumer or a fleet.
Sample logs include expected negative security-fixture diagnostics; the runner
reported all 175 tests passed.

The baseline Aspire AppHost was inspected and stopped. The final
`aspire ps --non-interactive` reported no running AppHost. This baseline check
supplies no production or two-component capability qualification.

## Isolated mutation controls

`python3 scripts/verify-event-evolution-replay-routing.py --output /tmp/story-6-6-router-mutations-verified --timeout 60`
builds and tests isolated source copies. Each mutation lane has a combined
60-second build/test deadline. The unmutated control passed 22 tests. Every
mutated lane built successfully, then exited nonzero with its named killing test;
compilation failure is not accepted as a kill. The shared source tree is not
mutated. [Result and exact diagnostics](replay-router-admission-2026-10-07/mutations/result.json)
and [runner log](replay-router-admission-2026-10-07/isolated-mutations.log) are
retained with each lane's build/test logs in `replay-router-admission-2026-10-07/mutations/`.

| Removed guard | Named killing test | Observed failure |
| --- | --- | --- |
| Async private admission | `OversizedSourceRefusesBeforeReadsOrServiceResolution` | `InvalidOperationException`: deliberate provider sentinel rejects forbidden keyed service access |
| Async custom-interface dispatch | `ExplicitDerivedAsyncReplayKeepsCustomInterfaceDispatch` | `ShouldAssertException`: custom state result differs |
| Sync custom-interface dispatch | `ExplicitDerivedSynchronousReplayKeepsCustomInterfaceDispatch` | `ShouldAssertException`: custom state result differs |
| Complete prefix before async route selection | `IncompleteOrDuplicatePrefixRefusesBeforeOwnershipCallbacks` | `InvalidOperationException`: deliberate provider sentinel rejects forbidden keyed service access |
| Read-only input slots | `OwnershipMutationCannotReplacePrivateInputAndCompletionClearsCopies` | `ShouldAssertException`: mutable array exposed |

The provider-sentinel exceptions detect forbidden access; they are not assertion
failures. The other three kills are Shouldly assertions. Parent review reproduced
and resolved three concrete findings: inherited custom interface dispatch bypass,
mutable list-slot disposal ownership, and cancellation during count admission.
The parent found no further defect in this local runtime/test diff. That scoped
review does not cover every inherited change in the parent story.

## Cleanup and limits of this evidence

Tests retain the handler-visible private payloads and observe that disposal
clears them after handler success, thrown failure and post-handler cancellation.
They observe unchanged caller bytes, source mutation isolation, refusal of list
slot replacement, and cancellation during ownership with no handler invocation.
The `using` scope also covers ownership callback failure/cancellation and typed
route, prefix and version refusals. Private-byte clearing at those earlier
boundaries is inferred from that disposal scope; these tests do not install a
private-byte observer there.

`git diff --check` exited zero with no output. The parent observed
`git diff --cached --name-only` exiting zero with no paths. The required profile
check `test -f deploy/dapr/production-profile.yaml` exited **1 with no output**:
the production profile is absent. AD-26 profile ratification, authoritative
catalog/artifact/peer binding and required production/fleet evidence remain
external gates. This absence does not prevent dormant local preparation.

[The remaining-task inventory](replay-router-admission-2026-10-07/remaining-tasks.md)
names unfinished local implementation and each activation dependency separately.
No M1–M8 checkbox or O-row closes. V2 and unavailable proof-dependent operations
remain fenced; the parent story stays `in-progress`.
