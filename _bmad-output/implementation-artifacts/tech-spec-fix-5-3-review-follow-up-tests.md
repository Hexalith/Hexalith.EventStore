---
title: "Fix 5-3 review follow-up tests"
type: "bugfix"
created: "2026-03-18"
status: "done"
baseline_commit: "3b8d5bca86f5bd55df00926069fb33b62e044778"
context:
    - 'd:\Hexalith.EventStore\CLAUDE.md'
    - 'd:\Hexalith.EventStore\_bmad-output\implementation-artifacts\5-3-three-layer-multi-tenant-data-isolation.md'
---

# Fix 5-3 review follow-up tests

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Several Story 5.3 verification tests currently overstate what they prove, rely on brittle source-text scanning, or carry stale acceptance-criteria comments. That weakens confidence in the security verification story even when the product code itself is behaving correctly.

**Approach:** Tighten the affected tests so they verify the intended behavior more directly, align test names/comments with actual coverage, and keep the changes scoped to the four reviewed test files without altering unrelated Story 5.4 work.

## Boundaries & Constraints

**Always:** Keep edits scoped to the four reviewed test files; preserve existing product behavior unless a test cannot be made truthful without a tiny supporting change; prefer stronger behavioral assertions over implementation-detail coupling; follow the repo’s xUnit/Shouldly/NSubstitute patterns; leave the unrelated `_bmad-output/implementation-artifacts/5-4-dapr-service-to-service-access-control.md` change untouched.

**Ask First:** Any change that requires modifying production code outside the minimum needed to support truthful tests; any expansion beyond the reviewed findings into broader Story 5.3 acceptance coverage.

**Never:** Rewrite the Story 5.3 architecture; add new dependencies; change unrelated stories/spec artifacts; auto-fix out-of-scope acceptance-audit gaps such as pub/sub/YAML coverage in this pass.

## I/O & Edge-Case Matrix

| Scenario                               | Input / State                                                                                   | Expected Output / Behavior                                                                                                    | Error Handling                                                                          |
| -------------------------------------- | ----------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Truthful isolation test                | Existing tenant-isolation tests in `StorageKeyIsolationTests.cs`                                | Tests demonstrate key-based isolation rather than state-manager separation alone                                              | Replace misleading test logic/name with behavior that fails if tenant keying breaks     |
| Robust payload protection verification | Source files contain logger calls and redacted envelope `ToString()` output                     | Tests detect payload leakage with stronger assertions and fewer text-scan blind spots                                         | Keep assertions deterministic and file-based if runtime log capture is disproportionate |
| Domain service failure-path coverage   | Resolver/invoker tests simulate empty config, malformed config, and downstream invocation paths | Tests distinguish “no registration” from “config store unavailable” and validate tenant/domain routing assumptions accurately | Assert the real exception/null behavior instead of naming mismatch                      |
| Traceability cleanup                   | Stale AC/story comments in reviewed files                                                       | Comments accurately describe Story 5.3 scope and the behavior under test                                                      | N/A                                                                                     |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.Tests/Security/StorageKeyIsolationTests.cs` -- storage-key isolation coverage and misleading cross-tenant test naming/logic
- `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs` -- payload redaction and logger source-scan assertions
- `tests/Hexalith.EventStore.Server.Tests/Security/DomainServiceIsolationTests.cs` -- resolver/invoker tenant-routing and failure-path coverage
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs` -- metadata/persistence assertions and any naming/comment cleanup needed
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` -- authoritative behavior for config lookup and malformed config handling
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` -- authoritative downstream invocation and failure wrapping behavior
- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` -- logger patterns used by payload-protection source scans
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` -- contract envelope redaction behavior
- `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs` -- server envelope redaction behavior

## Tasks & Acceptance

**Execution:**

- [ ] `tests/Hexalith.EventStore.Server.Tests/Security/StorageKeyIsolationTests.cs` -- replace or rename the misleading separate-state-manager test and keep the stronger shared-state verification as the actual tenant-key isolation proof -- avoids false confidence in AC2 coverage
- [ ] `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs` -- strengthen redaction assertions and harden source-scan logic for logger calls without overreaching into full runtime logging infrastructure -- improves SEC-5 test credibility
- [ ] `tests/Hexalith.EventStore.Server.Tests/Security/DomainServiceIsolationTests.cs` -- align test names with actual resolver behavior, add missing negative/precision assertions for requested config keys and malformed registrations, and improve downstream routing coverage -- makes tenant-scope verification truthful
- [ ] `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs` -- clean up misleading metadata-field naming/comments if needed and keep persistence assertions behavior-focused -- improves test readability and traceability
- [ ] `tests/Hexalith.EventStore.Server.Tests/{Security,Events}/*` -- correct stale Story/AC traceability comments in the touched files -- keeps verification notes consistent with Story 5.3
- [ ] targeted test execution -- run the affected server test files (or focused test names) and confirm they pass -- validates the fixes without disturbing unrelated work

**Acceptance Criteria:**

- Given the updated isolation tests, when tenant key derivation breaks, then at least one reviewed test fails for the keying reason rather than only because separate state stores do not share state.
- Given the updated payload-protection tests, when payload bytes are exposed through obvious alternate string forms or logger source scans miss covered log methods, then the reviewed tests fail.
- Given the updated domain-service tests, when resolver behavior differs between empty configuration, malformed configuration, and tenant/domain-specific lookup, then the reviewed tests distinguish those cases accurately.
- Given the touched test files, when a reviewer reads their headers and inline comments, then Story 5.3 references and coverage statements are internally consistent.
- Given the targeted test run, when the modified tests execute, then they pass without requiring changes to unrelated Story 5.4 artifacts.

## Spec Change Log

## Design Notes

The goal is not to turn this into a full Story 5.3 reimplementation. The safest improvement is to make each reviewed test truthful about what it proves.

Two guiding principles:

1. Prefer assertions derived from the same public behavior the production code guarantees, not incidental formatting or broad source-text heuristics.
2. If a test name promises an outage, injection, or routing scenario, the arrangement must actually simulate that scenario.

## Verification

**Commands:**

- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~StorageKeyIsolationTests|FullyQualifiedName~PayloadProtectionTests|FullyQualifiedName~DomainServiceIsolationTests|FullyQualifiedName~EventPersisterTests"` -- expected: all targeted tests pass

**Manual checks (if no CLI):**

- Review the touched test names/comments and confirm they describe the actual behavior under test.
- Review the unrelated `_bmad-output/implementation-artifacts/5-4-dapr-service-to-service-access-control.md` diff and confirm it remains unchanged by this work.
