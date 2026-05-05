# Post-Epic Deferred DW4: Operational Evidence Schema Validation

Status: done

<!-- Source: sprint-change-proposal-2026-05-04-deferred-work-triage.md - Proposal E / DW4 -->
<!-- Source: deferred-work.md - query and SignalR operational evidence validator deferrals through 2026-05-04 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Test Architect and technical documentation maintainer,
I want operational evidence templates to be mechanically validated for required fields, placeholders, taxonomy, and redaction rules,
so that future query and SignalR proof stories can reject incomplete or unsafe evidence before reviewers rely on it.

## Story Context

`deferred-work.md` now carries explicit evidence-template validator gaps for both query operational evidence and SignalR operational evidence. R9-A8 produced a query evidence pattern and template, and R10-A6 produced a SignalR evidence pattern and template. Both templates define schema versions, required fields, classifications, controls, and redaction expectations, but enforcement is still manual.

DW4 is the automation bridge between those documentation contracts and future proof work. It should add a lightweight validator, tests or samples, and documentation alignment so reviewers no longer have to rely on honor-system placeholder checks. It is not a new runtime proof, not a perf harness, not a telemetry implementation story, and not a broad governance rewrite. DW4 should keep Aspire-specific evidence optional or profile-scoped so non-Aspire deployments can still produce valid evidence when the proof shape does not use Aspire.

Current HEAD at story creation: `d392506b`.

## Acceptance Criteria

1. **Validator scope is explicit and bounded.** Given the query and SignalR operational evidence templates exist, when DW4 starts, then the developer must document which schema versions are validated in this story: `query-operational-evidence/v1` and `signalr-operational-evidence/v1`. The validator must not attempt to validate unrelated BMAD stories, arbitrary markdown evidence, release evidence, runtime logs, screenshots, or future schema versions unless they are explicitly mapped.

2. **Required-field enforcement is mechanical.** Given an evidence file declares one of the supported schema versions, when the validator runs, then it fails when required metadata, required sections, required table columns, final classification, reviewer verdict, controls, or redaction statement fields are missing. Required fields that are genuinely not applicable must use the template's explicit per-field `not-applicable: <reason>` marker or another documented marker accepted by the validator.

3. **Placeholder detection is strict enough to prevent false closure.** Given an evidence file still contains unreplaced placeholders such as `<required>`, `<...>`, template row labels like `scenario-id`, or empty required table cells, when the validator runs, then it fails with a stable diagnostic that names the schema, file, section, and field or row. The validator must not require optional fields when the proof shape marks them out of scope.

4. **Classification taxonomy is single-source and drift-resistant.** Given `docs/operations/query-operational-evidence.md`, `_bmad-output/test-artifacts/query-operational-evidence-template.md`, `docs/operations/signalr-operational-evidence.md`, and `_bmad-output/test-artifacts/signalr-operational-evidence-template.md` each describe allowed classifications, when DW4 closes, then the validator and docs/templates agree on the allowed run-level classifications and fail-closed downgrade vocabulary. If wording still differs intentionally between query and SignalR, record the difference in one visible mapping table rather than leaving duplicate reviewer checklists to drift silently.

5. **Redaction rules are validated before commit.** Given an evidence file includes tokens, connection strings, production hostnames, tenant/user identifiers, raw payloads, HAR/network traces, logs, or diagnostics, when the validator runs, then it must either require a redaction section that explicitly states the material was redacted or fail on obvious unsafe markers such as bearer tokens, connection-string keywords, unredacted production hostnames, or raw secrets. The validator should favor deterministic pattern checks and clear false-positive escape guidance over broad secret-scanning claims it cannot prove.

6. **Controls and correlation checks remain first-class.** Given query and SignalR evidence templates require false-positive controls and correlation-integrity controls, when the validator runs, then missing controls fail. A control whose observed result is blank, not recorded, or not tied to the same run or a clearly linked control run must not be accepted as pass evidence.

7. **Aspire-specific evidence is optional or profile-scoped.** Given an evidence run is not an Aspire proof, when the validator sees Aspire resource state, dashboard URL, DAPR placement, scheduler, or AppHost fields marked `not-applicable` with a reason, then it accepts the file if the rest of the schema is complete. Given an evidence run claims Aspire/DAPR runtime proof, then those fields become required for that profile. Do not make Aspire-only fields mandatory for every evidence file.

8. **Positive and negative samples prove the validator.** Add or update representative samples or tests that show at least one valid query evidence file, one invalid query evidence file, one valid SignalR evidence file, and one invalid SignalR evidence file. Negative samples must cover missing required metadata, unreplaced placeholder, missing control, invalid classification, and unsafe or absent redaction. Samples must be safe to commit and must not include real tokens, real production hostnames, or customer payload data.

9. **The implementation fits the repository toolchain.** Use the smallest maintainable local mechanism: a script under `scripts/`, a focused test project path, or a docs-validation extension that matches existing repository patterns. Prefer built-in parsing and current dependencies before adding new packages. If JSON Schema is used, pin the supported dialect and explain the choice. If a custom markdown/YAML lint script is used, keep the rules data-driven enough that adding `v2` templates later does not require rewriting the whole parser.

10. **CI integration is deliberate, not accidental.** Given the current docs validation workflow already runs markdown lint, DAPR doc-version checks, link checking, and sample build/test, when DW4 closes, then the developer must either wire the validator into the local and CI docs validation path or record exactly why CI wiring is deferred. If CI is changed, update both `.github/workflows/docs-validation.yml` and local `scripts/validate-docs.ps1` / `scripts/validate-docs.sh` consistently.

