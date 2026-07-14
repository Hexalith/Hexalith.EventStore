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
