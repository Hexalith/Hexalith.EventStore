# Reconciliation — 2026-07-07 Generated API Command-Status Location Policy

- **Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-generated-api-command-status-location-policy.md`
- **Verdict:** `partially represented`

## PRD evidence

- **FR12** requires typed generated controllers, gateway delegation, OpenAPI visibility, and generator/runtime error-semantics tests (`prd.md:125`).
- **FR13** places generated controllers in dedicated external API hosts (`prd.md:126`).
- **FR27** owns command-status/archive re-keying by message id (`prd.md:157`).
- Public API/package contract stability is an explicit product concern (`prd.md:89`), and post-proof REST generator hardening remains backlog work (`prd.md:289`).

## Proposal decisions or requirements not represented

- FR12 does not state the approved public response contract: a generated command `202` must emit an **absolute, gateway-authoritative** command-status `Location` when configured, omit `Location` when unconfigured or on failure, and never emit a relative/dangling external-host URL.
- The PRD also does not state that the status key is the single gateway-owned tracking field and must not depend on a `CorrelationId == MessageId` coincidence while FR27 migrates keying.
- The runtime option/builder, dependency injection, emitter code shape, exact tests, Story 2.6, and AD-17 are architecture/implementation details and do not belong in the PRD.

## Conflicts

- No direct semantic conflict: the proposal keeps the change as backlog hardening and says MVP is unaffected, consistent with `prd.md:289`.
- There is a traceability gap: the proposal says it refines FR12, but neither the approved response behavior nor the proposal source appears in the PRD (`prd.md:7-12`, `125`). The decision is also absent from `.memlog.md`, whose FR/NFR count is stale (`.memlog.md:14`).

