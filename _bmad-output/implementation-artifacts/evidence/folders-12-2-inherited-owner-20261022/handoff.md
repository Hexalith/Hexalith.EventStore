# Folders Story 12.2 Task 36: EventStore correction and consumer handoff

The EventStore inherited-owner duplicate correction is implemented and qualified
in this uncommitted worktree. Folders consumer requalification remains outstanding.
No Folders source, dependency pins or prior evidence was changed. Nothing was
staged, committed, pushed or published.

## Correction

`SharedProjectionEpochCoordinator.JournalAsync` now checks an omitted owner's
retained checkpoint and exact captured identity evidence before applying the
next-sequence rule. An unchanged inherited original returns `AlreadyCaptured`;
the ordinary named dispatcher returns `AlreadyCompleted` without folding again.
The current capture map remains the inventory of the new rebuild, not a replacement
for retained historical identity proof.

The added path applies to copy-on-write inheritance. It authenticates the original
scope/source binding and bounded proof page, compares the original source,
position, owner sequence and canonical payload, and rereads the epoch ETag before
acknowledgement. A changed fence retries through the lease/phase checks. Paged
proof must belong to the inherited lineage: its epoch must be at least the
copy-on-write anchor and no later than the selected generation. Inline legacy
proof has no epoch and is accepted on this path only when the anchor is zero.
Replacement generations and unprovable migration history remain refused.

Missing references on retained checkpoints, missing/retired inventories, missing
nodes, corrupt nodes, wrong-source bindings, altered bytes/messages/positions/
sequences, future proofs, stale leases, source gaps and CAS races remain refused.
Ordinary growth preserves the captured prefix. An aborted rebuild preserves the
selected inherited proof. There is no public API or persistence-format change.

## Exact source and binary graph

Baseline commit: `a964048c3f90a53f8cd306e864f31df0bfc4d021`.
This commit alone does **not** include the correction. The modified coordinator
source SHA-256 is
`23f82c1898318e7d9a1fc0e6892586d210a5d48debab58087f2c9c2176b1cbe9`.

[source-binary-identities.json](source-binary-identities.json) records 3,744 source
and project inputs, 100 DLL/PDB/configuration identities from the three test output
graphs, root configuration hashes and dependency revisions. The tracked source
delta is [final-source.patch](final-source.patch); the two new test source files
remain in `tests/` and are separately byte-bound in that manifest.

| Loaded module | SHA-256 | MVID |
| --- | --- | --- |
| Client | `2BDF48488D2B4B707D4D3A5D7E016B95774F0A44648D2E0F92385D00F67F8D91` | `358a38d8-194f-4f4b-8a05-1dc46bff2209` |
| DomainService | `D1537FB20CAEBFD89DA0E2CF50BE4C2D6A7F4F802500B7B1D4C48ED6ED286565` | `fea0346e-16dd-46d0-b082-48376043137a` |
| Server | `57F0B929996C4376A1A6BAB639759ED1F97C95474D1E4A6A3C635F5F3B0CE957` | `3684f231-b263-4b0d-8e08-6420b7a52bd2` |
| LiveSidecar.Tests | `B109F5FDF32804BA45B5E8A9FE5E740D342E2215E843BF521A129A2EA8742C3B` | `c8831d76-13e8-4ba6-9ac5-1a3f195199f8` |

[final-live-summary.json](final-live-summary.json) binds the loaded images, portable
PDBs and PDB document checksums to actual source bytes. All 18 final worker
processes loaded the same four-module graph. [final-tiny-binding.json](final-tiny-binding.json)
binds the retained Folders handler/test binaries to this corrected Client and
DomainService graph; their Client hash matches the final live and Client suites.
This tiny handler reproduction is not a fresh Folders host qualification.

## Commands and results

All commands ran from `/home/administrator/projects/hexalith/eventstore`. For each
label below, its `.json` retains exact argv, working directory, UTC start, elapsed
time and exit code; `.log` retains complete output and `.xml` the xUnit result.
The builds use Debug, `UseHexalithProjectReferences=true`, the pinned .NET SDK
10.0.402 and individual test projects/assemblies.

| Evidence label | Result |
| --- | --- |
| `tiny-red-executed` | Expected failing acceptance assertion, exit 134: original replay changes from `AlreadyCaptured`/`AlreadyCompleted` to `IdentityConflict`/`delivery_identity_conflict`; callback delta zero. |
| `client-red-omitted-owner` | Expected failing SDK regression, 1 failure: unchanged omitted owner returned `IdentityConflict`. Exact original test source and DLL/PDB identities retained. |
| `client-migration-red` | Expected 2 failing regressions: an older, discarded owner's proof was incorrectly acknowledged after migration. |
| `client-final-build`, `final-domain-build`, `final-live-build` | Successful Debug/source builds; zero warnings and errors. |
| `client-final-focused` | 359 passed; zero failures, errors or skips, including all 21 new inherited-owner rows and existing bounds/CAS/epoch regressions. |
| `client-final-full` | 2,073 passed, zero failures/errors, 2 preexisting skips: `PendingBatchRecoveryLiveTests.RestartRecoversInterruptedConditionalBatchAndPreservesLaterSharedIndex` false/true, requiring isolated Dapr endpoints. These skips are not live acceptance. |
| `final-domain-full` | 1,164 passed; zero failures, errors or skips. Includes all four new buffered/streamed and registry-presence named-dispatch variants. |
| `final-tiny` | Exit 0; real retained Folders handler, named rebuild and ordinary dispatch pass without/with the registered `FolderCreated` evolution registry and Web JSON round trip. Before/after canonical delivery and checkpoint bytes agree; direct replay remains `AlreadyCaptured`, named replay remains `AlreadyCompleted`, callback delta zero. No actual upcaster is claimed. |
| `final-live-qualification` | 5 passed; zero failures, errors or skips. Real Dapr/Redis and distinct OS processes, described below. |
| `final-diff-check` | Exit 0. Git reports the existing CRLF-to-LF normalization notice for the Client test class; no whitespace error. |

