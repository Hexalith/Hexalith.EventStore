# Operational Evidence Validator — DW4 Fixtures Root

This folder is the curated root for DW4 (`post-epic-deferred-dw4-operational-evidence-schema-validation`).

## Contents

- `fixtures/` - Positive and negative evidence fixtures used by
  `scripts/validate-operational-evidence.py --self-test`.
- `entrypoint.txt` - Single line declaring the script entrypoint used by the
  existing DW4 test scaffold: `pwsh:scripts/validate-evidence.ps1`.

## Validator Scope

DW4 validates only these schema versions:

- `query-operational-evidence/v1`
- `signalr-operational-evidence/v1`

Unknown or future schema versions fail closed until a later story maps them.
The default docs-validation gate runs the curated fixture self-test only; it
does not scan historical evidence folders.

Run locally:

```powershell
.\scripts\validate-evidence.ps1 --self-test
.\scripts\validate-evidence.ps1 --json path\to\evidence.md
```

```bash
bash scripts/validate-evidence.sh --self-test
bash scripts/validate-evidence.sh --json path/to/evidence.md
```

## Classification Mapping

| Schema | Allowed run-level classifications |
| --- | --- |
| `query-operational-evidence/v1` | `pass`, `path-viability`, `sample-only`, `diagnostic-only`, `not-claimable`, `product-failure`, `environment-blocker`, `instrumentation-gap`, `inconclusive` |
| `signalr-operational-evidence/v1` | `pass`, `product-failure`, `environment-blocker`, `instrumentation-gap`, `sample-only`, `inconclusive` |

Query-only downgrade values are `path-viability`, `diagnostic-only`, and
`not-claimable`. `instrumentation-gap` is shared even though the proof
boundaries differ by schema.

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
- Adding a new fixture requires adding the expected rule id to
  `EXPECTED_FIXTURE_RULES` in `scripts/validate-operational-evidence.py`.
