# Story 6.6: Local command-state envelope admission

Date: 2026-10-07. Scope: compatible local M4 preparation, not completed M4,
authenticated source admission, consumer activation or production qualification.

The implementation spec and all three frontmatter context inputs were read before
editing. The canonical workflow baseline remains
`1329b35e52852952ecb2c94aabf100674e9691e3`; this pass started at
`4b1377a755636ec1eb0844de5901f0c73626ca59`. Frozen intent, in-progress status, all M1–M8 task dispositions and
V2/proof-dependent fences remain unchanged.

## Local implementation

`DomainProcessorStateRehydrator` now uses one disposable
`LegacyCommandReplayInput` for contract-envelope histories. It snapshots source
references before copying payloads, refuses oversized declared counts before
enumeration/list allocation, and stops uncounted enumeration incrementally.
Nested snapshot envelope sequences share the same totals. Empty nested wrappers
are charged and limited to 64 before further recursion, returning the existing
`LegacyArrayLimit` outcome.

Admission counts the 100,000-event ceiling, 8,192 bytes per yielded event,
64 MiB readable-source ceiling and separate 256 MiB legacy-array ceiling. Source
payloads and private copies count together. The declared list capacity is charged
even if a collection reports more entries than it yields. The complete encoded
metadata/extensions image with empty payload is capped at 512 KiB, using default
Web JSON escaping. Retained strings and conservative distinct source/copied
dictionary container capacity also count, including 256 bytes per extension
entry. These are conservative local admission charges, not a measured bound on
application state graphs or serializer/converter working allocations.

Historical extension maps are preserved instead of imposing A3's new-writer
64-entry/256-byte-key/4,096-byte-value/256-KiB-map declarations on retained
legacy reader input. Their actual values and container charges remain bounded by
the local metadata/array admission. This is not A3 producer or raw-ingress
qualification. Invalid Unicode, metadata overflow and aggregate capacity
overflow refuse before converters, state construction or Apply.

Every copied envelope payload is cleared through the reconstruction owner's
`using` lifetime, including success, admission refusal, converter failure and
cancellation. Cancellation retains the originating token. Private payloads are
not returned as source authority. Direct typed-state identity and existing JSON
compatibility behavior are preserved; no public constructor, wire shape,
registration or activation fence changes.

## Verification

Exact commands, output and exit codes are retained in
[the execution directory](command-state-envelope-admission-2026-10-07/).
[The source manifest](command-state-envelope-admission-2026-10-07/source-manifest.json)
identifies owned source/tests and the built Debug test artifacts. It excludes
concurrent external Server security-test and architecture-review changes.

- `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed, zero warnings/errors.
- Built Client assembly with `-class Hexalith.EventStore.Client.Tests.Handlers.LegacyCommandReplayInputTests` — 22 passed, zero failures/skips.
- Full built Client assembly — 1,205 passed, zero failures/skips, including direct typed-state, JSON and aggregate compatibility tests.
- `dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed, zero warnings/errors.
- Full built DomainService assembly — 495 passed, zero failures/skips.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` — passed, zero warnings/errors.
- Historical `6-5-integration/verify.py --mutations` and current `scripts/verify-event-evolution.py --mutations` — passed as separate, non-activating checks.
- `python3 scripts/check-deferred-work.py --json` — exit zero; its 406 unclassified diagnostics establish no closure.
- Owned-source `git diff --check` — passed. New source and Markdown use LF under the tracked `.gitattributes` rules.

Controls include early count refusal without enumeration or state construction,
bounded uncounted enumeration, later oversized payload/escaped metadata refusal,
shared nested payload totals, empty-wrapper depth refusal, composed extension
dictionary charges, independent Web JSON metadata boundary checks at
512 KiB−1/512 KiB/512 KiB+1, private-copy clearing and caller-byte preservation.
An early event converter mutates both the caller's later payload and collection
reference; reconstruction still consumes the original private value. That
control exercises the copy boundary before successor deserialization, unlike a
state-constructor-only mutation. Converter cancellation reaches no state
constructor or Apply and retains the original token. Clearing is inspected on
the owner directly; production lifecycle coverage comes from the rehydrator's
`using` scope and successful/failing/cancelled reconstruction routes, not a heap
or allocator instrumentation claim.

Before edits, the repository Aspire skill started the AppHost, inspected resources
with `aspire describe`, and `aspire wait eventstore --timeout 10` observed a healthy
EventStore resource. `aspire stop` then stopped the owned AppHost before builds.
This baseline observation supplies no live evolution, profile or recovery proof.

## Unresolved requirements and negative evidence

Raw `JsonElement` cloning, snapshot-aware transport deserialization and base64
materialization still occur through the existing compatibility adapters without
the required complete pre-materialization admission. Decoded contract envelopes
are subject to the new owner once they reach it; that cannot retroactively bound
the earlier JSON allocations. Arbitrary typed events and standalone typed state
graphs also have no registered working-state bound or authenticated source
proof. Addressed prefix, source/serializer/continuation authority and private
paged-session integration remain required before complete M4 qualification.

Caller-owned typed snapshots are still aliases on snapshot-plus-tail replay.
The [standalone diagnostic inputs](command-state-envelope-admission-2026-10-07/snapshot-probe/)
and [observed output](command-state-envelope-admission-2026-10-07/snapshot-alias-probe.log)
exercise the public `DomainProcessorBase` route: Apply at sequence 6 adds one;
Apply at sequence 7 adds two and throws. Command handling is never called, but
the supplied snapshot has `Total == 3` after failure, instead of its original
zero. The diagnostic exits zero only to confirm reproduction; this is negative
last-good isolation evidence, not a passing safety requirement. Its first build
failed repository analyzers (missing null validation and ConfigureAwait), was
corrected, and that initial output is retained separately.

Repairing the snapshot alias while preserving standalone typed-state identity
needs the admitted private state/serializer contract. This pass does not invent
an unbounded serialization clone or silently change legacy typed-state behavior.
Complete authoritative catalogs, transitive/framework/native immutable execution
binding, qualified observations, production-profile authority, all consumer
integrations and Dapr/fleet/package/API acceptance remain open. No M1–M8 task or
O-row closes from these local results.
