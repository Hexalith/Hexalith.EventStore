# Reviewer Gate — 2026-07-19 Rubric Walker

## Verdict

**FAIL — no critical finding, but two high-severity findings must be resolved before AD-24 is a convergent production invariant.** The OpenBao provider choice, DAPR abstraction boundary, least-privilege scoping, TLS posture, bootstrap separation, and AD-23 payload-key-custody separation are directionally sound. The handoff still binds an unsupported runtime claim and a provider/configuration model that cannot be implemented on every production target the brownfield repository currently presents as supported.

## Scope And Method

Reviewed `_bmad-output/planning-artifacts/architecture.md` as a feature-altitude build substrate, concentrating on the 2026-07-19 AD-24 update while walking the entire good-spine checklist. The review traced AD-24 through AD-9, AD-10, AD-12, AD-23, Consistency Conventions, Stack, Structural Seed, Capability Mapping, and Deferred; reconciled Story 7.6 and FR34/NFR4/NFR17; checked current repository deployment guidance and runtime pins; and verified named technology against official Dapr and Azure Container Apps documentation.

## High Findings

### H1 — AD-24 binds an unsupported Dapr runtime floor and uses a .NET SDK package pin as runtime proof

**Evidence:** AD-24 says Dapr officially supports OpenBao “from runtime 1.16” and that the repository's “DAPR 1.18.4 baseline satisfies that floor” (`architecture.md:305`). The Stack identifies `1.18.4` only as **Dapr .NET SDK packages** (`architecture.md:345`) and then repeats “since DAPR runtime 1.16” for the OpenBao row (`architecture.md:346`). The official [Dapr OpenBao page](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/) says there is no dedicated OpenBao component and that the `secretstores.hashicorp.vault` component has been tested and confirmed compatible; it does not state a 1.16 minimum. The official [Vault component metadata](https://github.com/dapr/components-contrib/blob/master/secretstores/hashicorp/vault/metadata.yaml) establishes that the underlying component is stable v1, but does not establish an OpenBao runtime floor.

Repository reality also keeps runtime and SDK versions separate: `.github/workflows/integration.yml:24` pins runtime `1.18.0`, `deploy/README.md:220`, `:238`, and `:256` use `daprio/daprd:1.18.0`, and live evidence records runtime `1.18.1` alongside .NET packages `1.18.4`. Conversely, the current Kubernetes guide still directs operators to runtime/Helm `1.14.4` (`docs/guides/deployment-kubernetes.md:136-152`).

**Why it matters:** named technology is not verified-current as written, and an implementer can use the SDK row as false evidence that the deployed sidecar/control plane meets a runtime requirement. The Kubernetes guide can also remain compliant with its own instructions while violating AD-24's claimed floor.

**Disposition: autofix plus source reconciliation.** Replace the unsupported “since runtime 1.16” claim with the verified statement: current Dapr documentation confirms OpenBao compatibility through stable `secretstores.hashicorp.vault` v1. Add a separate Dapr runtime Stack row using the actual deployment/CI pin (currently `1.18.0`, with live evidence separately reporting `1.18.1`) and never infer runtime from NuGet packages. Update or explicitly supersede the Kubernetes guide's `1.14.4` instructions before treating AD-24 as finalized.

### H2 — AD-24 is universal across production, but Azure Container Apps does not support the selected component or Dapr secret-scope configuration

**Evidence:** AD-24 requires all production deployment/application secrets to use the `openbao` Dapr component, `auth.secretStore`, `secretKeyRef`, and per-app Dapr `Configuration` secret scopes (`architecture.md:305-307`). The rule and Deferred section do not exempt or retire any production environment (`architecture.md:447`). Brownfield documentation, however, presents Azure Container Apps as a production deployment target and selects managed identity plus Azure Key Vault there (`docs/guides/deployment-progression.md:48-85`, `:128-154`; `docs/guides/deployment-azure-container-apps.md:843-879`).

