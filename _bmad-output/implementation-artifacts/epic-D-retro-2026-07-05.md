---
project: eventstore
epic: D
epic_title: EventStore REST Controller Source Generator
date: 2026-07-05
status: complete
source_status_file: _bmad-output/implementation-artifacts/sprint-status.yaml
source_stories:
  - _bmad-output/implementation-artifacts/D-1-contract-seam.md
  - _bmad-output/implementation-artifacts/D-2-generator-skeleton-spike.md
  - _bmad-output/implementation-artifacts/D-3-controller-emission.md
  - _bmad-output/implementation-artifacts/D-4-generator-tests.md
  - _bmad-output/implementation-artifacts/D-5-proof-sample-blazorui-queries.md
  - _bmad-output/implementation-artifacts/D-6-proof-counter-commands.md
  - _bmad-output/implementation-artifacts/D-7-proof-tenants-ui-host-submodule.md
  - _bmad-output/implementation-artifacts/D-8-packaging-docs-guardrail.md
---

# Epic D Retrospective: EventStore REST Controller Source Generator

## Facilitation Notes

Amelia (Developer): "Administrator, I found Epic D in `sprint-status.yaml`: eight story records, all marked `done`, with the retrospective still `optional`. I treated this run as an evidence-based retrospective using the story files, approved correct-course proposal, architecture remediation proposal, PRD, and architecture spine."

John (Product Manager): "The important product outcome is clear: external applications now have a generated typed REST integration path, and interactive UI hosts use client libraries instead of hosting controllers."

Winston (System Architect): "The biggest architectural learning is just as clear: the original UI-host generation premise was wrong for the system boundary. The correction to dedicated external API hosts is now an adopted invariant."

Murat (Test Architect): "The test lesson is that source generators only get real coverage when the emitted source is compiled and inspected across realistic hosts. D3 and D4 moved us in that direction, and D5-D7 proved why it matters."

Paige (Technical Writer): "The documentation and guardrails had to move with the architecture. D8 closed a lot of stale guidance, but the deferred hardening list still needs to stay visible."

## Epic Summary

Epic D delivered the REST controller generator capability from contract seam through packaging and domain proofs.

- Completed stories: 8 of 8.
- Retrospective status before this document: `optional`.
- Primary scope delivered: `ICommandContract`, REST routing attributes, analyzer project, generated gateway-backed ASP.NET Core controllers, generator test harness, Sample external API proof, Counter command proof, Tenants external API proof, and analyzer package release governance.
- Production incidents recorded in story files: none found.
- Previous retrospective found for Epic C: none found in `_bmad-output/implementation-artifacts`.

## What Went Well

- D1 kept the contract seam narrow. It added the author-facing marker and route metadata without sneaking generator behavior into the contracts project.
- D2 proved the Roslyn analyzer shape with manifest-only output before controller emission. That staged approach made later generator work easier to reason about.
- D3 exposed a large set of generated-controller edge cases and patched them in the generator instead of pushing complexity into adopters.
- D4 converted the generator behavior into persistent `CSharpGeneratorDriver` tests, including source-shape, diagnostics, compile checks, and deterministic output checks.
- D5 correctly escalated a design flaw instead of accepting contract duplication in the UI host. The July 2 correct-course decision resolved the mismatch by moving generated controllers to external API hosts.
- D6 fixed the Sample command path to use typed command contracts and `IEventStoreGatewayClient`, removing the raw generic command POST and `Guid.NewGuid()` path.
- D7 gave the Tenants proof real weight: generated external API host, UI client-library migration, old controller retirement, AppHost coverage, submodule tests, integration evidence, and review-found platform patches.
- D8 registered `Hexalith.EventStore.RestApi.Generators` in the manifest-driven release inventory, validated analyzer-only package contents, and updated docs/guardrails to prevent UI-host controller regression.

## Challenges And Growth Areas

- The original Epic D plan embedded generated controllers in interactive UI hosts. D5 proved that this created duplicate contract identities and an unused generated endpoint. The team corrected the architecture, but only after implementation exposed the issue.
- Several story records carried stale acceptance criteria after the correct-course decision. This created review noise and forced later story-record reconciliation.
- Generated REST behavior had many edge cases: duplicate routes, route/body mismatch handling, keyword-safe identifiers, ETag normalization, unsupported contract shapes, duplicate JSON names, route-template validation, and referenced-contract filtering.
- Referenced-contract discovery needed an explicit `ApiScope` seam. The missing Tenants `ApiScope = "tenants"` metadata produced a zero-controller generated surface until review caught it.
- The query metadata path remains incomplete. Tenants handlers can produce freshness information, but the EventStore query hop still drops some metadata, so generated freshness headers and stale indicators are not fully production-backed yet.
- Environment and topology issues blurred verification: missing DAPR placement/scheduler blocked one live Sample smoke, and Tenants solution/package-mode validation hit workspace-layout and package-version constraints.
- Review scope drift appeared more than once. Story diffs, submodule pointers, D8 edits, and unrelated governance work surfaced inside narrower story reviews.

