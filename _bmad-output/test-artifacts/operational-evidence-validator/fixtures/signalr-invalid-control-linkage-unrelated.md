# SignalR Operational Evidence Fixture - Invalid Control Linkage Unrelated

Schema version: `signalr-operational-evidence/v1`

## Metadata

```yaml
schema_version: signalr-operational-evidence/v1
evidence_run_id: dw9-signalr-unrelated-001
story_key: post-epic-deferred-dw9-evidence-validator-and-governance-polish
run_profile: non-aspire-static-fixture
classification: pass
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; no bearer tokens, secrets, production hostnames, or customer payloads are present
linked_control_run_ids: dw9-signalr-allowed-control-001
reliability_control: unrelated reliability control control_run_id:dw9-signalr-other-control-001 recorded pass
apphost_url: not-applicable: non-aspire static fixture
dapr_placement: not-applicable: non-aspire static fixture
dapr_scheduler: not-applicable: non-aspire static fixture
resource_snapshot: not-applicable: non-aspire static fixture
```

## Controls

Reliability control intentionally references a control run not declared by the evidence.

## Redaction

Synthetic sample only.

| Signal | Observed result |
| --- | --- |
| unrelated-link | fail |
