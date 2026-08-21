---
project: eventstore
date: 2026-08-20
workflow: bmad-correct-course
mode: incremental
scope_classification: minor
status: approved
approval: Approved by Administrator on 2026-08-20
trigger: commit messages footer, header, body lines max size should be 200
---

# Sprint Change Proposal — Commit Message Header, Body, and Footer 200-Character Limits

**Author:** Developer via `bmad-correct-course`  
**Change scope:** Minor direct adjustment; repository-governance configuration and verification  
**Status:** APPROVED AND IMPLEMENTED  

## 1. Issue Summary

The repository commit-message contract requires commitlint to enforce a maximum length of 200 characters across all three structural sections of a commit message:
1. Header line (`header-max-length`: 200)
2. Body lines (`body-max-line-length`: 200)
3. Footer lines (`footer-max-line-length`: 200)

### Background and Context

Previously, `commitlint.config.mjs` configured `header-max-length` and `body-max-line-length` as 200, but omitted `footer-max-line-length`. Consequently, commitlint inherited the 100-character default for footer lines from `@commitlint/config-conventional`. When commit messages contain longer footer lines (e.g. detailed `BREAKING CHANGE:` explanations, references, or co-author trailers exceeding 100 characters), commitlint fails with:
`footer's lines must not be longer than 100 characters [footer-max-line-length]`.

Additionally, `CONTRIBUTING.md` stated that the entire header and each body line must be 200 characters or fewer, but did not explicitly document the footer line ceiling. Furthermore, `CommitMessagePolicyTests.cs` did not assert the presence of all three explicit 200-character limits in `commitlint.config.mjs`.

### Trigger Classification

- **Type:** Repository-governance policy clarification and configuration alignment.
- **Triggering story:** None (repository-level maintenance and developer tooling governance).
- **Core problem:** Align executable commitlint configuration, contributor documentation, and repository policy tests to explicitly enforce the 200-character ceiling for headers, body lines, and footer lines.

## 2. Impact Analysis

### Epic and Story Impact

| Area | Impact | Disposition |
| --- | --- | --- |
| Epic 3 — Release and Repository Reliability | Governs repository infrastructure and tooling. | No epic or story edit required. Existing stories remain intact. |
| Epics 1–2 and 4–8 | No runtime dependency on commit message lint rules. | No change. |
| Epic ordering / priority | No change to sprint backlog or delivery order. | No resequencing. |

No epic or story is added, removed, redefined, reopened, renumbered, or deferred.

### Artifact Impact

| Artifact | Impact |
| --- | --- |
| `commitlint.config.mjs` | Add explicit `'footer-max-line-length': [2, 'always', 200]`. |
| `CONTRIBUTING.md` | Update Commit Messages section to document that header, body lines, and footer lines must all be 200 characters or fewer. |
| `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` | Add assertions verifying `'header-max-length'`, `'body-max-line-length'`, and `'footer-max-line-length'` are explicitly configured to 200. |
| PRD / Architecture / UX | No change. Commit message linting is repository-governance tooling, not a runtime or functional product requirement. |
| `sprint-status.yaml` | No change. |

### Technical Impact

- Local Husky hooks (`.husky/commit-msg`) and CI workflows will execute commitlint with the updated 200-character ceiling for footer lines.
- Conventional Commit types, description lowercase rules, and the prohibition against `chore` remain unchanged.
- Authors still receive guidance to prefer concise headers near 50 characters, while 200 characters remains the mechanical ceiling.

## 3. Recommended Approach

**Selected: Option 1 — Direct Adjustment.**

- **Effort:** Low — small configuration, documentation, and test update.
- **Risk:** Low — relaxes the overly restrictive default footer line limit (100 -> 200) to match header and body lines.
- **Timeline / MVP impact:** None.

**Option 2 — Potential Rollback:** Not applicable.  
**Option 3 — PRD MVP Review:** Not applicable.

## 4. Detailed Change Proposals

### Proposal 1: Commitlint Configuration

