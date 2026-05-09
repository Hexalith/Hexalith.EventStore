# SignalR Operational Evidence Fixture - Valid Linked Control Run

Schema version: `signalr-operational-evidence/v1`

## Metadata

```yaml
schema_version: signalr-operational-evidence/v1
evidence_run_id: dw9-signalr-linked-001
story_key: post-epic-deferred-dw9-evidence-validator-and-governance-polish
run_profile: non-aspire-static-fixture
classification: pass
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; no bearer tokens, secrets, production hostnames, or customer payloads are present
linked_control_run_ids: dw9-signalr-reliability-control-001
reliability_control: linked reliability control control_run_id:dw9-signalr-reliability-control-001 recorded pass
apphost_url: not-applicable: non-aspire static fixture
dapr_placement: not-applicable: non-aspire static fixture
dapr_scheduler: not-applicable: non-aspire static fixture
resource_snapshot: not-applicable: non-aspire static fixture
```

## Controls

Reliability control uses an explicit linked control-run id declared in metadata.

## Redaction

Synthetic sample only.

| Signal | Observed result |
| --- | --- |
| linked-control | pass |
