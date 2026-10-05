---
title: Shared Payload-Protection Dapr Amendment Candidate
type: security-architecture-amendment-draft
created: 2026-10-05
updated: 2026-10-05
status: draft-unapproved
decision: unresolved-qualification
implementation_authorized: false
base_authority: _bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md
base_normative_sha256: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e
base_full_file_sha256: 542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82
base_replacement_approval: AR-20260913-01
base_story_8_2_completion_and_8_3_authorization: AR-20260914-01
proposed_sections: ['5', '11', '16', '17.4/PF-01']
context:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-10-05.md
  - _bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md
  - _bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md
---

# Shared Payload-Protection Dapr Amendment Candidate

## Disposition And Unchanged Authority

**DRAFT / UNAPPROVED / NON-AUTHORIZING.** This detached candidate proposes a Dapr-first production key-operation boundary for shared-spec §§5/11/16 and PF-01. The 2026-10-05 policy approval authorizes planning reconciliation and this draft, not a crypto amendment, direct provider exception, runtime implementation, package selection, service provisioning, or repinning evidence. Qualification remains unresolved.

The shared authority remains byte-for-byte unchanged, approved-authorized at normative SHA-256 `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e` and full-file SHA-256 `542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82`. `AR-20260913-01` approves the replacement, and `AR-20260914-01` approves Story 8.2 and authorizes Story 8.3. `AR-20260801-01` is historical evidence for its original bytes, not authority for this candidate. The separate Story 8.1 wrapper is not part of these hash bindings.

Story 8.1/8.2 remain done; Story 8.3 remains in progress under its unchanged frozen intent and existing constructibility approvals, including `AR-20260914-02`. This draft neither changes that isolated core work nor authorizes later stories. Existing predecessors, Security/Operations decisions, package/release authority, readiness blocks, and G5 remain required.

## Proposed Delta To §5 — Package And Application Boundary

Replace the automatic application dependency on `Azure.Identity`, `Azure.Security.KeyVault.Keys`, provider credential/client construction, and direct `CryptographyClient` calls with **qualification before dependency selection**. For each supported operation, use the highest suitable Dapr API/component. A missing .NET convenience method requires checking supported Dapr HTTP/gRPC APIs; it does not automatically justify a direct SDK or raw provider HTTP bypass.

The provider-neutral core continues to own the approved envelope/AAD/codecs/local AES mechanics and generic contracts. The production adapter would depend inward on the engine and use a qualified Dapr client/API. It would consume logical component/key identities; deployment/component profiles would own provider endpoint, authentication, scopes, and identity configuration. Do not introduce Azure packages while suitability is unknown. A separately approved unsupported-operation exception, if necessary, must isolate exact provider code/dependencies/options and cannot become a transitive dependency of Contracts or the core.

No package identity, public registration signature, options contract, version, dependency, or count is selected by this draft. Preserve the current 14-package release manifest and non-packable core. If the qualified design changes the frozen engine/companion identity or 14-to-16 release plan, return to Architecture/Release approval and implement it atomically in Story 8.8 with inventory tests, metadata, SBOM/provenance, and package-only proof. Do not hide an adapter in another package or release a partial set.

Proposed application/component responsibility split, to be made exact in the reviewed amendment:

| Concern | Proposed responsibility | Preserved gate |
| --- | --- | --- |
| Component and logical key identity | Closed application options and adapter contract | Unknown fields/identities fail startup; no arbitrary endpoint, credential, tenant-secret, version alias, or provider connection string. |
| Vault endpoint, managed-identity selection and cloud/profile settings | Operator-owned Dapr component/profile configuration | Deterministic selected runtime identity; no ambient/developer fallback outside approved Development use; exact scopes and no credential disclosure. |
| RSA-HSM type, size, operations, algorithm and current-version freshness | Approved adapter/component contract and startup evidence | Ordinary Premium vault, provider-generated non-exportable RSA-HSM-3072, exactly wrap/unwrap, RSA-OAEP-256, matching identity/version, bounded freshness. |
| Operation timeout, key-version refresh, retry and buffer ownership | Reviewed logical options plus explicit client/sidecar/component contract | Existing bounded budgets, exact-version historical reads, caller cancellation, zeroing, and no nested retry multiplication. |
| Provider-specific application options | Only an evidence-backed separately approved exact-operation exception | Exact paths/owner/evidence/review trigger under PRD §8.4; no global Azure exemption. |

