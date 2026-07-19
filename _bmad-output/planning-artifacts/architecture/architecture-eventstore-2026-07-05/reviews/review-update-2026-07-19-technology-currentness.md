# Reviewer Gate — Technology And Currentness Lens (2026-07-19 AD-24 Update)

Verdict: **changes required**. OpenBao is a viable Dapr secret-store choice and the component type, v1 status, Dapr 1.16 runtime floor, component scoping, secret scoping, and TLS controls are supported by current official documentation. AD-24 is nevertheless unsafe to finalize unchanged: it misstates an unapproved payload-protection specification as approved, treats the repository's .NET SDK package version as a runtime pin, and promises bootstrap rotation and fail-start behavior that the Dapr Vault component does not provide automatically.

## Review Scope

Reviewed the 2026-07-19 changes to `_bmad-output/planning-artifacts/architecture.md`, concentrating on AD-24, the Secrets convention, Stack entry, topology diagram, Deferred row, and their interaction with AD-23 and `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`.

Every changed named-technology assertion was checked against current primary Dapr/OpenBao sources and current repository reality. No assertion in this review relies only on model training data.

## Evidence

Primary upstream sources:

- [Dapr OpenBao component reference](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/) — no dedicated OpenBao component; use `secretstores.hashicorp.vault` v1; the HashiCorp Vault metadata fields are compatible.
- [Dapr supported secret stores](https://docs.dapr.io/reference/components-reference/supported-secret-stores/) — OpenBao is Stable, component v1, since Dapr runtime 1.16.
- [Dapr HashiCorp Vault component reference](https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/) — `vaultAddr`, CA/TLS fields, `skipVerify`, `vaultToken`, `vaultTokenMountPath`, KV prefix/path, and value-type semantics.
- [Dapr component secret references](https://docs.dapr.io/operations/components/component-secrets/) — `auth.secretStore` and `secretKeyRef` semantics; Kubernetes' implicit bootstrap-store behavior when `auth.secretStore` is empty.
- [Dapr component scopes](https://docs.dapr.io/operations/components/component-scopes/) and [Dapr secret scopes](https://docs.dapr.io/developing-applications/building-blocks/secrets/secrets-scopes/) — component `scopes`, `defaultAccess: deny`, and `allowedSecrets` behavior.
- [Dapr component schema](https://docs.dapr.io/reference/resource-specs/component-schema/), [component update behavior](https://docs.dapr.io/operations/components/component-updates/), and [sidecar health](https://docs.dapr.io/operations/resiliency/health-checks/sidecar-health/) — `ignoreErrors` defaults false; initialization errors stop the sidecar unless explicitly ignored; readiness includes component initialization, but a logical application secret is not proactively checked merely because it appears in an allowlist.
- [Dapr runtime/SDK support policy](https://docs.dapr.io/operations/support/support-release-policy/) and the official [.NET SDK v1.18.4 release](https://github.com/dapr/dotnet-sdk/releases/tag/v1.18.4) — runtime and SDK are distinct versioned products; 1.18.4 is an SDK release, while the current packaged runtime line is 1.18.0; same/current and N-2 minor combinations are supported.
- [Dapr v1.18.0 Vault component source](https://github.com/dapr/components-contrib/blob/v1.18.0/secretstores/hashicorp/vault/vault.go) — the component accepts either `vaultToken` or `vaultTokenMountPath`, reads a mounted token during initialization, retains it in memory, and uses it for requests; it does not implement OpenBao Kubernetes/AppRole/JWT login or automatic mounted-token rereading.
- [OpenBao policies](https://openbao.org/docs/2.5.x/concepts/policies/), [auth methods](https://openbao.org/docs/auth/), [AppRole](https://openbao.org/docs/auth/approle/), [Kubernetes/JWT integration](https://openbao.org/docs/auth/jwt/oidc-providers/kubernetes/), and [TCP listener TLS](https://openbao.org/docs/2.5.x/configuration/listener/tcp/) — deny-by-default policy and short-lived machine authentication are available in OpenBao itself, but they are not native auth modes of Dapr's Vault component; OpenBao enables TLS by default.

Repository reality:

- `references/Hexalith.Builds/Props/Directory.Packages.props:138-145` pins the **Dapr .NET SDK packages** to `1.18.4`.
- `.github/workflows/integration.yml:23-24,55-58` separately pins the integration-test **Dapr runtime** to `1.18.0`.
- `_bmad-output/planning-artifacts/architecture.md:298-310` contains AD-24.
- `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md:5-8,23-36,54-68,1567-1591,1679-1685` says `draft-not-authorized`, `Proposed; not approved`, records all required approvals as pending, and keeps Story 8.2 not authorized.
- The same payload specification at lines `969-997` selects ordinary Azure Key Vault Premium with an RSA-HSM-3072 KEK and explicitly rejects Dapr secret stores as production `pdenc-v2` key custody; lines `1016-1056` require direct managed-identity Azure Key Vault access with no application secret credential.

## Critical Finding

### C1 — AD-24 falsely calls the payload-protection specification approved

**Evidence:** AD-24 says it does not replace “the approved payload-protection specification's Azure Key Vault Premium RSA-HSM KEK wrap/unwrap backend.” The referenced artifact's frontmatter says `status: draft-not-authorized`, `decision: proposed`, and `story_8_2_authorized: false`. Section 1 says “Proposed; not approved,” all named approval rows are pending, and section 17 repeats that the ADR is technically specified but not approved. The document explicitly states that a planning approval is not approval of the security specification.

**Impact:** This bypasses the content-bound security authorization model in prose. A downstream implementer could cite AD-24's `[ADOPTED]` wording as evidence that the Azure production backend—or Story 8.2 itself—was already approved, despite the normative artifact saying the opposite.

**Disposition:** **Must fix before handoff.** State that AD-24 does not alter AD-23 or the payload specification's authorization gate; that no production `pdenc-v2` backend is currently authorized; and that, if the currently proposed payload specification is later approved unchanged, its Azure Key Vault Premium RSA-HSM backend remains separate from Dapr/OpenBao. Do not use “approved” until the named, digest-bound approvals actually exist.

**Compatibility result:** The boundary itself is correct. The proposed payload specification explicitly rejects standard secret stores and Dapr components as production KEK custody, requires direct `Azure.Security.KeyVault.Keys` operations through managed identity, and contains no application client secret for OpenBao to supply. OpenBao may hold ordinary runtime/application/component secrets, but must not store or perform the `pdenc-v2` KEK wrap/unwrap role.

## High Findings

### H1 — `1.18.4` is an SDK pin, not proof of the Dapr runtime floor

**Evidence:** The Stack labels `1.18.4` as “Dapr .NET SDK packages.” Central props confirm that. The only explicit runtime pin found is `DAPR_VERSION: '1.18.0'` in the live-sidecar integration workflow. Dapr's release policy versions runtime and SDK separately and lists runtime 1.18.0 as the current supported runtime line. The SDK's official repository separately publishes v1.18.4.

**Impact:** “The repository's Dapr 1.18.4 baseline satisfies that floor” is category-incorrect and does not constrain production sidecars, Kubernetes, Docker Compose, or a managed hosting platform to runtime 1.16 or later. A deployment could comply with every NuGet pin while running an older sidecar that does not carry the OpenBao certification claim.

**Disposition:** **Autofix the claim and add a deployment invariant.** Say that OpenBao requires Dapr runtime `>=1.16`; repository CI currently exercises runtime `1.18.0`; SDK packages are independently pinned to `1.18.4`. Every production target must prove or pin a supported Dapr runtime at or above the floor (prefer the repo-tested 1.18 runtime line) before loading the component. Managed platforms must expose equivalent runtime/component support evidence rather than inheriting the NuGet version.

**Compatibility result:** The repository's runtime 1.18.0 is above OpenBao's 1.16 floor, and the official 1.18.4 .NET SDK patch is compatible with the current 1.18 runtime line under Dapr's SDK/runtime compatibility policy. The issue is missing production-runtime authority, not an observed 1.18.0/1.18.4 incompatibility.

### H2 — The chosen Dapr component does not provide the implied production bootstrap-token lifecycle

**Evidence:** The current Dapr Vault component exposes only `vaultToken` or `vaultTokenMountPath`. In the v1.18.0 implementation, a mounted token is read once during component initialization and retained for later requests. The component does not perform OpenBao Kubernetes, JWT, or AppRole login, does not renew the token, and does not reread the mounted file on ordinary token rotation. OpenBao supports short-lived machine auth and renewable tokens, but those capabilities are outside this Dapr component unless another operator-controlled mechanism acquires a token and deliberately reloads/restarts the component.

**Impact:** AD-24 can drive implementers toward either an unsafe long-lived token or a short-lived token that eventually expires and takes secret retrieval down. “Supplied out-of-band” and “least privilege” do not fix issuance, TTL, renewal, revocation, handoff, or component reload semantics. Two deployment slices could choose materially incompatible bootstrap and rotation models.

**Disposition:** **Discuss and bind before production implementation.** Select one bootstrap path per hosting model—such as a hosting-platform/bootstrap secret store feeding `vaultToken`, or a mounted token through `vaultTokenMountPath`—and define its issuer, policy/path capabilities, TTL, renewal/rotation owner, sidecar/component reload or restart trigger, overlap/revocation behavior, and failure test. Keep committed inline token values forbidden. A root token is never an acceptable bootstrap credential. The deferred row may retain exact environment values, but not the lifecycle contract required to keep production access functioning safely.

### H3 — Missing application secrets do not automatically fail startup

**Evidence:** Dapr component initialization is fail-closed only when `spec.ignoreErrors` remains false. A missing credential used by another component's `secretKeyRef` can fail that component's initialization. In contrast, an application secret named only in `allowedSecrets` is not fetched or existence-checked at sidecar startup; the first Secrets API call may be the first time its absence is observed. Dapr health confirms component initialization, not the existence of every logical application secret the app will later request.

**Impact:** AD-24's absolute statement that every missing required secret fails deployment/startup before the dependent resource serves traffic is not supplied by Dapr. A service can become ready and fail later on its first lazy secret read unless EventStore or the deployment adds an explicit startup contract.

**Disposition:** **Autofix the architecture rule.** Require `spec.ignoreErrors: false` explicitly for `openbao` and all components that depend on it. Define a per-app required-secret manifest/startup probe that calls the Dapr Secrets API, validates required keys without logging values, and gates application readiness. Bind pod/resource readiness to both sidecar readiness and that application probe. Distinguish admission/deployment validation from runtime startup failure; neither should be claimed without corresponding deployment and test evidence.

## Medium Findings

### M1 — “Diagnostics identify only the configuration key” is not a Dapr/OpenBao guarantee

**Evidence:** Dapr's public component contract does not promise configuration-key-only errors. The v1.18.0 Vault component source includes the configured token mount path in file-read errors, and transport/provider errors are produced below EventStore's own diagnostic boundary. The payload specification separately rejects raw provider URI/key identifiers, exception messages, and arbitrary provider-originated details as safe evidence.

**Impact:** The AD currently imposes an unverified guarantee across Dapr and OpenBao logs that application code cannot enforce. Treating it as already provided can create an NFR4/no-leak evidence gap.

**Disposition:** **Tighten and verify.** Limit the constructive “configuration key only” rule to EventStore-owned diagnostics and require Dapr/OpenBao log level, sink access, retention, redaction, and failure-injection evidence as an operations control. Test bootstrap-file, TLS, authorization, missing-path, and unavailable-provider failures to prove that credentials and secret values never appear. If paths/endpoints are also classified, explicitly test and suppress them at the log pipeline rather than assuming the component does so.

### M2 — OpenBao server-version fit is intentionally deferred but must gate production

**Evidence:** Dapr's current OpenBao page says the Vault component was tested and confirmed to work and that the same metadata is compatible, but it does not publish an OpenBao server-version compatibility matrix. AD-24 deliberately defers the OpenBao server release and topology.

**Impact:** The architecture can select OpenBao at spine altitude, but no particular OpenBao release, upgrade path, KV engine configuration, or production topology is yet demonstrated against Dapr runtime 1.18.0. Calling the integration production-ready before that closure would exceed the official evidence.

**Disposition:** **Keep deferred with a hard revisit condition.** The deployment-hardening story must pin a supported OpenBao release/image digest, KV engine/path/value mode, HA/unseal/backup posture, and a Dapr 1.18.0 integration/conformance lane. Re-run this currentness check on every Dapr/OpenBao minor upgrade. Do not copy Dapr's illustrative OpenBao sample (`http` plus `skipVerify: true`) into non-development environments.

## Verified Without Finding

- OpenBao exists as a current Linux Foundation project and Dapr currently documents it as a supported Stable v1 secret store since runtime 1.16.
- `secretstores.hashicorp.vault` with `version: v1` is the correct Dapr component type for OpenBao; `openbao` is a valid architecture-selected component name and matches Dapr's example.
- Dapr components may use `auth.secretStore: openbao` and `secretKeyRef`; application code may retrieve secrets through the Dapr Secrets API without importing an OpenBao/Vault client.
- Component `scopes` restrict loading/access to named app IDs. Application Configuration secret scopes with `defaultAccess: deny` plus `allowedSecrets` are valid and are materially needed because access otherwise defaults to allow. Each sidecar must actually be bound to the intended Configuration resource.
- Dapr's Vault component defaults `skipVerify` to false and exposes `caPem`, `caPath`, `caCert`, and `tlsServerName`; requiring an HTTPS `vaultAddr` with verification enabled is supported. Production manifests should state `skipVerify: false` explicitly and provide the required trust material/server name.
- OpenBao policies are deny-by-default and can constrain a token to exact KV paths/capabilities, so least-privilege access is technically feasible once the bootstrap/token-lifecycle mechanism is fixed.
- Separating Dapr/OpenBao operational/application secrets from the proposed Azure Key Vault Premium RSA-HSM payload-protection backend is technologically coherent and preserves the payload specification's explicit rejection of Dapr secret stores for KEK custody.

## Required AD-24 Corrections Before Gate Pass

1. Remove the false “approved payload-protection specification” assertion and preserve its exact authorization gate.
2. Separate the Dapr runtime floor/pin from the 1.18.4 .NET SDK package pin; bind every production runtime to `>=1.16` with current supported-version evidence.
3. Bind a concrete bootstrap token and rotation/reload lifecycle; keep root/committed/recursive credentials forbidden.
4. Require `ignoreErrors: false` and an application required-secret startup/readiness probe instead of asserting that Dapr checks all required application secrets automatically.
5. Narrow the diagnostics claim and require sidecar/provider log no-leak evidence.
6. Keep OpenBao server release/topology deferred only until the named deployment-hardening gate pins and integration-proves them.

After these corrections, the OpenBao selection itself can pass the technology-currentness lens without changing AD-23's payload-protection ownership or proposed backend choice.
