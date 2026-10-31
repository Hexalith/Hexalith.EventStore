---
title: 'Story 4.6 Follow-up: Qualification Beyond Synthetic Commit Ambiguity'
type: 'feature'
created: '2026-10-31'
status: 'draft'
parent_story_key: '4-6-global-position-sharding-spec-renegotiation'
production_evidence_qualified: false
---

## Intent

Plan evidence beyond the bounded test-only provider commit/acknowledgement and
interrupted-admission phase. Its actual implementation, attempts, verification
and limitations are recorded in [the phase spec](spec-4-6-provider-commit-ambiguity.md).
This document grants no production runtime or infrastructure authority and
does not amend completed specifications.

Preserve the [owner approval and downstream authorization](spec-4-6-global-position-sharding-spec-renegotiation-2.md):
candidate `411005b4ccb2352249c83330c54967c6a22beba1`, successor blob
`160331d25451928ff3c3dea2300b65cab4f97c3b`, normative SHA-256
`995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`.
The remaining inputs and qualification mechanisms are evidence gaps, not another
request for the same general authorization.

## Evidence boundary

The current extension is a synthetic actor lane with one owned local PostgreSQL
database/volume, two independent hosts and two Dapr sidecars. It instruments
exact-key writes with a test-only PostgreSQL trigger/sequence and an unencrypted
protocol proxy. A second test-only witness key forces the provider's explicit
multi-operation transaction path; the ordinary single-key path does not emit
explicit COMMIT and remains separately unqualified for these fault windows.
Deterministic COMMIT gates sample two specific windows; they do
not exhaust protocol, provider, network, transaction, cancellation or crash
states. The trigger and proxy alter the tested configuration and cannot silently
qualify the uninstrumented production provider profile. Sequence observations
are captured before provider restart; PostgreSQL sequences are not a general
crash-durable execution ledger. Host journals detect executions within this
bounded process-replacement experiment, not machine-loss recovery.

## Required follow-up work

- [ ] `tests/Hexalith.EventStore.Sharding.Tests/`: separately instrument the ordinary one-key implicit-transaction path and extend fault sampling only with explicit observable boundaries and exact-attempt accounting. Cover additional runtime/provider versions, actual deployed transport/TLS, commit cancellation, transaction errors and overlapping routing failures. Given a failed request, complete durable state and all observed attempts must determine the outcome; failure to establish a boundary is a failed experiment.
- [ ] A separately designed durable retry-result lane: integrate the real allocator and command/event pipeline. Given failure after reservation or event persistence, preserve the original complete command result, aggregate sequence, `MessageId`, CloudEvent ID and full position identity; distinguish safe command replay from unsafe reservation replay.
- [ ] Production lifecycle design: inventory every admissible exact pair, physical identity and durable generation lineage. Given corrupt, missing, retired, concurrent or partially persisted admission, reject allocation until the approved authority transition completes. A single atomic synthetic record does not prove distributed admission phases, checkpoint invalidation or retirement.
- [ ] Backup/restore design: bind the exact archived authority, backup boundary and all externally granted ceilings. Given stale/incomplete restored authority, remain fenced; never reconstruct readiness or ceilings from event maxima. No backup/restore command is commissioned by this record.
- [ ] Physical fencing/failover design: identify approved replicas, partition topology and stale-writer controls. Given overlapping old and new writers, provider-enforced authority must reject stale writes. Same-storage provider restart and process replacement provide no HA or split-brain qualification.
- [ ] Public contracts/consumers: strict v1/v2 and opaque unsupported wire round trips, exact comparison partitions, mixed-version rejection, committed-source cursors, checkpoint/rebuild and migration evidence. Allocation ceilings cannot stand in for committed-event progress.
- [ ] Production capacity inputs: representative production trace, ratified provider/topology/resource/security profile, equivalent tenant/domain/composite candidates, frozen method/limits/expiry derivation and actual production peak. Synthetic counts and instrumented latency supply no production capacity figure.
- [ ] Operational/CI evidence: provider/key/value/batch/generation/page bounds, hot-pair and lifecycle-control cost, authenticated workload authority, rollout/rollback, content-bound Story 4.5 applicability, automated artifact/source drift validation, and a watchdog outside the test process. Given an externally killed test process or machine, recover only exact recorded owned resources; the current in-process watchdog cannot guarantee teardown then.

## Future run requirements

Before execution bind the approved subject, complete implementation/output/source
manifests, observed versions, actual provider configuration, topology/environment,
storage ownership, instrumentation, exclusive canonical UTC second expiry and
fence/drain/teardown plan. Record every attempt, uncertainty, count and complete
durable outcome, including failed experiments. Remove all owned resources and
independently verify their absence. Retain redacted ignored artifacts and report
CI identity when CI exists. No synthetic result closes original Story 4.6 or a
production section 7 gate.
