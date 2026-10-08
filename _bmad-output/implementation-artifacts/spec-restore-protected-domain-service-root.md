---
title: 'Restore the protected domain-service root for Conversations compatibility'
type: 'bugfix'
created: '2026-10-07'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '46a96f6a0769a807b3678fc47380e7c0b5b06589'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve the missing SDK root endpoint required by Conversations' unchanged canonical host composition test. Restore GET `/` with its former constant response, `Hexalith EventStore domain service`, protected by the existing AnyWorkload policy requiring a validated workload assertion and Dapr application-channel token. Preserve AD-16's exactly three anonymous probes, the operational route catalog, and existing operation policies. Verify root credential denial and authorized behavior through the real SDK HTTP pipeline, then rerun Conversations' Local verifier with source references. Preserve unrelated changes, existing Conversations expectations, and all dependency availability/acceptance fields. Perform no commits, remote operations, deployments, dependency updates or submodule initialization.

</frozen-after-approval>

## Implementation Notes

- Investigation: the security change deliberately removed the former anonymous root. AD-16 and Story 5.5 prohibit additional anonymous endpoints, not a protected root. Reuse the explicit AnyWorkload policy and existing HTTP test harness; no new policy, operation, hosting service, or public type is needed.
- Planned footprint: SDK endpoint mapping, SDK route metadata tests, HTTP trust-boundary tests, this local spec, and a separate Conversations verification follow-up. The change is small and reversible, with no unresolved intent questions or external effects.
- Resume observation (2026-10-08): the root mapping and regression tests were already present in EventStore revision `91aae06d49a23bac15be6a4bb363f37140bece4b`. The scoped diff from the recorded baseline contains the protected root, exact AnyWorkload metadata assertions, the root in the mapped-route inventory, and the four HTTP credential combinations. No runtime changes were needed in this resumed run.
- Review correction: updated `MapEventStoreDomainService` XML documentation to include the root's constant response and distinguish its AnyWorkload policy from operation-specific routes. The operational route catalog and policy implementation remain unchanged.
- Verified through the canonical `AddEventStoreDomainService` / `UseEventStoreDomainService` HTTP pipeline: missing both credentials, channel-only, and assertion-only requests receive 401; both valid credentials receive 200 and exactly `Hexalith EventStore domain service`, without invoking the domain processor. Endpoint metadata still enumerates exactly `/alive`, `/health`, and `/ready` as anonymous; all operational routes retain their exact catalog policies.

## Verification

Executed on 2026-10-08 with .NET SDK `10.0.401`. EventStore source revision: `91aae06d49a23bac15be6a4bb363f37140bece4b`; the only subsequent source edit is the XML documentation correction above.

From the EventStore repository:

```bash
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 --artifacts-path artifacts/protected-domain-service-root -p:UseHexalithProjectReferences=true > /tmp/eventstore-protected-root-20261008/source-build.log 2>&1
dotnet artifacts/protected-domain-service-root/bin/Hexalith.EventStore.DomainService.Tests/debug/Hexalith.EventStore.DomainService.Tests.dll -result-xml /tmp/eventstore-protected-root-20261008/source-domain-service-tests.xml > /tmp/eventstore-protected-root-20261008/source-tests.log 2>&1
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 --no-restore --artifacts-path artifacts/protected-domain-service-root -p:UseHexalithProjectReferences=true > /tmp/eventstore-protected-root-20261008/final-build.log 2>&1
```

