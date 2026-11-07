---
title: 'Story 8.7 on-premises backend amendment candidate'
type: 'architecture-amendment-candidate'
created: '2026-11-07'
status: 'approved-authorized'
implementation_authorized: true
execution_disposition: 'approved-not-started'
approval_date: '2026-11-07'
approval_source: 'Interactive project owner: approve and stop'
approval_assurance: 'single-maintainer-attested'
selected_custody_model: 'openbao-software-rsa3072'
custody_model_decision: 'accepted-for-qualification'
custody_model_decision_date: '2026-11-07'
candidate_normative_sha256: '43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3'
proposed_effective_authority_sha256: '835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180'
planning_finalization_revision: '12279c2429ddd9b0f044a0608fcd928fae6327fa'
eventstore_revision: '6ef8dcd4fc75e9bf20e403f2baf326e882a05d69'
platform_recorded_gitlink: 'ed991837945571a12743214fc18a87fbd323fc30'
platform_observed_revision: '019f593544ad157036b7d12173ff89a16464f3cc'
---

# Proposed on-premises payload-key backend

The owner selected option 2, “On-premises backend — revise the Azure dependency with Hexalith.Platform,” on 2026-11-07. The owner subsequently selected option 1, “Qualify OpenBao software keys; accept changing the hardware-custody requirement,” then approved the finalized specification and supplement with “approve and stop.” The software-key policy and implementation-plan decisions are recorded below. No infrastructure changes were authorized. Epic 8 remains post-MVP unless separately rescheduled.

## Recorded custody decision

The original approved profile requires non-exportable **hardware-protected** RSA-3072 keys. The owner accepted qualifying **software-managed OpenBao keys** for the new on-premises path, including the broader recovery boundary of an OpenBao host/storage administrator. This decision does not attest a deployed provider, hardware equivalence or completed conformance.

The following complete supplement is approved for the scoped EventStore work on a later resume. Its exact normative bytes remain unchanged by recording approval.

<!-- HX-PP-ONPREM-CANDIDATE-BEGIN -->

## 1. Scope and dependency replacement

Replace only the Azure-specific production prerequisite for the on-premises Story 8.7 path with a proposed **Platform/EventStore on-premises backend contract and real-service conformance prerequisite**. Do not mark Azure Story 8.6 complete or reuse its sprint key. Its adapter, approved exception and evidence remain retained and deferred. The new prerequisite and the existing capacity/index/erasure-memory work must complete before shared-engine host wiring.

Preserve Story 8.1's normative bytes, v2 envelope/AAD/goldens and Story 8.4 compatibility. Preserve Story 8.5 lifecycle safety; bind the new profile to its own approval identity. Approval must bind this supplement's final digest. Its precedence is limited to the following:

| Existing subject | On-premises supplement |
|---|---|
| Base 5.1 package graph | Put the neutral Dapr backend in the existing core; no Platform/provider assembly dependency or third adapter package. |
| Base 5.2/5.3 closed configuration and mode matrix | Add only explicit `PlatformCustody` backend selection and the closed subsection below. Existing Disabled, LocalDevelopment, Azure and write-mode rules remain. |
| Base 11.1–11.3, Azure options/probe and custody | Apply this software RSA/OpenBao/Dapr profile only when PlatformCustody is selected. Azure selection retains its existing approved profile and exception. |
| Base 11.4–11.6 and backend-specific 15.x vectors | Replace Azure resources, workload identity, native failures and custody observations with exact on-premises equivalents; retain failure, confidentiality and persisted-evidence obligations. |
| Base 10/13 lifecycle, fleet, restore and rollback | Retain monotonic state and all safety rules; new authorization must bind this supplement. No automatic migration or rollback of predecessor authority. |
| Base 16 and Epic 8 successor gates | Qualified on-premises conformance replaces the Azure prerequisite for this path only. Azure 8.6 remains incomplete/deferred; release/G5 and capacity gates remain. |

Let `E` be the predecessor effective lifecycle digest `eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9` and `P` this supplement's normative digest. The proposed on-premises authority is SHA-256 of ASCII `HXPA`, byte `02`, decoded 32-byte `E`, then decoded 32-byte `P`. The final approval must bind both `P` and this derived digest. Do not stamp existing records with it or claim predecessor approval covers the new profile. Existing generic-authority state requires a separately approved generation transition; until then, admission fails closed. Existing interaction histories retain their own authority and read behavior.

Represent selection with an internal closed `PayloadProtectionAuthorityProfile`: preserve the predecessor preset for existing/Azure paths; only explicit approved PlatformCustody selection uses the new digest. Thread that profile through the state codec, lease/deployment/reservation coordinators and reconciler instead of globally replacing `PayloadProtectionStateKeys.AuthorityDigest`. Configuration cannot supply an arbitrary authority digest. Refuse a root already bound to an incompatible authority; do not silently rewrite or migrate it.

