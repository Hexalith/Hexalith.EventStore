---
title: 'Story 8.6 scoped Azure SDK exception and adapter design'
type: 'security-architecture-amendment-candidate'
created: '2026-11-03'
status: 'approved-authorized'
implementation_authorized: true
candidate_normative_sha256: 'fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394'
approval_date: '2026-11-03'
approval_source: 'Interactive project owner: approve and stop'
assurance: 'single-maintainer-attested'
exception_id: 'payload-production-key-operations'
story_key: '8-6-azure-key-vault-production-adapter-conformance'
route_decision: 'Interactive user selected scoped Azure SDK exception'
---

# Story 8.6 Scoped Azure SDK Exception Candidate

The interactive project owner approved this exact content on 2026-11-03 and requested a stop before implementation. The approval record below governs local implementation on resume. The October Dapr amendment remains unapproved and its bytes are preserved.

<!-- HX-PP-AZURE-EXCEPTION-BEGIN -->

## 1. Authority and scope

This supplementary design applies only to Story 8.6's companion adapter. It preserves base normative SHA-256 `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`, base full-file SHA-256 `542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82`, approved lifecycle amendment SHA-256 `a853201a6df1e8e88730a40a582b87e20c5e5cf7a223f01ca905e9c9ee7df94d`, and effective engine authority `eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9`.

The owning source baseline is `45d29a03d8be155d2251251a5bc6ba42035b672d`. Existing 8.4 compatibility and 8.5 lifecycle evidence may be accepted as predecessors for additive companion development only after explicit owner disposition and subsequent affected regression checks. This candidate does not relabel their local results as CI or Azure conformance. No engine wire/state schema, approved-spec digest field, fixture, policy contract or public Contracts signature changes.

The exception narrows base sections 5/11/16 and PF-01 to the exact operation/path/package profile below under PRD 8.4 and AD-34. It permits Azure SDK metadata access and fixed remote wrap/unwrap requests because the investigated Dapr profile lacks required guarantees. It does not approve the broader October draft, other provider operations, Server activation, publication, deployment, Parties mutation or G5.

Incorporated planning identities (SHA-256, except the exact Builds Git object):

| Subject | Identity |
|---|---|
| Owning 8.6 frozen block (including tag lines) | `7fe1ee3af25c7199c16a510fc9e5cac210581fe94ef5030a364c04525c86cbff` |
| 8.4 predecessor specification | `1d82b6509b4ce24275988b2aa6251e56e3ad5d8302d7a81fa1caadf1fbca778b` |
| 8.5 predecessor specification | `82a257ca8b52dbe8a86f97dfc53280c389104444a112264e03e86937221e165c` |
| Qualification assessment | `c8c39f39ee4aa7d7991d68da0ee40bff6a8429ad5a79ba2477b20ee48e2ccb72` |
| Current Builds central catalog | `bade7f129df947377a7527c31f2fdc335e310700924c6acf2c88f79ea53fd1a1` |
| Builds gitlink | `6585f5860e87c146c26aa066d3a52b94eb56f271` |

## 2. Missing guarantees and alternatives