Disabled/default/no-op behavior remains unchanged. Explicit valid Enabled registration and successful startup/readiness are still required; no package/component presence or provider configuration implies enablement. Failures never select plaintext, no-op, LocalDevelopment, an older key version, or a direct SDK fallback.

## Proposed Delta To §11 — Qualify Key Operations And Custody

Before selecting the production adapter, qualify the exact pinned Dapr runtime, client APIs, and `crypto.azure.keyvault` candidate against the frozen key-operation contract. Its existence and an encrypt/decrypt happy path cannot prove suitability. A Dapr Secrets API/store cannot replace a cryptographic key-operation component.

Keep the ordinary Azure Key Vault Premium custody profile, provider-generated non-exportable RSA-HSM-3072 KEK, RSA-OAEP-256 wrapping, exact historical version identity, least-privilege key-scoped runtime identity, separate operator/IaC identity, private endpoint/DNS, public access/trusted-service bypass restrictions, TLS identity, diagnostic sink, soft-delete/purge protection and retention obligations unless a separately reviewed amendment explicitly changes them. Moving authentication into the sidecar does not weaken identity or custody requirements.

For every row below, retain an exact-profile supported/gap/unresolved disposition, API request/response and semantics evidence, reproducible command, observed result, required guarantee, limitation, and owner. No row is qualified by this draft.

| Required operation / guarantee | Qualification evidence required | Current disposition |
| --- | --- | --- |
| Exact-version wrap | Dapr API can wrap exactly 32-byte DEKs using RSA-OAEP-256 with the validated current RSA-HSM-3072 version; response retains exact version/algorithm identity and validated lengths. No alternate envelope. | Unresolved; Story 8.6 |
| Exact-version unwrap | Recorded historical provider-version identity is honored after restart/rotation; missing/disabled versions never silently use current aliases, local RSA, or another provider. | Unresolved; Story 8.6 |
| Strong current-version discovery | Dapr/component API proves current metadata/freshness, exact vault/key/version identity, one enabled/time-valid selection, key type/size/non-exportability, and exactly allowed wrap/unwrap operations; stale selection blocks new wraps. | Unresolved; Story 8.6 |
| Startup capability probe | Profile/TLS/identity validation plus fresh 32-byte wrap/unwrap, fixed-time comparison and buffer cleanup; no retained probe, key creation, or inference of conformance from one pass. | Unresolved; Story 8.6 |
| Identity and least privilege | Component/provider boundary proves deterministic system/user-assigned identity and ambiguity denial, exact approved role/data actions, no administrative/export/secret/certificate privileges, and separate provisioning identity. | Unresolved; Security/Operations and 8.6 |
| App channel, component scopes, network and custody | Authenticated/scoped sidecar access, approved profile identity, private connectivity and TLS, isolated test environment; sidecar/component logs and telemetry meet no-leak requirements. | Unresolved; Security/Operations and 8.6 |
| Cancellation and timeout | Caller cancellation propagates, owned buffers are cleaned, and client/sidecar/component/provider timeout behavior is bounded by the approved total operation budget. | Unresolved; 8.5/8.6 |
| Retry, throttling and breaker budget | Combined layers meet §10.6 exactly, preserve bounded `Retry-After`, permanent-failure rules, cancellation exclusion and open/half-open behavior; no nested retry multiplication. | Unresolved; 8.5/8.6 |
| Constructive typed failure classification | Dapr exposes enough structured status/code/state to distinguish denied/unavailable/missing/deleted/consistency/cancellation outcomes. Preserve token-endpoint versus service failures, versioned 404/lifecycle reconciliation, TLS security failures and ambiguous mutations without message-text inference. | Unresolved; 8.6 |
| Key material, buffer custody and no-leak | DEK input/output ownership and zeroing remain explicit across application and sidecar/component/provider memory/transport. RSA private keys are never exported; no plaintext/key/credential/provider-private body enters evidence, logs or support output. | Unresolved; Security/Test and 8.6 |
| State/lifecycle integration | Dapr state/actor ownership and qualified ETags/transactions/TTL preserve wrapped-record reservation, activation, index/fence/lease/audit, cache invalidation and ambiguous-operation reconciliation. Crypto use does not imply cross-component transactions. | Unresolved; 8.5/8.6/8.7 |
| Historical decryptability and rollback | Real persisted v2 events/snapshots/wrapped-DEKs remain readable across restart/rotation and post-v2 rollback, with exact versions, monotonic watermark/fence, retained legacy readers/writer and no downgrade/plaintext fallback. | Unresolved; 8.6/8.9/8.10 |