**File:** [commitlint.config.mjs](file:///home/administrator/projects/hexalith/eventstore/commitlint.config.mjs)

**OLD:**
```javascript
export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    'type-enum': [
      2,
      'always',
      ['build', 'ci', 'docs', 'feat', 'fix', 'perf', 'refactor', 'revert', 'style', 'test'],
    ],
    'header-max-length': [2, 'always', 200],
    'body-max-line-length': [2, 'always', 200],
  },
};
```

**NEW:**
```javascript
export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    'type-enum': [
      2,
      'always',
      ['build', 'ci', 'docs', 'feat', 'fix', 'perf', 'refactor', 'revert', 'style', 'test'],
    ],
    'header-max-length': [2, 'always', 200],
    'body-max-line-length': [2, 'always', 200],
    'footer-max-line-length': [2, 'always', 200],
  },
};
```

**Rationale:** Explicitly configures the 200-character maximum line length for footers, overriding `@commitlint/config-conventional`'s 100-character default.

---

### Proposal 2: Contributor Documentation

**File:** [CONTRIBUTING.md](file:///home/administrator/projects/hexalith/eventstore/CONTRIBUTING.md#L45-L51)

**OLD:**
```markdown
Start the description with a lowercase letter and omit a trailing period. Use
imperative mood as a repository authoring convention. Keep the entire header
and each body line at 200 characters or fewer, and prefer a concise header
near 50 characters. Use `feat` for a minor release, `fix` or `perf` for a
patch release, and `docs`, `test`, `refactor`, `build`, `ci`, `revert`, or
`style` when product behavior does not change. Use `!` or a
`BREAKING CHANGE:` footer for a major release.
```

**NEW:**
```markdown
Start the description with a lowercase letter and omit a trailing period. Use
imperative mood as a repository authoring convention. Keep the entire header,
each body line, and each footer line at 200 characters or fewer, and prefer a
concise header near 50 characters. Use `feat` for a minor release, `fix` or
`perf` for a patch release, and `docs`, `test`, `refactor`, `build`, `ci`,
`revert`, or `style` when product behavior does not change. Use `!` or a
`BREAKING CHANGE:` footer for a major release.
```

**Rationale:** Accurately documents that the 200-character limit applies to headers, body lines, and footer lines.

---

### Proposal 3: Repository Contract Test

**File:** [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs](file:///home/administrator/projects/hexalith/eventstore/tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs#L453-L476)

**OLD:**
```csharp
        commitlintConfig.ShouldContain("extends: ['@commitlint/config-conventional']");
        commitlintConfig.ShouldContain("'type-enum'");
        commitlintConfig.ShouldNotContain("'chore'");
```

**NEW:**
```csharp
        commitlintConfig.ShouldContain("extends: ['@commitlint/config-conventional']");
        commitlintConfig.ShouldContain("'type-enum'");
        commitlintConfig.ShouldContain("'header-max-length': [2, 'always', 200]");
        commitlintConfig.ShouldContain("'body-max-line-length': [2, 'always', 200]");
        commitlintConfig.ShouldContain("'footer-max-line-length': [2, 'always', 200]");
        commitlintConfig.ShouldNotContain("'chore'");
```

**Rationale:** Pins the exact commitlint rules in unit tests so neither header, body, nor footer limits can drift or be accidentally removed.

---

## 5. Implementation Handoff

**Scope classification:** Minor — direct implementation by Developer agent.

| Recipient | Responsibility |
| --- | --- |
| Developer agent | Apply edits to `commitlint.config.mjs`, `CONTRIBUTING.md`, and `CommitMessagePolicyTests.cs`. |
| Test suite | Verify boundary behavior (200 chars pass, 201 chars fail for header, body line, footer line) and run `CommitMessagePolicyTests`. |

### Implementation Sequence

1. Edit [commitlint.config.mjs](file:///home/administrator/projects/hexalith/eventstore/commitlint.config.mjs) to add `'footer-max-line-length': [2, 'always', 200]`.
2. Edit [CONTRIBUTING.md](file:///home/administrator/projects/hexalith/eventstore/CONTRIBUTING.md) to update the commit line limit description.
3. Edit [CommitMessagePolicyTests.cs](file:///home/administrator/projects/hexalith/eventstore/tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs) to assert all three limits.
4. Execute boundary verification tests with commitlint:
   - Header (200 pass, 201 fail)
   - Body line (200 pass, 201 fail)
   - Footer line (200 pass, 201 fail)
5. Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests`.

### Success Criteria

1. `commitlint.config.mjs` explicitly defines `header-max-length`, `body-max-line-length`, and `footer-max-line-length` as 200.
2. A 200-char footer line passes and a 201-char footer line is rejected by commitlint.
3. `CONTRIBUTING.md` documents 200 characters for header, body lines, and footer lines.
4. `CommitMessagePolicyTests` passes and asserts all three rules.
5. No runtime, PRD, architecture, or submodule files are modified.

---

## Change Analysis Checklist

### 1. Understand the Trigger and Context
- [x] **1.1** Trigger identified: Footer, header, body lines max size should be 200.
- [x] **1.2** Core problem defined: `footer-max-line-length` defaulted to 100 and needed explicit 200 limit aligned across config, docs, and tests.
- [x] **1.3** Evidence gathered: Commitlint CLI tests demonstrated 150-char footers failing under default config.

### 2. Epic Impact Assessment
- [N/A] **2.1** Epic 3 encompasses repository governance; no story invalidated.
- [N/A] **2.2** No epic changes required.
- [x] **2.3** Remaining epics reviewed; no dependencies affected.
- [N/A] **2.4** No epics invalidated or created.
- [N/A] **2.5** No priority or sequence adjustments.

### 3. Artifact Conflict and Impact Analysis
- [x] **3.1** PRD checked — no conflicts.
- [x] **3.2** Architecture checked — no runtime or architecture conflicts.
- [N/A] **3.3** UI/UX — not applicable.
- [x] **3.4** Artifacts updated: `commitlint.config.mjs`, `CONTRIBUTING.md`, `CommitMessagePolicyTests.cs`.

### 4. Path Forward Evaluation
- [x] **4.1** Direct Adjustment viable (Low effort / Low risk).
- [N/A] **4.2** Rollback not applicable.
- [N/A] **4.3** MVP review not applicable.
- [x] **4.4** Direct Adjustment selected.

### 5. Sprint Change Proposal Components
- [x] **5.1** Problem statement & context documented.
- [x] **5.2** Impact analysis documented.
- [x] **5.3** Recommended approach & rationale documented.
- [x] **5.4** Detailed before/after diffs documented.
- [x] **5.5** Implementation handoff & success criteria defined.

### 6. Final Review and Handoff
- [x] **6.1** Checklist complete.
- [x] **6.2** Proposal verified against repository state.
- [x] **6.3** User approval obtained (approved by Administrator on 2026-08-20).
- [N/A] **6.4** Sprint status update not needed.
- [x] **6.5** Implementation completed and verified.

---

## 7. Workflow Execution Log

| Date | Event | Result |
| --- | --- | --- |
| 2026-08-20 | Activated `bmad-correct-course` workflow for 200-char commit message line limits. | Complete |
| 2026-08-20 | Incremental review mode selected by Administrator. | Complete |
| 2026-08-20 | Completed Change Analysis Checklist across repository artifacts. | Complete |
| 2026-08-20 | Presented 3 incremental edit proposals (`commitlint.config.mjs`, `CONTRIBUTING.md`, `CommitMessagePolicyTests.cs`). | Approved |
| 2026-08-20 | Compiled and presented Sprint Change Proposal document. | Approved |
| 2026-08-20 | Applied edits to `commitlint.config.mjs`, `CONTRIBUTING.md`, and `CommitMessagePolicyTests.cs`. | Implemented |
| 2026-08-20 | Executed commitlint boundary checks (header 200/201, body 200/201, footer 200/201). | All passed |
| 2026-08-20 | Executed `CommitMessagePolicyTests` suite via `dotnet test`. | 21 tests passed (0 failed) |

