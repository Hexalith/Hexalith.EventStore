# Next bmad-build prompt: Story 3.21 local failure diagnosis

Use this after named-owner approval of the proposal and this slice's exact
intent, with a valid current-subject Story 9.1 corrective-work authorization.
The prompt itself grants none of those authorities. Platform and Builds changes
are later work in their owning repositories.

```text
$bmad-build

Implement only ES-DIAG from:
_bmad-output/implementation-artifacts/spec-3-21-local-broker-failure-diagnosis.md

Read its approved exact text and:
- _bmad-output/planning-artifacts/sprint-change-proposal-2026-10-20.md
- _bmad-output/implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md
- _bmad-output/implementation-artifacts/evidence/story-3-21/verification-2026-10-20/report.md
- references/Hexalith.Platform/README.md
- The effective repository baseline, architecture AD-5/8/9/11/12/24/26/28/31/34/36,
  and relevant build/test/dependency-mode rules.

First verify exact frozen-intent approval and validate the scoped Story 9.1
authorization against the real current input subject and changed-path rules.
This draft, the prompt and the course-correction request are not that record.
If approval, validator or record is missing/invalid, report the exact blocker;
do not forge authority, broaden allowed paths or start gate-closing work.

Work in EventStore. Preserve all user changes and original Story 3.21 packets,
including rejected preservation runs and earlier valid historical captures.
Keep the current candidate identities: Dapr 1.18.7 / CLI 1.18.2,
components-contrib 1.18.6, RabbitMQ 4.3.6-management and Kafka 4.3.1.
The course-correction read-only recheck rejects the latest packet's pre-existing
unlisted inputs/tools/__pycache__/dapr_broker_qualification.cpython-314.pyc.
Preserve that file and failure. Keep interpreter caches and verifier outputs
outside all new closed packets; do not scrub or reseal historical evidence.

1. Establish an exclusively allocated disposable local worker/VM and local
   Unix-socket Docker daemon, binding effective endpoint, daemon identity and
   ownership before mutation. Keep the full unrelated-container specification
   preservation gate; no exclusions for disappearing containers. If exclusive
   infrastructure is unavailable, retain an environment blocker and stop before
   resource creation. Do not use the shared development daemon under a merely
   cooperative lock or enable remote Docker endpoints.
2. Diagnose both secret-rotation failures. Retain publish/overlap/all-consumer
   acknowledgement/startup-only rollout/revocation and broker session/principal
   diagnostics. Test the old live connection and a fresh sidecar explicitly
   loading the old lab credential generation, beside new-generation success.
   Explicit attributable denial and fresh Dapr state reconciliation are needed;
   RabbitMQ 599 is unknown, and Kafka 204 is no denial proof.
3. Reproduce RabbitMQ queue pressure/negative confirms and missing bindings
   with positive controls, native diagnostics, exact per-ID HTTP receipts and
   drain/restart Dapr readback. Preserve failure predicates. Reconcile cancelled
   or timed-out IDs without assuming no effect or retrying as a fresh effect.
   If the runtime cannot supply the guarantee, retain FAIL with a minimal
   reproduction and owner/upstream remedy proposal; do not change pins, add
   application provider clients or weaken the guarantee.
4. Execute all 12 scenarios for both brokers, the meaningful preservation and
   verifier mutation tests, and the explicit real C# capture. Capture integrity,
   candidate qualification and production authority are separate gates.
   --allow-failures may accept an integrity-valid rejected observation; it must
   never hide preservation failure or establish technical/production PASS.
5. Retain new immutable source/lock/loaded-identity/command/time/exit/state and
   cleanup receipts in fresh directories. Link the four old failures to each
   new finding or uncertainty. Produce proposed Story 3.17 inventory rows and
   owner-bound Platform/Builds handoffs. Keep parent Story 3.21 in progress,
   selectedBroker null and production eligibility FAIL. Complete only ES-DIAG
   if its own acceptance, review and applicable authority controls pass.

Do not modify Platform or Builds, start another shared AppHost, run Kubernetes
or Helm operations, change production resources or Secrets, choose a broker,
publish, change gitlinks/dependencies, perform Git mutations or send messages
to external owners. No sealed ci.yml/integration.yml/docs/ci.md/OQ8 input edits;
no Story 2.13 evidence changes. The new manual lane may change only within the
approved scope and remains non-promoting. No source-mode success may be inferred
from the historical package-mode Debug build; retain the CS1704 blocker and
use the repository's focused fallback checks without dependency refresh.

Platform owns the later shared composition/Kubernetes rehearsal and production
bundle. Builds owns shared runner/descriptor and catalog/CLI changes. Neither is
implemented by this local diagnostic slice. The exact proposal gives their
ordered follow-up work and preserves AD-26, Story 9.1, assurance/sealed-run,
5.7, 3.19, append-envelope, restore and consumer-authority gates.
```
