# Projection delivery writer v2 cutover

Projection delivery v2 upgrades each projection-scoped checkpoint row in place. It is a maintenance cutover, not a rolling upgrade: Story 1.12 writers must never overlap Story 1.13 writers.

## Preconditions

1. Record the exact EventStore repository commit and the state-store component/provider version.
2. Stop every EventStore server replica and verify that no old immediate-delivery, polling, or projection retry worker remains active.
3. Take a provider-native backup that includes `projection-checkpoints:*`, `projection-checkpoints-migrated:*`, projection retry state, lifecycle state, and read-model batch state. Record the immutable backup reference and verify that it can be listed or restored in an isolated environment.
4. Deploy the v2 binaries everywhere while traffic remains stopped. `/ready` must remain unhealthy because `projection-delivery-writer-protocol` is absent.
5. Run authorized delivery reconciliation for every non-zero sequence-only projection checkpoint. Reconciliation reloads the authoritative EventStore prefix, preserves the checkpoint sequence, writes only delivery receipts/provenance, and never invokes `/project/v2` or changes read models or batch receipts.
6. Verify all v2 rows, retry workers, and server replicas are quiescent. Activate the marker through `POST /api/v1/admin/projections/delivery-writer-protocol/activate` as a `GlobalAdministrator`, supplying the exact commit, backup reference, and all three explicit attestations.
7. Read back `projection-delivery-writer-protocol`. It must contain schema `1`, writer protocol `2`, the exact cutover commit, and an activation timestamp. Only then restore traffic and require `/ready` to be healthy.

The activation endpoint uses first-write ETag concurrency. A conflicting marker is a failed cutover, not permission to overwrite control-plane state.

## Rollback boundary

Before the v2 marker is written, keep all writers stopped. You may restore the verified pre-cutover backup and redeploy the complete old fleet.

After the v2 marker is written, an in-place rolling downgrade is forbidden. Old binaries can deserialize the five legacy checkpoint fields but overwrite v2 receipts, reservations, and fingerprints. V2 classifies that write as `delivery_schema_regression` and fails closed. Recovery requires stopping every writer, preserving the regressed rows for diagnosis, restoring the complete pre-cutover backup into an isolated or replacement store, and moving the whole fleet across one maintenance boundary. Never delete or lower the global marker during tenant, aggregate, or projection erasure.

## Rehearsal and evidence

Rehearse against a disposable copy of the production component topology before changing an existing environment:

- confirm readiness is unhealthy with no marker and healthy only with the exact v2 marker;
- hydrate both zero and non-zero legacy rows and compare their sequence before and after;
- prove a five-field write after activation is classified as schema regression and never invokes a handler;
- erase a projection scope and prove the global marker survives;
- restore the pre-marker backup and confirm the old fleet can start only on that restored store;
- capture bounded evidence: commit, SDK/DAPR/runtime/provider versions, component type/version, commands, backup reference, row classifications, and persisted assertions. Do not capture payloads, fingerprints, state keys, ETags, or secrets.

The local Tier-3 rehearsal uses the repository live-sidecar fixture with `state.redis` (`actorStateStore=true`) and Redis. A successful HTTP response or log line is not evidence; inspect the marker and delivery rows after quiescence.

## Source guard qualification

The optional source guard uses the [approved version 1 cost and sequence contract](../../_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md).
Local correction-pass workload and persisted-state evidence, actual runtime profiles, and failed or unproven checkout gates are recorded in the [local qualification and acceptance record](../../_bmad-output/implementation-artifacts/evidence/story-6-4/review-5-parent-acceptance.md). Formal acceptance, authenticated exact-head sealed CI and the AD-12/NFR8 transition remain open. Each environment still requires its inventoried maintenance, backup/restore and qualification evidence before activation.

The source guard retains the existing store-global writer marker and adds one immutable, exact tenant/domain/aggregate/projection record. There is no global guard switch or wildcard admission. Every delivery-row read retrieves both markers. Erasure retains both admission records; an old incarnation cannot admit a recreated stream.