## 2. Ownership and transport

EventStore owns the provider-neutral engine, occurrence identity, key-record lifecycle, typed failure mapping and Server persistence. Platform owns the on-premises infrastructure component, provider identity, credentials, TLS, network, key administration, custody, backup/restore and composed deployment at `192.168.1.30`.

Add a provider-neutral Dapr binding backend inside the existing engine/core sources, using their existing `Dapr.Client` dependency. Propose the explicit configuration choice `Backend=PlatformCustody`. Provider endpoints, native credentials and provider libraries remain inside Platform's infrastructure boundary. EventStore resolves one installed logical binding and installation/profile identity; there is no direct HTTP/OpenBao fallback.

The only fields in `EventStore:PayloadProtection:PlatformCustody` are `BindingName` (1–128 ASCII lowercase letters/digits/hyphens, matching the installed binding), `InstallationId` (1–128 canonical ASCII letters/digits/dot/underscore/hyphen), and `ProfileSha256` (64 lowercase hex characters). All are required when this backend is enabled. Unknown fields fail validation; none carries credentials or a provider address. Omitted/Disabled registration never resolves or contacts this backend. Enabled mode requires the real installed binding, qualified profile, shared PF-02 state namespace and current fleet/fence authority, including in Development.

Extend compatible Platform custody transport/authority primitives rather than creating a new module server, CLI or duplicate custody system. Existing interaction-specific DTOs must not be reinterpreted as generic backend responses. Define the smallest versioned neutral contract in EventStore Contracts if a shared wire contract is needed; Platform depends inward on it. Do not create a dependency from EventStore into Platform assemblies or a third adapter NuGet package.

## 3. Proposed software-custody profile

Use a dedicated, provider-generated OpenBao Transit **rsa-3072** KEK with remote RSA-OAEP using SHA-256/MGF1-SHA-256. Require `exportable=false`, `allow_plaintext_backup=false`, imported/derived/convergent keys rejected, and runtime grants limited to exact existing-key metadata/encrypt/decrypt operations. No runtime create/upsert, export, backup, restore, rotation, trim, delete or signing rights. Provisioning is a separate identity and task.

OpenBao **2.6.2** is the candidate version present in Platform's prepared assets, not a deployment or newest-release claim. Qualify the exact source, image digest, policy and API behavior before selection. Use a dedicated recoverable payload-key custody profile with retained historical versions. Do not repurpose ordinary application secrets or the existing derived AES erasure-root keys.

Do not adopt the prepared erasure-root store's `DurabilityPolicy.None` or no-backup policy. Platform must qualify backup membership, restoration, private trust, identity renewal and retained-key obligations for this separate profile. Native key export flags do not prove that storage backups, administrators or process memory cannot recover a software KEK. Online invalidation is not proof of all-copy destruction.

## 4. Operations and persisted identities

Add versioned request/response/profile contracts under `Hexalith.EventStore.Contracts/Security`, with one type per file and a source-generated JSON context. The closed operation set is `Profile`, `Wrap`, `WrapExact`, `Unwrap`, `Lookup`. Requests bind protocol version 1, canonical ULID operation/request IDs, installed identity/profile digest, optional exact provider version (at most 1,024 UTF-8 bytes), and operation-specific owned bytes. Requests/responses are bounded to 16 KiB decoded JSON; wrap inputs/unwrap outputs are exactly 32 bytes, wrapped RSA bytes exactly 384. Reject extra fields, unknown operations/outcomes, mismatched request/installation/profile identities and unexpected byte fields before consuming output. Profile/Lookup carry no key bytes; WrapExact/Unwrap require an exact version; Wrap selects its version through the fresh Profile operation.

Responses bind the same request and the SHA-256 of its exact encoded bytes, carry only the seven existing backend outcome equivalents, optional exact version/owned bytes, and bounded retry delay. A successful Profile response additionally states software RSA-3072, export/backup/import/derivation/convergence flags, the current exact version and supported operations. It is a fresh authenticated observation, never an authority grant or cached readiness claim. Request/response formatting must not expose byte or provider fields.

Reuse Platform's installed current-authorization/transport metadata and admission primitives; do not invent substitute signatures or treat transport receipt as native completion. Validate provider/key identity and current lifecycle/fleet authority before native admission. Caller-provided provider paths never select a key.

New wraps first select a fresh qualified current version, then send that explicit version. Rewrap sends its retained exact target; historical unwrap uses only the recorded version. Rotation races may fail closed and never redirect to another version.

