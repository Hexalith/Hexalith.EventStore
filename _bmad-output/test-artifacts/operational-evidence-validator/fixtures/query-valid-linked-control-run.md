# Query Operational Evidence Fixture - Valid Linked Control Run

Schema version: `query-operational-evidence/v1`

## Metadata

```yaml
schema_version: query-operational-evidence/v1
evidence_run_id: dw9-query-linked-001
story_key: post-epic-deferred-dw9-evidence-validator-and-governance-polish
run_profile: non-aspire-static-fixture
final_classification: pass
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; no bearer tokens, secrets, production hostnames, or customer payloads are present
linked_control_run_ids: dw9-query-false-positive-control-001,dw9-query-correlation-control-001
false_positive_control: linked false-positive control control_run_id:dw9-query-false-positive-control-001 recorded pass
correlation_control: linked correlation control control_run_id:dw9-query-correlation-control-001 recorded pass
apphost_url: not-applicable: non-aspire static fixture
dapr_placement: not-applicable: non-aspire static fixture
dapr_scheduler: not-applicable: non-aspire static fixture
resource_snapshot: not-applicable: non-aspire static fixture
```

## Controls

Both controls use explicit linked control-run ids declared in metadata.

## Redaction

Synthetic sample only. All identifiers use non-production fixture names.

| Scenario | Observed result |
| --- | --- |
| linked-control | pass |
