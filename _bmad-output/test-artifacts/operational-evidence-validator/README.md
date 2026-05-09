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

DW9 adds explicit control-linkage validation for required controls. A control
result must name either the same run with `evidence_run_id:<id>` or a linked
control run with `control_run_id:<id>` listed in `linked_control_run_ids`.
Missing references emit `control-linkage-missing`; mismatched references emit
`control-linkage-unrelated`.

Directory or explicit-file validation skips Markdown files that:

- contain a stand-alone `<!-- evidence-validator: skip -->` HTML comment in the
  first 20 lines (whitespace-tolerant — `<!--   evidence-validator: skip   -->`
  also skips, but the marker must occupy its own line so prose like
  `... add a \`<!-- evidence-validator: skip -->\` opt-out marker` does NOT
  shadow-ban the surrounding doc), or
- have a filename matching `*-template.md` (case-insensitive — `Foo-Template.md`
  on Windows resolves the same as `foo-template.md`).

Skipped files emit an informational `evidence-file-skipped` diagnostic with
reason `marker` or `template-pattern`; they do not count as pass or fail.

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

The validator, the operations docs, and the templates share this single mapping table.
Update all four sources together when classifications change.

| Classification | `query-operational-evidence/v1` | `signalr-operational-evidence/v1` | Notes |
| --- | --- | --- | --- |
| `pass` | yes | yes | Run met all required gates. |
| `product-failure` | yes | yes | Product behaviour did not meet the proof. |
| `environment-blocker` | yes | yes | Test rig/environment prevented evidence capture. |
| `instrumentation-gap` | yes | yes | Required telemetry/instrumentation absent; verdict blocked. |
| `sample-only` | yes | yes | Sample-sized data only; cannot ground a release claim. |
| `inconclusive` | yes | yes | Evidence inconclusive — neither pass nor fail asserted. |
| `path-viability` | yes | no | Query-only downgrade: shape works but coverage incomplete. |
| `diagnostic-only` | yes | no | Query-only downgrade: diagnostics observed without product proof. |
| `not-claimable` | yes | no | Query-only downgrade: redaction/legal/scope blocks claim. |

Sources of truth (must agree):

- `scripts/validate-operational-evidence.py` (`QUERY_CLASSIFICATIONS`, `SIGNALR_CLASSIFICATIONS`)
- `docs/operations/query-operational-evidence.md` and `_bmad-output/test-artifacts/query-operational-evidence-template.md`
- `docs/operations/signalr-operational-evidence.md` and `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`

## Fixture Layout

Each fixture is a small markdown file representing one validator outcome:

- `query-valid-*.md` — query/v1 fixtures expected to pass.
- `query-invalid-*.md` — query/v1 fixtures expected to fail with a specific rule id.
- `signalr-valid-*.md` — signalr/v1 fixtures expected to pass.
- `signalr-invalid-*.md` — signalr/v1 fixtures expected to fail with a specific rule id.
- `schema-*.md` — schema-identification failure fixtures (missing/duplicate/contradictory/unsupported).
- `parse-*.md` — parser-failure fixtures (malformed YAML/table/heading).
- `skip-*.md` — DW9 skip-behavior fixtures used by focused tests; these are
  intentionally not part of the curated `--self-test` matrix.

The expected outcome and rule-id set per fixture is pinned in
`tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Fixtures/Dw4FixtureCatalog.cs`.

## Rules

- All fixtures must be **safe to commit** — no real tokens, no real production
  hostnames, no customer payload data.
- Adding a new fixture for the curated `--self-test` matrix requires adding the
  expected rule id to `EXPECTED_FIXTURE_RULES` in
  `scripts/validate-operational-evidence.py` AND a row in
  `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Fixtures/Dw4FixtureCatalog.cs`.

### DW9 skip-fixture catalog exception

`skip-marker-optout.md`, `skip-template.md`, and `template-looking-invalid.md`
are intentionally **not** registered in `EXPECTED_FIXTURE_RULES` or
`Dw4FixtureCatalog`. They exist to cover skip-marker and template-pattern
behavior via `Dw9EvidenceValidatorPolishTests` only. Registering them in the
self-test matrix would either pollute the curated baseline (they emit info
diagnostics that the matrix does not classify) or violate AC-5 by recursively
auditing template-shaped files in the default run. Future fixture audits should
treat the three filenames above as the documented carve-out — any *new*
skip-related fixture must either follow the same pattern (focused-test only)
or document why it joins the curated matrix.
