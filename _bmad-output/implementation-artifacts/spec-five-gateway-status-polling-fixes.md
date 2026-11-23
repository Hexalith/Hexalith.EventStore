---
title: 'Make gateway command-status polling consistent'
type: 'bugfix'
created: '2026-11-23'
status: 'in-review'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Address five deferred items around polling a submitted command. Pin the default GET route and its escaped identifier. Reject a missing configured status path before sending a request. Give the legacy client interface the same missing-identifier behavior as the HTTP client. Refuse a rejection event type on a lifecycle state that cannot carry one, while accepting the actor's recorded event type on both a domain rejection and a publication failure. Make the REST generator and sample gateway fakes honor caller cancellation on status reads.

</frozen-after-approval>

## Implementation Notes

- The starting EventStore revision was `ba2c67d8c829d1e5c8f22006504d50db9e71b9ae`. The superproject and ten direct submodules were pulled to main. Seven dependency pins advanced.
- The shared status validator now checks the rejection event against the lifecycle. Both the first-attempt and resumed publication paths in AggregateActor write PublishFailed with the rejection event when its publication fails. The status remains an infrastructure failure for callers.
- The two test gateways inherit the interface implementation. It validates the supplied identifier, observes cancellation, and returns a missing status for a valid unsupported read. The configured path is validated on status reads so other gateway operations stay independent.
- Before the production changes, twelve of twenty-nine focused Client cases failed as expected. The pre-edit Aspire AppHost built with zero warnings and errors. Startup then exited 2 on a write to the read-only home directory, and `aspire describe` found no running AppHost.
- Final Debug source-reference builds passed with zero warnings and errors for Client, REST generator and Sample tests. Direct xUnit runs passed 2,455 Client tests, 320 generator tests and 231 sample tests. Two unrelated Client batch-recovery cases require isolated Dapr endpoints and were skipped. Deferred-work governance returned exit code 0.
- The five individual findings and their two duplicate reports are resolved. The Phase 4 completion writer returned `STORY_IDENTITY_MISSING` for this standalone batch, which has no tracker story key. The spec remains in review.

## Review Triage Log

| Finding | Verdict and disposition |
| --- | --- |
| The interface default threw invalid-identifier errors before returning a task. | Medium. It now returns a faulted task with the same parameter and exception type as the HTTP client. The test obtains the task before asserting its failure. |
| The workload status route lacked a regression for the shared response validation. | Medium. Workload tests use matching tenant and message identities and exercise both the refused Completed receipt and the accepted PublishFailed receipt. |
| The source reports were still open, and the related configured-path report also named slash-only paths. | Low. The client checks the base path after trimming slashes. The ledger links the five fixes and the two duplicate reports to this spec. |
