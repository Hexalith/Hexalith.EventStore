# Query Operational Evidence Fixture - Invalid Control Linkage Unrelated

Schema version: `query-operational-evidence/v1`

## Metadata

```yaml
schema_version: query-operational-evidence/v1
evidence_run_id: dw9-query-unrelated-001
story_key: post-epic-deferred-dw9-evidence-validator-and-governance-polish
run_profile: non-aspire-static-fixture
final_classification: pass
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; no bearer tokens, secrets, production hostnames, or customer payloads are present
linked_control_run_ids: dw9-query-allowed-control-001
false_positive_control: unrelated false-positive control control_run_id:dw9-query-other-control-001 recorded pass
correlation_control: same-run correlation control evidence_run_id:dw9-query-unrelated-001 recorded pass
apphost_url: not-applicable: non-aspire static fixture
dapr_placement: not-applicable: non-aspire static fixture
dapr_scheduler: not-applicable: non-aspire static fixture
resource_snapshot: not-applicable: non-aspire static fixture
```

## Controls

False-positive control intentionally references a control run not declared by the evidence.

## Redaction

Synthetic sample only.

| Scenario | Observed result |
| --- | --- |
| unrelated-link | fail |
