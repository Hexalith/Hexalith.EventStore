---
id: REST-GEN-HARDENING
title: REST Generator Hardening
classification: backlog
status: draft
source_story: 7.5
created: 2026-07-05
related_story: _bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md
source_evidence:
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
---

# REST Generator Hardening

## Scope

Track REST generator hardening that remains beyond the Epic 2 proof scope. The backlog covers unsupported contract-shape diagnostics, duplicate command JSON-name diagnostics, invalid `RestQueryBinding` source diagnostics, empty constant binding diagnostics, route-template constraint behavior, case-insensitive route/JSON-name matching, referenced-contract incrementality, generated-controller authorization checks, and generated external API error-semantics coverage.

## Non-Goals

- Do not mix generator hardening into unrelated security, correctness, UI, or topology stories.
- Do not move generated controllers into interactive UI hosts.
- Do not treat generated API smoke responses as persisted integration evidence.

## Dependencies

- Existing deferred-work entries and Epic D retrospective findings.
- Query metadata propagation from Story 7.6 for freshness, projection version, ETag, and paging evidence.
- Generator test infrastructure in `tests/Hexalith.EventStore.RestApi.Generators.Tests/`.

## Risks

- Diagnostics that miss invalid contract shapes can push failures to runtime.
- Incomplete referenced-contract incrementality can produce stale generated surfaces.
- Generated error semantics can diverge from gateway behavior if tests only use mocks.

## Validation Expectations

- Generator tests must cover diagnostics, generated output, referenced-contract incrementality, route/body mismatch, invalid cursor/envelope behavior, `304`, and safe problem details.
- Any higher-tier proof must inspect persisted or gateway-owned evidence where applicable, not only generated endpoint status codes.
- The dedicated implementation story must keep source-generator changes warnings-as-errors clean.
