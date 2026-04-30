# Story Creation Lessons

This ledger was bootstrapped automatically by `jobs/preflight-predev-hardening.py`
because this repository had no existing story-creation lessons file.

Use this file to record durable lessons for recurring BMAD story creation,
party-mode review, advanced elicitation, and code-review automation.

## L08 - Party Review vs. Elicitation

- Party-mode review is the cross-role critique and triage pass before
  development; it should produce dated trace evidence when completed.
- Advanced elicitation is a separate hardening pass after a completed
  party-mode trace exists; a recommendation to run elicitation is not itself
  completed elicitation evidence.

## L09 - Sample Blazor UI Smoke Evidence

- Future sample UI stories should record a repeatable smoke-test evidence
  block, not just "smoke test passed". Use
  `docs/guides/sample-blazor-ui.md#smoke-test-evidence-pattern` as the
  minimum format.
- Record the AppHost command, resource state, browser target, pattern pages,
  commands exercised, observed results, and links or references to Aspire
  traces, logs, screenshots, or the reason artifacts were unavailable.
- Current decision for the sample Blazor UI: build verification plus runtime
  smoke testing remains the chosen strategy. The repository uses bUnit for
  Admin UI, but the sample UI does not get a component test project until a
  future story creates enough repeated UI branching risk to justify it.
