# Epic 3 Context: Maintainers Can Release Reproducible, Verifiable Artifacts

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make EventStore builds, tests, packaging, publication, and verification reproducible independently of local checkout state. Maintainers must identify the exact package and deployed-runtime lineage, reject invalid candidates without granting authority, and retain trustworthy proof without making source checkouts grow with raw qualification output.

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

- Keep deterministic release tests separate from live-sidecar tests. Live qualification needs dedicated execution, readiness retries, and persisted-state assertions; an infrastructure outage leaves proof unestablished and is distinguished from a product failure.
- Preserve production actor-request timeout defaults while permitting integration overrides. Use the logical Aspire resource name `security` for Keycloak.
- Source references require explicit `UseHexalithProjectReferences=true` and available root-declared sources. Unset or false selects packages in every configuration. Release commands assert package mode and cannot publish dependency-submodule projects.
- The shared Builds catalog owns all source-owned NuGet versions. Refresh compatible families coherently with validation; retained exceptions need reasons, evidence, and removal triggers. Submodules live under `references/`; nested initialization is forbidden.
- Package scope is manifest-governed. Container publication requires an immutable OCI index with exactly `linux/amd64` and `linux/arm64`. Published versions, including rejected releases, remain immutable; correction uses a later conforming version.
- Retain bounded candidate/run/attempt summaries with original failed and interrupted dispositions. Attempt metadata is at most 10 MiB and command excerpts at most 2,048 UTF-8 bytes. Full command output, large history inventories, generated reports, traces, videos, run screenshots, and archives belong in external storage. Source-owned regression baselines remain tracked.
- Diagnostic retention is 30 days; acceptance-relevant bytes need durable immutable retrieval, and active holds preserve required proof. Redact before storing artifacts or excerpts. Missing or mismatched bytes fail their consuming gate.
- Automatic Git admission excludes blobs over 10 MiB and generated-evidence selections over 50 MiB with explicit omission reporting. Splitting archives cannot bypass limits. Historical retirement requires enumerated consumer migration, verified availability, and preservation amendments; it never rewrites sealed subjects or approvals.

## Technical Decisions

Release artifacts and evidence bind immutable source, package, OCI, producer/validator, and receipt identities. The shared SHA-pinned Builds publisher/validator owns OCI validation and `ReleaseEvidenceCodec`; EventStore consumes that implementation instead of duplicating it. Shared security workflows use the mandated shared reference while third-party actions remain pinned through those workflows.

Publication advances through `built`, `evidence-candidate-published`, `evidence-validated`, `release-available`, and `production-promoted`. Evidence validation alone grants no release, deployment, readiness, or consumer-removal authority. Later states need authenticated role-bound predecessor records, exact subjects, validity and revocation checks, and the applicable sealed transition evaluation. A changed subject starts again at `built`.

High-risk proof observes the owning Dapr state/actor boundary; direct broker/backend diagnostics do not substitute. Runtime infrastructure must map to a qualified Dapr API/component or an explicitly accepted, narrowly scoped architecture exception. Repository fixtures establish repository behavior; Platform owns shared-host Kubernetes rehearsal and composed deployment subjects. Production-profile selection remains subject to owner ratification and exact qualification gates.

External evidence uses a versioned manifest binding artifact identity, exact bytes, source/producer, attempt disposition, and retention. Content-dependent readers materialize and verify complete output before parsing; hashes and excerpts alone cannot establish witnesses. Preserve legacy readers for original sealed packets, bounded offline regression fixtures, source seals, and adjacent-credential rejection. Storage migration appends records without repinning historical receipts or manufacturing qualification authority.

## Cross-Story Dependencies

- Layout and dependency-mode work precede manifest packaging; packaging and the deterministic/live split support workflow migration and publishing validation.
- Generated API smoke preflight depends on Epic 2's generated Sample API and complements the dedicated live lane.
- Stories 3.13–3.15 distinguish rejected historical evidence, a corrective candidate, and bounded parity validation; none substitutes for later publication authority.
- Story 3.19 consumes production component, qualified runtime/broker, restore, route/catalog, shared-codec, and assurance inputs. Story 3.20 additionally requires exact per-consumer removal authority.
- Corrective high-risk work requires applicable current-subject Story 9.1 authorization; Stories 9.2 and 9.3 own assurance controls and planning-baseline reconciliation.
- Story 3.22 coordinates admission changes with the Hexalith.AI.Tools owner. Story 3.23 follows migrated readers and verified external availability. Initial retirement excludes OQ8 packets unless their migration is separately proven safe.
