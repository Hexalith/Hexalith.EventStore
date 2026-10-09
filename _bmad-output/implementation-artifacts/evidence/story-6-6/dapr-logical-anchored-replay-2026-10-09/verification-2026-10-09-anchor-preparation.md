# Dormant logical anchor preparation — 2026-10-09

This packet qualifies a local M4 dependency: a distinct anchor selection and
covered-history genesis schema, the actual AggregateActor's first paired logical
snapshot save/readback boundary, and private canonical initial-state adoption.
It supplies no anchored signed prefix, committed replay progress, tail, terminal
or command proof. Existing event-only v1 decoding/claims, absent prefix tags
0d–0f and legacy snapshot storage remain intact. Parent tasks, acceptance and
O01–O20 stay open; production registration remains unavailable.

The [companion model](../../../story-6-6-dapr-logical-anchored-replay-model.md)
records the separate schema and controls before codec implementation. Seven
selection/seed vectors in [vectors.json](vectors.json) were generated independently
with Python struct/hashlib before production parity controls. They cover exact
selection, state changes, eligible head zero-tail/MAX arithmetic and three
covered-history seeds. Strict decoding rejects unsupported model/scope/arithmetic,
invalid UTF-8, tag/order, truncation and trailing bytes. The writer measures
actual separator/field bytes and charges the shared budget before allocation.
These vectors establish framing, not authenticity or replay authority.

Only the actual AggregateActor stages the separate canonical snapshot/witness
pair and calls its literal SaveStateAsync boundary; no helper saves. The issuer
pins its actual StateManager, actor identity, fixed source, reconstruction,
serializer and full-history completed origin. The supplied common serialized
owner fence must complete exactly one original-token decision. First issuance
requires an absent prior pair; an exact desired pair is idempotent with zero
staging/save. Any nonidentical, torn or malformed prior pair holds without
overwrite. Source/origin currentness and private SDK-array pins surround
individual callbacks, stages and actual awaits.

Fresh independent cache discard and exact paired readback classify Proven,
NoCommit or Indeterminate; save acknowledgement cannot establish the outcome.
Uncertain staging/cache/readback keeps private arrays charged and pending until
reconciliation, which never automatically repeats save. Failed cache release
sets the actor's existing unsafe-cache barrier, blocking ordinary actor work
until discard succeeds. Conclusive independent readback can release pending
ownership after serving authority expires; fresh authority remains required to
release its result. Cancellation withholds output, clears releasable private
capacity and preserves committed durable bytes. This is recoverable logical
readback between separate owners, not cross-actor atomicity or live SDK assurance.

Private adoption requires the exact candidate, requested source, parent budget,
current registry/reconstruction and originating token. It retains canonical
bytes, exact selection and three distinct seeds; rechecks the actual pair/origin
across yielding fences; and expires borrowed codec facades before awaits.
Refusal zeroes full private capacity, drops references and restores charges.
Adoption creates no operation, applies no tail, advances no page and invokes no
processor. Conservative issuer workspace refuses a configured 64 MiB maximum
under the 128 MiB parent budget before origin/materialization callbacks; this
configuration is a ceiling, not a claim that such a candidate fits.

## Verification scope

| Lane | Successful controls | Compiling mutants | Sealed command scope |
| --- | --- | --- | --- |
| [Final Debug/source anchor](final-debug/summary.json) | 56 anchor + 16 Client model + 66 snapshot + 53 reconstruction = 191 | 18/18 killed | 88 sequential commands; dynamic copied/helper/DLL and root sets unchanged |
| [Final Release/packages anchor](final-release/summary.json) | 191 | 18/18 killed | 88 sequential commands; copied/helper/DLL sets unchanged; one external root-test drift |
| [Established Release/packages snapshot](established-snapshot/summary.json) | 66 snapshot + 16 Client model + 53 reconstruction = 135 | 16/16 killed | 38 sequential commands; copied/helper/DLL sets unchanged; three external root paths drifted |
| [Independent vectors, style and CI](narrow-checks.json) | Seven vectors; owned Python syntax, LF/type/diff checks; actionlint | — | Exact commands/results and file scope retained |

