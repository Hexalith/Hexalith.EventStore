# Epic 3 Context: Maintainers Can Release Reproducible, Verifiable Artifacts

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make EventStore builds, tests, packaging, publication and verification reproducible independently of local checkout state. Prove exact package and deployed-runtime lineage, reject invalid candidates, and preserve separate release, production-promotion and consumer-removal authority.

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

- Separate deterministic release tests from dedicated live-sidecar coverage. Assert persisted end-state through Dapr or owning actor boundaries; classify environment blockers separately from product failures.
- Source references require explicit selection and available root-declared sources. Package mode remains the configured default; CI/publication use package references and Release outputs. Builds owns shared dependency/tool/runtime pins; validate coupled updates with representative package consumers.
- Package publication follows one manifest and excludes submodule packages. Public source/binary, generated, wire and package surfaces require compatible baselines; incompatible changes require an approved SemVer-major proposal.
- Containers use immutable OCI indexes with exactly Linux amd64 and arm64 manifests. Failed releases, including `v3.94.1`, remain immutable rejected evidence; a later conforming version supplies correction.
- Proof is repository tests plus CI. Record commands, counts, commit SHA and run link in the owning story/spec. Evidence folders are ignored scratch; committed evidence and files over 1 MiB are rejected outside explicit exceptions. Stories 3.22/3.23 are superseded. Scheduled stories re-specify deleted validators rather than restoring historical evidence machinery.

## Technical Decisions

Publication follows `built` → `evidence-candidate-published` → `evidence-validated` → `release-available` → `production-promoted`. Authenticate registered issuers and bind exact immutable subjects, predecessors, source/artifact identities, validator identity/result, role registry, issuance, expiry, revocation and invalidation. Reject skipped, duplicate, unknown or mismatched records. Subject changes restart the chain. EventStore's qualified server/package subject and Platform's composed image have separate issuers and records; existing receipts never transfer.

Platform supplies complete resolved production-profile bytes; the deployment owner accepts them at the sole `deploy/dapr/production-profile.yaml` slot. Validation hashes retained canonical bytes and rejects absent, partial, unknown or self-only inventories. Bind actual apps, components, scopes/ACLs, actor-state physical targets, immutable runtime, catalogs, issuers, secrets contract, append protection, restore posture and rendered deployment. Templates and development manifests supply no production authority. The production target still needs authenticated Architecture and Platform ratification.

EventStore owns backend-neutral requirements, contracts and application assertions; Platform owns composition, provider configuration, Kubernetes rehearsal and restore; Builds owns shared publishers, validators, catalogs and tools. Application infrastructure uses qualified Dapr interfaces or approved narrow exceptions. Repository AppHosts and provider diagnostics do not establish production conformance. Runtime minor upgrades require affected security, broker, persistence, scheduler/placement, restore and compatibility proof; append protection remains separately qualified.

Guarded transitions consume authenticated dedicated CI runs on the exact subject and workflow bytes, rerunning predecessor validators. Fixture failures block merges; truthful live failures block their guarded transition. Records preserve required/achieved assurance and the lowest input level. Human owner attestations observe the 24-hour subject-change window; tools and CI do not count as human approvers.

## UX & Interaction Patterns

Existing authorized Topology detail may display approved release identity, exact publication stage and assurance as read-only status. Missing verification stays unknown and non-authorizing. Exclude credentials and raw attestations; a tag never proves deployment.

## Cross-Story Dependencies

- Stories 3.13–3.15 separate rejected disposition, corrective publication and bounded runtime parity. Story 3.15 reaches only `evidence-validated`; publication and consumer-removal authority remain separate.
- Story 3.19 can build before issuance prerequisites close. Issuance requires production ratification, 3.15 parity, 5.7 component contents, 9.2 Assurance Control and applicable corrective authorization. Its inputs also include 3.21 runtime/broker qualification, 7.22 restore posture, 5.13 activated catalogs and the pinned Builds `ReleaseEvidenceCodec`; EventStore must not reimplement that codec.
- Story 3.20 consumes promoted exact-profile authority and per-consumer owner receipts. Every root-declared or referenced consumer is accounted for; a proposed removal cannot be `N/A`. Evidence, package availability and story completion confer no removal authority.