## Review Pattern Synthesis

Recurring review themes:

- Generated code must fail closed at generation time whenever possible. Silent fallback behavior repeatedly turned into review findings.
- External API hosts need both generated-source evidence and runtime/topology evidence. One without the other missed important defects.
- Gateway delegation is the right boundary, but it exposes shared-client and shared-pipeline behavior that UI-local controllers used to hide.
- Story text must be updated immediately after architectural correction. Otherwise reviewers waste attention separating stale ACs from real defects.
- Submodule proofs require explicit workspace rules and validation commands because nested submodules are intentionally not initialized.

Breakthroughs:

- The July 2 owner directive became the core architectural correction: UI uses client libraries; generated REST is for external applications.
- `RestRouteAttribute.ApiScope` gave referenced-assembly discovery a fail-closed filter.
- Persistent generator tests turned generated-source regressions into ordinary unit-test failures.
- D7's review exposed shared platform issues, not just Tenants-specific defects, and the patches improved the root generator/client/query pipeline.

## Previous Retro Follow-Through

No prior Epic C retrospective was found under `_bmad-output/implementation-artifacts`, so there were no recorded retro action items to reconcile.

Continuity note: the June 2 proposal said Epic C had completed the domain-authoring pattern proof and guardrails. Epic D reused that pattern, but the external REST proof showed the authoring guidance needed a more precise exception for domain-owned contracts libraries and external API hosts.

## Next Work Preview

The next tracked work is not an Epic E. `sprint-status.yaml` now lists the architecture-remediation backlog epics created by the July 4 proposal:

- `SEC-1-trust-boundary-auth`
- `COR-1-append-pipeline-correctness`
- `COR-2-replay-dispatch-versioning`
- `PERF-1-snapshot-projection-cost`
- `REL-1-delivery-poison-handling`
- `OPS-1-admin-plane`
- `CFG-1-topology-deploy-hardening`
- `TEST-1-test-ci-recovery`
- backlog tracks: `GDPR-1`, `IAM-1`, `KIT-1`

Epic D affects that work through these dependencies:

- Architecture invariants AD-3 and AD-4: generated controllers stay in dedicated external API hosts and delegate to `IEventStoreGatewayClient`.
- Architecture invariant AD-14: query evidence must cross the gateway as platform metadata, not ad hoc payload fields.
- Release invariant AD-11: the generator is part of the manifest-governed package set.
- Test invariant AD-12: high-risk behavior needs persisted evidence, not only response codes or mocks.

## Significant Discoveries

1. The UI-host generation model was invalid for this platform boundary.
   Impact: Epic D corrected course to external API hosts, and architecture now codifies that generated REST controllers do not belong in interactive UI hosts.

2. Referenced-contract discovery needs an explicit scope.
   Impact: `ApiScope` became part of the REST route seam. Missing scope metadata can otherwise produce either accidental exposure or zero generated controllers.

3. Query freshness metadata is not yet end-to-end.
   Impact: generated freshness headers and Tenants stale UI states need platform query metadata propagation before they can be treated as production evidence.

4. Live topology and submodule workspace validation need sharper preflight checks.
   Impact: missing local DAPR services, source/package mode differences, and intentionally uninitialized nested submodules can look like product defects unless tests explain the environment state.

No additional Epic D plan rewrite is required after this retrospective. The already-approved PRD and architecture artifacts capture the corrected direction. The carry-forward work belongs in the remediation/backlog epics and in a focused REST generator hardening story.

## Readiness Assessment

- Story completion: complete. All D1-D8 story keys are `done`.
- Build and unit evidence: strong. Story records show Release package-mode builds and focused test projects passing across Contracts, Client, Sample, Generator, DomainService, AppHost, SignalR, Testing, and Tenants source-reference lanes.
- Generated-source evidence: strong. Sample and Tenants generated controller output was emitted and inspected.
- Integration evidence: mixed but acceptable for Epic D closeout. D5 recorded Sample external API endpoint and Redis persisted end-state evidence; D6 live smoke was blocked by missing local DAPR placement/scheduler; D7 integration validation passed in source mode with the noted workspace constraints.
- Deployment evidence: not independently found in the story records. Treat Epic D as implementation-complete, not production-deployment-confirmed.
- Stakeholder acceptance: owner decisions are recorded in the July 2 correct-course and story review resolutions. No separate stakeholder acceptance artifact was found.
- Technical health: acceptable with explicit deferred hardening. The platform direction is coherent, but generator diagnostics, query metadata propagation, Tenants package-mode validation, and external API error-semantics coverage should not disappear.