Keep wrapped-key bytes within the existing 512-byte bound. For RSA-3072, validate a canonical native versioned carrier, decode its 384-byte RSA ciphertext and retain that plus an opaque exact installation/key/version identity in key state. Reconstruct the native carrier only from this validated pair. Native framing/version disagreement is a consistency failure. No provider URI, version or ciphertext enters public metadata or immutable v2 AAD.

## 5. Ambiguity, budgets and diagnostics

Reuse the existing closed backend outcomes. A denied/missing key or an absent local cache/journal entry cannot prove that a wrap never occurred. Native response loss remains `Ambiguous` unless durable exact-original evidence resolves it. Do not fabricate native idempotency. Lookup never resends a mutation or returns a previously delivered plaintext key.

Authenticated explicit native authorization refusal maps to `Denied`. Fresh exact-key/version metadata can establish `Missing`; it never proves operation absence. Validated lifecycle evidence alone establishes `Invalidated`. Profile/carrier/response disagreement maps to `ConsistencyMismatch`. `Unavailable` for a mutation requires evidence that no native effect was sent/completed; all other uncertain mutation transport/server failures remain `Ambiguous`. Never classify by provider error-message text. Unrecognized read failures return bounded unavailability with no plaintext result. Retry delays remain subordinate to the existing total budget.

Use one enclosing operation budget across engine, Dapr, component and native service, with no multiplied automatic retries or redirects. Confirm cancellation and actual I/O/buffer completion before releasing sensitive ownership. Clear owned key/request/response buffers; qualify remaining Dapr/runtime/native copies and diagnostic channels. Provider errors, endpoints, ciphertext and keys never become public errors, logs or metrics.

## 6. Qualification and transition gates

Before accepting this replacement prerequisite, run an isolated real OpenBao + real Dapr lane. Assert persisted wrapped-key records, exact provider versions, fences, leases and lifecycle state. Cover rotation with historical unwrap, exact-target rewrap, missing/denied/sealed/unavailable service, restart, response loss, budget/cancellation, tenant substitution, restore monotonicity and diagnostic sentinels. Test mocks establish classification only. Native RSA compatibility alone cannot establish custody or actor persistence.

After backend and capacity qualification, Story 8.7 retains its event-batch atomicity, snapshot, completion/reconciliation, every-read-path and rollback tests with real persisted backend evidence. Disabled/omitted registration retains exactly one no-op and contacts no provider. No enabled failure downgrades to plaintext, LocalDevelopment, Azure or another component.

Keep the 14-package release manifest and both existing future projects non-packable. Story 8.8 must separately reconcile its previous engine/Azure pair-release requirement with an on-premises release path before publication. No production deployment, key mutation, Parties migration or G5 closure follows from this proposal.

## 7. Implementation sequence and ownership

1. EventStore: add `PayloadKeyBackendRequest.cs`, `PayloadKeyBackendResponse.cs`, `PayloadKeyBackendProfile.cs`, operation/outcome enums and JSON context under Contracts/Security; add strict codecs, `DaprPayloadProtectionBackend.cs` and closed options/validation under the existing core. Verify neutral transport, no-op preservation, bounds, identity checks, cancellation and safe diagnostics before any host wiring.
2. Platform-owned prerequisite: extend `infrastructure/Hexalith.Platform.Custody.Dapr/BindingService.cs` and add a dedicated RSA payload-key implementation beside `OpenBaoNativeOperations.cs`, preserving existing operations. Qualify the new contract under `infrastructure/Hexalith.Platform.Custody.Dapr.Tests/`. This is an owning-repository handoff; approval of EventStore work alone does not authorize Platform edits, provisioning or live cluster use.
3. EventStore: extend the isolated real-Dapr lane with the approved component and dedicated disposable OpenBao profile. Record commands/results and source/profile identities in the owning spec and CI artifacts. Production network/backup/custody acceptance still belongs to Platform; local containers cannot attest the target cluster.
4. Complete the existing capacity/index/erasure-memory prerequisite under its own approved authority. Only then generalize Server's occurrence/completion hooks and run the full persistence/read/rollback matrix. Retain the closed shared namespace, host-incarnation, restore-identity and legacy-history obligations already recorded in deferred work.

Specification approval can start the independent EventStore contract/backend phase. Backend qualification, capacity completion and separately scoped Platform work remain explicit conditions before Server host wiring. Do not mark Story 8.7 done after only this first phase.

### Verification commands after implementation

Run these commands from the EventStore root, with source-mode Debug assets. Each built test assembly runs independently. Add contract coverage in `tests/Hexalith.EventStore.Contracts.Tests/Security/PayloadKeyBackendContractTests.cs` and transport/mode coverage in `tests/Hexalith.EventStore.PayloadProtection.Tests/DaprPayloadProtectionBackendTests.cs`; run the full owning unit projects, preserving all existing coverage.

