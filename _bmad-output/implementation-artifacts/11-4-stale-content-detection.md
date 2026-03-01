# Story 11.4: Stale Content Detection

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a documentation maintainer,
I want to identify stale documentation content within one CI cycle after a breaking code change,
so that I can update affected pages before users encounter outdated information.

## Prerequisites / Assumptions

These properties are delivered by Story 11-3 (`docs-validation.yml`) and are assumed to be true when this story's deliverables are verified:

- **P1**: The `sample-build` job in `docs-validation.yml` fails when a code change in `samples/` or `src/` breaks the sample project build or tests (NFR14). Changes to `src/` packages are caught transitively — the sample project references `src/` packages, so any breaking API change in `src/` causes the sample build to fail
- **P2**: The `sample-build` job output identifies which step failed (`dotnet build` vs `dotnet test`), making it clear which documentation pages may need updates
- **P3**: The `docs-validation.yml` workflow triggers on every PR and push to `main`, ensuring stale content is detectable within one CI cycle (FR37, NFR14)

## Acceptance Criteria

1. **AC1 - Documentation Mapping Comment**: A YAML comment block exists in `docs-validation.yml` (inside the `sample-build` job) that maps sample projects to their corresponding documentation pages, helping maintainers trace a build failure to specific pages that need review.

2. **AC2 - Stale Content Triage Guide**: A `### Triaging Documentation CI Failures` subsection exists in `CONTRIBUTING.md` at line ~101 (after `### Run Docs Validation Locally`, before `## Code Contributions`) explaining how to triage a `sample-build` failure: what it means, which docs pages to review, and how to fix the staleness.

3. **AC3 - Lint and Link Clean**: Modified `CONTRIBUTING.md` passes `markdownlint-cli2` and `lychee` with zero violations.

## Definition of Done

This story is complete when:
- **If `docs-validation.yml` exists** (Story 11-3 done): AC1 + AC2 + AC3 all pass
- **If `docs-validation.yml` does NOT exist** (Story 11-3 not yet done): AC2 + AC3 pass, and AC1 is documented as deferred — add a note in Completion Notes with the exact comment block to insert once 11-3 is merged. Story can be marked `done` in this case

## Tasks / Subtasks

- [ ] Task 1: Check Story 11-3 dependency status (Decision gate)
  - [ ] Check if `.github/workflows/docs-validation.yml` exists on disk
  - [ ] **If YES** → proceed with Tasks 2, 3, 4, 5
  - [ ] **If NO** → skip Task 2, proceed with Tasks 3, 4, 5. Document the deferred YAML comment in Completion Notes
- [ ] Task 2: Add documentation mapping comment to `docs-validation.yml` sample-build job (AC: 1)
  - [ ] **SKIP if `docs-validation.yml` does not exist** (see Task 1)
  - [ ] Add a YAML comment block at the top of the `sample-build` job listing the mapping (see exact text in Dev Notes)
  - [ ] Comment format: `# STALE CONTENT MAPPING:` header, then `#   <project> → <doc pages>` entries
- [ ] Task 3: Add stale content triage section to `CONTRIBUTING.md` (AC: 2)
  - [ ] Insert new subsection **after** `### Run Docs Validation Locally` (line ~101, after the markdownlint code block) and **before** `## Code Contributions` (line ~102)
  - [ ] Section title: `### Triaging Documentation CI Failures`
  - [ ] Use exact content from Dev Notes section below
  - [ ] Ensure 4-space indent for sub-list items (MD007 compliance)
  - [ ] Keep it concise — 10-15 lines maximum
