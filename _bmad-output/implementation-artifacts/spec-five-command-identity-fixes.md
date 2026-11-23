---
title: 'Align command identity across gateway requests, errors and operator guidance'
type: 'bugfix'
created: '2026-11-23'
status: 'in-review'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close five deferred findings on command and event identity. Use the Contracts correlation grammar (AD-32) for the gateway request header and supplied command correlations (DW-543). Apply the exact v1 MessageId grammar on command submission and existing identifier lookup routes, including rejection of a trailing newline. Preserve optional command correlation fallback and the direct gateway's bounded legacy lookup.

Verify that a real handled authorization outage returns the caller's correlation identifier in both the final 503 response header and its problem body. Document how to retrieve status using the accepted command's message ID and authorized scope. Correct the deployment guide's CloudEvent identity so subscribers can deduplicate a retried event by its persisted event message ID.

</frozen-after-approval>

## Implementation Notes

- The starting EventStore revision was `9590106d411347d992ee29b812feacf56bed2c30`. All ten direct references were pulled to upstream main and six pins advanced.
- The existing Contracts event validator owns the AD-32 predicate. The gateway previously shared one regular expression across message and diagnostic roles. Public status and replay lookup compatibility belongs to a separate owner decision.
- The pre-edit Aspire AppHost built successfully. `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` then exited 2 because the CLI attempted to write to the read-only home directory. `aspire describe` confirmed the AppHost had exited.

- Debug source-reference builds of the Server, Integration, Contracts and ServiceDefaults test projects passed with zero warnings or errors. Direct xUnit runs passed 164 server tests (command validation, status and replay, workload lookup, tracing and event publishing), 6 middleware and hosted-outage tests, 85 Contracts identity tests and all 160 ServiceDefaults tests. The 415 cases had zero skips. The 404 example was checked against the controller's route, scope, detail, and correlation extension, and the deployment guidance against EventPublisher's metadata.
- A negative control removed the final response-header callback in a disposable checkout and supplied a correlation valid under both grammars. The hosted outage test failed at its header assertion. The unmodified callback passes on the product source.
- An additional run of `Hexalith.EventStore.IntegrationTests.EventStore.CommandsControllerTests` had seven failures among eleven cases. The same seven failed on the synchronized pre-change revision `61a7657a39145633181ea631f3ebcdb62d795124`. The positive-submit fixture receives HTTP 503 with “Catalog authority cannot safely admit this operation.” This broad fixture needs a qualified local catalog to verify its 202 assertions.
- The five individual ledger entries are closed. The Phase 4 completion writer returned `STORY_IDENTITY_MISSING` for this standalone batch because there is no canonical tracker story key. The spec stays in review.

## Review Triage Log

| Finding | Verdict and disposition |
| --- | --- |
| Command API documentation applied the lookup grammar to command correlations and used a non-strict regex anchor. | Medium. The submit, status and replay references now describe their distinct whole-value rules and the header's single-value behavior. |
| An overlength identifier could receive a second, misleading character error. The message omitted its endpoint rule. | Low. Each validation chain stops at its first failure and its format message names the ASCII rule. Regression tests pin the length-only errors. |
| Workload denial telemetry could lose a correlation minted by gateway middleware. | Medium. Telemetry now prefers the validated assigned correlation. A duplicate-header regression verifies that denial and response values match. |
| Status and replay spans tagged the route key as the request correlation. | Medium. Both use the bounded request correlation. Activity-listener regressions supply separate request and lookup IDs. |
| A request carrying two valid header values lacked regression coverage. | Low. The middleware test requires one new identifier in both the request context and response. |
| Malformed workload status keys had no controller test. | Low. Authorized workload tests verify a 400 and zero status-store calls for a trailing newline and an edge hyphen. |
