# SignalR Operational Evidence Fixture - Valid Minimal

Schema version: `signalr-operational-evidence/v1`

## Metadata

```yaml
schema_version: signalr-operational-evidence/v1
evidence_run_id: dw4-signalr-valid-001
story_key: post-epic-deferred-dw4-operational-evidence-schema-validation
run_profile: non-aspire-static-fixture
classification: pass
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; no bearer tokens, secrets, production hostnames, or customer payloads are present
reliability_control: same-run reliability control dw4-signalr-control-001 recorded pass
apphost_url: not-applicable: non-aspire static fixture
dapr_placement: not-applicable: non-aspire static fixture
dapr_scheduler: not-applicable: non-aspire static fixture
resource_snapshot: not-applicable: non-aspire static fixture
```

## Controls

Reliability control is tied to the same synthetic run id.

## Redaction

Synthetic sample only.

| Signal | Observed result |
| --- | --- |
| hub-broadcast | pass |