If a required operation/guarantee cannot be supplied through supported Dapr APIs, retain reproducible exact-profile gap evidence and alternatives, then return to Architecture/Security for a compatible Dapr design, a separately reviewed format migration, or a narrowly scoped exception. Unfinished investigation, missing SDK helpers, mock success, or credential convenience is not a gap decision. Never select direct provider packages before this disposition.

The application/component options, startup metadata verification, structural typed-failure mapping, retry/timeout behavior, and emitted provider API version must be specified exactly before approval. Do not implement from this candidate's unresolved rows. Real isolated-service conformance remains mandatory; mocks prove classifier logic only.

## Frozen Security And Compatibility Invariants

- Preserve `pdenc-v2` HXP2 envelope, algorithms, 11-field byte-stable AAD, canonical path manifest, bounds, nonces, snapshot carrier and golden fixture identities. **Dapr Crypto Scheme v1 does not replace those bytes.** A format migration requires its own review/approval and historical-read/rollback proof.
- Preserve typed failures and unreadable/opaque/redacted/legacy routing. Missing/denied/unavailable/malformed/tampered state never becomes plaintext, absent/default success, skipped projection input or checkpoint advancement.
- Preserve operator KEK custody, key attributes/versions, lifecycle reservations/fences/indexes/leases, cache invalidation and buffer ownership/zeroing. Component credentials stay deployment-owned and never grant the application key administration.
- Preserve real-service/no-leak conformance, Parties legal/certificate/report ownership and truthfulness, retained legacy provider/DI rollback path, mixed-history reads and rollback after real v2 writes.
- Distinguish online wrapped-DEK invalidation from expiry/destruction of all recoverable replicas/exports/backups/restore/DR copies. Unknown copies remain pending/operator-required; recoverable KEK deletion is not immediate per-subject erasure.
- Preserve enabled fail-start and bounded runtime failure, disabled no-op defaults, release/non-packable boundaries and all readiness/production blocks. No new runtime, topology, submodule, provider resource or fixture/validator mutation occurs here.

## Proposed Delta To PF-01

Replace “select/reverify stable Azure SDK/NuGet and emitted service API versions” with a **Dapr operation/profile qualification and dependency-selection gate** owned by the Story 8.6 implementer with Architecture, Security, Operations and Release.

Retain exact Dapr runtime image, client/API/package versions and hashes, crypto component identity/version/metadata, effective emitted provider service API, component/provider profile, dependency lock/SBOM, scopes/credential contract, operation matrix, commands/results and limitations. Unsupported, preview-only, ambiguous or unobserved guarantees return to amendment/exception review. Only a proven qualified design may select the corresponding centrally governed dependencies; an accepted exception additionally binds exact provider SDK/API versions and paths. This is a proposed new gate, not evidence that the existing frozen PF-01 has been satisfied or rewritten.

## Proposed Delta To §16 — Impact And Reapproval

