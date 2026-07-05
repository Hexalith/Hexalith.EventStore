---
id: KIT-1
title: Aggregate Test Kit
classification: backlog
status: draft
source_story: 7.5
created: 2026-07-05
---

# KIT-1 - Aggregate Test Kit

## Scope

Define a future reusable aggregate test kit for domain authors using a `Given(events).When(command).Then(events)` style fixture. The kit must validate replay determinism, Apply idempotency expectations, command rejection behavior, event metadata identity, and package dependency boundaries.

## Non-Goals

- Do not implement the test kit in Phase 4 MVP.
- Do not add unnecessary Server dependencies to lightweight testing packages.
- Do not replace domain-specific tests that assert business behavior.

## Dependencies

- Stable domain-service SDK contracts and aggregate discovery conventions.
- Testing package split decisions for `Hexalith.EventStore.Testing` and integration helpers.
- Event versioning/upcasting decisions if fixtures replay mixed historical payload versions.

## Risks

- A fixture that bypasses platform identity or serialization rules can give false confidence.
- Pulling Server dependencies into test helpers can make domain modules heavier than intended.
- Generic assertions can obscure domain-specific invariants.

## Validation Expectations

- The kit must have self-tests for success, rejection, replay determinism, duplicate events, and unsupported handler shapes.
- Package tests must prove domain modules can consume the kit without referencing Server internals.
- Documentation must show one minimal Sample-domain example and one non-trivial domain-style example.
