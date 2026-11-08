# Epic 3 Context: Maintainers Can Release Reproducible, Verifiable Artifacts

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make EventStore builds, tests, packaging, publication and verification reproducible independently of local checkout state. Identify exact package/runtime lineage, reject invalid candidates and distinguish technical behavior from release, deployment and consumer authority.

## Stories

- Story 3.1: Re-Tier Live-Sidecar Tests from the Release Gate
- Story 3.2: Harden DAPR ETag Timeout for Integration Conditions
- Story 3.3: References-Based Submodule Layout
- Story 3.4: Aspire Security Resource Naming
- Story 3.5: Shared Package Catalog and Source/Package Reference Modes
- Story 3.6: Manifest-Driven Release Packaging
- Story 3.7: Shared Workflow Caller Migration
- Story 3.8: Workflow Reference and Validation Safety
- Story 3.9: Supply-Chain Publishing Backlog
- Story 3.10: Generated API DAPR/Aspire Smoke Preflight
- Story 3.11: Validated Central Package Catalog Refresh
- Story 3.12: Multi-Platform EventStore Container Publishing Correction
- Story 3.13: v3.94.1 Deployed Runtime Evidence Disposition
- Story 3.14: Corrective OCI Provenance Release
- Story 3.15: Corrected Deployed Runtime Parity Closure
- Story 3.16: Latest-Compatible Dependency And Root Submodule Refresh
- Story 3.17: Dapr Boundary Inventory And Enforcement
- Story 3.18: Public-Surface Compatibility Baseline And Release Gate
- Story 3.19: Publication Authority Record And Canonical Production Profile
- Story 3.20: Consumer-Removal Authority Manifest And Validator
- Story 3.21: Production DAPR Runtime Pin And Durable Broker Qualification
- Story 3.22: Bounded Qualification Evidence Production And Admission
- Story 3.23: Retire Bulky Qualification Evidence

## Requirements & Constraints

- Separate deterministic release and live-sidecar lanes. Live tests assert persisted end-state. Distinguish infrastructure blockers from product failures; HTTP success, mocks and historical passes do not establish durability or isolation.
- Builds owns shared package/tool/runtime pins. Move coupled dependencies coherently with restore/build/tests and consumer validation. Source mode requires explicit selection and available root-declared sources; configured defaults select packages. Local development follows the baseline source-reference/Debug policy; CI/publication use packages/Release. Report mode and blocked lanes accurately.
- Package inventory changes are atomic with packaging, metadata and consumer checks. Container releases use immutable OCI indexes containing exactly Linux amd64 and arm64 images. Published releases, including rejected ones, remain immutable; correction uses a later conforming version.
- Proof is repository tests plus CI. Record commands, pass/fail counts, full commit SHA and CI run link in the owning story/spec. Run output belongs in temporary directories or CI artifacts. Evidence folders are ignored local scratch; committed evidence paths and files over 1 MiB are rejected outside a small explicit allowlist.
- Evidence simplification supersedes external-store/materialization work and Stories 3.22/3.23, and removes Story 9.1's validator/registry. Do not recreate deleted machinery from historical references; scheduled owning stories re-specify replacements. Remaining production, seal and assurance controls apply.
- Current approved runtime refresh targets are runtime/control plane and CLI 1.19.1, embedded components-contrib 1.19.0, and .NET SDK 1.19.0 while latest-stable verification agrees. Previous runtime evidence retains its original subject. Verify exact source, release checksums, binaries, architecture-specific images and charts; new versions require new affected regression proof.
- Preserve sealed and original-runtime CI inputs until their separately planned reseal. An independently bound qualification lane may test a new runtime without claiming sealed-lane equivalence. No general dependency/gitlink refresh or cluster update follows from the runtime target.

## Technical Decisions

Application infrastructure access uses qualified Dapr APIs/components or an explicitly accepted, narrow architecture exception. Map operations and required guarantees to exact runtime/client/component versions. Provider administration and backend reads are diagnostics or fault injection, never substitutes for application Dapr-path readback. Cancellation, timeout or unavailable reconciliation does not establish absence or permit a second effect.

EventStore owns reusable contracts and repository qualification; Platform owns shared composition, rendering, Kubernetes rehearsal and deployment/restore; Builds owns catalogs/tools/runners. App IDs, scopes, ACLs, topics, sidecar options and loaded/rendered configuration agree across owners. Repository AppHosts prove repository behavior only.

The proposed production topology remains subject to authenticated Architecture and Platform ratification. Production uses qualified durable broker/runtime inputs, verified TLS, fail-closed authorization, scoped per-app actor state and ordinary OpenBao secrets. Redis is Development/test only. Candidate diagnostics, Platform startup and captured failures do not select a broker or authorize promotion. Exclusively allocated disposable local infrastructure is required for broker capture; every pre-existing unrelated resource remains subject to preservation checks.

Publication advances through built, candidate-published, evidence-validated, release-available and production-promoted. Immutable subjects and role-bound approvals govern transitions; subject changes invalidate inherited conclusions. Technical/story completion grants no release, deployment or removal authority. Label required/achieved assurance; tools and CI are not human approvers.

## Cross-Story Dependencies

- Layout and explicit dependency modes support manifest packaging, workflow migration and deterministic/live separation. Generated API smoke preflight also consumes Epic 2's generated-host work.
- Historical disposition, corrective publication and bounded runtime parity are separate outcomes. Compatibility and publication authority need their own inventory, classification and owner decisions; consumer infrastructure removal additionally needs each consumer owner's exact-subject authorization.
- Runtime qualification feeds the infrastructure-operation inventory, production component parity and canonical publication-profile work. Platform rehearsal binds the actual composed host and same immutable inputs. Missing qualification, rehearsal, ratification or accepted handoff keeps parent production eligibility failed.
- A runtime minor upgrade requires affected broker, routing/security, actor persistence/reminder, scheduler/placement, restore and compatibility checks. Atomic append/conflict protection remains a separate required capability and proof; broker success or a version number cannot establish it.
- The optional payload-protection successor depends on qualified runtime inputs and its own Dapr/Kubernetes cryptography specification and real component proof before Server integration. Its future package changes and production authority remain separate.