- [ ] Task 4: Verify lint and link compliance (AC: 3)
  - [ ] Run `npx markdownlint-cli2 "CONTRIBUTING.md"` — must exit with zero violations
  - [ ] Run `lychee --cache "CONTRIBUTING.md"` — must exit with zero errors (use `--cache` to avoid GitHub rate limits on CONTRIBUTING.md's 9+ links)
  - [ ] Fix any violations before proceeding
- [ ] Task 5: Final verification
  - [ ] If Task 2 was completed: visually confirm comment block is inside the `sample-build` job, not at the workflow top level
  - [ ] If Task 2 was skipped: add Completion Note with the deferred comment block text and instruction to apply after 11-3 merge

## Dev Notes

### Design Decisions (Resolved During Story Creation)

1. **Story 11-3 dependency handling**: Explicit decision tree in Tasks. If `docs-validation.yml` doesn't exist, the CONTRIBUTING.md triage guide (Task 3) is completed independently and the YAML comment (Task 2) is documented for deferral. Story can be marked done either way
2. **No new tooling needed**: The architecture designed the `sample-build` job to serve double duty — validating sample code quality (FR34) and detecting stale content (FR37). No additional scripts, tools, or workflows required
3. **Transitive `src/` detection**: Changes to `src/` packages are caught because `samples/Hexalith.EventStore.Sample/` references them as project dependencies. A breaking API change in `src/Hexalith.EventStore.Client/` (for example) causes the sample build to fail, which triggers the stale content signal
4. **Mapping is informational only**: The YAML comment and triage guide are aids, not enforced by tooling. As new sample projects and docs are added in future stories, the mapping comment should be updated accordingly
5. **CONTRIBUTING.md insertion point**: Insert after `### Run Docs Validation Locally` section (ends at line ~101 with the closing code fence) and before `## Code Contributions` (line ~102). This places the triage guide logically after "how to validate" and before "how to write code"

### Architecture Compliance

- **Architecture Decision D3**: CI Pipeline Architecture, Phase 1a
- **FR37**: "identify stale content through automated checks" — fulfilled by sample-build failure + mapping comment + triage guide
- **NFR14**: "detectable within 1 CI cycle" — fulfilled by docs-validation.yml triggering on PR and push to main
- **NFR12 intent**: "The code IS manually in markdown, but accuracy is enforced by the sample project being the source of truth"
- **No new files**: Only modifies existing files

### Stale Content Mapping (for Task 2 YAML comment)

```yaml
# STALE CONTENT MAPPING:
# When this job fails, review the corresponding documentation pages:
#   samples/Hexalith.EventStore.Sample/ build failure:
#     → docs/getting-started/quickstart.md (code examples reference sample project)
#     → README.md (programming model code examples)
#   tests/Hexalith.EventStore.Sample.Tests/ test failure:
#     → docs/getting-started/quickstart.md (expected behavior may have changed)
#     → README.md (if behavior examples are affected)
# Future mappings (add as documentation grows):
#   Additional sample projects → corresponding tutorial/guide pages
```

### CONTRIBUTING.md Triage Section (for Task 3)

Insert between `### Run Docs Validation Locally` (after the closing ``` on line ~100) and `## Code Contributions` (line ~102):

```markdown
### Triaging Documentation CI Failures

When the **Docs** CI pipeline fails on the `sample-build` job, it means the sample project
no longer compiles or its tests fail. This signals that documentation may reference outdated
code or behavior.

**How to triage:**

1. Check the CI failure output — identify whether `dotnet build` or `dotnet test` failed
2. Map the failure to documentation pages:
    - `samples/Hexalith.EventStore.Sample/` build failure → review `docs/getting-started/quickstart.md` and `README.md` code examples
    - `tests/Hexalith.EventStore.Sample.Tests/` test failure → review `docs/getting-started/quickstart.md` (expected behavior may have changed)
3. Update the affected documentation pages to match the new code/behavior
4. Push fixes and verify the Docs CI passes

As the project grows, additional sample-to-documentation mappings are documented in the
CI workflow comments inside `docs-validation.yml`.
```

### Files to Modify

- `.github/workflows/docs-validation.yml` — add stale content mapping comment to `sample-build` job (ONLY if file exists — Story 11-3 dependency)
- `CONTRIBUTING.md` — add triage subsection at line ~101

### CRITICAL: Do NOT Create or Modify These Files

- `.github/workflows/ci.yml` — existing CI workflow
- `.github/workflows/release.yml` — release workflow
- `.markdownlint-cli2.jsonc` — Story 11-1
- `.markdownlintignore` — Story 11-1
- `lychee.toml` — Story 11-2
- `.lycheeignore` — Story 11-2
- Any files in `src/`, `tests/`, `samples/`, `docs/`
- No new files — this story only modifies existing files

### Previous Story Intelligence

**Patterns to follow:**
- Branch: `feat/story-11-4-stale-content-detection`
- Commit: `feat: Complete Story 11-4 stale content detection`
- Pure documentation changes — no code changes to `src/` or `tests/`
- CONTRIBUTING.md was previously modified in Story 11-1 (sub-list indentation fix, solution filename, lint command scope)

**Lint rules affecting CONTRIBUTING.md (from Story 11-1):**
- MD007: 4-space indent for unordered sub-lists
- MD013: disabled (no line length limit)
- MD029: ordered list numbering must be sequential (1, 2, 3, 4)
- MD041: disabled globally (first line heading)
- MD033: inline HTML allowed for `details`, `summary`, `br`, `img`, `picture`, `source` only

**Story 11-3 reference (if applying Task 2):**
- `sample-build` job uses matrix: ubuntu-latest, windows-latest, macos-latest
- Steps: checkout → setup-dotnet → cache NuGet → dotnet restore → dotnet build → dotnet test
- Insert the STALE CONTENT MAPPING comment block as the first content inside the `sample-build:` job block (position-agnostic — exact YAML structure depends on Story 11-3 implementation)

### Git Intelligence

- `docs-validation.yml` does NOT exist on main as of story creation (Story 11-3 is `ready-for-dev`)
- `tests/Hexalith.EventStore.Sample.Tests/` EXISTS and builds successfully
- Recent pattern: feature branches merged via PRs, conventional commit messages
- No merge conflicts expected unless Story 11-1/11-2 branches are still open

### References

- [Source: architecture-documentation.md#D3] — CI Pipeline Architecture, sample-build job design
- [Source: architecture-documentation.md#NFR12-handling] — "NFR12's intent is 'no stale code' — CI-validated samples achieve this"
- [Source: architecture-documentation.md#Design-Defaults] — "lint and links are blocking; sample build is blocking"
- [Source: prd-documentation.md#FR37] — Documentation maintainers can identify stale content through automated checks
- [Source: prd-documentation.md#NFR14] — Stale content is detectable within 1 CI cycle after a breaking code change
- [Source: epics.md#Story-4.4] — Story definition (mapped as Epic 11, Story 4 in sprint status)
- [Source: 11-3-documentation-validation-github-actions-workflow.md] — docs-validation.yml design, sample-build job details
- [Source: CONTRIBUTING.md:94-102] — Insertion point: after `### Run Docs Validation Locally`, before `## Code Contributions`

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

### Completion Notes List

### File List
