# Reviewer Gate — Technology And Currentness Final Review (2026-07-19 AD-24)

Verdict: **pass**. The runtime-rotation amendment is consistent with Dapr 1.18.0's HashiCorp Vault/OpenBao secret-store behavior. The canonical spine now makes a technically coherent distinction between one-map `GetSecret` retrieval, non-atomic bulk enumeration, and optional `metadata.version_id` pinning. It introduces no new critical or high technology-currentness contradiction.

## Final Review Scope

Re-reviewed `_bmad-output/planning-artifacts/architecture.md:302-318` and the Stack at lines `352-356`, with particular attention to the amended serving-path and rotation rules.

Primary sources rechecked:

- [Dapr Secrets API reference](https://docs.dapr.io/reference/api/secrets_api/)
- [Dapr HashiCorp Vault/OpenBao component reference](https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/)
- [Dapr .NET secrets SDK reference](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-secrets/dotnet-secrets-howto/)
- [Dapr components-contrib v1.18.0 Vault implementation](https://github.com/dapr/components-contrib/blob/v1.18.0/secretstores/hashicorp/vault/vault.go)

Repository evidence rechecked:

- `.github/workflows/integration.yml:23-24,55-58` pins the live-sidecar Dapr runtime to `1.18.0`.
- `deploy/README.md:217-256` uses `daprio/daprd:1.18.0` as the deployment seed.
- `references/Hexalith.Builds/Props/Directory.Packages.props:138-145` separately pins Dapr .NET SDK packages to `1.18.4`.
- `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md:4-6` remains draft, proposed, and not authorized.

## Runtime-Rotation Verification

| Claim | Result | Currentness finding |
| --- | --- | --- |
| A runtime consumer retrieves one logical secret map with `GetSecret`. | **Pass** | With `vaultValueType: map`, one Dapr Get Secret operation returns the embedded key/value fields of one Vault KV v2 secret. The .NET spelling is `GetSecretAsync`; the architecture's `GetSecret` wording correctly names the Dapr API/component operation rather than prescribing a language-specific method name. |
| Values that must rotate atomically share one map. | **Pass** | The v1.18.0 component reads one KV v2 version and returns that version's complete inner `data` map. Keeping the credential fields and non-secret generation marker in that map prevents field-level generation mixing within a successful lookup. |
| Runtime serving paths do not use `BulkGetSecret`. | **Pass** | The component documentation says `vaultKVUsePrefix` must be `false` to use bulk retrieval, while AD-24 intentionally fixes it to `true`. The v1.18.0 implementation also performs bulk retrieval by listing keys recursively and issuing a separate get for each key; it is not an atomic multi-secret snapshot. Excluding it is both compatible with the selected component metadata and necessary for the stated rotation unit. |
| Runtime serving paths do not pin `metadata.version_id`. | **Pass** | The API and component support `version_id`, but the v1.18.0 implementation defaults an omitted version to `0`, meaning the latest KV v2 version. Avoiding a serving-path pin lets consumers discover the newly published generation; pinning would instead strand them on a selected historical version. |
| Publish-overlap-acknowledge-revoke is compatible with latest-version reads. | **Pass** | Publishing a new map version makes it the next successful unpinned read. The in-map generation marker gives the platform a non-secret acknowledgement identity. Keeping both old and new downstream credentials valid for the bounded overlap avoids an availability gap while consumers converge. Revocation occurs only after acknowledgement, as the spine requires. |
| Rollback publishes a restored generation. | **Pass** | Publishing restored values as a new version preserves unpinned latest-version retrieval and a monotonic generation acknowledgement path. It does not require consumers to select or pin an older Vault version. |

## Regression Check Against Prior Closures

No prior critical/high finding is reopened:

- AD-24 still describes the payload-protection specification as draft and not authorized, and does not assign `pdenc-v2` key custody to the Dapr secret store.
- The runtime `1.18.0` evidence remains distinct from the .NET SDK package version `1.18.4`.
- Bootstrap-token rotation still requires controlled sidecar restart and does not assume automatic token renewal or reload.
- Required-secret validation remains an explicit host readiness contract rather than an undocumented Dapr guarantee.
- Azure Container Apps managed Dapr remains correctly excluded from the conforming production profile for the reasons recorded in the preceding re-review.

## Non-blocking Implementation Note

Implement runtime-required consumers with the single-secret endpoint, such as `DaprClient.GetSecretAsync`, or an equivalent adapter that preserves that operation. Do not substitute a convenience API that preloads through `GetBulkSecretAsync`; that would conflict with AD-24's selected `vaultKVUsePrefix: true` behavior and its one-map rotation boundary. This is an implementation conformance check, not an architecture defect.

The preceding medium diagnostics-evidence obligation also remains unchanged: EventStore-owned diagnostics can enforce the logical-key and non-secret-generation contract, while the real-OpenBao deployment lane must still verify upstream Dapr/OpenBao and transport log behavior. It does not create a critical/high contradiction.

## Gate Result

- Critical findings: **0**
- High findings: **0**
- Technology-currentness recommendation: **pass AD-24 for architecture handoff**.
