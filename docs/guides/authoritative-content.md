# Authoritative content bodies

The domain-service SDK supplies `Hexalith.EventStore.DomainService.Content` for
actual bounded bytes. Register `AddAuthoritativeContent<TAuthority>()` with the
host's Dapr client and an `IContentAuthority` adapter. This is a source capability;
no package publication or production rollout is implied.

The adapter owns current domain truth and policy. The SDK owns physical Dapr
persistence, verified upload receipts, atomic visibility, recovery, content holds,
and physical disposition. Do not implement body persistence in domain modules or
reuse `IReadModelStore`, projections, events, snapshots, telemetry, or disposable
working files as a body store.

## Ingestion and immutable publication

1. Authenticate/delegate the original caller on the existing transport route.
   `AuthoritativeContentHttpTransport` handles a raw HTTP body and exact reads;
   `UploadAsync` also accepts a bounded multipart-section stream directly.
2. Supply `ContentUploadRequest` with canonical scope, associated task, expected
   lowercase SHA-256, exact length, media type, and a stable opaque `UploadId`.
   The SDK reads and hashes actual bytes, checks bounds, and invokes
   `ValidateUploadAsync` before any durable write. The adapter must reject
   confidential operational cleartext, reversible sealed values, reconstruction
   material, and unsupported policies. These APIs do not implement tokenization.
3. Retain the opaque receipt, never the body, in command metadata. Receipt IDs
   contain only `[a-z0-9._-]+` (`content_` followed by 64 random lowercase hex
   digits). An adapter can expose the computed digest as `hashref_` plus `Sha256`;
   this mapping is exact and does not make a caller-provided digest authoritative.
   Receipts also bind verified length, media type and assigned task.
4. `VerifyUploadAsync` freshly authorizes tenant/task receipt use and verifies the
   persisted bytes before admission of a descriptor-based command. Possession of
   an upload receipt grants no retrieval or mutation authority. Trusted internal
   mutation preparation can call `InspectUploadAsync` to derive content eligibility
   from verified exact bytes in memory. This uses distinct current
   `ContentOperation.InspectUpload` authorization, checks tenant/task ownership,
   completeness/disposition and physical integrity, and reauthorizes before return.
   It has no public upload-body route and grants no ordinary file-read authority.
   `VerifyUploadAsync` retains its metadata-only result. Equivalent stable
   upload intents reuse one receipt; conflicting bytes/media fail. An interrupted
   incomplete upload can be repaired with the same verified intent.
5. Under current authoritative aggregate guards, call `PrepareAsync` before
   appending metadata events. Include the authoritative command ID, task, lock
   fence, expected aggregate version, file incarnation/version, and complete
   `ExpectedReceipt` for each body. Include bounded immutable non-confidential
   `ResourceMetadata` when authoritative path/classification metadata is needed.
   `ValidatePreparationAsync` must validate that metadata and every current guard.
6. Durably append or reject through EventStore. `GetPublicationOutcomeAsync` must
   compare the *exact complete preparation*, including receipt metadata, resource
   metadata, fences and versions, against retained authoritative command/event
   state. Missing, ambiguous, or unavailable outcomes remain pending. A rejection
   must prove that later append is impossible; timeout or missing evidence is not
   a rejection.
7. Call `RecoverAsync`, or let a subsequent read reconcile the pending command.
   One metadata CAS exposes all accepted bindings together. Rejected batches stay
   invisible. Only one unresolved preparation is allowed per workspace scope.
   Capacity or contention fails closed rather than evicting recovery evidence.

`ReadAsync` authorizes before protected observation and again before returning
verified exact bytes. `ListAsync` returns current immutable bindings and resource
metadata after current authorization; domain policy adapters must authorize that
listing and apply their path/classification rules before serving it to a caller.
A removal tombstones the entire file incarnation, suppressing all its historical
versions. Replay never clears that tombstone. A later file requires a new
incarnation after the previous incarnation was removed.

## Persistence and crash windows

Physical bodies use `authoritative-content:body:` keys. Body-free upload,
publication, binding, hold and disposition metadata use
`authoritative-content:metadata:` keys. Both use a SHA-256 of the canonical scope;
no raw tenant, path or task is embedded in keys. Dapr component/backend access
remains a trusted infrastructure boundary.

