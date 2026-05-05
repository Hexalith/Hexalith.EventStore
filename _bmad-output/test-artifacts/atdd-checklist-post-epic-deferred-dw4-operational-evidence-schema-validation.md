---
storyId: post-epic-deferred-dw4-operational-evidence-schema-validation
storyKey: post-epic-deferred-dw4-operational-evidence-schema-validation
storyFile: _bmad-output/implementation-artifacts/post-epic-deferred-dw4-operational-evidence-schema-validation.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw4-operational-evidence-schema-validation.md
detectedStack: backend
testFramework: xunit-v3
inputDocuments:
  - _bmad-output/implementation-artifacts/post-epic-deferred-dw4-operational-evidence-schema-validation.md
  - _bmad-output/test-artifacts/query-operational-evidence-template.md
  - _bmad-output/test-artifacts/signalr-operational-evidence-template.md
  - scripts/validate-docs.ps1
  - scripts/validate-docs.sh
  - .github/workflows/docs-validation.yml
  - Directory.Packages.props
  - .claude/skills/bmad-testarch-atdd/resources/tea-index.csv
  - _bmad/tea/config.yaml
  - knowledge:data-factories
  - knowledge:test-quality
  - knowledge:test-healing-patterns
  - knowledge:test-levels-framework
  - knowledge:test-priorities-matrix
  - knowledge:ci-burn-in
generatedTestFiles: []
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-04c-aggregate
  - step-05-handoff
lastStep: step-05-handoff
lastSaved: 2026-05-05
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests.csproj
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4RuleVocabulary.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4Diagnostic.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/IDw4ValidatorInvoker.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4ValidatorInvokerFactory.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/InProcessValidatorInvoker.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/ShellScriptValidatorInvoker.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Fixtures/Dw4FixtureCatalog.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4ValidatorContractAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4QueryEvidencePositiveAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4QueryEvidenceNegativeAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4SignalrEvidencePositiveAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4SignalrEvidenceNegativeAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4SchemaIdentificationAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4ParseVsRuleSeparationAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4DiagnosticShapeAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4RedactionRulesAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4ProfileScopedAspireAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4NotApplicableMarkerAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4TaxonomyMappingAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4DocsValidationWiringAtddTests.cs
  - tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4DeferredWorkDispositionAtddTests.cs
  - _bmad-output/test-artifacts/operational-evidence-validator/README.md
  - _bmad-output/test-artifacts/operational-evidence-validator/fixtures/  (29 fixture stubs)
  - Hexalith.EventStore.slnx  (added test project entry)
totalScaffolds: 50
buildVerified: true
runtimeVerified: true
---

# ATDD Red-Phase Checklist — DW4 Operational Evidence Schema Validation

## Step 01 — Preflight & Context

### Stack Detection
- Detected: `backend` (.NET 10, xUnit v3, Shouldly, NSubstitute, YamlDotNet 16.3.0)
- DW4 is a **documentation-validator** story, not a runtime/product story. The validator itself is the deliverable; tests assert the validator's contract over fixture markdown files.
- Loading profile: backend-only knowledge fragments

### Prerequisites
- [x] Story has clear acceptance criteria (15 ACs covering schema scope, required-field enforcement, placeholder detection, taxonomy single-source, redaction, controls, profile-scoped Aspire fields, fixtures, toolchain fit, CI integration, scope, deferred-work hygiene, diagnostics shape, scope, bookkeeping)
- [x] Test framework configured: xUnit v3, Shouldly, NSubstitute, YamlDotNet are all centrally pinned in `Directory.Packages.props`
- [x] Dev environment available (.NET SDK 10.0.103 pinned in `global.json`)

### Implementation-Shape Decision (story Task 0.3) — DEFERRED TO DEV
Story explicitly leaves the choice between three validator shapes:
1. **PowerShell/shell script** under `scripts/` (matches `validate-docs.ps1/.sh` precedent — closest to existing docs-validation pattern)
2. **.NET test/tool project** (matches xUnit/Shouldly/YamlDotNet that already ship in the repo; aligns with story testing guidance "If implemented as a .NET test/tool, add focused tests around the parser/rule engine and run only the affected test project")
3. **JSON Schema + markdown lint companion** (story warns this cannot validate arbitrary markdown structure on its own; would need a conversion step)

**ATDD scaffold approach (shape-agnostic on assertion surface):**

Scaffold tests live in **a new .NET test project** so they compile and run regardless of which validator shape the dev picks:
- If the dev builds a .NET validator library/tool, tests reference it directly.
- If the dev builds a PowerShell/Python script, tests `Process.Start` the script and assert exit code + stdout/stderr diagnostic lines.
- Either way, the assertion contract (rule ids, diagnostic shape, fixture-by-fixture pass/fail outcomes) is the same.

This keeps the scaffolds:
- **Compile-clean** today (no dependency on yet-to-exist validator code — see Red-Phase Strategy below).
- **Stable** across the dev's eventual implementation choice.

### Target Artifacts (read-only context for scaffolds)

**Schema source documents** (validator must accept these as positive):
- `_bmad-output/test-artifacts/query-operational-evidence-template.md` — `query-operational-evidence/v1` template, 9 allowed run classifications, 13 required metadata fields, fail-closed reviewer checklist
- `_bmad-output/test-artifacts/signalr-operational-evidence-template.md` — `signalr-operational-evidence/v1` template, 6 allowed classifications, smaller required-field set, intentionally invalid example included

**Authoritative docs** (must agree with validator on taxonomy):
- `docs/operations/query-operational-evidence.md`
- `docs/operations/signalr-operational-evidence.md`