The qualification assessment binds Dapr 1.18.7 source `b32a815fa2260983e181b95272ea555fe17d3895`, executable SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`, components-contrib 1.18.6 source `eac9e31dd14cd798c9996aae30818b572aa12c59`, and Dapr.Client 1.19.0. The isolated executable returned 501/ERR_API_UNIMPLEMENTED for get-key/wrap/unwrap. Tagged source additionally loses full HSM/profile metadata and constructive provider failure details; a build flag alone does not close qualification.

Alternatives considered: stock streaming encryption changes frozen pdenc-v2; a secret store supplies no cryptographic operation; enabling/augmenting the tagged Dapr implementation requires a separately qualified runtime/component; deferral supplies no adapter. The user selected preparation of this bounded SDK exception. No Dapr failure can select it dynamically: it is the explicitly registered Azure backend, or the operation fails.

## 3. Operations and isolation

Only these data-plane operations are allowed, all with explicit stable API `2025-07-01` and the configured ordinary-vault origin:

| Operation | Implementation | Required binding |
|---|---|---|
| Current or exact-version metadata | `KeyClient.GetKeyAsync`; `KeyClientOptions.ServiceVersion.V2025_07_01` | Configured vault/key and returned exact version; never list/query-all |
| Fresh or retained-target wrap | Azure.Core `HttpPipeline` POST `/keys/{name}/{version}/wrapkey` | Explicit validated version, `alg=RSA-OAEP-256`, exactly 32 input bytes; exactly 384 output bytes and matching returned `kid` |
| Historical unwrap | Same SDK pipeline POST `/keys/{name}/{version}/unwrapkey` | Recorded exact version, bounded 384-byte wrapped input, exactly 32 output bytes and matching returned `kid` |

The public cryptography client can use local public-key wrapping; its remote-only constructor is internal. Consequently the two POSTs use public Azure.Core pipeline primitives with bounded owned JSON buffers. No reflection, patched SDK, local RSA, arbitrary URL, alternate algorithm or HTTP fallback exists. Request shape is exactly `alg`/`value`; nonce/tag/AAD stay in the unchanged engine. TLS remains validated and redirects are refused.

Metadata `KeyClient` keeps authentication-challenge resource verification enabled. The custom crypto pipeline uses public `BearerTokenAuthenticationPolicy` with a fixed deployment-cloud vault scope; it never reuses an internal SDK challenge policy or accepts provider-supplied authority/resource/scope. The fixed suffix/audience pairs are `.vault.azure.net` / `https://vault.azure.net/.default`, `.vault.usgovcloudapi.net` / `https://vault.usgovcloudapi.net/.default`, and `.vault.azure.cn` / `https://vault.azure.cn/.default`. Only the pair matching the approved deployment cloud is accepted. A crypto 401 is denial, without challenge-driven retargeting or credential fallback.

Runtime provider code is confined to these files under `src/Hexalith.EventStore.PayloadProtection.AzureKeyVault/`: `AzureKeyVaultPayloadProtectionBackend.cs`, `AzureKeyVaultMetadataClient.cs`, `AzureKeyVaultDataPlane.cs`, `AzureKeyVaultWireCodec.cs`, `AzureKeyVaultOwnedRequestContent.cs`, `AzureKeyVaultSafeTransport.cs`, `AzureKeyVaultCredentialFactory.cs`, `AzureKeyVaultFailureClassifier.cs`, `AzureKeyVaultPayloadProtectionOptions.cs`, `AzureKeyVaultOptionsValidator.cs`, `AzureKeyVaultKeyProfile.cs`, `AzureKeyVaultReadinessProbe.cs`, `AzureKeyVaultServiceCollectionExtensions.cs`, and the project file. Supporting pure types follow one type per file within that project and introduce no provider calls or additional capability.

Tests are confined to `tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/` and `tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests/`; their mock transport/credentials cannot be selected through runtime configuration. Workflow path: `.github/workflows/payload-protection-azure.yml`. Scope additionally permits the solution entry, explicit companion/test friend access in the core project, existing package/lane guard updates, the two central catalog additions below, and owning story/boundary documents. No SDK reference enters Contracts, the engine, Server, domain libraries or linked/generated runtime inputs.

## 4. Dependencies and catalog edits

All application/test references remain versionless and the companion remains `IsPackable=false`. Proposed direct dependencies are the three exact packages below; metadata inspection selected their `net10.0` assemblies. Identity is already centrally pinned; the two additions are Azure.Core and Keys in `references/Hexalith.Builds/Props/Directory.Packages.props`. Approval must explicitly include these two owning-submodule edits. Do not move pins into the root import wrapper or edit other catalog entries.

| Package/version | Downloaded nupkg SHA-256 | NuGet repository source commit |
|---|---|---|
| Azure.Security.KeyVault.Keys 4.10.2 | `9c9411314aedb651e83f33dcababa87201d5b1deebaf5288edcd16daa3e71d8f` | `94d96d1ca05657c51326df85d11ec829a89b85fd` |
| Azure.Identity 1.21.0 | `47712e11f5016aa55617b4330b8943e4ef1fb1f9875bbb5857ae9fcaeee57a0d` | `f7400e4471111bf7f2154834022786119acbdd77` |
| Azure.Core 1.54.0 | `28f7a981751c03b2931775ba9e6fb14b366ebfd8751b367cd0a0fab2c51723a6` | `01ff3af0100f62575b20fba4730c77e92713a168` |