Current official [Azure Container Apps Dapr support](https://learn.microsoft.com/azure/container-apps/dapr-overview#dapr-components) lists only `secretstores.azure.keyvault` under supported secrets components and says only listed GA/Tier 1/Tier 2 components are supported. The same page lists the Dapr Configuration spec as unsupported. Azure Container Apps also uses its simplified `secretStoreComponent`/`secretRef` schema rather than the Kubernetes `auth.secretStore`/`secretKeyRef` shape documented in AD-24; see [Dapr components in Azure Container Apps](https://learn.microsoft.com/azure/container-apps/dapr-components).

**Two compliant but incompatible deployment slices:**

- A Kubernetes slice implements AD-24 literally with `secretstores.hashicorp.vault`, component scopes, `auth.secretStore`, `secretKeyRef`, and a Dapr `Configuration` carrying default-deny secret scopes.
- An Azure Container Apps slice follows the repository's supported production guide and platform support contract, using managed identity/`secretstores.azure.keyvault`, `secretStoreComponent`, and `secretRef`, with no Dapr Configuration secret scopes.

The first satisfies AD-24 but cannot be supported on Azure Container Apps; the second is platform-valid but violates AD-24.

**Disposition: discuss, then mandatory architecture decision.** Choose one of two coherent boundaries: (1) OpenBao is mandatory only for self-managed Dapr/Kubernetes production while Azure Container Apps retains managed identity/Key Vault as an explicit platform exception with equivalent least-privilege invariants; or (2) OpenBao is mandatory for every production target, in which case Azure Container Apps must be removed from the supported deployment envelope and its guides updated. The spine must name the deployment envelope rather than leaving both interpretations live.

## Medium Finding

### M1 — AD-24 claims to prevent secret-path divergence while explicitly deferring secret paths

**Evidence:** AD-24's `Prevents` includes incompatible “secret paths” (`architecture.md:304`), but the Deferred table leaves production secret paths and rotation policy to deployment stories/templates (`architecture.md:447`). The Rule fixes the provider, component name, access mechanism, component/app scoping, TLS, and bootstrap posture, but no environment-independent path root, namespace convention, or ownership rule.

Two deployment slices can therefore choose incompatible path roots/naming and both comply with the Rule and Deferred. This does not invalidate the OpenBao decision, but the stated divergence prevention is unenforceable.

**Disposition: autofix or consciously defer.** Either add a stable path ownership/naming invariant that all overlays consume, or remove secret paths from `Prevents` and state that deployment templates are the single owner of per-environment path values. Do not claim both prevention and deferral.

## Good-Spine Checklist

| Check | Result | Notes |
| --- | --- | --- |
| Fixes the real divergence points one level down | **Fail** | Provider choice is fixed, but the supported deployment envelope is not; H2 leaves two incompatible production interpretations. |
| Misses no owned divergence point | **Fail** | Runtime identity and provider support per production target remain unresolved. |
| Every AD Rule is enforceable and prevents its stated divergence | **Fail** | H1 uses the wrong kind of version evidence; M1 claims path convergence without a path rule. |
| Deferred contains no divergence-producing architecture decision | **Fail** | Exact secret paths are deferred while AD-24 claims to prevent path divergence. |
| Named technology is verified-current | **Fail** | OpenBao-through-Vault v1 is verified; the 1.16 floor and SDK-as-runtime proof are not. |
| Ratifies rather than contradicts brownfield reality | **Fail** | Current Kubernetes and Azure Container Apps production guidance conflicts with the universal AD-24 rule. |
| Covers driving requirements/spec capabilities | **Pass** | FR34, NFR4, NFR17, and Story 7.6 are mapped; required-secret failure and safe diagnostics land. |
| Does not weaken inherited invariants | **Pass** | No parent spine is inherited; AD-24 strengthens AD-9/AD-10 and explicitly preserves AD-23. |
| Every feature-altitude dimension is decided, deferred, or open | **Partial** | Security/operations are represented, but the environment/provider matrix is neither decided nor openly marked. |

## Positive Compliance Evidence

- The canonical component name and `secretstores.hashicorp.vault` v1 type prevent provider/type drift for supported self-managed Dapr environments.
- Application code remains behind the Dapr Secrets API and cannot introduce direct OpenBao/Vault client dependencies.
- Component scopes, default-deny secret access, verified TLS, out-of-band bootstrap credentials, fail-before-serve behavior, and support-safe diagnostics align with AD-9, AD-10, and AD-12.
- AD-24 explicitly does **not** replace AD-23's payload-protection key custody. This is the right boundary: operational/application secret retrieval and production `pdenc-v2` KEK wrap/unwrap remain separate authorities.
- No other whole-spine contradiction was introduced by AD-24; the blocking defects are runtime evidence and deployment-envelope compatibility.

## Gate Closure Order

1. Decide whether Azure Container Apps remains supported under an explicit Key Vault/managed-identity exception or is removed from the production envelope.
2. Separate Dapr runtime and .NET SDK pins; remove the unsupported 1.16 claim unless a primary release source is added.
3. Reconcile the Kubernetes, Azure Container Apps, deployment-progression, and security guidance with the selected boundary.
4. Resolve the secret-path `Prevents`/Deferred contradiction, then re-run the gate.
