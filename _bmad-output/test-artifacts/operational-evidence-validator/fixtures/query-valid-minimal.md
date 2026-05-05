# Query Operational Evidence Fixture - Valid Minimal

Schema version: `query-operational-evidence/v1`

## Metadata

```yaml
schema_version: query-operational-evidence/v1
evidence_run_id: dw4-query-valid-001
story_key: post-epic-deferred-dw4-operational-evidence-schema-validation
run_profile: non-aspire-static-fixture
final_classification: pass
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; no bearer tokens, secrets, production hostnames, or customer payloads are present
false_positive_control: same-run false-positive control dw4-query-control-001 recorded pass
correlation_control: same-run correlation control dw4-query-correlation-001 recorded pass
apphost_url: not-applicable: non-aspire static fixture
dapr_placement: not-applicable: non-aspire static fixture
dapr_scheduler: not-applicable: non-aspire static fixture
resource_snapshot: not-applicable: non-aspire static fixture
```

## Controls

False-positive control and correlation-integrity control are tied to the same synthetic run id.

## Redaction

Synthetic sample only. All identifiers use non-production fixture names.

| Scenario | Observed result |
| --- | --- |
| cache-hit-control | pass |
