# Bounded captured owner identity — final source qualification

**Required capability matrix: 74/74 passed.** All six Dapr process scenarios passed, including
both production dispatcher interfaces at 10,001 and 32,769 events, growth and repeated rebuild.
**The complete regression gate remains failed:** six baseline Server failures and one unchanged
Domain process-sweep test failure. The latter passed its isolated retry. The final packet correctly
retains `qualified: false`; the full-run failures were preserved.

This is the separate EventStore-owned prerequisite for Folders Story 12.2. The source candidate
is based on `ae917d3e4dbab20b8982c5222a13611c3e3faf35` plus the captured uncommitted sources.
No package/release or Folders acceptance is claimed.

## Exact final evidence

- [Results and physical maxima](run-20261021T152119Z/results.json)
- [Exact commands, environments, exit codes and log paths](run-20261021T152119Z/commands.json)
- [All 74 mandatory cases and individual results](run-20261021T152119Z/matrix-results.json)
- [Source SHA-256 inventory](run-20261021T152119Z/sources.json), [PE/PDB SHA-256 inventory](run-20261021T152119Z/binaries.json), [all packet hashes](run-20261021T152119Z/packet-sha256.json)
- [Loaded module/PDB/source preflight](run-20261021T152119Z/runtime-binding-preflight.json)
- [Independent post-review acceptance audit](parent-acceptance-audit-postreview.json)
- [Detailed comparison of baseline Server failures](diagnostics/broad-server-failure-baseline-audit.json)
- [Isolated process-sweep retry command/result](diagnostics/process-sweep-isolated-20261021T164359Z/command.json)
- [Completion document hashes](completion-sha256.json)

All four Debug/source builds passed with `-m:1 -p:UseHexalithProjectReferences=true`.
The final packet's **108 files, 3,654 current sources and 86 PE/PDB files** passed an independent
hash audit. Workers match loaded MVIDs to exact PE files, PE/PDB identities, and portable-PDB
document checksums to source bytes, including captured embedded generated sources. Each build
graph is retained separately. Raw/filtered Git hashes also match for all 76 archived generated
source files, including 32 CRLF files, under the narrow exact-byte evidence attribute.

| Complete suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Client | 1,936 | 0 | 2 |
| DomainService | 1,104 | 1 | 0 |
| Server | 6,218 | 6 | 25 |
| Dapr process scenarios | 6 | 0 | 0 |

Client skips are the two existing isolated-Dapr opt-in cases. Server skips are existing DW1 ATDD
red-phase cases; raw XML preserves their reasons. **No required matrix case was skipped.**
All 33 bounded-owner Domain cases and all seven producer/roster boundary cases passed.

The Server failures are the historical tracked-content secret scan (139 findings across 123
unchanged paths), one corrupt-manifest test setup, and four working-memory test setups invoking
the unchanged manifest fingerprint parser with invalid/incomplete JSON. The Domain failure is
`ProcessSweepRenewalTests.SweepRenewalFaultPreservesCompletedSourceObservation`: its synthetic
fault did not fire before its assertion. That exact test passed a single isolated retry against
the same binaries, without rebuilding. The post-review audit confirms zero diff from the baseline
for all 143 implicated code/evidence paths. No unrelated code, test expectations or historical
evidence were changed; neither the retry nor this report clears a failed full-suite result.

## Process replacement and measured bounds

| Original history / input contract | Stage PID | Interrupted promotion PID | Resume PID |
| --- | ---: | ---: | ---: |
| 10,001 / streamed | 87785 | 84152 | 86181 |
| 32,769 / streamed | 88656 | 73634 | 76488 |
| 10,001 / buffered | 57071 | 59927 | 62056 |
| 32,769 / buffered | 7615 | 63657 | 70016 |
| 129 / streamed canary | 96864 | 434 | 1035 |
| 129 / buffered canary | 5578 | 9021 | 9260 |

Each scenario uses two original owners and three distinct OS processes. The first exits after a
staging participant is installed with a prepared marker, before staging is sealed or returns.
The next resumes staging, seeds owner A, then exits after owner B's epoch participant is installed
while its checkpoint remains absent. Evidence proves the handoff fence returns Backpressure and
does not mutate the pending epoch. The replacement observes and recovers this partial handoff,
verifies complete views/checkpoints, historical duplicates and durable conflicts, appends an event,
and repeats the full rebuild. Original persisted source pages are checked unchanged. Per-phase
JSON and logs reside in the final packet's `runtime/` directory.

| Observed maximum | Bytes / operations |
| --- | ---: |
| Physical value / canonical batch | 110,556 bytes |
| Identity value | 12,302 bytes |
| Checkpoint value | 1,696 bytes |
| Owner manifest value | 1,258 bytes |
| Batch operations | 68 |

The protocol limits remain **100 operations / 1,048,576 canonical batch bytes**. `PhysicalWrites`
in results is the largest worker-phase count, not a run-wide sum. Commands and worker logs retain
elapsed times; these results do not establish a production latency SLA.

Three independent review layers returned 14 findings, all triaged in the story spec. Corrections
cover legacy receipt replay, preservation of paged proof during legacy retries, reclamation of
aborted page inventories, exact evidence bytes, buffered negative/abort checks, pending-write
reconciliation and precise process interruptions. No owned finding remains open; none was deferred.
Focused correction results and failed canary attempts are retained in
`diagnostics/review-corrections-20261021T151807Z/`.

Earlier run directories and `parent-acceptance-audit.json` are preserved pre-review/diagnostic
evidence, superseded by the final packet and post-review audit. The initial Aspire baseline was
captured after implementation began and is explicitly labelled late. Its isolated local host was
stopped; unrelated applications were untouched.

## Supported Folders consumption

Use this exact source candidate through the existing source-reference graph. From the Folders root,
the supported build instruction is below; it was **not executed as Folders acceptance** in this story:

```bash
dotnet build src/Hexalith.Folders.EventStore/Hexalith.Folders.EventStore.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
```

Keep original owner IDs, complete histories, owner sequences and global positions. Use existing SDK
`AccumulatePart` transport (at most 256 events/two MiB), then `AccumulateComplete` with the complete
fingerprint. Configure `ProjectionDispatchOptions.MaxRebuildEventCount` for the admitted history;
**40,000** admits the 32,770-event qualification. Do not truncate events or split an original owner.

The existing Folders `IAsyncDomainSharedProjectionStreamingRebuildHandler` is supported. Its admitted
owner input is buffered during folding; the SDK bounds retained identity values and checkpoint
handoff. `IAsyncDomainSharedProjectionStreamedOwnerHandler` also streams input without materializing
the entire owner. No Folders persistence protocol or identity-index implementation is required.

Upgrade every reader, writer and maintenance process before writing paged checkpoints. New readers
support old inline checkpoints and interrupted inline manifests. Old readers cannot safely consume
paged checkpoints. After the first paged write, rollback requires compatible readers or coordinated
restore of the complete pre-upgrade epoch/checkpoint/proof state with writers drained. Retain identity
proof and batch metadata with checkpoints; selected/inherited evidence has no automatic TTL. Abort
reclaims nodes, message indexes and page inventories while retaining its reservation fence; begin
retirement before opening another epoch. See the [SDK authoring guide](../../../../docs/guides/named-projection-authoring.md).

For a fresh qualification, run `python3 tools/qualify-bounded-owner-identity.py` from EventStore.
The driver preserves broad-suite failures and returns nonzero when any gate fails. Package
qualification, Folders regression/acceptance, dependency pins, publication and deployment remain
separate work. No staging, commit, push, publication, deployment or pin update was performed.