11. **Existing evidence is not mass-rewritten.** Given historical evidence artifacts may predate the validator or intentionally fail new rules, when DW4 runs, then it must not rewrite old evidence folders broadly. The story may validate only template files and curated samples, or it may audit existing evidence and record known failures as deferred follow-up. Do not make this story depend on repairing every old evidence artifact in the repository.

12. **Deferred-work dispositions are updated narrowly.** Given DW4 closes or routes relevant bullets in `_bmad-output/implementation-artifacts/deferred-work.md`, when the story moves to review, then each touched bullet must receive a clear disposition marker such as `STORY:post-epic-deferred-dw4-operational-evidence-schema-validation`, `RESOLVED`, `ACCEPTED-DEBT`, `DUPLICATE`, or `NO-ACTION`. Do not rewrite unrelated DW1-DW3, DW5, DW6, SignalR policy, Admin UI, or release-governance entries.

13. **Validation output is useful for reviewers and automation.** Given a validator failure occurs, when the command exits non-zero, then output must include stable file paths, schema version, rule id or rule name, section/field where possible, and a short remediation hint. The output must be concise enough for CI logs and deterministic enough for a future automation run to summarize.

14. **Scope boundaries stay intact.** DW4 must not implement runtime query proof, SignalR proof, telemetry histograms, load testing, browser automation, Admin UI polish, deferred-work governance, DAPR component changes, public API changes, or nested submodule initialization. Any pressure to do those things belongs to DW2, DW5, DW6, or a new story.

15. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Change Log, Verification Status, and any deferred-work dispositions. Move this story and its sprint-status row to `review` only after validator implementation, positive/negative samples or tests, and targeted validation are recorded. Move both to `done` only after code review signoff.

## Scope Boundaries

- Do not create or run new Aspire/DAPR runtime evidence.
- Do not implement query latency metrics, SignalR latency metrics, load-testing harnesses, or observability exporters.
- Do not change query, SignalR, Admin, MCP, DAPR, AppHost, or public HTTP behavior.
- Do not rewrite historical evidence folders just to satisfy the new validator.
- Do not make Aspire-only fields mandatory for non-Aspire evidence profiles.
- Do not add broad third-party dependencies without first proving existing repo dependencies or a small script cannot satisfy the validator.
- Do not initialize or update nested submodules.
- Do not edit generated preflight JSON audit files.

## Implementation Inventory

| Area | File / artifact | Expected use |
| --- | --- | --- |
| Planning source | `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md` | Proposal E scope and acceptance direction |
| Deferred source | `_bmad-output/implementation-artifacts/deferred-work.md` | raw query and SignalR validator deferrals |
| Query evidence docs | `docs/operations/query-operational-evidence.md` | source of query classification, claim, redaction, and reviewer rules |
| Query evidence template | `_bmad-output/test-artifacts/query-operational-evidence-template.md` | `query-operational-evidence/v1` required field and placeholder contract |
| SignalR evidence docs | `docs/operations/signalr-operational-evidence.md` | source of SignalR classification, latency, controls, redaction, and storage rules |
| SignalR evidence template | `_bmad-output/test-artifacts/signalr-operational-evidence-template.md` | `signalr-operational-evidence/v1` required field and placeholder contract |
| Local docs validation | `scripts/validate-docs.ps1` and `scripts/validate-docs.sh` | local validation path to extend or document as intentionally unchanged |
| CI docs validation | `.github/workflows/docs-validation.yml` | CI validation path if the validator becomes a required gate |
| Package versions | `Directory.Packages.props` and `package.json` | dependency constraints and existing tooling patterns |
| Future samples | `_bmad-output/test-artifacts/operational-evidence-validator/` or equivalent | curated positive/negative samples for validator proof |

## Current Code Intelligence

- There is no current evidence-schema validator script in `scripts/`; existing docs validation focuses on markdown linting, link checking, DAPR SDK prose/version consistency, and sample build/test.
- Query and SignalR templates both use Markdown with embedded YAML snippets and tables, not standalone JSON. A validator therefore needs either a disciplined markdown parser/rule scan or a conversion step; a pure JSON Schema file alone will not validate the current artifacts unless the evidence files are reshaped first.
- `query-operational-evidence-template.md` already has a required metadata list, a YAML metadata block, allowed classifications, cache-state matrix, measurement boundaries, controls, redaction, reviewer verdict, fail-closed checklist, and intentionally invalid example.
- `signalr-operational-evidence-template.md` has a schema version, allowed classifications, required run identity, evidence index, environment/topology, SignalR config, correlation, join, trigger/broadcast, receipt, query refresh, latency calculation, controls, diagnostics, redaction, deferred instrumentation, result, and an intentionally invalid example.
- The query template is stricter than the SignalR template today: it uses `not-claimable`, `diagnostic-only`, `path-viability`, and a fail-closed reviewer checklist. SignalR uses a smaller classification set. DW4 should not flatten those differences accidentally; it should map them explicitly.
- `Directory.Packages.props` already includes `YamlDotNet` for tests and current .NET 10 package versions. If a .NET validator is chosen, prefer existing central package management and focused tests over ad hoc package installs.
- `package.json` is currently semantic-release focused and does not define a docs validation script. Adding Node validation would require intentional package and CI/local-script changes.

## Latest Technical Notes

- JSON Schema's current official meta-schema is draft 2020-12. If DW4 chooses JSON Schema, use `$schema: "https://json-schema.org/draft/2020-12/schema"` and keep unsupported markdown/table checks in a companion lint rule rather than pretending JSON Schema can inspect arbitrary markdown structure. Source: <https://json-schema.org/specification>
- The JSON Schema validation vocabulary includes structural validation keywords such as `type`, `required`, `enum`, `pattern`, and object/property constraints. These are useful for extracted metadata blocks but do not replace repository-specific rules like same-run control linkage or placeholder detection. Source: <https://json-schema.org/draft/2020-12/json-schema-validation>
- `System.Text.Json` provides UTF-8 JSON reader/writer APIs and DOM APIs; `JsonNode` is the mutable DOM option when a .NET validator needs to inspect generated JSON/YAML-converted structures. Source: <https://learn.microsoft.com/dotnet/api/system.text.json?view=net-10.0>