All append, immediate, poller, retry, reconciliation, rebuild and erasure writers must be upgraded and stopped together. Capture exact source/package identities and a consistent backup of source, delivery rows, selected models, batch receipts, lifecycle and retry work. Verify a restore before recording the authenticated operator, approved specification digest, inventory, backup and no-downgrade attestations. The source bootstrap endpoint is `POST /api/v1/admin/projections/{tenantId}/source-proof/{domain}/{aggregateId}/bootstrap`; it reads the bounded canonical source through the actor, rereads the same head, then saves only its metadata anchor. Existing metadata and the proposed anchor must fit 4096 bytes under the pinned actor SDK's writer profile before any mutation. The authenticated response returns `sourceIncarnation`, including an already-proven incarnation; use that exact value as qualification's `expectedIncarnation`. Bootstrap invokes no projection handler and rewrites no stored envelope.

`POST /api/v1/admin/projections/{tenantId}/source-guard/{domain}/{aggregateId}/{projectionName}/qualify` proves the complete readable source and the existing selected-model terminal receipt at the existing checkpoint. It preserves C, the exact receipts, complete ID digests, retained floor and fingerprint version, and records the actual Idle revision. It persists schema2 and the attestations together in the existing reconciliation control record. History alone never supplies missing model completion proof. Missing source/model evidence, active work, trimmed history and incomplete inventories remain unqualified.

The separate `/activate` action repeats qualification and conditionally saves the exact scope marker, unchanged qualified row and operator evidence in one same-component transaction. Its readback checks all three records. A racing row or scope CAS prevents admission; an ambiguous response requires persisted readback. Implemented routes include previously completed single-event, mapped incremental and explicitly declared full-replay owners, plus truly empty new single-event scopes with the explicit fingerprint-bound `aggregate-first-event/v1` initialization protocol. Empty maintenance retains the actor's seed/incarnation and writes no model or completion receipt; ordinary empty inspection stays Unknown. It cannot initialize unknown nonempty progress. Full-replay and cold mapped fallback use bounded complete-prefix staging and the original immutable manifest; their separately scheduled cleanup verifies the existing terminal selection/receipt and deletes only matching private preparation. Cleanup uncertainty preserves original work and cannot advance C or create freshness. Shared-current qualification remains unsupported because per-source fold receipts do not establish complete shared-family selection/freshness within the approved hot-path profile. A marker for an earlier incarnation requires its explicit expected incarnation and complete requalification before CAS replacement. The linked local evidence records prescribed workloads and live-sidecar profiles with their remaining failed or unproven gates; each target environment requires the separate maintenance and production evidence above.

New metadata requests explicitly negotiate `projectionSourceGuardVersion: 1`. Requests without that field retain historical catalog fingerprints and ordinary dispatch semantics, including when the handler package supports guard descriptors. Qualification requires the negotiated descriptor-bound catalog and cannot use a historical fingerprint as proof of guard support.

Schema2/source completion cannot be downgraded in place. Recovery after activation stops all writers and restores the consistent pre-cutover environment as one isolated or replacement fleet operation. Never lower/delete admission markers or selectively restore only a checkpoint or model.

Scheduled activation/retry cleanup follows an independently proven current observation. Activation completion is conditional on the sampled outbox revision. Retry deletion claims the original work's own durable revision/lease and requires its exact MessageId and catalog binding. A newer completed head can settle an older unreserved wake-up only after valid scoped prefix/digest, source, selected-owner and Idle proof; sequence ordering alone is insufficient. Private replay cleanup uses the authenticated SDK rebuild policy at `/project/rebuild/reclaim-guarded/v1`, verifies original selected owners/generations and immutable receipt fingerprint, and conditionally reclaims the original manifest. Its bounded immutable reference remains to prevent delayed preparation under the same identity. Ordinary current inspection remains zero-write.

Qualification applies to the requested inventoried scope; an unrelated sibling without a supported descriptor does not veto it. Runtime inspection preserves that exact scope boundary and cannot report the incomplete catalog Current. Missing, invalid or removed sibling admission remains separately diagnosable and cannot clear or downgrade its persisted evidence. Source metadata bodies have their independent 4 KiB admission limit; a scope over that limit remains unqualified even when its delivery row fits a larger proof budget.