Exact proposed catalog additions:

```xml
<PackageVersion Include="Azure.Core" Version="1.54.0" />
<PackageVersion Include="Azure.Security.KeyVault.Keys" Version="4.10.2" />
```

Record the evaluated transitive graph, assembly/file versions, resolved hashes and SBOM during implementation. Central transitive pinning may affect other existing Azure consumers: inspect that evaluated impact and run affected checks before accepting the catalog edits. An unexpected version downgrade or incompatible graph returns to review; no unrelated upgrade is authorized. Current 14-package release inventory remains unchanged.

## 5. Configuration, credentials and readiness

Keep the base closed `EventStore:PayloadProtection:AzureKeyVault` fields: `VaultUri`, `KeyName`, `ManagedIdentityClientId`, fixed `ExpectedKeySize=3072`, fixed `WrapAlgorithm=RSA-OAEP-256`, `KeyVersionRefreshInterval` default five minutes/max one hour, and positive `RequestTimeout` default ten seconds/max 30 seconds. Effective requests also obey the enclosing ten-second engine budget. Unknown fields fail closed. Only exact Development is development; missing/null/empty environment is Production and custom names are non-development. URI/key grammar, TLS, approved cloud suffix and token audience must agree; there is no configurable arbitrary credential/token endpoint.

Always use a deterministic system-assigned or explicitly selected user-assigned `ManagedIdentityCredential`, including Development in this first implementation. Do not add a developer/default credential chain. Configure authentication and data-plane retries to zero under the existing executor. Runtime privileges remain exactly metadata-read/wrap/unwrap at the selected key scope, with no management, export, secret or certificate permission. Separate provisioning evidence must establish the ordinary Premium vault, provider-generated non-exportable HSM key, 90-day retention/purge protection, private endpoint/DNS, disabled public/trusted-service bypass and diagnostic sink; metadata alone cannot prove origin, RBAC or network custody.

Readiness validates exact vault/key/version, enabled/time-valid RSA-HSM-3072, non-exportability and exactly wrap/unwrap operations. It then runs a fresh 32-byte remote wrap/unwrap probe under one bounded phase, fixed-time compares and clears all owned arrays. Missing or contradictory profile evidence refuses readiness; it cannot synthesize an HSM/profile assertion from the public modulus. Existing PF-02/lifecycle/fence readiness remains engine-owned. Backend registration uses the base companion extension signature and grants no Server replacement/automatic enablement.

Current metadata selection uses bounded monotonic freshness; new wraps cannot use an expired selection after refresh failure. Retained-target wrap and historical unwrap validate their exact version independently of the current alias. Current-version changes never rewrite immutable payloads or retire old KEKs.

## 6. Failure, reconciliation, buffers and diagnostics

Reuse the existing executor, breaker, lifecycle and local-operation retention. There is one effective three-attempt/ten-second budget with no nested SDK retries. Caller cancellation propagates. Typed authentication/configuration/401/403 denial is permanent. Proven pre-dispatch token/transport transients and affirmative service rejections such as throttling may return Unavailable with a structurally parsed, bounded Retry-After. Post-dispatch wrap timeout/network/5xx uncertainty is Ambiguous; neither text nor a generic status proves absence. Certificate/hostname failures refuse readiness and do not retry. Malformed response, algorithm/ID/version/length mismatch, provider 404 or disabled/expired state without authoritative deletion proof returns ConsistencyMismatch. No provider condition independently manufactures lifecycle deletion.

`ReconcileAsync` returns Ambiguous whenever exact operation completion/absence cannot be established. This candidate adds no in-memory or durable result journal, no provider query capability and no wrap replay exception. Lost wrap responses, restarted pending creates and unknown rewrap outcomes may therefore remain indexed/operator-required indefinitely; prior material is retained and irreversible completion withheld. Approval accepts this liveness limitation, not automatic crash recovery.

