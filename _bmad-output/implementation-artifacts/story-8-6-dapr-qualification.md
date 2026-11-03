# Story 8.6 Dapr Qualification Assessment

Date: 2026-11-03. Source: `45d29a03d8be155d2251251a5bc6ba42035b672d`.
Disposition: preliminary local qualification; architecture decision required. This assessment does not select dependencies, amend authority, approve an exception or establish Azure conformance.

## Authority and predecessor observations

Stories 8.4 and 8.5 are currently `done`. The latest 8.5 handoff records 1,021 unit tests, 31 real-Dapr project tests and the invariant V029 case passing, with no CI link or Azure custody claim. Neither predecessor specification supplies an approved crypto design or explicit content-bound 8.6 successor decision. Earlier reconciliation prose describing these stories as backlog is historical.

The crypto amendment dated 2026-10-05 remains `draft-unapproved` with `implementation_authorized: false`. The accepted-exception register still marks `payload-production-key-operations` unresolved. The approved lifecycle amendment explicitly preserves the opaque provider boundary and does not approve the crypto amendment. Existing evidence-storage simplification requires commands/results in the story and CI artifacts, not a new committed evidence-packet system.

Recomputed identities:

| Subject | SHA-256 |
|---|---|
| Base normative content | `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e` |
| Base full file | `542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82` |
| Approved lifecycle amendment | `a853201a6df1e8e88730a40a582b87e20c5e5cf7a223f01ca905e9c9ee7df94d` |
| Effective lifecycle authority | `eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9` |

## Exact investigated profile