The earliest tiny build attempt inherited repository analyzers and failed to
compile; it was rerun from an isolated temporary project. The first corrected
tiny graph omitted a matching Contracts image and failed to load; the full
current SDK graph corrected it. Early Client fixture/cleanup assertions were
corrected before acceptance. The first live launch exhausted host filesystem
watchers; the supported fixture settings
`EVENTSTORE_TEST_DAPR_HOT_RELOAD=false` and
`DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false` removed the environmental blocker.
All attempts remain retained; final acceptance uses only the final labels above.

## Persisted Dapr and process recovery

The final run used daprd 1.18.7 (CLI 1.18.2), Dapr's Redis state component and an
isolated Redis 6 container on port 62859. The actual daprd executable SHA-256 is
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.
[runtime-processes.json](runtime-processes.json) retains the exact launch arguments,
executable hashes and materialized component/runtime configuration. The Redis
image is `sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf`.
Application state was written/read through the production Dapr batch protocol.

| Original A count / contract / mode | Worker PIDs in execution order |
| --- | --- |
| 129 / buffered / inherited | 66100, 60180, 60539, 57673 |
| 129 / streamed / inherited | 57468, 52848, 53134, 50610 |
| 10,001 / buffered / inherited | 82369, 24366, 25490, 66736 |
| 129 / buffered / replacement | 89184, 89747, 86953 |
| 129 / streamed / replacement | 84500, 84734, 82463 |

Stage workers exit abruptly with code 73 after installing a real durable
participant while its marker is Prepared. Promotion workers exit with the first
owner seeded and the second owner/epoch handoff partially installed. A distinct
resume worker observes the raw pending envelope, proves the ordinary-delivery
fence, completes the original operation, verifies both persisted owners, delivers
ordinary growth and rebuilds the grown history. In inherited cases it then
promotes only B and exits. A fourth process proves A is absent from the current
capture, its retained proof belongs to an older selected generation, and exact
original replay succeeds through both public JournalAsync and ordinary named
dispatch. Raw checkpoint bytes and selected rows remain unchanged; altered
original message identity is refused. The large case checks A sequences 1,
5,000, 10,001 and the grown 10,002.

Every observed batch remained within 100 operations/1,048,576 canonical bytes;
maximum observed operations were **68**, canonical batch bytes **109,638**,
physical value bytes **109,638**, identity value bytes **12,294**, checkpoint
bytes **1,696**. Retained JSON includes raw checkpoint evidence and epoch state.

## Cleanup and consumer requalification

The startup Aspire baseline was inspected and stopped successfully before builds
(`aspire-state.*`, `aspire-stop.*`). Final test fixtures disposed their sidecar and
host. All final worker/parent/sidecar PIDs were checked; no owned processes remain.
The fixture component directory and owned temporary tiny-project directory were
removed. The disposable Redis container
`d943cc8e7cef9cae17d83053d0a4af6961c6ddc16239cf8edc578f4727b7651f`
was identity-checked and removed with volumes; subsequent inspection proves
absence. Shared Dapr infrastructure was left unchanged. See [cleanup.json](cleanup.json)
and `redis-cleanup.*`. Source and Debug outputs remain available for requalification.
An unrelated `.mcp.json` change appeared during the session and was left untouched.

The Folders qualifier should consume this exact source delta through its existing
project-reference seam, rebuild its actual host/test graph, and retain loaded
DLL/PDB hashes and MVIDs. Re-run the authenticated 10,001-position Task 36 case:
stage, replace the managed host processes, resume/verify the same session, promote
B without A, then replay A's unchanged original and prove `AlreadyCompleted`
without invoking the fold. Compare pre/post checkpoint/reference and canonical
delivery hashes. Include original-owner growth and altered/missing referenced
proof controls. Do not equate the frozen handler tiny reproduction with that
consumer host requalification, especially the previously different DomainService
copies described in the original handoff.

Internal actor HTTP429/admission remains a separate unresolved issue. This work
does not close Folders Task 36, package/PD10, SM4, whole-story review or release
gates. Independent review found no remaining introduced defects; two preexisting
issues (buffered copy-on-write staging and arbitrary whole-checkpoint loss) are
recorded in [review.md](review.md) and appended to the EventStore deferred ledger.
Referenced identity proof absence/corruption is covered; recovery after deleting
the entire authoritative checkpoint is not claimed.
