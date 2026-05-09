# Template-Looking Evidence Fixture - Still Audited

Schema version: `query-operational-evidence/v1`

## Metadata

```yaml
schema_version: query-operational-evidence/v1
evidence_run_id: dw9-template-looking-invalid-001
story_key: post-epic-deferred-dw9-evidence-validator-and-governance-polish
run_profile: non-aspire-static-fixture
final_classification: pass
reviewer_verdict: pass
redaction_statement: reviewed synthetic fixture; no bearer tokens, secrets, production hostnames, or customer payloads are present
false_positive_control: control recorded pass but no explicit run reference
correlation_control: same-run correlation control evidence_run_id:dw9-template-looking-invalid-001 recorded pass
apphost_url: not-applicable: non-aspire static fixture
dapr_placement: not-applicable: non-aspire static fixture
dapr_scheduler: not-applicable: non-aspire static fixture
resource_snapshot: not-applicable: non-aspire static fixture
```

## Controls

This filename contains the word template but does not match `*-template.md` and has no skip marker, so it must still be audited.

## Redaction

Synthetic sample only.

| Scenario | Observed result |
| --- | --- |
| still-audited | fail |
