---
title: 'P1R Repair current-source blocking CI tests'
type: 'bugfix'
created: '2026-10-13'
status: 'done'
route: 'oneshot'
baseline_commit: '93982b4e1fe312169c57e2735c90be24b49460fb'
review_loop_iteration: 0
---

<frozen-after-approval reason="human-owned P1R prerequisite intent">

## Intent

Current EventStore CI at the recorded baseline fails two blocking tests and prevents the selected Green CI publication path for a P1R successor. Restore the exact public-domain diagnostics expectation after private-handler exclusion, and make the copy-on-write retirement fault follow committed root state instead of a global dispatch count consumed by background cleanup. Preserve all discovery, persisted-value and retry-order assertions. Verify both affected test projects and review this bounded test-only repair; publication, package selection, P1R acceptance and Story 6.1 readiness remain separately gated.

</frozen-after-approval>

## Implementation Notes

- Live CI run 38440881325 failed the diagnostics discovery assertion and ordinary retirement interruption assertion. Its blocking-test-results artifact was downloaded to `/tmp/p1r-ci-38440881325-01emyvfq` and its TRX files identified both failures.
- The five core published NuGet families still end at 3.120.0; no newer published successor is selected. Integration and P1R Preparation pass at the source baseline.
- Aspire baseline attempt: `aspire start --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` did not become runnable during the attempt. `aspire describe` reported no running AppHost; the pending start was cancelled gracefully with Ctrl-C, exit 0. No topology change or readiness claim is made.

- Restored the exact three-domain diagnostics list; private projection helpers are excluded by current discovery and registration.
- Retirement fault now fires while the new root is durable, regardless of competing background cleanup. Review strengthened the existing test by checking obsolete-value absence inside the later delivery fold, before its mutations run.
- Initial verification: both Debug/source project builds passed without warnings; Client 1497/1497, copy-on-write 8/8, discovery 89/89, and retirement in 20 fresh runner processes passed. Full DomainService: 871/872 passed, one failure because `references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` is absent inside EventStore. Nested initialization remains prohibited; the gate is not weakened.

## Review Triage Log

- Diagnostics projection-only positive coverage: medium / defer. Both discoverable projection fixtures use the aggregate-owned widget domain; removing projection diagnostics enumeration could escape this particular assertion. This existing coverage gap predates the repair; the invalid private counter expectation never supplied conforming positive coverage. Add isolated public projection-only coverage separately.
- Retirement-before-later-delivery coverage: medium / patch. Existing final-state checks could pass if deletion happened after the second fold. The fold now observes and asserts obsolete-value absence before producing owner-two mutations.


## Verification

- `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo` and the corresponding DomainService test project build: exit 0, zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml /tmp/p1r-client-tests-post-review.xml`: exit 0; 1497/1497 passed after the ordering assertion.
- `dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests -result-xml /tmp/p1r-domainservice-discovery.xml`: exit 0; 89/89 passed.
- Full DomainService assembly with `-result-xml /tmp/p1r-domainservice-tests.xml`: exit 1; 871 passed, one guardrail failed solely because the required nested Tenants source file is absent. No skip, assertion weakening, or nested initialization was used.
- Twenty fresh-process invocations with `-method Hexalith.EventStore.Client.Tests.Projections.SharedProjectionCopyOnWriteTests.OrdinaryRetirementResumesAfterRootBatchAndBeforeLaterDelivery`: all exit 0 after review.
- Actual XML results and build/repetition logs retained under `TestResults/p1r-current-ci-repair-2026-10-13/` (ignored local validation artifacts). They are Debug/source evidence, not published-package qualification.
- `git diff --check` and pinned `node_modules/.bin/commitlint --edit /tmp/p1r-ci-repair-commit-message.txt`: exit 0.

This spec completes only the bounded local CI fixture repair. A fresh successful exact-source push CI run and separately authorized publication, archive qualification, four owner decisions and conformance remain required for current P1R usability. Existing acceptance records, package/catalog pins and Story 6.1 readiness are unchanged.
