# Operational Evidence Validator — DW4 Fixtures Root

This folder is the curated root for DW4 (`post-epic-deferred-dw4-operational-evidence-schema-validation`).

## Contents

- `fixtures/` — Positive and negative evidence fixtures used by the validator's
  test project (`tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests`).
- `entrypoint.txt` — **(dev-created during implementation)** Single line declaring
  the validator entrypoint. One of:
  - `pwsh:scripts/validate-evidence.ps1`
  - `sh:scripts/validate-evidence.sh`
  - `dotnet:Hexalith.EventStore.OperationalEvidenceValidator.Validator, Hexalith.EventStore.OperationalEvidenceValidator`
  Until this file is committed, all DW4 ATDD scaffolds remain skipped.
- `deferred-work-snapshot.md` — **(dev-created during implementation)** A snapshot of
  `_bmad-output/implementation-artifacts/deferred-work.md` taken at the start of
  DW4 implementation, used by `Dw4DeferredWorkDispositionAtddTests` to detect
  drift in unrelated bullets.

## Fixture Layout

Each fixture is a small markdown file representing one validator outcome:

- `query-valid-*.md` — query/v1 fixtures expected to pass.
- `query-invalid-*.md` — query/v1 fixtures expected to fail with a specific rule id.
- `signalr-valid-*.md` — signalr/v1 fixtures expected to pass.
- `signalr-invalid-*.md` — signalr/v1 fixtures expected to fail with a specific rule id.
- `schema-*.md` — schema-identification failure fixtures (missing/duplicate/contradictory/unsupported).
- `parse-*.md` — parser-failure fixtures (malformed YAML/table/heading).

The expected outcome and rule-id set per fixture is pinned in
`tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Fixtures/Dw4FixtureCatalog.cs`.

## Rules

- All fixtures must be **safe to commit** — no real tokens, no real production
  hostnames, no customer payload data.
- Fixture stubs created by the ATDD red-phase scaffolding are intentionally
  minimal. Dev fills the body during implementation so the validator's rule
  evaluation has something concrete to fire against.
- Adding a new fixture requires a matching entry in `Dw4FixtureCatalog.All`.
- Removing a fixture requires updating both the catalog and any test method
  that references the fixture filename.