- Build: exit 0, zero warnings and errors, using the existing Commons source project reference.
- Final build after the XML documentation correction: exit 0, zero warnings and errors; [final build log](/tmp/eventstore-protected-root-20261008/final-build.log). `git diff --check` passed.
- Test project: exit 0, 521 passed, zero failures, skips, errors, or unexecuted cases. This includes all 52 trust-boundary cases and all 60 SDK extension cases.
- Raw local evidence: [build log](/tmp/eventstore-protected-root-20261008/source-build.log), [test log](/tmp/eventstore-protected-root-20261008/source-tests.log), [xUnit XML](/tmp/eventstore-protected-root-20261008/source-domain-service-tests.xml).
- Resolved execution-path issue: the first assembly run from `/tmp/eventstore-protected-root-20261008/bin/Hexalith.EventStore.DomainService.Tests/debug/Hexalith.EventStore.DomainService.Tests.dll` returned exit 1 (510 passed, 11 failed). Every failure was `DirectoryNotFoundException: Could not locate repository root from the test working directory.` The same full project passed from the repository's ignored `artifacts/` directory; no test expectation or guard was weakened. Initial evidence remains in `/tmp/eventstore-protected-root-20261008/tests.log` and `domain-service-tests.xml`.

## Conversations Verification Follow-up

From `/home/administrator/projects/hexalith/conversations`, with source revision `7351d35409fca1be1a0f6be1f5c5c8b5db6a3bfd`:

```bash
pwsh -NoProfile -File eng/verify-ext-conv-ai-1.ps1 -Mode Local -ArtifactsPath /tmp/conversations-protected-root-20261008
```

The verifier builds Debug assets with `UseHexalithProjectReferences=true`, explicitly selecting `/home/administrator/projects/hexalith/eventstore`, `/home/administrator/projects/hexalith/commons`, and `/home/administrator/projects/hexalith/tenants`. It resolves the EventStore source version once and applies that version throughout the local graph.

| Executed suite | Passed | Failed / skipped / errors / not run |
| --- | ---: | --- |
| Conversations.Contracts.Tests | 618 | 0 / 0 / 0 / 0 |
| Conversations.Tests | 185 | 0 / 0 / 0 / 0 |
| Conversations.Server.Tests | 699 | 0 / 0 / 0 / 0 |
| Conversations.Client.Tests | 39 | 0 / 0 / 0 / 0 |
| Focused six-seam simulation (also covered by Server.Tests) | 15 | 0 / 0 / 0 / 0 |

All four builds succeeded. The four full suites passed 1,541 cases, including the unchanged `ConversationsDomainServiceHostCompositionTest.CanonicalDomainEndpointShouldResolve` case for `/`. All seven required Local lanes passed; the verifier emitted its terminal [local-evidence.json](/tmp/conversations-protected-root-20261008/run-20261008T0635435329855Z/local-evidence.json). Build logs, test logs, and all five xUnit XML reports are retained beside that receipt, including the [Server test report](/tmp/conversations-protected-root-20261008/run-20261008T0635435329855Z/Hexalith.Conversations.Server.Tests.xml).

This evidence is `SerializedLocalSimulation` with `LiveReady: false`; it does not change dependency availability, owner acceptance, or live readiness. Conversations' eight pre-existing merge-conflict entries remained unchanged. No Conversations tracked files, dependencies, or submodule pointers were edited, and no commits, remote operations, deployments, or submodule initialization were performed.

## Review Triage Log

Independent Blind Hunter reviewed the restoration's scoped diff (5,944 bytes; finding floor 3) and reported four findings:

- **low, rejected:** missing root-specific invalid-token scenarios. The root uses the exact workload policy and authentication scheme already exercised by the 14-case invalid-credential matrix for `/process`; it adds no credential parsing or authentication branch. A second invalid-credential matrix adds more than a simple correction for negligible coverage beyond the root's missing-credential matrix and explicit policy metadata checks.
- **low, rejected:** missing additional operation-grant success cases and a no-operation denial case for the root. The root metadata is asserted to be exactly AnyWorkload; `CreateAnyWorkloadPolicy` requires an operation claim without selecting `/process`, and `WorkloadAssertionEvaluator` rejects missing operations. Separate root tests would duplicate those shared rules without exercising new mapping behavior.
- **low, patched:** mapping XML documentation omitted the root and implied every route had a catalog operation. The documentation now lists the constant root response and its AnyWorkload exception.
- **low, resolved during planned completion:** the in-progress spec lacked the Conversations verification command, source binding, results, and evidence links. The verification follow-up above records the actual source-reference execution and retains all existing availability and acceptance fields.

No verified runtime defects or deferred findings remain.
