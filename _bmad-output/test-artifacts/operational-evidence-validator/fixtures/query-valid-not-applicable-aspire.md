# Query Operational Evidence Fixture - Valid Non-Aspire Profile

Schema version: `query-operational-evidence/v1`

## Metadata

```yaml
schema_version: query-operational-evidence/v1
evidence_run_id: dw4-query-non-aspire-001
story_key: post-epic-deferred-dw4-operational-evidence-schema-validation
run_profile: non-aspire-static-fixture
final_classification: diagnostic-only
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; Aspire and DAPR fields are explicitly out of scope
false_positive_control: same-run false-positive control evidence_run_id:dw4-query-non-aspire-001 recorded pass
correlation_control: same-run correlation control evidence_run_id:dw4-query-non-aspire-001 recorded pass
apphost_url: not-applicable: non-aspire proof
dapr_placement: not-applicable: non-aspire proof
dapr_scheduler: not-applicable: non-aspire proof
resource_snapshot: not-applicable: non-aspire proof
```

## Controls

Same-run controls are recorded above.

## Redaction

Synthetic sample only.

| Scenario | Observed result |
| --- | --- |
| non-aspire-control | pass |