**Toolchain hooks** (validator wires here per AC #10):
- `scripts/validate-docs.ps1` (current: markdownlint → lychee → sample build/test; DW4 adds evidence-schema validation as a new stage)
- `scripts/validate-docs.sh` (parallel shell entrypoint)
- `.github/workflows/docs-validation.yml` (CI gate)

**Negative-fixture target location** (story Implementation Inventory):
- `_bmad-output/test-artifacts/operational-evidence-validator/` — curated positive/negative samples (fixtures), safe to commit, synthetic data only

### New Test Project & Files (planned)

Test project: **`tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests`** (new, conventional location matching repo `tests/Hexalith.EventStore.<Component>.Tests` pattern)

| File | Purpose | ACs targeted |
|---|---|---|
| `Hexalith.EventStore.OperationalEvidence.Validator.Tests.csproj` | xunit.v3 + Shouldly + YamlDotNet; references validator project OR shells out to script | n/a |
| `Fixtures/Dw4FixtureCatalog.cs` | Static index of curated fixture paths and expected rule-id sets per fixture | n/a |
| `Dw4ValidatorContractAtddTests.cs` | Validator-shape agnostic invocation entrypoint (`Validate(IEnumerable<string> paths) -> ValidationResult` or `RunValidatorScript(args) -> CliResult`); proves command exists, exits non-zero on invalid input | #1, #2, #13 |
| `Dw4QueryEvidencePositiveAtddTests.cs` | Valid query evidence fixtures pass, including `not-applicable: <reason>` profile-scoped Aspire skip | #1, #2, #7 |
| `Dw4QueryEvidenceNegativeAtddTests.cs` | One negative fixture per required-rule family for query schema, each asserting **expected rule id** (not just non-zero exit) | #2, #3, #4, #5, #6, #13 |
| `Dw4SignalrEvidencePositiveAtddTests.cs` | Valid SignalR evidence fixtures pass | #1, #2, #7 |
| `Dw4SignalrEvidenceNegativeAtddTests.cs` | One negative fixture per required-rule family for SignalR schema, each asserting expected rule id | #2, #3, #4, #5, #6, #13 |
| `Dw4SchemaIdentificationAtddTests.cs` | Missing/duplicate/contradictory/unsupported schema markers fail with stable schema-id rule ids | #1, #13 |
| `Dw4ParseVsRuleSeparationAtddTests.cs` | Malformed YAML, malformed table, duplicate heading produce **parser** rule ids distinct from business-rule ids | #13 |
| `Dw4DiagnosticShapeAtddTests.cs` | Every emitted diagnostic carries `file`, `schema`, `rule`, `section`, `field`, `hint`; sort order is stable across runs | #13 |
| `Dw4RedactionRulesAtddTests.cs` | Bearer-token, connection-string keyword, production hostname, raw secret markers fail unless explicitly redacted; documented synthetic markers pass | #5 |
| `Dw4ProfileScopedAspireAtddTests.cs` | Aspire fields `not-applicable: <reason>` accepted when proof is non-Aspire; required when proof claims Aspire/DAPR runtime | #7 |
| `Dw4NotApplicableMarkerAtddTests.cs` | `not-applicable: <reason>` accepted with reason; rejected when reason missing or generic; rejected when used on a field the schema does not mark profile-specific or optional | #2 |
| `Dw4TaxonomyMappingAtddTests.cs` | Validator's allowed-classification enum for query and SignalR matches what the templates declare; mapping table is the single source so query↔SignalR drift fails | #4 |
| `Dw4DocsValidationWiringAtddTests.cs` | If validator is wired into `validate-docs.{ps1,sh}` and `docs-validation.yml`, those entrypoints invoke the validator stage and exit non-zero on bad fixture; if CI is intentionally deferred, presence of a documented CI-deferred reason is asserted | #10 |
| `Dw4DeferredWorkDispositionAtddTests.cs` | DW4-relevant `deferred-work.md` bullets carry one of `STORY:post-epic-deferred-dw4-operational-evidence-schema-validation`/`RESOLVED`/`ACCEPTED-DEBT`/`DUPLICATE`/`NO-ACTION`; unrelated DW1/DW2/DW3/DW5/DW6/SignalR-policy/Admin-UI/release-governance entries are unchanged | #12 |

**Fixture catalog** (under `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/`):

| Fixture file | Schema | Verdict | Rule ids the test asserts |
|---|---|---|---|
| `query-valid-minimal.md` | query/v1 | pass | (none — pass) |
| `query-valid-not-applicable-aspire.md` | query/v1 | pass | (none — `not-applicable: non-aspire-proof` accepted) |
| `query-invalid-missing-metadata.md` | query/v1 | fail | `query-required-metadata-missing` (per missing field) |
| `query-invalid-placeholder-unreplaced.md` | query/v1 | fail | `placeholder-unreplaced` |
| `query-invalid-empty-required-table-cell.md` | query/v1 | fail | `required-table-cell-empty` |
| `query-invalid-classification-not-in-enum.md` | query/v1 | fail | `classification-invalid` |
| `query-invalid-control-missing.md` | query/v1 | fail | `control-required-missing` |
| `query-invalid-correlation-control-missing.md` | query/v1 | fail | `correlation-control-required-missing` |
| `query-invalid-redaction-bearer-token.md` | query/v1 | fail | `redaction-unsafe-bearer-token` |
| `query-invalid-redaction-connection-string.md` | query/v1 | fail | `redaction-unsafe-connection-string` |
| `query-invalid-redaction-production-hostname.md` | query/v1 | fail | `redaction-unsafe-production-hostname` |
| `query-invalid-redaction-section-missing.md` | query/v1 | fail | `redaction-section-missing` |
| `query-invalid-not-applicable-empty-reason.md` | query/v1 | fail | `not-applicable-reason-missing` |
| `query-invalid-not-applicable-on-required-field.md` | query/v1 | fail | `not-applicable-not-allowed-here` |
| `query-invalid-aspire-claimed-but-fields-missing.md` | query/v1 | fail | `profile-aspire-fields-missing` |
| `signalr-valid-minimal.md` | signalr/v1 | pass | (none — pass) |
| `signalr-invalid-missing-metadata.md` | signalr/v1 | fail | `signalr-required-metadata-missing` |
| `signalr-invalid-placeholder-unreplaced.md` | signalr/v1 | fail | `placeholder-unreplaced` |
| `signalr-invalid-classification-not-in-enum.md` | signalr/v1 | fail | `classification-invalid` |
| `signalr-invalid-control-missing.md` | signalr/v1 | fail | `control-required-missing` |
| `signalr-invalid-redaction-bearer-token.md` | signalr/v1 | fail | `redaction-unsafe-bearer-token` |
| `schema-missing.md` | none | fail | `schema-version-missing` |
| `schema-duplicate-markers.md` | both | fail | `schema-version-duplicate` |
| `schema-contradictory.md` | conflict | fail | `schema-version-contradictory` |
| `schema-unsupported-future-version.md` | query/v2 | fail | `schema-version-unsupported` |
| `parse-malformed-yaml.md` | query/v1 | fail | `parse-yaml-malformed` (parser ID, distinct from rule IDs) |
| `parse-malformed-table.md` | query/v1 | fail | `parse-table-malformed` |
| `parse-duplicate-required-heading.md` | query/v1 | fail | `parse-heading-duplicate` |

Total fixtures: ~28 (16 query + 6 SignalR + 4 schema-id + 3 parser failure-mode + 2 valid `not-applicable` cases reused).

### Stable Diagnostic Vocabulary (binding for assertions)

**Rule ids** (validator must emit these literal strings; tests assert with `ShouldBe`):
- `query-required-metadata-missing` — applies per missing field; diagnostic carries `field` name
- `signalr-required-metadata-missing` — applies per missing field
- `placeholder-unreplaced` — `<required>`, `<...>`, template row labels (`scenario-id`, etc.), empty required table cells
- `required-table-cell-empty`
- `classification-invalid` — value not in template's enum
- `control-required-missing` — at least one false-positive control absent
- `correlation-control-required-missing` — correlation-integrity control absent
- `redaction-section-missing` — required Redaction section absent
- `redaction-unsafe-bearer-token` — `eyJ...`, `Bearer <hex>`, etc.
- `redaction-unsafe-connection-string` — `Server=`, `Password=`, `Endpoint=sb://...`
- `redaction-unsafe-production-hostname` — `*.prod.*`, customer-domain hosts (validator ships with documented allowlist for synthetic placeholders)
- `redaction-raw-secret-marker` — `AKIA...`, GitHub PAT shapes, `xoxb-...`
- `not-applicable-reason-missing` — `not-applicable:` with empty/generic reason
- `not-applicable-not-allowed-here` — marker used on a field the schema marks required-non-optional
- `profile-aspire-fields-missing` — Aspire/DAPR claim but fields blank/missing
- `schema-version-missing`
- `schema-version-duplicate`
- `schema-version-contradictory`
- `schema-version-unsupported`
- `parse-yaml-malformed` (parser family — distinct prefix from rule family)
- `parse-table-malformed` (parser family)
- `parse-heading-duplicate` (parser family)
- `parse-section-ambiguous` (parser family)

**Rule-id naming contract** (asserted by `Dw4DiagnosticShapeAtddTests.cs`): regex `^[a-z][a-z0-9-]*$`, length ≤ 64. Parser-family ids must start with `parse-`. Schema-id-family ids must start with `schema-version-`. No other prefix is allowed to collide with these.

**Diagnostic shape** (asserted struct): `{ file: string, schema: "query-operational-evidence/v1" | "signalr-operational-evidence/v1" | null, rule: string, section: string?, field: string?, line: int?, hint: string }`. JSON output shape pinned for future automation summary.

**Diagnostic sort order** (deterministic): by `(file, schema, rule, section, field, line)` ascending. Stable across runs against same inputs.

**Disposition markers** (for `deferred-work.md` reconciliation, asserted by `Dw4DeferredWorkDispositionAtddTests.cs`):
- `STORY:post-epic-deferred-dw4-operational-evidence-schema-validation`
- `RESOLVED`
- `ACCEPTED-DEBT`
- `DUPLICATE`
- `NO-ACTION`

### TEA Config Flags
- `tea_use_playwright_utils`: true (skipped — no UI surface in DW4 scope)
- `tea_use_pactjs_utils`: true (skipped — no contract changes in DW4)
- `tea_browser_automation`: auto (skipped — pure docs/markdown validator)
- `test_stack_type`: auto → backend
- `risk_threshold`: p1

### Knowledge Fragments Loaded
- Core (always): `data-factories`, `test-quality`, `test-healing-patterns`
- Backend (mandatory for this stack): `test-levels-framework`, `test-priorities-matrix`, `ci-burn-in`
- Skipped: Playwright Utils, Pact.js Utils, MCP fragments, frontend selector/timing patterns

### Risk-Based Priorities (preview for Step 03)
- **P0**: AC #2 (required-field enforcement), AC #3 (placeholder detection), AC #5 (redaction rules), AC #6 (controls/correlation), AC #13 (diagnostic shape — automation depends on it)
- **P1**: AC #1 (schema scope is bounded), AC #4 (taxonomy single-source), AC #7 (profile-scoped Aspire), AC #8 (positive/negative samples)
- **P2**: AC #9 (toolchain fit), AC #10 (CI integration), AC #11 (no mass rewrite), AC #12 (deferred-work dispositions)
- **N/A (story closure)**: AC #14 (scope boundaries — reviewer guardrail), AC #15 (bookkeeping — Dev Agent Record)

### Confirmation

User confirmed all five items on 2026-05-05:
1. Test project location: `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests` — OK
2. Fixture root: `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/` — OK
3. Validator-shape-neutral scaffolds with `[Fact(Skip = "...")]` red-phase pattern — OK
4. Literal rule-id vocabulary as assertion contract — OK
5. Diagnostic shape `{ file, schema, rule, section, field, line, hint }` with stable sort — OK

## Step 02 — Generation Mode

**Mode chosen: AI Generation**

Rationale:
- `{detected_stack}` = `backend` → backend rule mandates AI generation (no browser recording).
- DW4 is a documentation/markdown validator story. There is no UI surface to record.
- Acceptance criteria already pin a stable rule-id vocabulary, fixture taxonomy, and diagnostic shape (Step 01 confirmation), which is sufficient to author failing xUnit scaffolds directly from the templates and story ACs.
- Story explicitly forbids new UI/MCP/runtime work (AC #14), so a recorded UI flow would be out of scope even if applicable.

Recording mode skipped per step-02 rule for backend stack.

## Step 03 — Test Strategy

### Story-Specific Constraint

DW4 is a **documentation-validator** story. Tests assert the validator's behavior over committed fixture markdown files. There is no DB, DAPR, HTTP, SignalR, or product runtime boundary to test. This collapses the standard "unit / integration / contract / E2E" matrix to **unit only**, plus a **shell-smoke** test that exercises whichever entrypoint the dev wires into `validate-docs.{ps1,sh}`.

### Acceptance Criteria → Test Mapping

| AC | Concern | Priority | Level | Target test file | Red-phase scaffold scenarios |
|---|---|---|---|---|---|
| #1 | Validator scope is bounded to `query/v1` and `signalr/v1` | P1 | Unit | `Dw4ValidatorContractAtddTests.cs`, `Dw4SchemaIdentificationAtddTests.cs` | (a) Validator accepts both supported schemas; (b) Validator rejects `query-operational-evidence/v2`, arbitrary BMAD story files, release evidence, runtime logs with `schema-version-unsupported`; (c) When invoked on a directory, validator only inspects files declaring a supported `schema_version` and skips others without false positives |
| #2 | Required-field enforcement is mechanical | P0 | Unit | `Dw4QueryEvidenceNegativeAtddTests.cs`, `Dw4SignalrEvidenceNegativeAtddTests.cs`, `Dw4NotApplicableMarkerAtddTests.cs` | (a) For each of the 13 query required-metadata fields, a fixture missing that field fails with `query-required-metadata-missing` and `field` = the missing name; (b) Same for SignalR required fields with `signalr-required-metadata-missing`; (c) `not-applicable: <reason>` accepted when reason is non-empty/specific; rejected when reason empty/generic with `not-applicable-reason-missing`; (d) `not-applicable` rejected on truly required (non-profile) fields with `not-applicable-not-allowed-here` |
| #3 | Placeholder detection is strict | P0 | Unit | `Dw4QueryEvidenceNegativeAtddTests.cs` | (a) `<required>`, `<...>`, angle-bracket templates fail with `placeholder-unreplaced` carrying section+field; (b) Template row labels (`scenario-id`, `gap`, `owner`) in required tables fail; (c) Empty required table cells fail with `required-table-cell-empty`; (d) Optional-section placeholders (e.g., `UI Render Evidence` when `Required for this proof: no`) do **not** fail |
| #4 | Classification taxonomy single-source | P1 | Unit | `Dw4TaxonomyMappingAtddTests.cs` | (a) Validator's allowed-classification enum for query schema matches the 9 values in the template literally; (b) Same for SignalR's 6 values; (c) An invalid classification (`pending`, `partial`, `tbd`) fails with `classification-invalid`; (d) Mapping table between query↔SignalR classifications is exposed by the validator (e.g., `validator describe taxonomy` or test-visible constant) and any drift between docs/templates and validator data fails the test |
| #5 | Redaction rules validated before commit | P0 | Unit | `Dw4RedactionRulesAtddTests.cs` | (a) Bearer-token-shaped string (`eyJ...`) outside a redacted-marker context fails with `redaction-unsafe-bearer-token`; (b) Connection-string keyword (`Server=`, `Password=`, `Endpoint=sb://`) fails with `redaction-unsafe-connection-string`; (c) Production hostname pattern (`*.prod.contoso.com`) fails with `redaction-unsafe-production-hostname`; (d) Raw secret marker (`AKIA[0-9A-Z]{16}`, `xoxb-...`, GitHub PAT shape) fails with `redaction-raw-secret-marker`; (e) Missing required `Redaction` section fails with `redaction-section-missing`; (f) Documented synthetic markers (`tenant-alias-001`, `<redacted>`, `safe-domain-alias`) under a Redaction section pass; (g) Documented escape path for false-positive synthetic example explicitly tagged works |
| #6 | Controls and correlation checks first-class | P0 | Unit | `Dw4QueryEvidenceNegativeAtddTests.cs` (control fixtures), `Dw4SignalrEvidenceNegativeAtddTests.cs` | (a) Missing false-positive control fails with `control-required-missing`; (b) Missing correlation-integrity control fails with `correlation-control-required-missing`; (c) Control with blank `Observed result` or `not-recorded` fails as if missing; (d) Control declared as `linked: <run-id>` is accepted only when `<run-id>` is non-empty and matches an existing pattern; (e) `Same-run as claim` field set to `linked: <run-id>` with empty run-id rejected |
| #7 | Aspire-specific evidence is optional / profile-scoped | P1 | Unit | `Dw4ProfileScopedAspireAtddTests.cs` | (a) Non-Aspire proof with Aspire fields filled `not-applicable: non-aspire-proof` → pass; (b) Non-Aspire proof with Aspire fields blank (no marker) → fail with `placeholder-unreplaced` (still a missing-required event under default profile, OR pass if profile detected as non-Aspire — assert the documented behavior); (c) Aspire-claimed proof (any field references `Aspire AppHost`, `DAPR placement`, etc.) requires those fields filled; missing fields fail with `profile-aspire-fields-missing` |
| #8 | Positive and negative samples prove validator | P0 | Unit (data-driven theory) | `Dw4ValidatorContractAtddTests.cs`, `Dw4FixtureCatalog.cs` | (a) Theory data row per fixture file in `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/`; assertion: validator's pass/fail outcome matches `Dw4FixtureCatalog.Expected[fixture]`; (b) For each negative fixture, the **expected rule id set** is asserted as a subset of emitted diagnostic rule ids — a non-zero exit alone is not sufficient (per AC; protects against parser regressions masquerading as rule coverage); (c) Fixture coverage matrix (each rule family has ≥1 negative fixture) is asserted by `Fixture_CoverageMatrix_AllRuleFamiliesCovered` test that walks `Dw4FixtureCatalog` and verifies every rule family in the vocabulary list is exercised by ≥1 negative fixture |
| #9 | Fits repository toolchain | P2 | Unit (smoke) | `Dw4DocsValidationWiringAtddTests.cs` | (a) Validator entrypoint exists at one of the documented locations (`scripts/validate-evidence.{ps1,sh}` OR `tools/Hexalith.EventStore.OperationalEvidenceValidator/` OR `dotnet test` invocation against this very test project); (b) Test reads dev's recorded entrypoint from `_bmad-output/test-artifacts/operational-evidence-validator/entrypoint.txt` (created during dev) and asserts file/command exists; (c) If JSON Schema is used, schema declares `$schema: "https://json-schema.org/draft/2020-12/schema"` |
| #10 | CI integration is deliberate | P2 | Unit (smoke) | `Dw4DocsValidationWiringAtddTests.cs` | (a) `scripts/validate-docs.ps1` and `scripts/validate-docs.sh` either invoke the new validator stage OR contain a documented CI-deferred reason marker (e.g., comment line `# DW4-CI-DEFERRED: <reason>`); (b) `.github/workflows/docs-validation.yml` either has a step invoking the new validator OR a documented CI-deferred reason; (c) Smoke: shell out to dev's entrypoint with a known-bad fixture, assert non-zero exit |
| #11 | No mass rewrite of existing evidence | P2 | Unit | `Dw4ValidatorContractAtddTests.cs` | (a) Validator's default scope is templates + curated fixture root; (b) Running validator over existing `_bmad-output/test-artifacts/post-epic-*` historical evidence folders is **not** in the default invocation; (c) If repository-wide audit mode exists, it is opt-in via explicit flag and does not gate CI |
| #12 | Deferred-work dispositions narrow | P2 | Unit | `Dw4DeferredWorkDispositionAtddTests.cs` | (a) Read `_bmad-output/implementation-artifacts/deferred-work.md`; (b) Identify DW4-relevant bullets (text matching query operational evidence validator OR signalr operational evidence validator); (c) Each must carry one of `STORY:post-epic-deferred-dw4-operational-evidence-schema-validation`, `RESOLVED`, `ACCEPTED-DEBT`, `DUPLICATE`, `NO-ACTION`; (d) DW1/DW2/DW3/DW5/DW6/SignalR-policy/Admin-UI/release-governance bullets unchanged from a recorded snapshot taken at story start (snapshot path: `_bmad-output/test-artifacts/operational-evidence-validator/deferred-work-snapshot.md`) |
| #13 | Diagnostic output is reviewer- and automation-friendly | P0 | Unit | `Dw4DiagnosticShapeAtddTests.cs`, `Dw4ParseVsRuleSeparationAtddTests.cs` | (a) Each diagnostic carries `file`, `schema`, `rule`, `section`, `field`, `line`, `hint`; (b) Rule ids match `^[a-z][a-z0-9-]*$`, length ≤ 64; (c) Parser-family ids start with `parse-`, schema-id-family ids start with `schema-version-`; (d) Sort order across runs is identical for the same input set (run validator twice, compare diagnostic sequences); (e) Parser failures (malformed YAML, malformed table, duplicate heading) produce parser ids and **not** business-rule ids — a malformed-YAML fixture must not also emit `query-required-metadata-missing` (because parsing failed before that rule could run); (f) Schema-id failures produce schema-id family rules and not business-rule ids |
| #14 | Scope boundaries intact | n/a | Code review | (none — reviewer guardrail) | Reviewer checklist; checklist asserts no DW2/DW3/DW5/DW6 file changes |
| #15 | Bookkeeping closed | n/a | Story closure | (none) | Dev Agent Record + sprint-status updates at handoff |

### Test Level Selection

- **Unit (single test project `Hexalith.EventStore.OperationalEvidence.Validator.Tests`)**: All DW4 ACs except #14 and #15.
  - Justification: every behavioral AC reduces to "given fixture file X, validator produces outcome Y with diagnostic rule ids Z." That is a pure unit-test shape.
  - Validator can be invoked **in-process** (if dev picks .NET library) via direct API call, or **out-of-process** (if dev picks shell script) via `Process.Start`. Tests use a `IDw4ValidatorInvoker` test seam with two implementations: `InProcessValidatorInvoker` and `ShellScriptValidatorInvoker`. Default invoker is selected by reading `_bmad-output/test-artifacts/operational-evidence-validator/entrypoint.txt` (single line, written by dev). If the file is absent, all scaffolds are skipped — preventing hard-coded coupling to a not-yet-chosen shape.
- **No Integration / Contract / E2E** — DW4 explicitly excludes runtime, contracts, browsers (AC #14).

### Red-Phase Strategy (CI-Compatible)

Project rule (CLAUDE.md): "All existing and new tests must pass before a story is complete." A literal red-phase suite would break CI immediately.

**Strategy** (same as DW1/DW3, proven 2026-05-05):
- Author scaffolds with `[Fact(Skip = "ATDD red phase — DW4 AC#X. Remove Skip when implementing.")]` and `[Theory(Skip = "...")]`.
- Each test compiles cleanly today (uses string literals for rule ids; references only YamlDotNet + System.Text.Json + xUnit + Shouldly types — all already pinned in `Directory.Packages.props`).
- The `IDw4ValidatorInvoker` interface ships in the test project — no production dependency. When dev picks a shape and writes `entrypoint.txt`, the matching invoker activates. Until then, any test removing its `Skip` will fail (no validator to call) — the desired red state.
- Dev workflow: as each rule implementation lands, remove `Skip` for the matching test(s), watch them go red (correct rule id not emitted yet), implement the rule, watch them go green.

### Reuse & Helpers

- New helpers in this test project (no shared cross-project plumbing):
  - `Dw4FixtureCatalog.cs` — `IReadOnlyDictionary<string, FixtureExpectation>` mapping each fixture filename to `(SchemaVersion, Outcome, ExpectedRuleIds)`.
  - `Dw4RuleVocabulary.cs` — frozen `IReadOnlySet<string>` of every rule id this story commits to.
  - `IDw4ValidatorInvoker` + `Dw4ValidatorInvokerFactory` — selects in-process vs shell-script invoker from `entrypoint.txt`.
  - `Dw4DiagnosticContract.cs` — strongly-typed diagnostic record (`{ File, Schema, Rule, Section, Field, Line, Hint }`) with stable `IComparer<Dw4Diagnostic>` implementing `(File, Schema, Rule, Section, Field, Line)` ascending sort.
- No reuse from other test projects — DW4's domain (markdown evidence validation) is disjoint from the existing `Hexalith.EventStore.*` test surfaces.

### Out of Scope for This Run

- **AC #14 (scope boundaries)** — reviewer guardrail.
- **AC #15 (bookkeeping)** — Dev Agent Record + Change Log + sprint-status updates at story close.
- **Real validator implementation** — that is the dev's job in `bmad-dev-story`; this run only writes failing scaffolds.
- **Real positive/negative fixture content** — fixture files are created with placeholder bodies in `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/` so the test project's theory rows compile; dev fills the bodies during implementation. The `Dw4FixtureCatalog` test will detect any drift between fixture filenames and catalog entries.

### Risk-Based Justification

P0 ACs (#2, #3, #5, #6, #8, #13): the validator is the gate that prevents unsafe evidence (secrets, drift, missing controls) from reaching reviewers. A miss in any of these defeats the story's purpose. Diagnostic shape (#13) is P0 because future automation depends on it being stable.

P1 ACs (#1, #4, #7): scope boundedness, taxonomy single-sourcing, and profile-scoping are correctness invariants that prevent silent over-reach (validating files it shouldn't) or quiet drift (validator and docs disagree on classifications).

P2 ACs (#9, #10, #11, #12): toolchain integration, CI wiring, no-mass-rewrite, and deferred-work hygiene are repo-hygiene concerns. They matter for closure but are not behavioral-correctness gates.

### Confirmation

Strategy locked in. Proceeding to step-04 (generate tests).

## Step 04 — Generated Tests

### Execution Mode

`sequential` — generated all test files in-process rather than dispatching API/E2E worker subagents. Same precedent as DW1 and DW3 ATDD runs (no UI surface; backend stack with no API/E2E split for a docs-validator story).

### Files

| File | Purpose | Tests | ACs |
|---|---|---|---|
| `Hexalith.EventStore.OperationalEvidence.Validator.Tests.csproj` | New test project — xunit.v3 + Shouldly + YamlDotNet (no project references; tests target an interface seam, see `IDw4ValidatorInvoker`) | 0 (project) | n/a |
| `Dw4RuleVocabulary.cs` | Frozen rule-id vocabulary (24 ids) + classification enums + disposition markers | 0 (constants) | #4, #13 |
| `Dw4Diagnostic.cs` | `Dw4Diagnostic` record, `Dw4ValidationOutcome` record, `Dw4DiagnosticComparer` for stable sort | 0 (types) | #13 |
| `IDw4ValidatorInvoker.cs` | Validator-shape-neutral invocation seam | 0 (interface) | #9, #10 |
| `Dw4ValidatorInvokerFactory.cs` | Selects in-process / pwsh / sh invoker from `entrypoint.txt`; throws `Dw4ValidatorNotConfiguredException` until dev declares the entrypoint | 0 (factory) | #9 |
| `InProcessValidatorInvoker.cs` | Reflection-based .NET validator invocation (`dotnet:` scheme) | 0 (impl) | n/a |
| `ShellScriptValidatorInvoker.cs` | Shells out to `pwsh:` / `sh:` script and parses JSON diagnostics | 0 (impl) | n/a |
| `Fixtures/Dw4FixtureCatalog.cs` | Single source of truth for fixture filename → expected verdict + rule-id set | 0 (catalog) | #8 |
| `Dw4ValidatorContractAtddTests.cs` | Validator-perimeter tests: entrypoint resolution; default-scope no-recursion; full catalog theory; coverage matrix | 4 (1 theory expanded to 29 rows = 32 emitted) | #1, #2, #8, #11 |
| `Dw4QueryEvidencePositiveAtddTests.cs` | Query/v1 valid fixtures pass | 2 | #1, #2, #7 |
| `Dw4QueryEvidenceNegativeAtddTests.cs` | Query/v1 negative fixtures emit expected rule ids | 6 | #2, #3, #6 |
| `Dw4SignalrEvidencePositiveAtddTests.cs` | SignalR/v1 valid fixture passes | 1 | #1, #2 |
| `Dw4SignalrEvidenceNegativeAtddTests.cs` | SignalR/v1 negative fixtures emit expected rule ids | 4 | #2, #3, #6 |
| `Dw4SchemaIdentificationAtddTests.cs` | Missing/duplicate/contradictory/unsupported schema markers fail closed; no business-rule noise | 4 | #1, #13 |
| `Dw4ParseVsRuleSeparationAtddTests.cs` | Parser failures isolate from business-rule diagnostics | 3 | #13 |
| `Dw4DiagnosticShapeAtddTests.cs` | Rule-id naming contract; required diagnostic fields; deterministic ordering; canonical sort; family-prefix invariants | 5 | #13 |
| `Dw4RedactionRulesAtddTests.cs` | Bearer / connection-string / hostname / raw-secret / missing-section all fail; documented synthetic markers pass | 7 | #5 |
| `Dw4ProfileScopedAspireAtddTests.cs` | Aspire fields profile-scoped: `not-applicable: <reason>` accepted; Aspire-claimed without fields fails | 2 | #7 |
| `Dw4NotApplicableMarkerAtddTests.cs` | Empty-reason marker fails; marker on non-profile required field fails | 2 | #2 |
| `Dw4TaxonomyMappingAtddTests.cs` | Validator's classification enum matches templates; query↔SignalR diff documented | 3 | #4 |
| `Dw4DocsValidationWiringAtddTests.cs` | `entrypoint.txt` exists; `validate-docs.{ps1,sh}` and `docs-validation.yml` invoke validator OR carry `DW4-CI-DEFERRED:` marker; smoke runs known-bad fixture | 5 | #9, #10 |
| `Dw4DeferredWorkDispositionAtddTests.cs` | DW4-relevant bullets carry disposition marker; unrelated bullets unchanged from snapshot | 2 | #12 |
| **Total tests emitted** | | **50 (49 facts + 1 theory expanded over 29 fixture rows)** | |
| `_bmad-output/test-artifacts/operational-evidence-validator/README.md` | Fixture root descriptor + dev-instructions for `entrypoint.txt` and snapshot | n/a | #9, #12 |
| `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/` | 29 fixture stubs (placeholder bodies; dev fills during implementation) | n/a | #8 |
| `Hexalith.EventStore.slnx` | Added new test project to `/tests/` folder | n/a | n/a |

All 50 tests use `[Fact(Skip = "ATDD red phase — DW4 AC#X. Remove Skip when implementing.")]` (or `[Theory(Skip = ...)]` on the catalog theory).

### Build & Runtime Verification

- `dotnet build tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/ --configuration Release -p:NuGetAudit=false` — **0 warnings, 0 errors** (1.33s).
- `dotnet test tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/ --configuration Release --no-build -p:NuGetAudit=false` — **Failed: 0, Passed: 0, Skipped: 50, Total: 50** (7ms).
- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` — **0 warnings, 0 errors** (9.30s). No regressions in existing projects.

### Compile-time fixes applied during generation

- `Shouldly.ShouldBe(IEnumerable<T>, IEnumerable<T>, ignoreOrder, customMessage)` requires `customMessage` as named arg in xUnit v3 — used named args throughout.
- `IComparer<int?>` for `Line` sort: used `Comparer<int?>.Default.Compare` to handle nullable comparison cleanly.
- `Type.GetMethod(string, BindingFlags, ...)` — used the explicit `(BindingFlags, Binder?, Type[], ParameterModifier[]?)` overload to disambiguate from `GetMethod(string)`.

## Step 04C — Aggregation

### TDD Red Phase Compliance

✅ All 50 tests emitted with `Skip = "..."`.
✅ No active passing tests written.
✅ Tests reference only types defined within this test project (no production dependency that would block compilation today).
✅ Validator-shape-neutral invoker seam ensures scaffolds remain stable when dev picks pwsh/sh/dotnet shape.
✅ Fixture catalog is the single source of truth; tests assert against it directly so a missing fixture is caught at test discovery, not at validator runtime.

### Anti-noise guarantees

- Theory data provider (`FixtureCatalogRows`) returns string filenames only — no disk I/O at discovery.
- All disk reads happen inside the test body (which is skipped in CI).
- `Dw4ValidatorInvokerFactory.Create()` only runs when a test removes its `Skip` AND `entrypoint.txt` is committed — preventing false-red noise for dev who has not yet declared the validator shape.

## Step 05 — Dev Handoff

### How the dev opens the red phase

DW4 is unusual: the "validator" itself is the deliverable, so the dev's first action is **declaring the validator shape**, not removing a `Skip`.

1. **Pick a validator shape** (story Task 0.3):
   - **PowerShell/shell script** → write `scripts/validate-evidence.ps1` and `scripts/validate-evidence.sh`. Add line `pwsh:scripts/validate-evidence.ps1` to `_bmad-output/test-artifacts/operational-evidence-validator/entrypoint.txt`.
   - **.NET tool/library** → create `src/Hexalith.EventStore.OperationalEvidenceValidator/` (or similar), declare a public static `Validate(IEnumerable<string>) -> Dw4ValidationOutcome` method, project-reference it from the test csproj, write line `dotnet:Hexalith.EventStore.OperationalEvidenceValidator.Validator, Hexalith.EventStore.OperationalEvidenceValidator` to `entrypoint.txt`.
   - **JSON Schema + markdown lint companion** → write the schema files + lint script and pick the `pwsh:` or `sh:` scheme to invoke them.
2. **Take the deferred-work snapshot** (one time, before editing `deferred-work.md`):
   - `cp _bmad-output/implementation-artifacts/deferred-work.md _bmad-output/test-artifacts/operational-evidence-validator/deferred-work-snapshot.md`
3. **Fill the fixture bodies** (29 stubs in `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/`):
   - Each stub has a `<!-- TODO(dev): … Expects rule '<rule-id>'. -->` directive describing the intended content.
   - Reference the templates (`query-operational-evidence-template.md`, `signalr-operational-evidence-template.md`) for valid/invalid shapes.
   - Keep all data **synthetic** (no real tokens, hostnames, or customer payloads) per AC #8.
4. **Iterate AC by AC**, per usual TDD red-phase workflow:
   ```bash
   dotnet test tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/ \
     --filter "FullyQualifiedName~Dw4" --configuration Release -p:NuGetAudit=false
   ```
   Remove `Skip` from the test(s) for the AC under implementation; watch them fail; implement; watch them pass.

### AC → Test File index

| AC | Where to remove Skip first |
|---|---|
| #1 (validator scope bounded) | `Dw4ValidatorContractAtddTests.cs` — `ValidatorEntrypoint_*`, `ValidatorDefaultScope_*`; `Dw4SchemaIdentificationAtddTests.cs` — all 4 |
| #2 (required-field enforcement) | `Dw4QueryEvidenceNegativeAtddTests.cs` — `QueryEvidence_MissingRequiredMetadata_Fails`; `Dw4SignalrEvidenceNegativeAtddTests.cs` — `SignalrEvidence_MissingRequiredMetadata_Fails`; `Dw4NotApplicableMarkerAtddTests.cs` — both |
| #3 (placeholder detection) | `Dw4QueryEvidenceNegativeAtddTests.cs` — `*_PlaceholderUnreplaced_*`, `*_EmptyRequiredTableCell_*`; `Dw4SignalrEvidenceNegativeAtddTests.cs` — `*_PlaceholderUnreplaced_*` |
| #4 (taxonomy single-source) | `Dw4TaxonomyMappingAtddTests.cs` — all 3; `Dw4QueryEvidenceNegativeAtddTests.cs`/`Dw4SignalrEvidenceNegativeAtddTests.cs` — `*_ClassificationNotInEnum_*` |
| #5 (redaction) | `Dw4RedactionRulesAtddTests.cs` — all 7 |
| #6 (controls/correlation) | `Dw4QueryEvidenceNegativeAtddTests.cs` — `*_FalsePositiveControlMissing_*`, `*_CorrelationControlMissing_*`; `Dw4SignalrEvidenceNegativeAtddTests.cs` — `*_ControlMissing_*` |
| #7 (Aspire profile-scoped) | `Dw4ProfileScopedAspireAtddTests.cs` — both |
| #8 (positive/negative fixtures) | `Dw4ValidatorContractAtddTests.cs` — `Fixture_ProducesExpectedVerdictAndRuleIds` (theory) and `FixtureCoverageMatrix_*` |
| #9 (toolchain fit) | `Dw4DocsValidationWiringAtddTests.cs` — `Toolchain_EntrypointDeclarationFile_IsCommitted` |
| #10 (CI integration) | `Dw4DocsValidationWiringAtddTests.cs` — `CiIntegration_*` (3 wiring + 1 smoke) |
| #11 (no mass historical rewrite) | `Dw4ValidatorContractAtddTests.cs` — `ValidatorDefaultScope_DoesNotRecursivelyAuditHistoricalEvidence` |
| #12 (deferred-work dispositions) | `Dw4DeferredWorkDispositionAtddTests.cs` — both (after taking the snapshot) |
| #13 (diagnostic shape) | `Dw4DiagnosticShapeAtddTests.cs` — all 5; `Dw4ParseVsRuleSeparationAtddTests.cs` — all 3 |

### Diagnostic-vocabulary reminders for dev

- **Rule ids** are pinned literals in `Dw4RuleVocabulary.cs`. Renaming any one requires updating both the vocabulary AND every test file that uses the constant — this is intentional drift gating.
- **Family prefixes**: parser-family rules MUST start with `parse-`; schema-id-family rules MUST start with `schema-version-`. `Dw4DiagnosticShapeAtddTests.RuleVocabulary_FamilyPrefixesAreDistinct` enforces this.
- **Diagnostic shape** is fixed: `{ file, schema, rule, section, field, line, hint }`. Output JSON uses these exact field names so a future automation summarizer can consume the output without remapping.
- **Sort order**: emit diagnostics ordered by `(file, schema, rule, section, field, line)` ascending. Two invocations against the same input set must produce byte-identical diagnostic sequences.
- **Schema-id failures fail closed**: when schema cannot be identified, do NOT also evaluate business rules. The parser-vs-rule separation test guards against this.

### Stop-sign reminders (from story Scope Boundaries)

These remain in scope for the dev to refuse:

- New runtime evidence (query, SignalR, Aspire/DAPR proofs) — DW2 / DW5 / future stories.
- Latency metrics, observability exporters, load-testing harnesses (DW6, future).
- Changes to query, SignalR, Admin, MCP, DAPR, AppHost, public HTTP behavior.
- Mass historical evidence repair across `_bmad-output/test-artifacts/post-epic-*` folders.
- New broad third-party dependencies before proving existing repo deps cannot satisfy the validator (YamlDotNet 16.3.0, System.Text.Json, xunit.v3 already pinned).
- Nested submodule initialization.
- Edits to generated preflight JSON audit files.

### Out-of-Scope items that still need closure (story bookkeeping)

- **AC #14 (scope boundaries)** — reviewer guardrail; no test scaffold.
- **AC #15 (bookkeeping)** — Dev Agent Record, File List, Change Log, Verification Status, sprint-status row updates at handoff.

### Sign-off

- Master Test Architect run completed 2026-05-05.
- Build clean: 0 warnings, 0 errors at solution level (9.30s) and test-project level (1.33s).
- Runtime: 50 skipped scaffolds visible to dev and reviewer; 0 passed; 0 failed.
- Story moves forward to dev-story execution (Amelia / `bmad-dev-story`) with this checklist as the test-side input.
