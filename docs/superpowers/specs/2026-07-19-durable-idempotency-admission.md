# Durable Tenant-Scoped Idempotency Admission

status: approved implementation work item; release blocked
approved by: Administrator
approved on: 2026-07-19
source authority: Hexalith.Folders Story 12.6 and OQ8 design 1.0.0

## Goal

Provide one EventStore-owned admission boundary that serializes every mutation by managed tenant and protected opaque idempotency key. A trusted domain adapter supplies a versioned canonical intent descriptor and a server-registered replay tier before aggregate, provider, repository, projection, audit, or other side-effect work begins.

## Required contract

- Derive tenant-scoped key partitions, collision-verification tags, and intent digests with domain-separated HMAC-SHA-256. Never persist or log the raw key.
- Validate the adapter, operation, descriptor version, and fixed mutation or commit tier against server configuration. Public extension values cannot select any of them.
- Serialize `reserved`, `pending`, `recoverable`, `unknown_provider_outcome`, `terminal`, and `expired` state through a durable actor and require the current fence for side-effect entry and completion.
- Return live equivalent replay without aggregate or advisory-store mutation; return live different intent as `idempotency_conflict`; return both equivalent and different expired reuse as `idempotency_key_expired`.
- Use `max(lastObservedAt, TimeProvider.GetUtcNow())`; expiry is inclusive. Mutation replay is exactly 86,400 seconds and commit replay uses `DateTimeOffset.AddYears(7)`.
- Atomically compact an expired terminal record to the approved minimal tombstone while preserving tenant partition, key digest and verification tag, key version, retention tier, first-consumed time, expiry time, monotonic observation time, schema, state, and fence.
- Fail closed for unavailable state, failed verification, unknown schema/key versions, and legacy records that cannot be transformed without losing consumed-key knowledge.
- Preserve the stable HTTP 409 Problem Details contract for `idempotency_key_expired`, including `retryable = false`, `clientAction = refresh_state_then_submit_with_new_key`, and current-request correlation only.

## Implemented source candidate

The current working-tree candidate adds the trusted descriptor contract, operation registry validation, key protector, dedicated admission actor/coordinator, aggregate expiry terminal behavior, gateway mappings, diagnostic redaction, and handler ordering. Unit and live-sidecar coverage includes tenant isolation, same-key/different-intent conflict, exact expiry compaction, collision verification, unavailable state, clock rollback, fixed tiers, concurrent first writers, Redis end-state inspection, and application-plus-sidecar restart survival.

This source candidate is not a released platform capability and must not be consumed as one.

## Release gates

- Enforce the trusted-adapter boundary for canonical intent. The public gateway must not accept caller-authored canonical bytes as authoritative merely because the adapter, operation, version, and tier names are registered.
- Separate the caller's opaque idempotency key from public command/message identity, or introduce a protected internal identity mapping. The descriptor path must not persist or log the raw key through command status, archive, correlation-index, exception, or downstream actor diagnostics.
- Implement and verify retained-reader-key lookup plus crash-safe, atomic promotion from a retiring digest-key version. A mixed-version deployment must not admit the same raw key through two actor identities.
- Define a versioned legacy raw-key/full-result inventory and migration protocol that detects unsafe cross-aggregate reuse. Same-aggregate lookup alone is insufficient because the new authority is tenant/key scoped. Unknown, corrupt, or uninventoried legacy state must fail closed.
- Rehydrate the exact original logical success or deterministic failure without consulting advisory stores, including payload-withholding and typed rejection mappings, and implement bounded fenced resume/reconciliation for recoverable and unknown outcomes.
- Prove multiple EventStore hosts sharing the same production-equivalent state store, including concurrent equivalent and different first writers, compaction races, host failover, and unknown-outcome recovery.
- Add governed tenant-deletion/legal-hold cleanup and digest-key retirement operations for the lifetime-plus-400-day tombstone class.
- Complete senior review, run all owning-repository release checks, publish a version, and update the root-declared Hexalith.EventStore pin only with explicit dependency/pin authorization.

## Verification commands

Run projects individually with repository project references enabled; do not use a solution-wide test command:

```text
dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -p:UseHexalithProjectReferences=true
dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -p:UseHexalithProjectReferences=true
dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -p:UseHexalithProjectReferences=true
dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -p:UseHexalithProjectReferences=true --filter FullyQualifiedName~IdempotencyAdmissionLiveSidecarTests
git diff --check
```

## Consumption rule

Folders integration may develop against this source candidate only as explicitly authorized cross-repository work. Story 12.6 cannot mark the EventStore prerequisite complete, close OQ8, or claim production durability until every release gate above is satisfied and a reviewed release or root-declared pin is consumed.
