# Query Operational Evidence Fixture - Invalid Control Linkage Missing

Schema version: `query-operational-evidence/v1`

## Metadata

```yaml
schema_version: query-operational-evidence/v1
evidence_run_id: dw9-query-link-missing-001
story_key: post-epic-deferred-dw9-evidence-validator-and-governance-polish
run_profile: non-aspire-static-fixture
final_classification: pass
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; no bearer tokens, secrets, production hostnames, or customer payloads are present
false_positive_control: control recorded pass but no explicit run reference
correlation_control: same-run correlation control evidence_run_id:dw9-query-link-missing-001 recorded pass
apphost_url: not-applicable: non-aspire static fixture
dapr_placement: not-applicable: non-aspire static fixture
dapr_scheduler: not-applicable: non-aspire static fixture
resource_snapshot: not-applicable: non-aspire static fixture
```

## Controls

False-positive control intentionally omits both same-run and linked-control-run references.

## Redaction

Synthetic sample only.

| Scenario | Observed result |
| --- | --- |
| missing-link | fail |