## Action Items

1. Create a dedicated REST generator hardening story from `deferred-work.md`.
   Owner: John (Product Manager)
   Trigger: before generator hardening is mixed into security, correctness, or UI stories.
   Success criteria: unsupported contract shapes, duplicate command JSON names, invalid query binding sources, empty constant bindings, route-template constraint behavior, and case-insensitive JSON/route matching are represented as explicit ACs or explicitly deferred with rationale.

2. Specify query metadata propagation through the gateway.
   Owner: Winston (System Architect)
   Trigger: before stories depend on freshness, stale indicators, projection version, paging, or ETag evidence as user-visible truth.
   Success criteria: architecture/story guidance says where `IsStale`, `ProjectionVersion`, ETag, paging, and freshness state live; generated controllers and UI hosts consume the platform-owned contract.

3. Add external generated API error-semantics tests.
   Owner: Murat (Test Architect)
   Trigger: when Tenants/generated API compatibility validation resumes.
   Success criteria: generated external API tests cover 403/RBAC, gateway transport failure problem details, invalid cursor, invalid envelope, ETag/304, and route/body mismatch behavior.

4. Add a local DAPR/Aspire smoke preflight for generated API proofs.
   Owner: Amelia (Developer)
   Trigger: before accepting a live-smoke blocker as product evidence.
   Success criteria: the preflight reports placement/scheduler availability, DAPR sidecar endpoints, generated API endpoint URLs, and support-safe failure details.

5. Make correct-course story rewrites mandatory after architectural pivots.
   Owner: John (Product Manager)
   Trigger: whenever an approved proposal supersedes story ACs or design assumptions.
   Success criteria: affected story files contain current ACs before implementation/review continues; stale ACs are either removed or clearly marked superseded.

6. Document the domain-owned contracts-library exception.
   Owner: Paige (Technical Writer)
   Trigger: before another domain proof splits contracts for external API generation.
   Success criteria: authoring guidance explains when a domain-owned contracts library is permitted and how it stays compatible with the domain-centric rule.

7. Resolve or explicitly track Tenants package-mode and Gateway dependency posture.
   Owner: Winston (System Architect)
   Trigger: before treating Tenants external API proof as package-mode release evidence.
   Success criteria: `Hexalith.EventStore.Gateway` dependency mode is documented or pinned, and the Tenants package/source validation path is unambiguous without nested submodule initialization.

## Preparation For Next Work

Critical preparation:

- Carry AD-3, AD-4, AD-11, AD-12, and AD-14 into the next story handoffs.
- Keep `deferred-work.md` visible during planning so generator hardening does not become invisible debt.
- Use `sprint-status.yaml` action items as the retro follow-through list.

Parallel preparation:

- Re-run package manifest and analyzer package validation when release inventory changes.
- Keep external API host guardrails active for Sample, Tenants, and future domain proofs.
- Keep DAPR topology tests aligned with AppHost resources and access-control YAML.

Nice-to-have preparation:

- Add a short generated-controller evidence checklist to future story templates.
- Add a reviewer prompt that asks whether generated API behavior is proven by source inspection, compile tests, runtime tests, and persisted evidence where applicable.

## Closing Notes

Amelia (Developer): "Epic D is complete from a story-status perspective. The team shipped the generator capability and, more importantly, corrected the platform boundary when the first proof showed the original premise was wrong."

Winston (System Architect): "The corrected architecture is stronger than the starting plan: external APIs are explicit adapters, UI hosts stay thin, and the gateway remains the policy boundary."

Murat (Test Architect): "The next improvement is not more broad review; it is sharper test intent. Generated API behavior needs source-generation tests, external-host tests, and persisted evidence when state matters."

John (Product Manager): "The action list is not optional housekeeping. It is the mechanism that prevents deferred generator and metadata work from being lost under the next remediation epics."

Paige (Technical Writer): "The docs now need to stay in lockstep with the architecture. The corrected story is simple: contracts define the REST surface, external hosts expose it, UI hosts consume clients."

Administrator (Project Lead): "Retrospective requested for Epic D through `$bmad-retrospective Epic D`; no additional live discussion input was provided during this run."
