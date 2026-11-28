# Epic 2 Context: API and UI Developers Get Safe Integration Surfaces

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Developers expose typed external REST APIs and interactive clients through gateway contracts while keeping metadata, tenant scoping, status identity, conditional caching, error privacy, and projection truth intact. Generated controllers live only in external API hosts; UI hosts consume client libraries directly. Corrective stories tighten tenant boundaries, status links, SignalR bounds, build quality, conditional validators, and query failure redaction.

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
- Story 2.18: Bind Conditional Queries To The Returned Representation
- Story 2.19: Classify And Redact Query Adapter Failures

## Requirements & Constraints

- **Generated controllers.** OpenAPI-visible, delegating only to `IEventStoreGatewayClient`, forwarding bounded query metadata, supporting `304`, and returning safe Problem Details. UI hosts expose no per-message controllers.
- **Tenant boundary.** Generated controllers, Tenants API, gateway `ClaimsTenantValidator`, and Admin Server tenant checks canonicalize every request tenant and `eventstore:tenant` grant via the shared `Contracts` canonicalizer before routing, state access, or status lookup: lowercase without trimming, validate `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` (1–64 chars), reject missing, duplicate, conflicting, invalid, unauthorized, or `system` values. Each route takes one scope (tenant or cataloged platform namespace, never both); grants authorize, never select scope.
- **Command-status `Location`.** Accepted commands always return `202` with `Retry-After: 1`. Emit an absolute gateway-authoritative `Location` only for a valid gateway base and a `MessageId` valid under the endpoint's declared version; otherwise omit `Location` (never reject after acceptance, never substitute `CorrelationId`, never emit a relative URI).
- **Conditional queries (ETags).** Emit an ETag in headers or body metadata only when bound by the producer to the selected representation—never from a later lookup. Untagged reads return `200` with no ETag even after projection advances, preserving paging and provenance. Return `304` only after auth and routing on a matching eligible producer-bound strong ETag; changed representations return `200`.
- **Query failure privacy.** Map recognized adapter reason codes to documented status, problem type, and safe detail, and legacy sentinels via an explicit allowlist. Never classify by substrings; unknown adapter text returns `500` with generic detail and correlation ID, keeping raw text out of Problem Details and structured logs.
- **SignalR metadata & build quality.** Detail metadata is additive to signal-only compatibility, capped at 16 entries and 2,048 UTF-8 bytes (`Contracts` ceilings; options may only lower), with opaque keys and values logged only at Debug or below. Generated code and generator packages build with zero warnings under warnings-as-errors, nullable annotations, `ConfigureAwait(false)`, and no `Guid.TryParse` on identifiers. Changed public surfaces require compatibility classification; incompatible changes require an approved SemVer-major proposal.

## Technical Decisions

- **Query provenance & lifecycle.** Only persisted projection-backed routes assert freshness (`Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`); other/missing provenance degrades to `Unknown` without projection headers. `ProjectionVersion` is an opaque equality token. A `304` never inherits prior `Current` without explicit lifecycle evidence.
- **Status URI & MessageId grammars.** `{gatewayStatusBase}/api/v1/commands/status/{escapedStatusKey}` from trusted config; invalid bases (relative, non-HTTP(S), user-info, query, fragment) omit `Location`. Generated hosts never map status endpoints; failures omit `Location`. `Contracts` owns **v2** (26-char uppercase Crockford ULID passing `Ulid.TryParse` with byte-identical round-trip) and **v1** (legacy 1–128 ASCII alphanumeric/hyphen, no edge hyphens, byte-for-byte only where declared).
- **Version manifest & correlation.** Each command declares one version in `Contracts`; callers cannot override it. `deploy/dapr/eventstore-routing-catalog.json` is the sole drift-checked runtime manifest, carrying SHA-256 declaration digests from the `Contracts` codec (computed at host startup because the `netstandard2.0` generator cannot call it). `X-Correlation-ID` (1–128 alphanumeric/hyphen) is diagnostic only—minted once, propagated, never GUID/ULID-parsed, never used for status links.
- **Platform scope & outbound DAPR.** Platform routes use a disjoint cataloged namespace with human-bearer operation claims and never forward `system`; signed platform-scoped workload commands also verify the forwarded human bearer (`sub == PrincipalId`) and `RequiredHumanPermission`. Existing `system` usage migrates without editing persisted events. `AddEventStoreDaprServiceInvocation` (last/innermost) replaces—never appends—`dapr-app-id`, `dapr-api-token`, and workload-assertion headers.
- **Notifications.** Signals are best-effort, unordered, at-least-once hints; the opt-in Redis SignalR backplane holds a scoped exception granting no production multi-replica freshness claim.

## UX & Interaction Patterns

- **Mutation lifecycle.** Submit → `202` accepted → evidence pending → projection-confirmed or failed. Neither `202` nor SignalR proves completion; timeouts resolve to pending.
- **Evidence-gated actions.** Show lifecycle distinctly from freshness thresholds using non-color-only indicators; enable high-impact mutations only on projection-confirmed `Current` and fail closed otherwise. Reconnect/rejoin fetches authoritative typed evidence.

## Cross-Story Dependencies

- **Status links.** Story 2.9 stays done for absolute-or-absent `Location`; Story 2.15 removes its `MessageId ?? CorrelationId` fallback. Gateway direct `GET` keeps bounded legacy correlation lookup pending a separate compatibility decision.
- **Tenant boundary.** Story 2.14 builds on 2.12 and completed 5.12 catalog declarations (no 5.13 activation needed); 5.10 keeps `system` provisioning rejection, 5.2 keeps Admin auth/filters (2.14 swaps tenant comparison), and 5.15 owns Admin storage key isolation.
- **Query metadata & failures.** Stories 2.18–2.19 build on 1.2 and 2.11; 2.18 replaces post-read ETag lookup tests while keeping 2.11 provenance rules.
- **Labels & inventory.** Prior `done` labels on 2.2, 2.4, 2.5, and 2.9 do not satisfy 2.14/2.15. Story 3.18 imports 2.15 compatibility classifications into the release inventory.
- **Assurance & workflows.** Corrective authorizations live in the owning spec. High-risk gates require sealed CI runs plus owner attestation (`single-maintainer-attested`, never `independent`), with Story 9.2 wiring verifiers and 2.14 supplying its completion adapter. Proof is tests plus CI runs (never commit `evidence/` folders); route workflow changes through `Hexalith.CiCd`.
