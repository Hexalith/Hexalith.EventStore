# OQ8 Durable Idempotency Design

status: design-approved
decision owner: Architecture + Contract/Delivery
approval authority: Architecture + Security + Test
design version: 1.0.0
approved on: 2026-07-19
approved by: Administrator
source inputs: PRD FR41, FR42, FR44, Architecture A-9 and D-7, ADR-0004, C3 retention, Contract Spine, C13, Story 12.6
last reviewed: 2026-07-19
open questions: none for production implementation

## Decision

OQ8 uses one EventStore-owned, tenant-scoped admission contract for every current and future mutation. Folders supplies a trusted canonical intent descriptor after authentication, authorization, and canonical validation; EventStore owns reservation, fencing, replay, conflict, recovery, expiry, and consumed-key evidence. Reads reject a supplied idempotency key before invoking a query, projection, provider, audit, diagnostic, or content source.

### Partition and key protection

- The authoritative partition is the managed tenant plus HMAC-SHA-256 key digest. The actor identity is derived from the managed tenant identifier, digest-key version, and base64url digest of the caller's opaque idempotency key. Raw keys are never persisted or logged.
- A tenant-scoped digest key is derived and versioned by the platform secret authority. The active key writes new records; retained reader keys support lookup during rotation. A record matched through a retiring version is atomically promoted when the presented key permits recomputation. A reader key is not destroyed while governed records still reference it.
- A second domain-separated HMAC verification tag distinguishes a digest collision from a valid match. A matching partition digest with a different verification tag is corrupt state: admission fails closed, emits bounded metadata-only evidence, and never executes domain or external work.
- Reusing the same opaque key in another managed tenant is independent. Reusing it for another operation, aggregate, target, task scope, or behavior-affecting credential scope in the same tenant conflicts while live and expires identically after compaction.

### Trusted canonical intent

The domain adapter builds a versioned descriptor from Contract Spine authority. The descriptor includes operation identifier, canonical target identity, normalized semantic payload/options, policy version, delegated task scope, and behavior-affecting credential scope. It excludes correlation identifiers, authentication tokens, clocks, trace data, delivery attempts, and transport retry metadata.

The descriptor uses length-prefixed, type-tagged, ordinal field encoding; rejects duplicate JSON properties; applies only schema-declared normalization; and stores a keyed SHA-256 intent digest in a live record. Public `SubmitCommandRequest.Extensions` values are untrusted and cannot select the digest, partition, policy version, state, or retention tier. EventStore validates the adapter registration, operation identifier, descriptor version, and fixed tier before admission.

### Admission state machine

The EventStore-owned admission actor serializes one tenant/key partition and issues a monotonically increasing fencing token. Its happy path is reserved -> pending -> terminal. The full state vocabulary and permitted behavior are:

| State | Meaning | Equivalent request | Different request |
|---|---|---|---|
| `reserved` | First writer owns a durable reservation but has not crossed the side-effect boundary | Return the existing reservation or wait outcome; never issue another fence | `idempotency_conflict` |
| `pending` | The fenced writer crossed the side-effect boundary and execution is in flight | Return the recorded accepted/task evidence; never execute again | `idempotency_conflict` |
| `recoverable` | A durable checkpoint proves bounded resume is safe | Resume only from that checkpoint under the existing fence | `idempotency_conflict` |
| `unknown_provider_outcome` | An external effect may have occurred but cannot yet be proved | Return the unknown outcome and permit read-only reconciliation only | `idempotency_conflict` |
| `terminal` | A successful or deterministic failed result is finalized and replayable | Rehydrate the same logical result after current authorization | `idempotency_conflict` |
| `expired` | Replay payload and intent digest were compacted to minimal consumed-key evidence | `idempotency_key_expired` | `idempotency_key_expired` |

The initial reservation, descriptor comparison, state transition, fencing-token issue, and state write are one actor-serialized durable operation. A worker or domain executor must present the current fence before crossing a side-effect boundary or finalizing. A crash never converts a consumed key to missing. Recoverable resumes use persisted checkpoints; unknown external outcomes never permit blind execution and can only move through bounded reconciliation.

### Ordering and key consumption

Every mutation enforces authentication and authoritative tenant/organization/folder/action authorization and canonical structural/semantic validation before admission. In short, authorization and canonical validation before admission is a non-negotiable ordering invariant. The trusted descriptor is then constructed and durably admitted before aggregate state, readiness, credential, provider, repository, path, content, Git, audit, projection, or scheduling work.

