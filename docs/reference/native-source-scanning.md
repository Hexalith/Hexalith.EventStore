# Native source scanning

`AddEventStoreGatewayNativeSourceScanner(domain)` registers `INativeSourceScanner`
using a separate HTTP client and the actual workload issuer. A fresh
`INativeSourceScanFold<T>` receives contiguous source events on each of at most
three attempts. It must retain only bounded selected facts and perform no effects.
Only the EventStore client constructs a successful `NativeSourceScanResult<T>`.

Each request uses `POST /api/v1/streams/scan/workload`, operation
`eventstore:stream-scan-native`, exact tenant and domain assertion bindings,
and a thirty-second RSA proof over the operation and the complete page request.
The caller configures `Authentication:NativeSourceScan:PrivateKey`; the receiver
separately enrolls `Authentication:NativeSourceScan:PublicKeys:<caller>` and
`AllowedDomains:<caller>`. These keys are independent of `BoundStreamRead`.
The realm owner must enroll the operation for the actual tenant/domain pair.
Registration itself grants no authority. Human credentials and missing, altered,
foreign or expired assertion/proof inputs refuse before source invocation.
Existing `MetadataOnlyCallers=true` constraints on ordinary reads remain intact.

The scanner keeps one owned page (at most 255 positions and a 2 MiB response),
not a complete event history. It checks identity, range, contiguous positions,
every native digest step, retained floor one, incarnation and the entire final
native proof against the initial current observation. Growth discards the fold
and retries with a fresh selector. Missing proof, gaps, unsupported events,
unreadable payloads, prefix mismatch, exhausted growth retries or a finite
whole-scan deadline return no value or observation and a safe reason code.

The default maximum whole-scan bound remains thirty seconds; caller cancellation
wins and propagates. Every external provider or selector invocation uses the
existing sixteen-slot admission mechanism; a noncooperative invocation retains
its slot until actual termination after the caller stops waiting. Abandoned
results cannot release evidence. The existing complete-source reader retains
its separate 10,000-event and 16 MiB limits unchanged.

A selector can retain facts at an exact derived checkpoint cut while continuing
the full current native scan. The consumer compares that independently folded
prefix and view against its selected generation, then rechecks the selected
checkpoint before release. Folders additionally checks original candidate bytes,
metadata, global position and user identity, cross-position MessageId reuse,
original earliest task closure and source-time expiry. Scans create no durable
history or replacement projection protocol.

If a prepared shared catch-up batch receives an existing store `Conflict`, the
coordinator retains its prepared journal and reports the existing
`SharedProjectionWriteUnavailableException`. It never acknowledges completion.
A cold retry resumes the exact prepared bytes; an unresolved conflict can require
owner repair. No retry count, deadline, CAS, generation or recovery rule changes.
Source and receipt identity validation failures keep their existing classification.
# Synthetic original-primary qualification

The private bounded-history acceptance lane supplies original synthetic envelopes that were
never production aggregate events. It explicitly selects the EventStore.Testing
`synthetic-original-primary-v1` source; this is separate evidence from production actor qualification.
There is no HTTP import endpoint or automatic missing-native fallback. Ordinary and unselected
scan resources retain AggregateActor authority, and metadata reads retain their restrictions.

The existing private proof build (`Debug`, `EnablePrivateCatalogInvocationProof=true`) must
explicitly configure `EventStore:Testing:OriginalSource:Enabled=true`, Mode, RunId, Directory,
Tenant, Domain and AggregatePrefix. The supported private fixture requires Development, its
actual private technical-HTTP catalog, gateway identity `eventstore-<16 lowercase hex run id>`,
tenant `durability-<run id>`, domain `folders` and selected synthetic namespace `capacity-`.
Every exact resource is enrolled by its first contiguous private original-input append. A selected
but unenrolled, incomplete or corrupt resource refuses without consulting an actor.

The private append worker uses the owner-private, link-free Linux IPC boundary (same effective
UID, mode0700 directory, mode0600 single-link files, bounded closed JSON). It persists immutable
pages of at most255 original envelopes and2MiB, validates all existing-position duplicates and
uses the owner source-v1 codec to atomically commit a head/incarnation and bounded primary page
index (maximum4096 pages and8 resources). Conflicts preserve original bytes. Completed control
messages are removed; pages and heads survive process replacement. Each range reads addressed
primary pages and revalidates original bytes and the committed head; no projection, rebuild
capture, read model or client-created success proof supplies authority.

One owner holds an exclusive kernel lease on the verified private directory until disposal or
process exit. Reopening verifies every committed index entry and the complete source chain
before retiring any uncommitted page; a corrupt or incomplete index refuses without deleting
original pages. Private control recovery retains the original request deadline and retires
completed controls only after actual work ends. These rules use the existing source/control
formats and do not change the native scan or callback deadlines.

The receiver consults this source only after the unchanged exact workload operation, single
tenant/domain grants and resource/page-bound RSA proof. The ordinary NativeSourceScanner verifies
the complete contiguous prefix and equal initial/final current observations within its existing
30-second maximum. Test source selection is included in Development catalog input observations.