## Party-Mode Hardening Notes

- Primary implementation path should be explicit before development starts: prefer a small validator under `scripts/` with PowerShell and shell entrypoints only when that matches the existing docs-validation pattern. If a .NET test/tool is chosen instead, record the reason in the Dev Agent Record and run only the affected test project.
- Keep schema-specific rules data-driven. Rule data for `query-operational-evidence/v1` and `signalr-operational-evidence/v1` should live in named rule sets or versioned config/schema artifacts, not only as scattered procedural checks. Unknown schema versions must fail closed until a future story explicitly maps them.
- Validator diagnostics must be deterministic and assertion-friendly. Use stable ordering and include `file`, `schema`, `rule id`, `section`, `field or table row`, and `hint` whenever available.
- Define a fixture coverage matrix before closing the story. Each negative fixture should map to at least one required rule family: missing required metadata, unreplaced placeholder, missing control, invalid classification, unsafe redaction, absent redaction, malformed evidence, unknown schema version, and missing profile-scoped Aspire fields when the profile requires them.
- `not-applicable: <reason>` is allowed only when the rule set marks the field as profile-specific or optional. A blank reason or generic escape must fail.
- Redaction validation needs concrete boundaries: synthetic tokens, tenant IDs, user IDs, URLs, trace IDs, and connection-string-like values may appear only when clearly marked as synthetic or redacted; obvious bearer tokens, real connection-string keywords, production hostnames, and raw secret markers must fail with an escape path for documented false positives.
- CI/local validation should stay centralized. Prefer invoking the new validator from `scripts/validate-docs.ps1` and `scripts/validate-docs.sh`, then from `.github/workflows/docs-validation.yml`, unless the Dev Agent Record records a precise reason for advisory-only or deferred CI wiring.

## Advanced Elicitation Hardening Notes

- Treat evidence-file discovery as part of the validator contract. The implementation must define whether the command validates explicit file paths, directories, or known template/sample roots, and it must skip or fail unsupported files deterministically instead of relying on incidental glob order.
- Fail closed before rule evaluation when schema identity is missing, duplicated, malformed, or contradictory. A file that declares both query and SignalR evidence schema markers, or declares an unsupported future version, must produce a stable schema-identification diagnostic.
- Keep parser errors distinct from business-rule failures. Malformed YAML snippets, malformed Markdown tables, duplicate required headings, and ambiguous section matches should have their own rule ids so reviewers can tell "cannot parse" from "parsed but invalid".
- Negative fixtures must assert expected rule ids, not just any non-zero exit. This prevents a broken parser or unrelated failure from falsely proving placeholder, control, classification, or redaction coverage.
- Redaction checks should distinguish obviously unsafe committed material from allowed synthetic examples. The accepted escape path must require an explicit synthetic/redacted marker in the evidence, not a broad allowlist that could hide real tokens or tenant data.
- CI integration should avoid broad historical evidence scans until explicitly opted in. The first gate should validate templates and curated fixtures, while any optional repository-wide audit reports known failures without making DW4 depend on repairing old artifacts.

## Tasks / Subtasks