Failures before admission, including authentication, authorization, and canonical validation failures, do not consume the key and disclose no key disposition. All deterministic post-admission failures consume the key and are finalized as replayable terminal results. Transient failures before any side effect move to `recoverable`; failures after an unprovable external effect move to `unknown_provider_outcome`. Current authorization is re-evaluated before returning replay, conflict, expired, pending, recoverable, or unknown disposition to a caller; denial leaves the stored record unchanged.

### Clock and retention

- EventStore host `TimeProvider.GetUtcNow()` is the sole clock authority. Every record persists `lastObservedAt`; effective time is `max(lastObservedAt, TimeProvider.GetUtcNow())`, so clock rollback cannot resurrect a record.
- Expiry is inclusive: now >= expiresAt is expired. Just before the boundary the result is live; at and after the boundary the request returns `idempotency_key_expired` regardless of submitted intent or aggregate drift.
- The replay clock starts when a terminal result is durably finalized. Unresolved `reserved`, `pending`, `recoverable`, and `unknown_provider_outcome` records do not age into fresh work.
- mutation replay result: PT24H. This is exactly 86,400 seconds after terminal finalization.
- commit replay result: P7Y. The UTC finalization timestamp uses `DateTimeOffset.AddYears(7)` calendar behavior; a February 29 anniversary resolves to February 28 when the seventh year is not a leap year.
- After replay expiry, compaction atomically removes the replay result and live intent digest while retaining consumed-key evidence for the managed-tenant lifetime plus 400 days after an approved tenant-deletion request enters the deletion workflow. Legal hold pauses that 400-day countdown; release resumes the unelapsed remainder. Final deletion removes the tombstones and tenant digest keys together.

An expired tombstone contains only schema version, state, tenant partition, key digest, verification tag, digest-key version, retention class, first-consumed timestamp, replay-expired timestamp, and monotonic last-observed timestamp. It contains no raw key, canonical intent fingerprint, operation, target, payload/result, path, content, diff, repository/ref, credential/token, commit message, provider body, or identity hint.

### Public expired-key mapping

The canonical result is HTTP 409 with category and code `idempotency_key_expired`, `retryable = false`, `clientAction = refresh_state_then_submit_with_new_key`, RFC 9457 metadata-only details, and the current request correlation identifier. CLI exit 76 and MCP kind `idempotency_key_expired` preserve that result exactly. Expired-equivalent and expired-different responses are indistinguishable apart from approved current-request correlation fields.

Unavailable state, malformed records, unknown schema/digest versions, failed digest verification, or unsafe legacy records fail closed as bounded infrastructure/corrupt-state outcomes. They never become `Missing`, `idempotency_conflict`, or `idempotency_key_expired`, and never authorize execution. Legacy records migrate only through an atomic versioned transform that preserves consumed-key state; otherwise they remain fail closed.

## Rationale

Aggregate-scoped idempotency cannot detect same-tenant reuse against another target. A tenant/key admission actor supplies the missing serialization identity, while EventStore ownership follows the domain-module persistence boundary and avoids a Folders-owned state store. Separate replay payload and tombstone lifetimes preserve exact replay without retaining protected intent for the full consumed-key lifetime.

The inclusive boundary, persisted monotonic high-water mark, fixed tiers, fenced side-effect transition, and fail-closed migration rules remove the ambiguity that previously allowed an expired record to be deleted and executed as new work.

## Verification impact

Verification generates the mutation/read denominator from the Contract Spine. Every mutation covers new, live-equivalent, live-different, expired-equivalent, and expired-different cases plus current authorization, concurrent writers, restart, unavailable/corrupt/legacy state, and persisted state-store end state. Every read proves key rejection before source execution. Time tests exercise one tick before, exact, and one tick after both tiers. Leakage tests inspect persisted records and every declared diagnostic channel.

Production-path evidence must use the approved durable Dapr state component with multiple hosts and restart. In-memory, source-text, fake-only, or recreated-service tests are supporting evidence and cannot close OQ8.

## Deferred implementation

None of the decisions above is deferred. Story 12.6 implements them through the EventStore prerequisite, Folders adapter, Contract Spine/generated surfaces, complete C13 matrix, and final evidence package. Story 12.1 remains the required durable Folders repository and replay prerequisite for final Folders integration.