A reservation is durably registered before writing a body, so an interrupted
upload is discoverable through authorized `ListUploadsAsync`. Bodies are immutable
create-only values and are verified after write and on every qualification/read.
Pre-append preparation protects their retention. A process dying after event
append leaves the preparation available to the next process; source-bound
outcome reconciliation completes its atomic visibility CAS. Missing/corrupt
bytes produce an unavailable failure, never an empty successful result.

Use a Dapr component qualified for strong reads and first-write ETag CAS,
including create-only collision behavior. Current source qualification exercises
local `state.redis` through a real sidecar. Backend replacement needs equivalent
qualification. Size the sidecar request/response and SDK gRPC receive limits for
the configured maximum body (base64 JSON adds approximately one third), or choose
a smaller bound. The live fixture uses `--max-body-size 16Mi`; live payload
qualification is 8192 bytes, with configured boundary behavior additionally
covered by the focused protocol lane. A default operation has one 30-second
deadline covering input, policy, persistence and recovery; non-cooperative calls
are also bounded by that deadline. This is a bounded scope ledger, not an
unbounded archive/catalog: its default retained-entry limit is 4096.

## Authoritative-body disposition

Bodies have no automatic TTL. Preserve live staged/committed references and every
unresolved publication, including references being superseded or removed. Do not
infer eligibility from projected status, lock loss, an upload age, an archive
flag, or a process recovery deadline.

`SetHoldAsync` durably sets/releases a content-specific hold after current
resource authorization. A hold preserves physical bytes but never restores
ordinary visibility. Release clears any prior cleanup epoch. External governing
holds returned by the current source owner are also enforced; closed-scope
Conversation/AgentInteraction governance APIs are not repurposed for other
content scopes.

`ApplyDispositionAsync` asks `GetDispositionAsync` for fresh terminal/no-active/
no-reference/hold state. An upload with no assigned task additionally requires
SDK-owned durable `AbandonUploadAsync` before its cleanup epoch starts. Assigned
orphans retain their immutable receipt task identity, allowing an owner to prove
terminal disposition even when command admission never occurred. A missing
command alone is not terminal proof.

Eligibility starts a seven-calendar-day epoch. The owner's generation must change
on every resume/reference/hold transition, even if a complete cycle happens
between cleanup visits. Before irreversible receipt disposal, resume, a new
pending reference, an active hold, or a changed generation cancels the old epoch. Subsequent eligibility starts a fresh
one. Fresh hold/reference/source checks are required at deletion.

At maturity the SDK acquires a durable, unique CAS claim, blocking new store-owned
holds, uploads or bindings. This claim is revocable: a concurrent cleanup visitor
can cancel or replace it using fresh source/hold/reference proof. A stale caller
must still own the exact claim before it can commit deletion.

`TryAuthorizeErasureAsync` atomically rechecks the exact source generation and
terminal/no-active/reference/hold predicates, then durably disposes this immutable
receipt. This source grant permanently forbids every later reference to that
receipt, including from resumed work. It need not prohibit unrelated later tasks
or lock events. A resume or source change before the grant invalidates the old
source proof; a preservation hold must win before irreversible receipt disposal.
An unavailable owner fails closed. Recovery before the SDK deletion decision
must re-read current disposition and re-prove the source grant, rather than trust
an old successful response. A changed generation requires a fresh P7D epoch.

While it still owns the exact claim, the SDK atomically records
`DeletionAuthorized` after a successful source grant and before any physical
delete. This phase is irreversible: another visitor, failed grant, or stale claim
cannot clear it or reopen references/holds. Replacement processes can complete
physical erasure from that durable decision without requesting a new lifecycle
disposition. Strong CAS replaces actual bytes with a metadata-only tombstone at the exact body
key. This permanent create fence prevents delayed upload retries from recreating
bytes, including when erasure wins before the first write. Its explicit erased
flag distinguishes an erased body from a live empty file. Byte erasure and the
completion marker are idempotent, including a crash after the tombstone is stored
but before completion; replacement processes retain the fence while recovering.
The store retains metadata-only erasure receipts and incarnation tombstones;
historical events and independently governed audit retention remain unchanged.
