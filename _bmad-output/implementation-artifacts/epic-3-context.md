# Epic 3 Context: Maintainers Can Release Reproducible, Verifiable Artifacts

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 3 enables maintainers to build, test, package, publish, and verify EventStore independently of local checkout state. It separates deterministic release gates from live infrastructure coverage, makes dependency and release inventory authority explicit, and establishes fail-closed evidence from one exact source and package set through an immutable multi-platform deployed image. Invalid candidates remain preserved but non-authorizing; only a separately authorized corrective release followed by independent parity verification may select a conforming deployed-runtime identity.

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

## Requirements & Constraints

Deterministic release-gate tests and live-DAPR tests run in separate, unfiltered lanes. Live coverage remains visible in a dedicated integration workflow but does not gate semantic release. High-tier validation uses bounded readiness and persisted end-state evidence, not HTTP status or mock calls alone. The DAPR ETag actor timeout is overridable per service instance while retaining its three-second production default and established failure semantics.

Root-declared Hexalith submodules live under `references/`; solution, build, documentation, Aspire, and instruction paths use that layout. The Keycloak-backed Aspire resource uses the stable service-role name `security`, with AppHost, deployment output, fixtures, diagnostics, and topology lookups aligned while Keycloak remains the implementation technology.

Package references are the default in every configuration. Source references require explicit `UseHexalithProjectReferences=true` and an available root-declared source path. All source-owned NuGet versions come from the Hexalith.Builds catalog; consumers define no local authority or fallback. Catalog refreshes require configured-source and compatibility evidence, coherent updates for coupled families, and explicit rollback groups.

Restore and build use the `.slnx`; tests run per project. Release commands assert package mode, exclude submodule projects, and publish exactly the 14 root-owned packages in `tools/release-packages.json`. Validation inspects archive bytes and metadata, rejects inventory drift, and proves package-only consumption. EventStore workflows remain thin callers of shared Hexalith.Builds automation; deferred supply-chain improvements remain explicit, non-authorizing backlog work.

The EventStore container uses .NET SDK container support and publishes as one immutable OCI image index containing exactly `linux/amd64` and `linux/arm64` children. Raw registry bytes, digests, descriptors, platform fields, release-bound provenance labels, and bounded smoke results must agree; any unresolved or mixed lineage blocks the gate. Published tags are immutable, so failed candidates are corrected only by a new semantic version.

`v3.94.1` remains rejected, immutable, non-authorizing evidence and cannot select a deployed identity. A corrective release requires durable one-use pre-publication authority. Positive deployed-runtime parity independently binds one canonical source/package/workflow/OCI/smoke lineage. Planning approval, story completion, tags, prior pass flags, self-declared roles, or evidence from another release authorize neither publication, deployment, nor consumer infrastructure removal.

## Technical Decisions

Release authority is deliberately split: the Builds catalog governs dependency versions, `tools/release-packages.json` governs package inventory, and the SHA-pinned shared publisher/validator governs OCI shape and validation. Release validation defaults to package mode; source mode is never a publication input.

AppHost resources, DAPR identities and options, component and ACL scopes, deployment overlays, and topology tests change as one unit. High-risk validation inspects persisted state, package contents, raw registry objects, immutable digests, and smoke evidence; mutable labels and inherited pass flags are insufficient.

The platform-owned, versioned evidence codec emits the canonical bytes used for hashing; verifiers do not reserialize them. Deployment identity is the validated OCI index digest, mapped only through its recorded manifest/config chain. Positive closure requires content-bound receipts from the authenticated EventStore owner, Release owner, and Test Architect. These establish evidence only; deployment and consumer removal require separate authority.

## Cross-Story Dependencies

Stories 3.1 and 3.3 establish the test-lane and repository-layout foundations. Story 3.2 depends on 3.1; 3.4 on 3.3; 3.5 on 3.3 plus completed Stories 1.20 and 2.12; and 3.6 on 3.5. Stories 3.7-3.8 establish the shared-workflow path; 3.9 records deferred supply-chain work; 3.10 also uses Epic 2's generated API surface; and 3.11 refreshes the catalog from 3.5.

Story 3.12 builds on 3.6 and 3.8. Story 3.13 preserves the negative `v3.94.1` disposition independently of 3.14. Story 3.14 builds on 3.6, 3.8, and 3.12. Story 3.15 depends only on completed Story 1.20 source/package parity and 3.14; it cannot splice in 3.13 evidence. Epic 3 closes only after positive Story 3.15 parity and all other stories complete.
