# Story 6.6: Dapr-only implementation amendment

**Decision date:** 2026-10-05

**Authority:** The owner explicitly directed Story 6.6 to stay within the Dapr abstraction.

**Status:** Current implementation constraint; Story 6.6 remains in progress.

This amendment supersedes the direct PostgreSQL and provider-extension portions of the
previously approved AD-13 event-evolution design. The reviewed
`spec-event-versioning-upcasting.md` and its content digest remain historical evidence of
the earlier decision; its validator must not be repinned to imply that this amended
design received the earlier review. Where that document conflicts with this amendment,
this amendment controls Story 6.6. The remaining exact schemas and bounds are inputs
only where they can be implemented and verified through Dapr.

## Application storage boundary

- Aggregate events, metadata, snapshots, command results and outbox stay under
  `AggregateActor` and `IActorStateManager`. The actor stages related values and calls
  `SaveStateAsync` once. A failed or ambiguous save is reconciled by a fresh,
  addressed Dapr actor read whose independence from staged state is demonstrated
  by tests before retry or acknowledgment. No application SQL, Npgsql connection,
  PostgreSQL schema, Dapr private actor key, or provider fork is part of Story 6.6.
- New non-actor coordinator state, if required, uses Dapr state APIs with ETags and
  transactional operations only after the selected component proves those capabilities.
  A dedicated actor can own a control record when actor serialization and one actor
  save are sufficient. Dapr does not provide a transaction across actor state,
  separate actors, broker delivery, and external objects; those boundaries use
  durable intent, idempotency, and reconciliation. No cross-boundary atomicity claim
  may be inferred from a shared physical database.
- The production component may remain `state.postgresql` behind the Dapr sidecar
  under AD-26. The application has no PostgreSQL credential or schema ownership.
  Deployment and backup of that component remain platform responsibilities.

## Event-evolution contract

- Preserve stable canonical event contract type and positive payload version for new
  events, an allow-listed legacy alias/version mapping, a deterministic contiguous
  bounded upcaster chain, addressed identity/sequence checks, and immutable stored
  history. Upcasting changes a private in-memory view; it never rewrites a retained
  event or changes MessageId, correlation, aggregate identity, or sequence.
- Preserve the original **application payload bytes** in an immutable actor-owned
  value. Compute and verify an application digest over those bytes and the relevant
  metadata. Readback may establish that Dapr returned the same logical value. It
  cannot establish the exact physical database representation, a provider-signed
  commit receipt, or an independently authenticated older committed generation.
  Dapr's typed state API may normalize a serialized envelope. Do not label an
  application digest or current readback as provider attestation.
- The old purpose-10/11 provider-signed carriers, historical-generation witness,
  branch-02 retroactive raw capture, PostgreSQL control row/index/queue schema, and
  same-database SERIALIZABLE participant set are withdrawn from Story 6.6. Their
  dependent activation gates and tests must be replaced with Dapr actor/state
  capability, logical-byte readback, crash/retry, and mixed-version consumer tests.
  Any operation whose safety specifically depends on an unavailable provider proof
  remains disabled and reports a typed unavailable/hold outcome.
- Legacy history is resolved from allow-listed metadata and the logical payload
  returned through Dapr. A missing/ambiguous mapping, corrupt digest, unreadable
  protected payload, unsupported version, or incomplete contiguous prefix fails
  closed before domain code, projection checkpoint, publication marker, or handler
  effect. No arbitrary CLR type loading or silent skip is permitted.

## Delivery sequence and acceptance evidence

1. Retain the existing V2 admission fence until writer capability and every serving
   reader/consumer are compatible. Implement registry/upcasters and a single shared
   evolution service; apply it to actor replay, projections, subscriptions,
   reconstruction, and inspection. Keep old V1 wire and package behavior additive.
2. Test actor same-save logical event/result/outbox behavior and lost-ack readback
   against a live Dapr component. Test ETag/transaction capability separately for
   each non-actor control actually introduced. Never use direct database reads as
   product evidence.
3. Test original application payload bytes before and after replay, all consumer
   end states, cancellation, crash/retry and rolling upgrade/rollback. Record the
   exact Dapr runtime/component profile, actor/state API calls and observed values.
   A backend-specific forensic claim requires a separate future decision.

This change does not complete Story 6.6 or authorize V2 writes, migration,
deployment or production promotion. Unqualified or unimplemented paths remain
fenced; Story 6.6's tasks and evidence must be re-evaluated against this amendment.
