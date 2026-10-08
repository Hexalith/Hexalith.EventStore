---
title: 'Restore the protected domain-service root for Conversations compatibility'
type: 'bugfix'
created: '2026-10-07'
status: 'in-progress'
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
