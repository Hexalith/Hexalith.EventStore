# Verification Gap findings (Story 3.13)

### Incomplete-runtime fail-closed test never observes unstructured smoke logs
- Changed surface: `ValidateRuntimeExecution` / `IncompleteRuntimeEvidenceFailsClosed` at DeployedRuntimeParityClosureTests.cs:347-367 and 2751-2936.
- Impacted consumer: fail-closed AC2 runtime blocker path via `ValidateActualFailClosedSubject` and packet claims in identity-crosswalk / runtime-verification.
- Gap: IncompleteRuntimeEvidenceFailsClosed only asserts ValidateRuntimeExecution false via DeepEquals/schema/Development≠Production; never asserts retained smoke logs fail ValidateRuntimeLog for missing structured fields.
- Consequence: packet can keep claiming unstructured-log incompleteness after logs become structured without failing the named incomplete-runtime test.

### Fail-closed subject accepts contradictory smoke-results pass
- Changed surface: ValidateActualFailClosedSubject over ExpectedSupportSafeJsonReports including smoke-results.json.
- Gap: fail-closed path only support-safety-checks smoke-results.json; no assertion that it must not claim result=pass when runtime_both_platforms is fail. Current evidence already has this contradiction and ValidateActualFailClosedSubject still passes.
- Consequence: fail-closed subject validation can certify a packet whose retained smoke summary claims pass while runtime checks remain fail-closed.