Positive controls report errors, failed, skipped and not-run zero. Builds use
-warnaserror and finish with zero warnings/errors. Every negative lane compiles
and reaches its named refusal, byte/charge, callback, save or liveness assertion
within the 60-second combined build/execution limit. All lanes ran sequentially.
The final decision-completion mutant fails `refusedBeforeReadRelease` after
bounded cleanup; it is not a timeout kill or exception-message match. The runner's
console `tests: 66` refers to the existing snapshot class; structured controls
correctly record all 56 new cases and the full 191-control total.

Readable summaries and compressed complete receipts retain every command, UTC
time, transcript, dynamic before/after path set and SHA-256 bytes for copied
inputs, imported helpers/config/build metadata and each executed DLL directory.
New anchor copies preserve all external runtime source with zero exclusions or
source substitutions. [Source inputs](source-inputs.json) bind restored copied
runtime, governing .editorconfig/.gitattributes/nuget.config/global.json/build
imports and fixtures. Executed DLL hashes are retained per command; intermediate
DLL bytes overwritten by later mutation builds are not archived.

The Release root set drifted only in external
`tests/Hexalith.EventStore.Client.Tests/Streams/AuthoritativeEventStreamReaderTests.cs`
(`f6783589274274544e97c66cbf93d4da08c2a14b28202535bad4922c42a62a84`
to `c091ecf8e4e114daa94b71b77207b75524f5350eca37d985149b111819d21d69`).
The established snapshot lane later observed external changes only in
`src/Hexalith.EventStore.Client/Streams/AuthoritativeEventStreamReader.cs`,
`src/Hexalith.EventStore.Server/Streams/DaprSourcePublicationWriterRegistration.cs`
and `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorPublicationRegistrationTests.cs`;
its summary retains full before/after hashes. No owned runtime input drifted.
These qualify execution-time private copies, not a stable whole workspace. The
established snapshot copy retains its explicit 37 external Security exclusions
and required dependencies, with zero substitutions. Historical earlier receipts
retain their original narrower meaning; no fixed-path seal is upgraded.

[Earlier attempts](earlier-attempts/README.md) preserve passing draft root/focused
checks, a missing-vector copy failure, a noncompiling CA2007 mutation definition
and an unbounded liveness timeout. Failed attempts are unqualified; timeout is
never counted as a kill. The repaired finite control releases its read barrier
and awaits cleanup before asserting early refusal. Both final lanes kill the
compiling completion mutant directly.

Aspire startup/describe/stop completed before edits. The [sanitized baseline](aspire-baseline.json)
preserves the 30 resources' state/health and exact raw local log hashes, excluding
dashboard login material, URLs, properties, environment and health-report detail.
This is inspection, not serving qualification. No AppHost/build/test remains
active in this slice at handoff.

The root agent owns the required subsequent full-workspace Release/package
solution gate and affected root regressions; this packet does not claim it
passed. [Owned inventory](owned-files.json), [source snapshot](source-snapshot.json)
and [seal](seal.json) bind final owned bytes, model, Code Map and retained evidence.
Frozen intent SHA remains de1751b9887e48f8302be092797eba2ab5bbb2595958dab3d7325bd380cf548d.

## Unfinished local dependency and separate authority

The next executable local M4 dependency is matching distinct anchored
prefix/trust, actual operation genesis/ledger/transcript/terminal consumers, and
composed source-tail/zero-tail/full-replay/retry controls. Existing replay still
starts at one. Eligible older-snapshot replacement is unfinished; nonidentical
prior pairs hold. Checkpoint owner/readback, actual rebase equivalence,
protected snapshot adaptation, public reconstructor/actor routing, query HTTP
reader ownership/complete encoded transport capacity, public named-store/full-key
guards, safe historical cache eviction and bounded named-generation/root
producers also remain local implementation work. The selected model authorizes
these dormant choices; production-key/profile authority is not needed to continue
local implementation.

Production-only qualification separately lacks authoritative catalog/domain/state
declarations, serving keys and ratified profile, exact immutable execution and
actual common-owner serialization, Dapr cache/materialization/component/topology/
fleet observations and activation evidence. The supplied fence must serialize all
participating source/operation reads/writes and await one original-token decision.
Fixtures cannot prove live cross-owner atomicity, activation/destruction cleanup,
historical provider assurance, transport-reader lifetime or production cost.
Unsupported composition and serving remain refused/dormant. This prerequisite
closes no story, parent task or O obligation.
