# SignalR Operational Evidence Fixture - Invalid Control Linkage Missing

Schema version: `signalr-operational-evidence/v1`

## Metadata

```yaml
schema_version: signalr-operational-evidence/v1
evidence_run_id: dw9-signalr-link-missing-001
story_key: post-epic-deferred-dw9-evidence-validator-and-governance-polish
run_profile: non-aspire-static-fixture
classification: pass
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; no bearer tokens, secrets, production hostnames, or customer payloads are present
reliability_control: reliability control recorded pass but no explicit run reference
apphost_url: not-applicable: non-aspire static fixture
dapr_placement: not-applicable: non-aspire static fixture
dapr_scheduler: not-applicable: non-aspire static fixture
resource_snapshot: not-applicable: non-aspire static fixture
```

## Controls

Reliability control intentionally omits both same-run and linked-control-run references.

## Redaction

Synthetic sample only.

| Signal | Observed result |
| --- | --- |
| missing-link | fail |
