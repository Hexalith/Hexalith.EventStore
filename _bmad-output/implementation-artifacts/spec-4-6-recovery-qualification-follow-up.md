---
title: 'Story 4.6 Follow-up: Recovery Qualification Evidence Gaps'
type: 'feature'
created: '2026-10-31'
status: 'draft'
parent_story_key: '4-6-global-position-sharding-spec-renegotiation'
production_evidence_qualified: false
---

## Intent

Extend the bounded same-storage synthetic recovery results toward decision-grade
recovery evidence only after the relevant real inputs and mechanisms exist.
This is a follow-up planning record, not authorization to change production.
The completed phase is recorded in [the provider recovery spec](spec-4-6-isolated-postgresql-recovery.md).
Preserve the [owner approval and downstream authorization](spec-4-6-global-position-sharding-spec-renegotiation-2.md):
candidate `411005b4ccb2352249c83330c54967c6a22beba1`, successor blob
`160331d25451928ff3c3dea2300b65cab4f97c3b`, normative digest
`995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`.
No repeated general authorization is required; missing production data remains
an evidence input, not an approval failure.

## Evidence boundary

The implemented lane uses one local disposable PostgreSQL database, two synthetic
.NET hosts and two Dapr sidecars. It exercises graceful shutdown, SIGKILL with a
blocked save, caller response loss, exact persisted authority and later host
replacement. Its seeded Int64 boundary is synthetic. It has no real aggregate,
event, persisted command result, authenticated production authority, ratified
production profile, comparative capacity trace, backup image or replica.

A provider process restart on unchanged storage is not backup/restore or HA.
The deterministic blocked save does not sample all crash windows. The discarded
response occurs after successful HTTP receipt inside the caller transport; it
does not cover every sidecar/provider response-loss window. Request exceptions
remain uncertain, and durable outcomes may consume unused ranges. Internal
rerouting/component retry behavior is not a general exactly-once guarantee.

## Concrete follow-up work

- [ ] `tests/Hexalith.EventStore.Sharding.Tests/`: extend observable fault coverage to commit/ack loss between PostgreSQL and Dapr, admission interruption, and repeated failure cycles. Bind the exact runtime's retry behavior and verify every attempt without replaying ambiguous reservations. Given any such failure, durable authority must remain complete, generation-stable and above all acknowledged/reserved ceilings after recovery and replacement.
- [ ] A separately designed backup/restore lane: identify exact backup boundary, archived authority and all externally acknowledged positions; prove restore cannot reopen below any granted ceiling or reuse a generation. Given a stale or incomplete restore, allocation must remain fenced rather than infer state from event maxima. No backup command is authorized by this record.
- [ ] A separately designed physical-fencing/failover lane: introduce approved replica/placement/partition topology and old-writer controls. Given overlapping old/new writers, provider-enforced authority must reject stale writes. Process replacement alone is insufficient.
- [ ] Real allocator integration design: inventory every admissible tenant/domain pair and durable generation lineage; cover admission, retirement, partial lifecycle persistence, restore and collision handling. Given missing/corrupt authority, all reservations must fail closed without reconstruction.
- [ ] `tests/Hexalith.EventStore.Server.Tests/` and a separately reviewed live lane: prove real aggregate sequence, `MessageId`/CloudEvent identity and persisted retry-result fidelity. Given a retried command after failure, the original full result and position identities must survive.
- [ ] Public metadata/consumer design: strict v1/v2 wire validation, opaque unsupported round trips, mixed-version comparison rejection, committed-source cursor/checkpoint/rebuild and migration proof. Allocation ceilings cannot be committed-event cursors.
- [ ] Production evidence inputs: obtain representative trace and ratified provider/topology/resource/security profile, then freeze option-capacity method, limits, peak derivation and expiry. Compare equivalent tenant/domain/composite implementations as required by successor sections 2 and 7. Synthetic counts supply no capacity figure.
- [ ] Operational qualification: measure provider key/value, batch, generation, cursor/page bounds, hot-pair isolation, lifecycle cost and rollout/rollback behavior. Preserve the permanent no-v1 boundary after the first durable production v2 allocation.
- [ ] Content-bound Story 4.5 applicability: review the exact future production diff against every append-fencing/provider-write seam; obtain the specified owner evidence/approval for the applicable declaration. This test-only phase changes no such production seam.

## Run and completion requirements

Every future empirical run must bind the exact approved subject, artifact,
configuration, provider/topology, environment, storage ownership and exclusive
canonical UTC expiry before execution; fence/drain and verify complete owned
teardown. Preserve actual versions, commands, counts, source/output manifests,
durable outcomes and limitations as test/CI artifacts. The current in-process
watchdog cannot clean resources after its process or machine is killed; ownership
journals support exact manual recovery. CI integration remains outstanding.

The original Story 4.6 and production section 7 rows retain their independent
operator/evidence state. None of these gaps is silently waived or represented as
another request for the same general approval.