- [x] Task 0: Baseline schema contracts and choose validator shape (AC: #1, #4, #7, #9, #11, #14)
    - [x] 0.1 Re-read Proposal E / DW4 and the relevant validator entries in `deferred-work.md`.
    - [x] 0.2 Inventory query and SignalR evidence docs/templates and record the exact supported schema versions.
    - [x] 0.3 Decide whether implementation is a PowerShell/Python script, .NET test/tool, or JSON Schema plus markdown lint companion.
    - [x] 0.4 Record why Aspire/DAPR fields are profile-scoped rather than globally required.
    - [x] 0.5 Confirm historical evidence folders are not mass-rewritten.
    - [x] 0.6 Record the validator input contract: explicit files, directories, curated roots, unsupported-file behavior, and stable traversal order.

- [x] Task 1: Define the machine-readable rules (AC: #2, #3, #4, #5, #6, #7, #13)
    - [x] 1.1 Define required fields, required sections, required tables, classification enums, control requirements, and redaction rules for `query-operational-evidence/v1`.
    - [x] 1.2 Define required fields, required sections, required tables, classification enums, control requirements, and redaction rules for `signalr-operational-evidence/v1`.
    - [x] 1.3 Add rule ids or stable rule names for missing field, placeholder, empty required table cell, invalid classification, missing control, and unsafe redaction findings.
    - [x] 1.4 Add a visible mapping table if query and SignalR classifications intentionally differ.
    - [x] 1.5 Define fail-closed behavior for malformed evidence, unknown schema versions, unknown classifications, placeholder-looking values in required fields, and missing profile-scoped Aspire fields when an Aspire/DAPR profile is claimed.
    - [x] 1.6 Define the exact `not-applicable: <reason>` rule and reject empty, generic, or unsupported use of the marker.
    - [x] 1.7 Define schema-identification diagnostics for missing schema, duplicate schema markers, contradictory schema markers, and unsupported future versions.

- [x] Task 2: Implement the lightweight validator (AC: #2, #3, #5, #6, #7, #9, #13)
    - [x] 2.1 Add the validator in the smallest maintainable repo location.
    - [x] 2.2 Parse enough Markdown structure to detect schema version, sections, YAML metadata blocks, table headers, placeholders, and required field values.
    - [x] 2.3 Fail on unreplaced placeholders and empty required cells while allowing documented optional or `not-applicable` fields.
    - [x] 2.4 Detect invalid classification values and missing fail-closed reviewer verdict data.
    - [x] 2.5 Detect obvious unsafe tokens, connection-string markers, production hostnames, or raw-secret indicators, and document false-positive handling.
    - [x] 2.6 Return deterministic non-zero exit code and concise file/schema/rule diagnostics.
    - [x] 2.7 Keep diagnostics sorted stably and shaped for tests: `file`, `schema`, `rule`, `section`, `field`, and `hint`.
    - [x] 2.8 Keep schema-specific rule data separate from parser flow so a future `v2` can be added without rewriting the validator.
    - [x] 2.9 Separate parse diagnostics from rule diagnostics for malformed YAML, malformed tables, duplicate headings, and ambiguous section matches.

- [x] Task 3: Add positive and negative proof samples (AC: #8, #11, #13)
    - [x] 3.1 Add one minimal valid query evidence sample and one minimal invalid query evidence sample.
    - [x] 3.2 Add one minimal valid SignalR evidence sample and one minimal invalid SignalR evidence sample.
    - [x] 3.3 Ensure negative samples cover missing metadata, placeholder, missing control, invalid classification, and redaction failure.
    - [x] 3.4 Keep all samples synthetic and safe to commit.
    - [x] 3.5 Add a fixture coverage matrix mapping each invalid sample to the exact validator rule ids it is expected to trigger.
    - [x] 3.6 Include one valid `not-applicable: <reason>` profile-scoped case and one invalid unsupported or empty `not-applicable` case.
    - [x] 3.7 Assert negative fixtures fail for the expected rule ids so unrelated parser failures cannot masquerade as coverage.

- [x] Task 4: Align docs, templates, and validation paths (AC: #4, #7, #10, #12, #14)
    - [x] 4.1 Update query and SignalR operations docs only where needed to point reviewers to the validator and supported schema versions.
    - [x] 4.2 Update templates only where needed to reduce duplicate taxonomy drift or make required fields validator-friendly.
    - [x] 4.3 Either wire the validator into `scripts/validate-docs.ps1`, `scripts/validate-docs.sh`, and `.github/workflows/docs-validation.yml`, or record a precise CI-deferred reason.
    - [x] 4.4 Update only DW4-relevant `deferred-work.md` bullets with disposition markers.
    - [x] 4.5 Document the local command, docs-validation command, and CI command path in the Dev Agent Record.
    - [x] 4.6 Document whether CI validates curated fixtures only or also runs an advisory repository-wide evidence audit.

- [x] Task 5: Validate and close bookkeeping (AC: #8, #10, #12, #15)
    - [x] 5.1 Run the validator against positive samples and confirm pass.
    - [x] 5.2 Run the validator against negative samples and confirm expected failures are detected.
    - [x] 5.3 Run targeted markdown validation for changed docs, templates, samples, and this story.
    - [x] 5.4 Run focused tests if the validator is implemented as .NET test/tool code.
    - [x] 5.5 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and sprint-status row at dev handoff.
    - [x] 5.6 Record sample passing output and at least one expected failing-case output in the Dev Agent Record.
    - [x] 5.7 Record at least one schema-identification failure output for missing, duplicate, or unsupported schema markers.

## Dev Notes

### Architecture Guardrails

- Treat the evidence templates as documentation-backed contracts, not product runtime APIs.
- Prefer deterministic static validation over heuristic "looks good" checks.
- Keep schema-version handling explicit. A future `v2` template should fail as unsupported or route to a new rule set until DW4 is extended.
- Do not let the validator silently accept a run-level `pass` when required metadata, controls, redaction, raw samples, or reviewer verdict data are missing.
- Keep optional/profile-specific evidence honest: omitted Aspire/DAPR fields require a reason when out of scope, and become required when the proof claims Aspire/DAPR runtime behavior.
- Keep unsafe evidence out of committed artifacts. The validator is a focused guard, not a replacement for broader secret scanning.

### Previous Story Intelligence

- R10-A6 created the SignalR evidence pattern and template, including storage rules, latency boundaries, controls, classifications, redaction guidance, and a deferred item for falsifiable schema validation.
- R9-A8 created the query evidence pattern and template, then code review and elicitation hardened fail-closed classification, metadata, p99/throughput rules, controls, and future validator readiness.
- R10-A8 established that follow-through work must leave direct evidence, visible owning story/status rows, and reusable closure rules rather than relying on narrative closure.
- DW2 and DW3 are adjacent but separate: DW2 proves live Admin/DAPR/MCP runtime behavior; DW3 hardens Admin debugging JSON/large-stream behavior. DW4 validates evidence artifacts and must not absorb those runtime or product scopes.

### Testing Guidance

- Primary validation is the new validator's own positive/negative samples plus markdown validation for changed docs/artifacts.
- If implemented as a .NET test/tool, add focused tests around the parser/rule engine and run only the affected test project.
- If implemented as a script, include deterministic command examples in the Dev Agent Record and ensure the script exits non-zero on invalid samples.
- Product unit tests are not required unless product code changes unexpectedly.
- Do not run solution-level `dotnet test`; follow repository guidance and run relevant projects individually.
- Validator self-tests are required even when product tests are not. Cover parser success/failure, each independent rule family, valid fixture pass behavior, invalid fixture fail behavior, diagnostic ordering, and exact diagnostic code/path output.
- Invalid fixture tests should verify expected diagnostic rule ids. A test that only asserts "the command failed" is not enough to prove the intended validator rule is working.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md#Proposal-E-DW4-Operational-Evidence-Schema-Validation`] - DW4 scope and acceptance direction.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md`] - raw query and SignalR evidence validator deferrals.
- [Source: `_bmad-output/implementation-artifacts/post-epic-9-r9a8-query-operational-evidence-pattern.md`] - query evidence pattern story, review findings, and future validator guidance.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a6-signalr-operational-evidence-pattern.md`] - SignalR evidence pattern story and deferred validator gap.
- [Source: `docs/operations/query-operational-evidence.md`] - query evidence source of truth.
- [Source: `_bmad-output/test-artifacts/query-operational-evidence-template.md`] - query evidence template to validate.
- [Source: `docs/operations/signalr-operational-evidence.md`] - SignalR evidence source of truth.
- [Source: `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`] - SignalR evidence template to validate.
- [Source: `scripts/validate-docs.ps1` and `scripts/validate-docs.sh`] - local validation scripts.
- [Source: `.github/workflows/docs-validation.yml`] - CI docs validation workflow.
- [Source: `Directory.Packages.props`] - existing centrally managed .NET package versions.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-04T18:16:48Z`, result `pass`.

### Completion Notes List

- Created ready-for-dev story from first backlog row after DW3 in the Post-Epic Deferred Work Cleanup package.
- No implementation work has been performed for this story.
- No `project-context.md` file was present in the repository at story creation.
- 2026-05-05: Started implementation without running the ATDD workflow; chose a small Python validator with PowerShell/Bash wrappers so local and CI docs validation can invoke the same rule engine.
- 2026-05-05: Implemented bounded static validation for `query-operational-evidence/v1` and `signalr-operational-evidence/v1`; unsupported schema versions fail closed.
- 2026-05-05: Added curated positive and negative fixtures for required metadata, placeholders, empty table cells, invalid classifications, missing controls, redaction failures, not-applicable misuse, profile-scoped Aspire fields, schema identification, and parser failures.
- 2026-05-05: Wired fixture self-test into PowerShell/Bash local validation and GitHub docs-validation; default validation does not scan historical evidence folders.
- 2026-05-05: Updated DW4-related deferred-work bullets only; left unrelated query/SignalR governance and proof-quality entries untouched.
- 2026-05-05: ATDD scaffolds remain skipped per user direction; validation was performed through the implemented self-test, wrapper smoke runs, markdown lint, and test-project compile/run.

### File List

- `.github/workflows/docs-validation.yml`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/post-epic-deferred-dw4-operational-evidence-schema-validation.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/operational-evidence-validator/README.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/entrypoint.txt`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/parse-duplicate-required-heading.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/parse-malformed-table.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/parse-malformed-yaml.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-aspire-claimed-but-fields-missing.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-classification-not-in-enum.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-control-missing.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-correlation-control-missing.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-empty-required-table-cell.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-missing-metadata.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-not-applicable-empty-reason.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-not-applicable-on-required-field.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-placeholder-unreplaced.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-raw-secret-marker.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-redaction-bearer-token.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-redaction-connection-string.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-redaction-production-hostname.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-redaction-section-missing.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-valid-minimal.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-valid-not-applicable-aspire.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/schema-contradictory.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/schema-duplicate-markers.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/schema-missing.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/schema-unsupported-future-version.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-aspire-claimed-but-fields-missing.md` (review-2026-05-05)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-classification-not-in-enum.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-control-missing.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-empty-required-table-cell.md` (review-2026-05-05)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-missing-metadata.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-not-applicable-empty-reason.md` (review-2026-05-05)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-not-applicable-on-required-field.md` (review-2026-05-05)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-placeholder-unreplaced.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-raw-secret-marker.md` (review-2026-05-05)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-redaction-bearer-token.md`
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-redaction-connection-string.md` (review-2026-05-05)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-redaction-production-hostname.md` (review-2026-05-05)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-redaction-section-missing.md` (review-2026-05-05)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-valid-minimal.md`
- `_bmad-output/test-artifacts/query-operational-evidence-template.md`
- `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`
- `docs/operations/query-operational-evidence.md`
- `docs/operations/signalr-operational-evidence.md`
- `scripts/validate-docs.ps1`
- `scripts/validate-docs.sh`
- `scripts/validate-evidence.ps1`
- `scripts/validate-evidence.sh`
- `scripts/validate-operational-evidence.py`

## Verification Status

### Initial dev pass (2026-05-05)

- `python scripts/validate-operational-evidence.py --self-test` passed: 29 fixtures checked.
- `.\scripts\validate-evidence.ps1 --self-test` passed.
- `bash scripts/validate-evidence.sh --self-test` passed.
- `python scripts/validate-operational-evidence.py --json _bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-missing-metadata.md` emitted expected `query-required-metadata-missing` diagnostic and exited non-zero as expected.
- `dotnet test tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests --configuration Release` passed compile/run with 50 existing ATDD scaffold tests skipped.
- `npx markdownlint-cli2` passed on changed operations docs, evidence templates, validator README, and this story. Invalid fixture markdown and historical `deferred-work.md` lint debt were intentionally excluded from that markdownlint run.
- Full `scripts/validate-docs.*` was not run end-to-end because it includes broader link/sample validation outside the DW4 change surface.

### Post-review patch validation (2026-05-05)

After applying review patches, re-ran:

- `python scripts/validate-operational-evidence.py --self-test`: **PASS, 37 fixtures checked** (8 new SignalR negative fixtures added).
- `bash scripts/validate-evidence.sh --self-test` (from repo root): PASS.
- `bash scripts/validate-evidence.sh --self-test` (from `scripts/` cwd): PASS — wrapper now repo-root-resilient.
- `pwsh scripts/validate-evidence.ps1 --self-test` (from repo root): PASS.
- `python scripts/validate-operational-evidence.py /tmp/dw4-stress/apostrophe.md`: PASS — apostrophe in YAML value no longer trips `parse-yaml-malformed` (was a false-fail before patch).
- `python scripts/validate-operational-evidence.py /tmp/dw4-stress/fenced-schema.md`: PASS — schema markers inside non-YAML fenced blocks are correctly ignored.
- `python scripts/validate-operational-evidence.py /tmp/dw4-stress/missing-arg.md`: exit 2 — distinct argument-error exit code (was indistinguishable from validation failure before).
- `python scripts/validate-operational-evidence.py --json fixtures/query-invalid-placeholder-unreplaced.md`: emits `placeholder-unreplaced` with `section=Metadata`, `field=evidence_run_id`, `hint="Replace template placeholder '<required>' before evidence can close."` — section/field context now populated (was `section=Content`, `field=null`).
- `python scripts/validate-operational-evidence.py --json fixtures/schema-duplicate-markers.md`: emits `schema-version-duplicate` with hint *Same schema declared multiple times in one source*; `fixtures/schema-contradictory.md` emits `schema-version-contradictory` with hint *Different schema values declared* — taxonomy now matches rule names.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-04 | 0.1 | Created ready-for-dev DW4 operational evidence schema validation story. | Codex automation |
| 2026-05-05 | 0.2 | Applied party-mode hardening for validator contract, fixtures, diagnostics, and CI expectations. | Codex automation |
| 2026-05-05 | 0.3 | Applied advanced elicitation for schema identification, parser failure separation, fixture assertions, and CI audit boundaries. | Codex automation |
| 2026-05-05 | 0.4 | Started DW4 implementation; selected a script-based validator path and skipped ATDD artifact generation per user direction. | GPT-5 Codex |
| 2026-05-05 | 1.0 | Implemented operational evidence validator, wrappers, curated fixtures, docs/CI wiring, deferred-work dispositions, and validation handoff. | GPT-5 Codex |
| 2026-05-05 | 1.1 | Code review patches applied: profile substring → token match (CRITICAL AC #7); schema-duplicate vs contradictory rule logic now value-driven (AC #4); apostrophe in YAML value no longer triggers parse-yaml-malformed; YAML key parser broadened to allow kebab/dotted keys; single-column tables now exercise empty-cell rule; schema marker scanner skips non-YAML fenced blocks; self-test asserts no extra rules; placeholder diagnostic carries section/field context; previous_pipe_count reset after parse-table-malformed; clean_value case-insensitive; not-applicable reasons reject stop-words (n/a, tbd, todo, -); validate_tables scoped to required-table heading allowlist; redaction heading prefix-match; scenario-id placeholder bounded to literal `<scenario-id>`; redaction connection-string regex extended (pwd/User Id/Initial Catalog/AccessKey/Secret) and production-hostname extended (.production./live.); wrappers resolve repo-root via SCRIPT_DIR; Python discovery harmonized (python/python3/py); validate-evidence.sh marked executable; validator default fixtures-root resolves script-relative; CLI argument-not-found emits ERROR + exit 2; control fields removed from `*_REQUIRED` to avoid duplicate diagnostics with control-required-missing; 8 SignalR negative fixtures added (redaction-section, connection-string, production-hostname, raw-secret, not-applicable-empty-reason, not-applicable-on-required-field, aspire-claimed-but-missing, empty-required-table-cell); `schema-duplicate-markers.md` rewritten to be a true same-source same-value duplicate; `query-invalid-empty-required-table-cell.md` table moved under `## Controls`; `query-invalid-classification-not-in-enum.md` value changed from `tbd` (now a stop-word) to `purple-haze`; README classification mapping table replaced with combined query+SignalR matrix and source-of-truth list. Self-test now covers 37 fixtures and passes. | Code review (Claude Opus 4.7) |

## Party-Mode Review

- Date/time: 2026-05-05T00:11:03+02:00
- Selected story key: `post-epic-deferred-dw4-operational-evidence-schema-validation`
- Command/skill invocation used: `/bmad-party-mode post-epic-deferred-dw4-operational-evidence-schema-validation; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: all reviewers recommended `needs-story-update`; the story is directionally sound, but the development handoff needed a sharper validator contract, explicit implementation entrypoint expectations, rule-data separation, deterministic diagnostics, fail-closed behavior, fixture coverage matrix, redaction boundaries, and CI/local command clarity.
- Changes applied: added Party-Mode Hardening Notes; expanded rule, implementation, sample, docs-validation, and verification tasks; added validator self-test guidance; added change-log row.
- Findings deferred: exact schema/config file format and location; whether the implementation is a script, .NET tool/test, or JSON Schema plus markdown lint companion; whether CI blocks immediately or starts advisory; long-term taxonomy ownership and historical evidence migration policy.
- Final recommendation: needs-story-update

## Advanced Elicitation

- Date/time: 2026-05-05T09:03:58+02:00
- Selected story key: `post-epic-deferred-dw4-operational-evidence-schema-validation`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-deferred-dw4-operational-evidence-schema-validation`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Architecture Decision Records; Security Audit Personas; Failure Mode Analysis.
- Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction.
- Findings summary: the story had strong rule and fixture coverage, but it could still falsely pass if schema discovery was ambiguous, parser failures were conflated with rule failures, negative fixtures only asserted non-zero exit, or CI tried to validate all historical evidence before curated fixtures were stable.
- Changes applied: added Advanced Elicitation Hardening Notes; added input-contract, schema-identification, parse-diagnostic, exact-negative-fixture, CI audit-boundary, and schema-identification-output tasks; strengthened validator self-test guidance; added change-log row.
- Findings deferred: exact command syntax, exact rule-id names, parser implementation language, and whether repository-wide historical evidence auditing starts as advisory or deferred.
- Final recommendation: needs-story-update

### Review Findings

Code review run: `/bmad-code-review post-epic-deferred-dw4-operational-evidence-schema-validation` (2026-05-05). Reviewers: Blind Hunter (adversarial general), Edge Case Hunter, Acceptance Auditor. Diff scope: DW4-only slice of commit `591f97c1` (43 files, +1445/-122).

#### Decision resolutions

- [x] [Review][Resolved-Dismiss] `parse-malformed-table.md` fixture inconsistency — **dismissed as false positive**. Re-ran `python scripts/validate-operational-evidence.py --json fixtures/parse-malformed-table.md`; the data row reads `| cache-hit-control | pass | extra |` (4 pipes, with trailing `|`), not 3. Blind Hunter's pipe count omitted the trailing `|` from the diff snippet. Self-test legitimately passes; rule fires correctly.
- [x] [Review][Resolved-Defer] DW3 deferred-work bullets added in DW4 commit (AC #12) — **accepted as scope deviation**. Bullets are already merged on `main` in commit `591f97c1`; reverting would require a new commit removing them and would lose useful DW3 review context. Recorded in `deferred-work.md` review-of-DW4 section (2026-05-05) so future audits know the coupling. Discipline note: future code-review patches must isolate per-story disposition markers in their own commit.
- [x] [Review][Resolved-Patch] Connection-string regex extended (`pwd=`, `User Id=`, `Initial Catalog=`, `AccessKey=`, `Secret=`) and now requires `\s*=\s*\S` (a non-whitespace value) so prose like `Server =` (with no value) does not false-fire. Comprehensive escape mechanism (synthetic-marker comment) deferred to a follow-up story; documented as a known limitation.
- [x] [Review][Resolved-Patch] Production-hostname rule extended to `(prod|production|live)` substring forms. Broader cloud-provider hostname patterns (`*.azurewebsites.net`, `*.amazonaws.com`) and configurable per-tenant policy deferred — judgment call on false-positive risk; current rule covers the most common production naming conventions.
- [x] [Review][Resolved-Patch] `validate_tables` now scoped to a `REQUIRED_TABLE_HEADINGS` allowlist (`controls`, `cache state matrix`, `cache-state matrix`, `latency calculation`, `correlation`, `correlation matrix`, `scenario matrix`, `false-positive controls`, `fail-closed reviewer checklist`). Empty cells in tables under other headings no longer false-fire `required-table-cell-empty`.
- [x] [Review][Resolved-Patch] Redaction heading match relaxed to permissive prefix (`startswith("redaction")` plus `redaction `, `redaction:`). Variants like `## Redaction Notes` or `## Redaction & Privacy` now satisfy `redaction-section-missing`.
- [x] [Review][Resolved-Patch] Placeholder regex narrowed to literal `<scenario-id>` (angle-bracketed) so field references like `scenario-id: counter-warm-001` no longer false-fire. `<required>`, `<...>`, `TODO(dev)`, and `<scenario-id>` remain in the placeholder vocabulary.
- [x] [Review][Resolved-Dismiss] `not-applicable: <reason>` on a required non-profile-scope field — **kept current dual-diagnostic behavior** (`not-applicable-not-allowed-here` + `not-applicable-reason-missing` if reason invalid). The two rules describe orthogonal violations; reviewers benefit from both signals. `*_REQUIRED` no longer includes control fields, so no longer co-fires `*-required-metadata-missing` with `control-required-missing` on the same field.
- [x] [Review][Resolved-Defer] Skipped .NET ATDD scaffold (50 tests) at `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/` — **kept as-is**. Python `--self-test` covers all 37 fixtures with rule-id assertions and now enforces no-extra-rules equality (the actual coverage gate). Deciding whether to wire fixtures into the .NET project or delete the scaffold is a separate refactoring story; coverage is not regressed.

#### Patch

- [x] [Review][Patch] `run_profile` substring match treats `non-aspire-static-fixture` as Aspire-claimed (CRITICAL, AC #7) [`scripts/validate-operational-evidence.py:402-403`] — `"aspire" in profile` matches `non-aspire-...`. Replace with allowlist or prefix match (`profile.startswith("aspire")` / explicit set). Today only masked because every fixture stuffs the four Aspire fields with `not-applicable`.
- [x] [Review][Patch] Schema-duplicate vs schema-contradictory rule labeling inverted (AC #4) [`scripts/validate-operational-evidence.py:1759-1762`] — current logic uses source-equality (markdown vs yaml) instead of value-equality. `schema-duplicate-markers.md` declares two **different** values, which is contradictory; same-value duplicates never fire any rule. Replace with: `len(values) > 1 → schema-version-contradictory`; `len(markers) > 1 and len(values) == 1 → schema-version-duplicate`.
- [x] [Review][Patch] YAML "malformed" detector flags any single apostrophe / unbalanced quote (HIGH) [`scripts/validate-operational-evidence.py:1779`] — `redaction_statement: it's redacted` triggers `parse-yaml-malformed` and short-circuits all business-rule checks. Drop the `count('"')%2` and `count("'")%2` heuristics, or restrict to truly unterminated quoted scalars.
- [x] [Review][Patch] YAML key parser too restrictive; kebab/dotted/digit-prefixed keys parse as malformed (HIGH) [`scripts/validate-operational-evidence.py:1787`] — broaden regex to `^[A-Za-z_][\w.-]*$` (or skip the offending line with diagnostic but continue parsing remaining lines so business rules still run).
- [x] [Review][Patch] Single-column markdown tables silently bypass empty-cell validation (HIGH) [`scripts/validate-operational-evidence.py:336`] — divider regex `(\|\s*:?-{3,}:?\s*)+\|?$` requires ≥1 additional column. Change `+` → `*` so `| --- |` is recognised as a valid single-column divider.
- [x] [Review][Patch] Schema marker scanner is fenced-block-blind (HIGH) [`scripts/validate-operational-evidence.py:1741`] — prose mentioning the sibling schema name in fenced code blocks or comparison sentences triggers false `schema-version-contradictory`. Strip fenced code blocks before scanning, or limit detection to first YAML metadata block + first H1/H2 heading area.
- [x] [Review][Patch] SignalR negative-fixture coverage materially thinner than query (HIGH, AC #8) [`_bmad-output/test-artifacts/operational-evidence-validator/fixtures/`] — query has 12 negative fixtures, SignalR has 5. AC #8 mandates symmetric coverage of redaction, not-applicable misuse, empty cells, and Aspire-profile-claimed. Add: `signalr-invalid-redaction-section-missing.md`, `signalr-invalid-redaction-connection-string.md`, `signalr-invalid-redaction-production-hostname.md`, `signalr-invalid-raw-secret-marker.md`, `signalr-invalid-not-applicable-empty-reason.md`, `signalr-invalid-not-applicable-on-required-field.md`, `signalr-invalid-empty-required-table-cell.md`, `signalr-invalid-aspire-claimed-but-fields-missing.md`, `signalr-invalid-correlation-control-missing.md`.
- [x] [Review][Patch] Self-test does not assert "no extra rules" on negative fixtures (HIGH, hardening note) [`scripts/validate-operational-evidence.py:171-176`] — current logic compares `expected ⊆ rules`. A negative fixture firing its expected rule plus 5 unrelated false-positives passes silently. Change to exact-set equality (`rules == expected`) for negative fixtures.
- [x] [Review][Patch] `validate_placeholders` short-circuits on first match and emits `section="Content"`, `field=None` (MEDIUM, AC #3 + #13) [`scripts/validate-operational-evidence.py:1902-1907`] — collect all placeholder hits; resolve `section` from the most recent heading and `field` from the closest YAML key or table row label.
- [x] [Review][Patch] `previous_pipe_count` not reset after `parse-table-malformed` (MEDIUM) [`scripts/validate-operational-evidence.py:1822-1827`] — `continue` skips the `previous_pipe_count = pipe_count` assignment, masking subsequent mismatches in the same table. Update the variable before `continue`, or clear `table_active` so the table is treated as ended.
- [x] [Review][Patch] `clean_value` stop-words case-sensitive (MEDIUM) [`scripts/validate-operational-evidence.py:441`] — `tbd`, `todo`, lowercase `n/a` pass. Lowercase the comparison: `stripped.lower() not in {"-", "todo", "tbd", "n/a", "tba"}`.
- [x] [Review][Patch] `not-applicable: <reason>` accepts `n/a`, `-`, `tbd` as valid reasons (MEDIUM, Task 1.6) [`scripts/validate-operational-evidence.py:393-395`] — Task 1.6 mandates rejecting "empty, generic, or unsupported" use of the marker. Currently only empty is rejected. Add stop-word filter on reason.
- [x] [Review][Patch] Wrappers and `validate-docs.ps1` are CWD-dependent and inconsistent on Python discovery (MEDIUM) [`scripts/validate-evidence.sh:13`, `scripts/validate-evidence.ps1:8-17`, `scripts/validate-docs.ps1:21`] — wrappers invoke `scripts/validate-operational-evidence.py` as a relative path; `validate-docs.ps1` only probes `python` (not `py` / `python3`); bash wrapper does not probe `py`. Resolve script directory with `SCRIPT_DIR=$(cd "$(dirname "$0")"/.. && pwd)` and pass an absolute path; align Python discovery (`python` → `py` → `python3`) across all three.
- [x] [Review][Patch] `scripts/validate-evidence.sh` checked in mode 100644, not 100755 (LOW) [`scripts/validate-evidence.sh`] — `./scripts/validate-evidence.sh` invocation fails permission-denied. Either set executable bit or document `bash scripts/validate-evidence.sh` as the canonical invocation in the README.
- [x] [Review][Patch] README mapping table omits SignalR-only fail-closed downgrade vocabulary (LOW, AC #4) [`_bmad-output/test-artifacts/operational-evidence-validator/README.md`] — README mentions query-only downgrade values but does not record SignalR-side classification differences nor cross-reference `docs/operations/*.md` and the templates. Add a single visible mapping table covering both schemas.
- [x] [Review][Patch] `validate_paths` accepts non-existent paths and emits `parse-file-unreadable` indistinguishable from a real validation failure (LOW) [`scripts/validate-operational-evidence.py:1681-1695`] — return a distinct `argument-error` diagnostic (or non-zero exit class) when a CLI argument resolves to no file.

#### Defer

- [x] [Review][Defer] No fixture for blank/whitespace/non-linked control result (AC #6 partial) [`_bmad-output/test-artifacts/operational-evidence-validator/fixtures/`] — `clean_value` already rejects empty/`-`/`TODO`/`TBD` controls, so missing-value detection works; the unenforced part is AC #6's "tied to the same run or a clearly linked control run" linkage check, which requires cross-field correlation logic and is bigger than DW4. Deferred — pre-existing scope; address in a follow-up validator-rules story.
- [x] [Review][Defer] Templates self-trigger placeholder noise if a future repo-wide audit mode is added (LOW) [`scripts/validate-operational-evidence.py:194-199`] — only triggers if Task 4.6's "advisory repository-wide audit" path is implemented. Default validation is fixture-only today. Deferred — add a `<!-- evidence-validator: skip -->` marker or `**/*-template.md` skip-list when audit mode lands.
