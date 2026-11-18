# Epic 2 Context: API and UI Developers Get Safe Integration Surfaces

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Developers can expose typed external REST APIs and build interactive clients through gateway contracts, keeping metadata, tenant scoping, status identity, and projection truth intact. Generated controllers live only in external API hosts; UI hosts use client libraries. Stories 2.14–2.17 are corrective successors that tighten the tenant boundary, status identity, SignalR metadata bounds, and generator build quality.

## Stories

- Story 2.1: REST Contract Seam for Command and Query Messages
- Story 2.2: REST API Generator Discovery and Controller Emission
- Story 2.3: Sample External API Host Proof
- Story 2.4: Tenants REST Contract Metadata and Routes
- Story 2.5: Dedicated External Tenants API Host
- Story 2.6: Tenants UI Client-Library Alignment and UX Evidence
- Story 2.7: Pre-Authorization Registration and Provenance Correction
- Story 2.8: Scoped Metadata-Rich Projection Notifications
- Story 2.9: Generated Command-Status Location Policy
- Story 2.10: Outbound DAPR Routing-Header Ownership
- Story 2.11: Query Provenance Consumption in Generated REST and Tenants
- Story 2.12: Tenants Runtime Identity Adoption and Package-Mode Validation
- Story 2.13: Dapr Notification Distribution Qualification
- Story 2.14: Canonical Tenant Boundary In Generated Controllers And The Tenants Host
- Story 2.15: MessageId-Only Command-Status Identity
- Story 2.16: Bounded SignalR Detail Metadata
- Story 2.17: Generated-Code And Source-Generator Build Quality

## Requirements & Constraints

- **Generated controllers.** OpenAPI-visible, gateway-delegating, forwarding query metadata headers, supporting `304`, returning safe Problem Details. UI hosts have no per-message controllers.
- **Status `Location`.** A `202` may carry an absolute, gateway-authoritative `Location` only when a `MessageId` valid under the endpoint's declared version selects the status resource. An absent, blank, invalid-for-version, or version-ambiguous `MessageId` omits `Location` or rejects safely. It never falls back to `CorrelationId` and never yields a relative URI.
- **Status tests.** Compiled and runtime tests cover v1/v2 positives; undeclared, ambiguous, or caller-selected versions; missing, invalid, or non-canonical v2 values; and a `CorrelationId` that differs from or lacks a `MessageId`.
- **Tenant boundary.** Each request and `eventstore:tenant` grant must resolve one tenant through the shared canonicalizer, before routing, state access, or status lookup.
  - The canonicalizer lowercases, then validates 1–64 characters matching `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`, with no trimming.
  - Missing, duplicate, conflicting, invalid, unauthorized, and `system` values are rejected.
  - Each boundary takes one scope, a tenant or the platform-operation namespace (never both). Grants authorize but never set scope.
- **SignalR detail metadata.** At most 16 entries and 2048 UTF-8 bytes. Keys are opaque, and values are logged only at Debug or below.
- **Generated code.** It builds with zero warnings under warnings-as-errors, uses nullable annotations and `ConfigureAwait(false)`, and never calls `Guid.TryParse` on identifiers.
- **Compatibility.** Every changed public surface needs a compatibility classification, and incompatible changes need an approved SemVer-major proposal.
- **Earlier labels.** The `done` labels on 2.2, 2.4, 2.5, and 2.9 do not satisfy the corrected tenant or status contracts.

## Technical Decisions

- **Query metadata.** Only projection-backed routes assert freshness; the rest report `Unknown`.
- **Status `Location` (from 2.9).** The format is `{gatewayStatusBase}/api/v1/commands/status/{escapedStatusKey}`, built from trusted request-time configuration.
  - An invalid base (relative, non-HTTP(S), user-info, query, or fragment) fails closed.
  - Generated hosts never map status endpoints.
  - Gateway failure emits no `Location`.
- **MessageId grammars.** `MessageId` is the sole status identity. `Contracts` owns two grammars:
  - **v2:** 26 uppercase Crockford ULID characters. `Ulid.TryParse` must succeed and the canonical round-trip must be byte-identical.
  - **v1:** legacy, 1–128 ASCII alphanumeric or hyphen characters with no edge hyphens. It is kept byte-for-byte and accepted only where explicitly declared.
- **Version declaration.** Each command contract declares exactly one version in its `Contracts` declaration, which is the only source. Callers never select or override it.
  - The route entry, generator metadata, and compatibility entry are derived from it mechanically.
  - Each carries the declaration digest: the SHA-256 of its canonical bytes from the Story 5.12 codec.
  - The `netstandard2.0` generator can't call the codec, so hosts compute digests at startup through it.
- **2.15 manifest.** Derived from those declarations, it fails on drift and has no compatibility authority; Story 3.18 later imports and retires it.
- **Correlation.** `X-Correlation-ID` (1–128 alphanumeric/hyphen, owned by `Contracts`) is diagnostic only: minted once, propagated, never parsed as a GUID or ULID, never status identity.

## UX & Interaction Patterns

- **Mutation lifecycle.** Submit → accepted → evidence pending → projection-confirmed or failed. A `202` or SignalR signal is never completion, and UIs time out to pending, never success.

## Cross-Story Dependencies

- **FR12 owners.** C1 belongs to 2.2, C2 to 2.14, and C3/C4 to 2.15. No slice closes FR12 alone.
- **2.9 fallback.** 2.9 stays done for absolute-or-absent `Location`. Its `MessageId ?? CorrelationId` fallback is superseded, and 2.15 removes it from the generator, tests, and docs.
- **2.14 and 2.15 prerequisites.**
  - Accepted Story 5.12 declaration/schema/codec delivery, which is still gated on package/validation.
  - A Story 9.1 authorization record for G-TENANT or G-STATUS-ID.
  - They do not need 5.13 runtime activation. 2.14 also depends on 2.12.
- **Approval level.** High-risk gates approve at the computed Assurance Control level: `single-maintainer-attested`, meaning sealed CI plus owner attestation. Never label it `independent`.
- **Gate validators.** The fixture suite blocks; the live gate reports PASS/FAIL without blocking merges. Use a new workflow file and never edit `.github/workflows/ci.yml`.