- Runtime 1.18.7, source `b32a815fa2260983e181b95272ea555fe17d3895`.
- Local executable `/home/administrator/.dapr/bin/daprd`, SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`, matches the repository runtime lock. `--version` and `--build-info` confirm version and full source identity.
- Components-contrib 1.18.6, source `eac9e31dd14cd798c9996aae30818b572aa12c59`, as bound by the runtime lock and retained runtime `go.mod`.
- Centrally governed application dependency: `Dapr.Client` 1.19.0. Its generated/.NET convenience surface has not been qualified; the HTTP and gRPC runtime implementations were inspected directly. No package/version update was made.
- Candidate component: `crypto.azure.keyvault`. No live component/provider configuration, emitted provider API, identity, vault, network or service operation was observed. This local executable result does not attest the separately pinned OCI image's features.

## Operation and guarantee assessment

| Requirement | Observed evidence | Disposition |
|---|---|---|
| Bare key operations preserving pdenc-v2 | Runtime declares Subtle get/wrap/unwrap RPCs and HTTP routes, but implementations depend on the `subtlecrypto` build tag. The investigated executable returns HTTP 501 for all three operations. | Confirmed gap for this executable. The streaming encrypt/decrypt API uses a different encryption scheme and cannot replace frozen bytes. |
| Strong current-version discovery and profile | Alternate tagged implementation returns name/version and public JWK/PEM. Component conversion accepts both RSA and RSA-HSM and constructs a generic RSA public key; JSON serialization omits validity fields. HSM origin, exact operation set and full current profile are not preserved. | Source-observed contract gap; a build flag alone cannot establish profile verification. |
| Exact-version wrap | Component parses `name/version`, but a versioned asymmetric key follows cached public-key local encryption rather than `vaultClient.WrapKey`. The response includes wrapped bytes/tag without a provider-returned version. | Source-observed semantic difference requiring design review; no live compatibility claim. |
| Historical exact-version unwrap | Component passes explicit name/version to Azure unwrap. The runtime stub still prevents access in this executable. | Source capability only; exact version, key length, service errors and restart/rotation remain unqualified. |
| Startup probe | No production adapter or Azure probe exists; the current engine probe verifies Dapr state capabilities only. | Unresolved. |
| Constructive failures | Tagged wrap/unwrap implementation deliberately replaces component errors with a generic crypto-operation error. Raw details may enter debug logging. | Source-observed gap for required denied/unavailable/missing/consistency and Retry-After distinctions; no message parsing permitted. |
| Combined retry/cancellation budget | Tagged runtime applies a component outbound policy; component constructs the Azure client without explicit retry overrides and applies its request timeout. | Unresolved effective attempt/deadline budget across all layers; no multiplication accepted by assumption. |
| Identity, private connectivity and custody | Metadata delegates credential selection to shared Azure authentication. No isolated resource receipt or deployed effective configuration was supplied. | Unresolved; no Azure calls or mutations performed. |
| Sensitive buffers and diagnostic safety | Source paths cross protobuf/JWK/Go/provider allocations; no clearing or sentinel qualification was executed. | Unresolved; application-array clearing alone cannot attest sidecar custody. |
| Stable-operation reconciliation | Existing engine SPI requires affirmative exact-mutation absence before Missing. No durable provider operation lookup was qualified. | Unresolved; lack of lookup must remain Ambiguous, never fabricated Missing. |
| State/lifecycle integration | Existing PF-02 qualification is retained predecessor evidence. Crypto transport cannot establish cross-component transactions. | Azure-specific persisted end-state/fault coverage remains open. |
| Historical decryptability, erasure and rollback | No real Azure-wrapped event/snapshot history was created. | Unresolved. Retain old keys/readers; online invalidation cannot certify all-copy erasure. |

These are source and local executable findings, not a blanket claim that every Dapr version or custom component is unsuitable. Adding the build tag leaves independent metadata, failure, custody and budget questions.

## Commands and observed results

- `sha256sum /home/administrator/.dapr/bin/daprd` — matches the exact hash above.
- `/home/administrator/.dapr/bin/daprd --version` — `1.18.7`.
- `/home/administrator/.dapr/bin/daprd --build-info` — full source identity above.
- An isolated subprocess used an empty temporary resources directory, unique app ID, four free ports, loopback API listeners, disabled metrics and error-only logging. `/v1.0/healthz/outbound` became ready. Metadata listed `HotReload` and `WorkflowsRemoteActivityReminder`, with no `_SubtleCrypto` feature.
- POST `/v1.0-alpha1/subtlecrypto/qualification/getkey`, `/wrapkey` and `/unwrapkey`, with JSON bodies matching the pinned proto, each returned **501**, code **`ERR_API_UNIMPLEMENTED`**. No component, key, credentials or provider service was used. The process was terminated and reaped and its temporary resources directory removed.
- `node scripts/payload-protection/verify-golden-vectors.mjs` and `python3 scripts/payload-protection/verify-golden-vectors.py` — each PASS, V001–V003. Normative and effective lifecycle digest recomputation — PASS against the identities above.

To reproduce the local API result, start that exact binary with `--app-id story86-surface --resources-path <empty-directory> --dapr-listen-addresses 127.0.0.1 --dapr-http-port <free-http-port> --dapr-grpc-port <free-grpc-port> --dapr-internal-grpc-port <free-internal-port> --metrics-port <free-metrics-port> --enable-metrics=false --log-level error`. After outbound readiness, POST JSON to the three paths above using `{ "name": "qualification-key" }` for get-key; `{ "keyName": "qualification-key/version", "algorithm": "RSA-OAEP-256", "plaintextKey": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=" }` for wrap; and `{ "keyName": "qualification-key/version", "algorithm": "RSA-OAEP-256", "wrappedKey": "AA==" }` for unwrap. Stop the owned subprocess afterwards. This reproduces API availability only.

## Primary source references

Inspected the exact pinned raw source bytes; the Dapr documentation page alone omits the subtle interfaces and is insufficient for qualification.

- [Runtime RPC declarations](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/dapr/proto/runtime/v1/dapr.proto) and [request/response schema](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/dapr/proto/runtime/v1/crypto.proto).
- [Default unimplemented methods](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/pkg/api/universal/subtlecrypto.go), [tagged implementation and error handling](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/pkg/api/universal/subtlecrypto_subtlecrypto.go), and [HTTP routes](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/pkg/api/http/subtlecrypto.go).
- [Azure component operations](https://github.com/dapr/components-contrib/blob/eac9e31dd14cd798c9996aae30818b572aa12c59/crypto/azure/keyvault/component.go), [key conversion](https://github.com/dapr/components-contrib/blob/eac9e31dd14cd798c9996aae30818b572aa12c59/crypto/azure/keyvault/jwk.go), [key serialization](https://github.com/dapr/components-contrib/blob/eac9e31dd14cd798c9996aae30818b572aa12c59/crypto/key.go), and [metadata initialization](https://github.com/dapr/components-contrib/blob/eac9e31dd14cd798c9996aae30818b572aa12c59/crypto/azure/keyvault/metadata.go).
- [Dapr 1.18 cryptography API documentation](https://v1-18.docs.dapr.io/reference/api/cryptography_api/), consulted as orientation rather than exact-build evidence.

## Decision needed

On 2026-11-03 the user selected **scoped Azure SDK exception**. This chooses preparation of that route, without approving unseen exception bytes or introducing dependencies. The exact candidate is `spec-8-6-crypto-boundary-amendment.md`; the shared frozen format and production custody profile remain unchanged.

## SDK design investigation after route selection

NuGet's version index identifies Keys **4.10.2** as the latest stable release inspected on 2026-11-03. Identity **1.21.0** is already centrally pinned. Keys requires Azure.Core **1.54.0**. Downloaded package bytes were hashed without restoring or changing any repository project; the candidate binds those hashes and exact source commits.

`dnx dotnet-inspect -y -- package Azure.Security.KeyVault.Keys -v:q`, member queries against `Azure.Security.KeyVault.Keys@4.10.2`, and `library` inspection confirm the package and `net10.0` API surface, including service version `V2025_07_01`, profile properties, wrap signature and assembly version `4.10.2.0`. Identity inspection reports assembly version `1.21.0.0`. The installed tool rejects the skill's older `--oneline` option; queries were rerun with its documented section/row options. A broad type scan reported unresolved forwarded generic constraints, so absence of an API was not inferred from it; exact SDK source was inspected as well.

The source-bound `CryptographyClient(Uri, TokenCredential, options)` initializes with `forceRemote: false`. `KeyClient.GetCryptographyClient` does the same. Public-key local wrapping is therefore possible; the private remote-only constructor is not an application extension seam. The candidate uses `KeyClient.GetKeyAsync` for metadata and the public Azure.Core SDK pipeline for the two exact REST wrap/unwrap operations, avoiding reflection, a forked SDK or local RSA. Both paths explicitly use stable Key Vault API `2025-07-01`.

Existing reconciliation contracts permit an adapter to return `Ambiguous` when the provider cannot establish an exact operation outcome. The candidate deliberately adds no result journal: missing local memory, restart and provider 404 cannot prove mutation absence. Lost responses can leave Create/rewrap pending and require operator resolution. This is a documented limitation for approval, not an automatic recovery claim.

Sources inspected:

- [Keys 4.10.2 package](https://www.nuget.org/packages/Azure.Security.KeyVault.Keys/4.10.2) and its [pinned cryptography client source](https://github.com/Azure/azure-sdk-for-net/blob/94d96d1ca05657c51326df85d11ec829a89b85fd/sdk/keyvault/Azure.Security.KeyVault.Keys/src/Cryptography/CryptographyClient.cs).
- [Pinned KeyClient construction](https://github.com/Azure/azure-sdk-for-net/blob/94d96d1ca05657c51326df85d11ec829a89b85fd/sdk/keyvault/Azure.Security.KeyVault.Keys/src/KeyClient.cs) and [Azure.Core pipeline builder](https://github.com/Azure/azure-sdk-for-net/blob/01ff3af0100f62575b20fba4730c77e92713a168/sdk/core/Azure.Core/src/Pipeline/HttpPipelineBuilder.cs).
- Stable service contracts: [Get Key](https://learn.microsoft.com/en-us/rest/api/keyvault/keys/get-key/get-key?view=rest-keyvault-keys-2025-07-01), [Wrap Key](https://learn.microsoft.com/en-us/rest/api/keyvault/keys/wrap-key/wrap-key?view=rest-keyvault-keys-2025-07-01), and [Unwrap Key](https://learn.microsoft.com/en-us/rest/api/keyvault/keys/unwrap-key/unwrap-key?view=rest-keyvault-keys-2025-07-01).