Engine inputs remain engine-owned. Copy mutable SDK/transport inputs, retain them until actual async completion even after timeout, and return fresh owned arrays. Bounded UTF-8 request/response buffers and base64url scratch containing DEKs are cleared on success, malformed result, cancellation, abandonment and disposal. Crypto messages set `HttpMessage.BufferResponse=false` and use the owned `RequestContent` subclass, not string/BinaryData convenience content. Read at most 65,536 response bytes into owned buffers and reject excess. Parse sensitive values with `Utf8JsonReader` over owned spans, without a retained JsonDocument or immutable plaintext/base64 DEK string. Reuse late-result draining before cache-clear acknowledgement.

Disable SDK content/logging/distributed tracing and unsafe identity logging. The owned HTTP transport must suppress/filter provider URI/header/body/token/exception emission through ambient HTTP instrumentation as well as SDK diagnostics. Expose only existing safe fingerprints, closed outcomes and bounded operation counts/timings. Sentinel tests must attach SDK, HTTP, activity, metric, log and exception observers; a host that cannot meet this diagnostic boundary cannot claim conformance.

## 7. Acceptance and external authority

Local implementation includes unit transport/profile/failure/cancellation/zeroing/no-leak tests and the existing core, compatibility, lifecycle, golden, invariant, package/lane and dependency guards. Tests verify emitted versioned POSTs/API version and remote response `kid`, rather than accepting successful SDK calls as proof of remote execution.

Real custody/conformance requires a separately authorized isolated Azure environment and named resource/identity/network/fault/cleanup scope. Live history/fault/rotation/rollback tests must read persisted event/snapshot/key end-state through its owning Dapr boundary, preserve historical keys/readers, record cleanup/retention limits and distinguish online invalidation from all-copy erasure. Default local work cannot provision Azure or pass this gate by skipping or mocking it. Missing environment authority leaves live conformance and story completion pending.

## 8. Exception ownership, review and removal

Owner: EventStore payload owner with Architecture, Security, Operations, Release and Test owners. Exact-content approval must identify the approving human and roles accurately; one person holding several roles is not independent review. The explicit decision must cover this exception, additive 8.6 implementation, predecessor disposition and the two catalog edits. Route selection, silence, prior digest approval and a status label supply none of those decisions.

After approval, move only the matching unresolved row into the accepted-exception register, link this exact normative digest and the source-bound qualification assessment, and hand the exact operation/path/version limits to Story 3.17's inventory/guard. It remains a preliminary register, without a whole-platform conformance claim.

Re-review on runtime/client/component/SDK/API/credential/profile/path/operation changes or new relevant Dapr capability. Remove the exception only when a released, identified Dapr profile supplies every required metadata/version/remote-operation/typed-failure/retry/custody/no-leak guarantee and passes the same real-service compatibility/rotation/rollback suite under an approved migration. No automatic transport switch follows an upgrade.

<!-- HX-PP-AZURE-EXCEPTION-END -->

## Approval state

Approved on 2026-11-03 by Administrator, the interactive project owner, through “approve and stop” at the checkpoint expressly covering this specification, the exact exception and its two named central catalog edits. This is a single-maintainer-attested owner decision, not independent review. The approved normative digest remains `fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394`; normative bytes and incorporated identities are unchanged.

The decision accepts the exact bound 8.4/8.5 evidence for additive 8.6 development under the candidate's regression conditions, the scoped SDK/API design and conservative reconciliation limitation, and the two specified catalog additions. It authorizes local implementation when the user resumes the workflow. No dependency/catalog or accepted-register edit has been applied. Azure resource authority, live conformance, production custody, Server enablement, release, later-story authorization and G5 remain separate. The session stops with the owning story `ready-for-dev`.

## Validation record

Package downloads, hashes, exact SDK source and dotnet-inspect queries are recorded in the qualification assessment. Approval-time verification reproduced the normative digest, owning frozen-block digest, both predecessor hashes, qualification hash, current catalog hash and Builds gitlink. No repository dependency, accepted register entry or provider resource was changed while preparing or recording approval of this candidate.
