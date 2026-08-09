# Verification Gap findings (Story 3.13) — 2026-08-09

### Incomplete-runtime fail-closed test never observes unstructured preflight log
- Changed surface: IncompleteRuntimeEvidenceFailsClosed asserts ValidateRuntimeLog on platform logs only.
- Gap: never asserts retained smoke-preflight.log fails ValidatePreflightLog.
- Consequence: structured preflight can land while the named incomplete-runtime test stays green.

### NullReferenceException fail-closed catches are not exercised by missing-key mutations
- Changed surface: NRE catch filters on ValidateOciProvenance and siblings.
- Gap: existing tests short-circuit or mutate values only; missing-key paths unexercised.
- Consequence: NRE hardening can regress unnoticed.

### Other
- LogIsSupportSafe empty-byte gate is unreachable from ValidateRuntimeLog/ValidatePreflightLog because JsonNode.Parse runs first.
