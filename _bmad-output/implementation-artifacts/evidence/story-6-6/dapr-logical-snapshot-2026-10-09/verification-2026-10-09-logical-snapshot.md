# Dormant logical snapshot candidate dependency — 2026-10-09

This slice implements read-only private aggregate snapshot-candidate admission
under a distinct local model. It preserves event-only logical v1, including absent
prefix tags 0d–0f. It supplies no replay continuation, serving registration,
checkpoint/rebase issuer or production activation authority. The parent and all
M1–M8/O rows remain open.

The [model](../../../story-6-6-dapr-logical-snapshot-model.md) was recorded before
codec/vector implementation. Snapshot, checkpoint and rebase framing have separate
separators and exact ascending fields. Six independently generated Python
struct/hashlib vectors cover snapshot/MAX/state/generation changes and the two
other schemas. Runtime byte parity, strict UTF-8, negative/overflow/tag/order/
truncation/trailing refusals and exact measured capacity are directly controlled.
Checkpoint and rebase support ends at their codecs; their records cannot enter
aggregate snapshot intake or historical prefix decoding.

The actual completed operation owner captures only fresh full-history
reconstruction origins, with every ledger/response/state/final participant and
next-ordinal absence. Candidate admission captures a separate plaintext canonical
storage/witness pair from the same actual source actor, pins the source, registry,
reconstruction, serializer and completed origin, roundtrips the supplied state
codec, then re-reads the exact pair and history. A retained candidate keeps a
fresh actual-owner fence. Individual state callbacks preserve the originating
token and expire borrowed leases/writers before any asynchronous fence.

Malformed privately captured witness records, missing/stale/mismatched evidence,
unsupported protection and the specifically typed canonical-roundtrip mismatch
return full replay from one. Typed Dapr byte-array materialization JsonException,
application JsonException/IOException and infrastructure failures propagate.
Stored/borrowed bytes are preserved; the path never calls SnapshotManager,
Apply, SetStateAsync, SaveStateAsync or RemoveStateAsync. Timelines and below-head
zero-tail candidates always refuse; MAX is never incremented. These are candidate
arithmetic controls, not proof of snapshot tail cost.

All private copy/decode/graph work uses the same composed parent budget as the
captured origin. Origins from other budgets and command-bound operations refuse.
Configured maximum is a ceiling: the conservative paired-read workspace makes a
64 MiB configured maximum refuse before reads under the 128 MiB parent budget.
Constructed candidate/origin ownership survives the final actual await and token
check. Intermediate pair-return ownership likewise survives its last token
check. Refusal clears private capacities, drops references and restores charges;
borrowed actor/caller arrays remain unchanged.

## Verification scope

| Lane | Controls | Compiling mutants | Input scope |
| --- | --- | --- | --- |
| [Final Release/packages snapshot](final-release/summary.json) | 66 snapshot + 16 Client model + 53 reconstruction | 16/16 killed | 38 sequential commands; dynamic private/helper/DLL sets stable |
| [Current Debug/source pair cleanup](final-pair-debug/summary.json) | 66 + 16 + 53 | 1/1 killed | Dynamic private/helper/DLL and root sets stable |
| [Established Release/packages reconstruction](established-reconstruction/summary.json) | 95 source-only/replay/reconstruction | 18/18 killed | 38 sequential commands; recorded initial inventories stable, narrower seal limits retained |
| [Independent vectors and style/CI checks](narrow-checks.json) | Six vector images; Python compile, actionlint, owned diff/LF/type checks | — | Exact logs and declared scope retained |

Controls have errors/failed/skipped/not-run zero. Negative lanes compile, complete
within the 60-second combined build/execution limit and fail on their named
behavioral control. The new final-return cleanup kills observe captured private
bytes remaining nonzero or budget.LiveBytes remaining charged; they do not depend
on exception-message wording. Every snapshot lane builds with -warnaserror and
reports zero warnings/errors. All check lanes ran sequentially.

The final snapshot runner dynamically seals before/after **path sets and bytes**
for private source/configuration, imported scripts/build metadata and executed
DLLs. Its readable summary and complete compressed receipts retain all command
arguments, UTC times, hashes, exclusions and failures. The final root input set changed in exactly `src/Hexalith.EventStore.Client/Streams/SourcePublicationFeed.cs`, `tests/Hexalith.EventStore.Client.Tests/Streams/SourcePublicationFeedTests.cs`. No owned snapshot runtime input drifted. The copied runtime, imported helpers/configs and executed assembly sets remained unchanged; this qualifies the execution-time private snapshot only.

The disposable snapshot copies exclude the explicitly listed external untracked
Security files and retain exact required external dependencies. No tracked source
was substituted and no root analyzer was suppressed. These controls qualify the
recorded private copy, not the whole workspace. The established reconstruction
runner retains its historical narrower initial-path seals: additions/removals
outside those initial inventories are not proved absent. Its old receipts are
not upgraded to dynamic-set guarantees.

[Earlier attempts](earlier-attempts/README.md) retain the first owned nullable
compile failure, historical passing root/focused checks and successful earlier
mutation runs. Each retains its actual byte scope and exact external drift;
passing pre-return/pair-cleanup runs are not final-byte claims.

Aspire startup/describe/stop succeeded through the tracked AppHost before source
work. [Sanitized baseline](aspire-baseline.json) preserves 30 resource states:
security was Running/Unhealthy and dependent application/admin resources were
Waiting. This is baseline inspection, not serving qualification. Raw local
artifacts are bound by SHA-256; dashboard credentials/environment were omitted
from the packet.

The root agent owns the required subsequent full-workspace Release/package
solution build and affected root regressions. This packet does not claim that
current-workspace gate passed. Runtime/check ownership is transferred at handoff;
no AppHost/build/check remains active in this implementation slice.

## Unfinished local dependencies and authority

The next executable local M4 dependency is an explicit distinct anchor-aware
prefix/ledger/transcript selection, actor-owned same-save snapshot pair issuance
and canonical initial-state adoption, with exact tail/retry/full-replay controls.
Existing replay still starts at one. Checkpoint owner/readback and actual rebase
equivalence/re-witnessing remain implementation work. Protected v1 adaptation,
public reconstructor/actor routing and earlier query HTTP-reader ownership,
complete encoded transport capacity, safe historical cache eviction and bounded
named-generation producers also remain unfinished. These local choices are
already authorized under the selected model; no new production key/profile is
needed to keep their dormant implementation progressing.

Production-only qualification separately lacks authoritative catalogs/domain
and state declarations, serving keys and ratified profile, actual common-owner
serialization/SDK materialization/component/topology/fleet observations and
activation evidence. The supplied owner fence must serialize every participating
source/operation read/write, invoke exactly one original-token decision and await
completion before returning. Local serialized fixtures and current logical
readback cannot establish live cross-owner atomicity, historical provider
assurance, transport-reader lifetime or production cost. Unsupported composition
and serving registration remain dormant.

[Owned inventory](owned-files.json) binds only this slice's files; existing files
include earlier owned changes and their inventories retain that history.
[Source snapshot](source-snapshot.json) and [packet seal](seal.json) bind the final
owned bytes, model, spec handoff and retained evidence. Frozen human intent SHA
remains de1751b9887e48f8302be092797eba2ab5bbb2595958dab3d7325bd380cf548d.
