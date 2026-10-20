# Epic 3 Context: Maintainers Can Release Reproducible, Verifiable Artifacts

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make EventStore builds, tests, packaging, publication, and runtime verification reproducible independently of local checkout state. Preserve rejected candidates as non-authorizing evidence, prove exact package and deployed-runtime lineage, and keep evidence validation distinct from release, production promotion, and consumer-removal authority.

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

## Requirements & Constraints

Separate deterministic release gates from dedicated live-sidecar coverage. Higher-tier proof must exercise production paths and persisted end states through the owning Dapr state or actor boundary; HTTP success, mocks, direct-backend diagnostics, and environment failures establish no correctness claim. Use bounded readiness and distinguish infrastructure blockers from product failures.

Package mode is the default in every configuration; source intent requires explicit opt-in and an available root-declared source. Hexalith.Builds owns source dependency versions; compatible coupled families move together with restore, build, test, and representative-consumer evidence. Use `.slnx` for restore/build and run tests per project. Initialize only root-declared submodules, preserve unrelated work, and never silently refresh release-bound evidence.

Release packaging is manifest-driven and excludes submodule packages. Public source/binary APIs, generated surfaces, wire contracts, and package assets need one compatibility inventory and passing baselines; incompatible changes require an approved SemVer-major proposal. Package additions and retirements require an atomic inventory transition. Deferred supply-chain work conveys no implementation authority.

Production qualification binds exact runtime, CLI compatibility, placement, scheduler, component, and backend identities. The runtime floor is `1.18.3`; mutable runtime identities and lower production pins fail closed. Qualify broker redelivery after subscriber failure, dead-letter handling, retention, CloudEvent identity, scopes, and secret references; unknown guarantees remain unresolved. Sealed OQ8 workflow inputs may change only through their planned reseal; otherwise use a new qualification workflow. Guide drift is reported separately, and Builds retains CLI/catalog ownership.

Passing qualification never selects a production broker or runtime. Selection requires an authenticated owner ratification or approved replacement. Production traffic, promotion, readiness, and consumer migration remain blocked while required authority, profile, operating-envelope, restore, security, or compatibility evidence is missing.

## Technical Decisions

Dapr is the runtime infrastructure boundary. Each operation maps to a qualified API/component and exact supported profile, or an architecture-owner-accepted exception with narrowly isolated purpose and paths. Provider credentials and clients never enter application code for operations Dapr supplies. Configuration, provisioning, fault injection, and diagnostics are distinct from application runtime authority. Changes to bound runtime/component/catalog versions require requalification; a runtime minor change also requires append-envelope re-proof.

The production target awaiting ratification is self-managed Kubernetes with per-app sidecars, a separate PostgreSQL v1 actor-state component per actor-hosting app, a durable broker, production resiliency, OpenBao, and the qualified PostgreSQL idempotency profile. Redis remains Development/test only. Production components use `secretKeyRef` and OpenBao with default-deny scopes; Kubernetes Secrets are limited to documented bootstrap exceptions. Shared physical infrastructure creates no transaction across state components, actors, or pub/sub.

The canonical production-profile slot is `deploy/dapr/production-profile.yaml`. Its digest binds exact immutable runtime identity, CLI compatibility, scheduler/placement, component physical targets and app scopes, ACLs, API tokens, resiliency, secret contract, route/idempotency catalogs, infrastructure inventory, writer envelope, replica posture, restore posture, and evidence. Qualification inputs cannot silently become that authorizing profile.

Container publication uses .NET SDK support and an immutable OCI index with exactly `linux/amd64` and `linux/arm64` children. SHA-pinned shared publishing/validation owns raw-byte lineage and provenance. Published tags, including rejected candidates, are never repointed or deleted; corrections use a new semantic version.

An unchanged subject advances through `built`, `evidence-candidate-published`, `evidence-validated`, `release-available`, and `production-promoted`. Each authority transition binds authenticated role, predecessor, subject, evidence, validity, and profile/deployment identity where applicable. Evidence validation grants no later authority. Consumer removal additionally requires its own complete manifest and consumer-owner receipt. High-risk transitions require sealed CI validation and the assurance level supported by the human-owner registry; tool personas confer no approval identity. Missing owner records produce truthful FAIL results, without turning non-authorizing live evaluations into release permission.

## Cross-Story Dependencies

The test split and references layout underpin dependency modes, manifest packaging, and shared workflows. Corrective OCI publication feeds independent runtime parity validation; the rejected historical candidate never supplies positive replacement lineage. Catalog maintenance, infrastructure-boundary enforcement, and public compatibility remain separate authorities.

Infrastructure inventory precedes runtime/broker binding. Production-profile issuance also needs production component parity, activated route/idempotency catalogs, scheduler-inclusive restore proof, the shared release-evidence codec, append-envelope proof, and owner ratification. Runtime/broker evidence feeds component parity and publication authority; those records feed consumer-removal authority. Epic 9 owns corrective authorization and sealed high-risk controls. Notification distribution and payload-key qualification coordinate with the infrastructure inventory without creating prerequisite cycles.

## Shared Platform Host Reconciliation (approved 2026-10-20)

The user approved `planning-artifacts/sprint-change-proposal-2026-10-20.md` and the ES-DIAG frozen intent. The exact-subject conversation planning receipt is `evidence/story-3-21-platform-host-reconciliation-2026-10-20/approval/approval.json`; it grants no AD-26, publication or production authority. A valid current-subject Story 9.1 authorization remains a build preflight.

EventStore Story 3.21 owns local Dapr/component qualification, failed guarantees and accepted inventory/component/profile-input handoffs. Platform owns shared composition, runner-owned environment lifecycle, disposable self-hosted Kubernetes rehearsal and the later exact deployment/restore bundle; Builds owns catalog, CLI, control-plane and embedded tool pins. Repository AppHosts/fixtures establish repository evidence only. Platform tests consume its environment descriptor without launching another host.

ES-DIAG is a separate bounded diagnostic slice: exclusively allocated local Unix-socket Docker worker, full unrelated-container preservation, both rotation failures and RabbitMQ backpressure/missing bindings, all 24 observations, exact packet inventory and independent Dapr persisted readback. Integrity-valid rejected observations may complete that slice while qualification fails and parent 3.21 stays in progress. Existing packets, including pre-existing unlisted bytecode and rejected preservation runs, remain unchanged.

Restore design/drills consume candidate scheduler/placement evidence before parent completion; selected parity and final append-envelope proof use the ratified profile. Upstream qualification never waits for Story 3.19 to issue records that consume it. The sole canonical resolved profile remains `deploy/dapr/production-profile.yaml`. EventStore server/package and Platform-composed images have separate subjects and registered issuers; old records never transfer. No broker is selected automatically and no sealed input or planning digest is silently repinned.
