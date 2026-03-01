# Definition of Done under External Blockers (Template)

Use this template when story implementation is complete but solution-level gates fail due to unrelated, pre-existing issues.

## Story Status Guidance

- Keep Story Status as `in-progress` when a required global gate is still open.
- Use review outcome `Approved with External Blockers` when all in-story findings are closed.
- Move to `done` only after the external blocker gates are cleared or explicitly waived.

## Task Framing Pattern

Use one task that separates story-scope verification from global health checks:

- [ ] Verify backward compatibility and record global solution blockers
  - [x] Story-scope build succeeds (project(s) touched by this story)
  - [x] Story-scope tests pass (targeted tests for changed behavior)
  - [x] Dependency boundary checks pass (no forbidden packages/behaviors)
  - [ ] Full-solution build and broad test matrix pass (blocked externally)

## Review Outcome Pattern

- Outcome: Approved with External Blockers
- Summary language:
  - All HIGH/MEDIUM findings for this story are resolved.
  - Remaining blockers are outside this story scope.

## External Blocker Record (Copy/Paste)

### External Blockers

- Blocker ID: {{blocker_id_or_link}}
- Scope: {{external_project_or_test_area}}
- Symptom: {{failing_gate_summary}}
- Evidence: {{command_or_log_reference}}
- Impact on story: prevents full-solution gate closure only
- Story-scope impact: none

### Decision

- Story implementation: complete
- Review decision: approved with external blockers
- Sprint status: remain `in-progress` until blocker is resolved or waived

## Changelog Entry Pattern

- {{date}}: Final review alignment — all in-story findings closed; story approved with external blockers; status remains `in-progress` pending unrelated global gate failures.

## Checklist (Quick Use)

- [ ] Story status and sprint status are aligned
- [ ] Story-scope build/test evidence is documented
- [ ] External blocker evidence is documented
- [ ] Review outcome is updated
- [ ] Changelog includes final alignment note
