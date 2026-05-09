# Skipped Evidence Marker Fixture

<!-- evidence-validator: skip -->

Schema version: `query-operational-evidence/v1`

## Metadata

```yaml
schema_version: query-operational-evidence/v1
evidence_run_id: <required>
story_key: <required>
run_profile: <required>
final_classification: <required>
reviewer_verdict: <required>
redaction_statement: <required>
false_positive_control: <required>
correlation_control: <required>
```

## Controls

The exact skip marker should suppress evidence-schema diagnostics while remaining visible as an informational skip.