```bash
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll
dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll
node scripts/payload-protection/verify-golden-vectors.mjs
python3 scripts/payload-protection/verify-golden-vectors.py
```

After the Platform prerequisite is available, add `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/OpenBaoBackendConformanceTests.cs`, using a dedicated disposable OpenBao store and real Dapr component; require all selected conformance cases to execute with zero skips. After capacity/backend acceptance, rebuild and run Server.Tests and Server.LiveSidecar.Tests individually, including existing persistence/snapshot/readability regressions and the new integration cases. Record exact live commands and qualified component identities when the component exists; no simulated command or current success is asserted here.

<!-- HX-PP-ONPREM-CANDIDATE-END -->

## Existing Platform seams and limits

- [Production composition](../../references/Hexalith.Platform/src/Hexalith.Platform.Protection/Fr34ProductionComposition.cs) and [occurrence key provider](../../references/Hexalith.Platform/src/Hexalith.Platform.Protection/ErasureRootOccurrenceKeyProvider.cs) connect Platform custody to the separate interaction engine.
- [Dapr custody invoker](../../references/Hexalith.Platform/src/Hexalith.Platform.Secrets.Runtime/CustodyBindingDaprInvoker.cs) supplies existing logical-binding/current-authority/transport patterns; it has no default production binding.
- [Native OpenBao component](../../references/Hexalith.Platform/infrastructure/Hexalith.Platform.Custody.Dapr/OpenBaoNativeOperations.cs) shows the provider boundary. Its AES-specific operations are not the proposed RSA backend.
- [Prepared erasure-root deployment](../../references/Hexalith.Platform/deploy/erasure-root/README.md) is unapplied and has an explicit all-copy blocker. Its AES/no-backup profile is not adopted here.
- [Staging foundation](../../references/Hexalith.Platform/eng/staging-foundation/README.md) and [staging secrets preparation](../../references/Hexalith.Platform/eng/staging-secrets/APPROVAL-PACKAGE.md) retain their own missing inputs and execution gates. The target address does not establish readiness.

The Platform revision above is the observed checkout, distinct from the EventStore-recorded gitlink. Neither was changed or updated for this investigation. Future conformance must bind one selected clean source graph; these observations are not deployed evidence.

## Primary-source checks

OpenBao documents RSA-3072 and OAEP/SHA-256 support. This supports investigating the proposed algorithm, not its custody qualification. [Transit engine](https://openbao.org/docs/secrets/transit/).

The 2.6.2 source exposes explicit encryption `key_version` and version-bearing ciphertext responses; the adapter must qualify the exact carrier rather than assuming interchangeability with Azure. [Encryption API source](https://github.com/openbao/openbao/blob/v2.6.2/builtin/logical/transit/path_encrypt.go), [decryption API source](https://github.com/openbao/openbao/blob/v2.6.2/builtin/logical/transit/path_decrypt.go).

PKCS#11 auto-unseal protects OpenBao's root-key unsealing mechanism. It does not by itself establish that each Transit private-key operation remains inside hardware. [Seal mechanism](https://openbao.org/docs/next/configuration/seal/pkcs11/). No development/next-only feature is selected by this candidate.

## Planning validation — 2026-11-07

Document links and context paths resolve; draft/pending states and marker uniqueness pass. Whitespace checks pass for both planning files. The unchanged base normative digest is `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`. The candidate digest above covers exact UTF-8/LF bytes between its begin/end marker lines, excluding those lines. No runtime test, CI, cluster access or remote mutation ran. Concurrent changes outside these two documents were preserved.

Self-review: product choices are resolved. The proposed first phase changes local EventStore Contracts/core code, tests and planning documents; it introduces public neutral wire DTOs and an internal Dapr backend/authority profile. No migration, data deletion, external key operation, Platform edit or deployment is authorized by that phase. Provider/capacity qualifications are explicit later dependencies, not inferred successes. The main build spec stays `draft` until Checkpoint 1 approval.

## Checkpoint 1 approval — 2026-11-07

After the finalized spec and complete supplement were presented, the interactive project owner answered **“approve and stop.”** This approval binds normative SHA-256 `43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3` and proposed effective authority SHA-256 `835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180`. The associated spec's frozen-intent SHA-256 is `da45c41e7994a306295cb5bf1f3d0d6d55fb0861c5d72156173a5940ae316f55`; approval was recorded at EventStore revision `18011dc30de568d865458d85bfa8ff762c5ea258`.

The spec is now `ready-for-dev`; implementation is authorized only within the supplement's existing scope on a later explicit resume. This run stops before implementation. Platform edits, provisioning, live cluster/key operations, capacity-authority changes, conformance closure and Server host wiring retain their stated separate conditions. This is one interactive owner's decision, not independent role reviews or production acceptance.