| Story | Required impact assessment and evidence before affected work | Reapproval / preservation gate |
| --- | --- | --- |
| 8.3 core | Assess public/backend seam and byte-core impact; keep provider-neutral/non-packable local mechanics isolated. | Frozen intent, authority, goldens and constructibility approvals remain unchanged. Any required incompatible core/API change returns to exact-content approval before implementation. Existing isolated authorized work is not rewritten by this draft. |
| 8.4 historical readers | Assess provider-version identity routing and typed unavailable/missing/deleted behavior for all v1/v2/redacted/legacy/snapshot histories. | Approved immediate predecessor and amended-design compatibility evidence; preserve every historical fixture and retained reader. |
| 8.5 policy/key lifecycle | Bind Dapr-backed record/state ownership, capabilities, retry/cancel/cache/zeroing and ambiguous-mutation semantics to the selected crypto boundary. | Reapprove affected backend SPI/options and state/profile evidence; no cross-component transaction assumption. |
| 8.6 production adapter | Complete every key-operation row, exact options/auth/profile and structured failure contract, then implement only the approved qualified design or exact accepted exception. | Both 8.4/8.5 packets must close and explicitly authorize 8.6; renewed Architecture/Security/Operations/Release/Test decisions and separate resource authority. Remains backlog. |
| 8.7 Server integration | Rebind reservation-to-active completion, event flush/snapshot semantics, publish/read/restore fail-closed paths and persistent evidence to the selected adapter. | Approved 8.6 predecessor; no automatic Server/no-op enablement, topology or public-contract changes. |
| 8.8 release | Assess engine/adapter identities/counts, inward graph and selected dependencies, SBOM/provenance and package-only real-path validation. | Atomic release-inventory change only under approved revised package decision and Release authority; current 14 count remains unchanged. |
| 8.9 Parties parity | Prove exact source/package/API identities, policy/erasure equivalence, dual-provider historical reads, v2 writer/snapshot/replay and custody/certificate wording with selected design. | Separate Parties authorization and predecessor approvals; retain legacy provider/DI reader/writer path. |
| 8.10 rollback | Exercise real post-v2 mixed-history writes/restart/publication/projection/admin reads and rollback through the same qualified key-operation profile. | Exact 8.9 predecessor, retained KEK/key records, monotonic watermark/fence, no plaintext/downgrade or mock-only proof. |
| 8.11 G5 | Assemble all source/spec/package/component/profile/provider/fixture identities, exact commands/results, no-leak/custody/compatibility/rollback and limitations. | Every prior affected packet must be mutually consistent and explicitly authorize closure; all named role decisions and existing Assurance Control required. G5 stays `needs-additive-api`/blocked until the final approved packet is available. |

Preserve the existing sequence: 8.3 follows approved 8.2; 8.4/8.5 follow approved 8.3; 8.6 follows both approved parallel predecessors; 8.7, 8.8, 8.9, 8.10 and 8.11 follow their own approved predecessors. A planning/status update supplies none of that authority.

## Approval And Evidence Gates

1. Complete an operation-level impact assessment and qualification report; distinguish supported, observed gap and unresolved results. Resolve the exact proposed options/package/API/profile contract before freezing a review candidate.
2. Freeze a separately identified successor/amendment with exact normative/content digest, source identities, incorporated fixture hashes, section deltas, residual risks and explicit authorization scope under the shared §1.2/§17.5 content-bound rules. Retain the current artifact and packets as historical/current-base evidence; do not overwrite their bytes or repin validators.
3. Obtain explicit decisions for the reviewed exact bytes from Architect, Security Reviewer, EventStore owner, Release owner, Operations owner, Parties maintainer and Test/vector reviewer as required by the existing approval/Assurance Control. Policy approval, earlier receipts, group aliases, silence and status labels are insufficient.
4. Reconcile and reapprove each affected story specification and evidence subject. Existing packets remain valid for their original identities; retained approval never transfers to changed bytes/profile/operations. Reproduce unchanged goldens and rerun affected API, lifecycle, custody, live Dapr, compatibility, no-leak, package-only, Parties and rollback gates.
5. Only the explicit exact-content approval and predecessor chain may authorize affected implementation. Exceptions require a separate exact-operation architecture-owner decision under PRD §8.4 and the 3.17 register/guard; production/provider provisioning and release remain separately authorized.

No qualification, approval, exception, package selection, resource provisioning, amended runtime authorization, G5 availability or production/readiness result is claimed by this draft. Its unresolved rows remain owned by Story 8.6 and the named Architecture/Security/Operations/Release/Test reviewers.
