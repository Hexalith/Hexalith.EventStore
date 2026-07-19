# Reviewer Gate — Technology And Currentness Re-review (2026-07-19 AD-24)

Verdict: **pass**. The tightened AD-24 resolves every prior critical/high technology-currentness finding. Its OpenBao/Dapr selection, runtime evidence, token lifecycle, required-secret readiness contract, Azure Container Apps exclusion, and separation from payload-protection key custody are consistent with current primary sources and repository reality. No critical or high finding remains.

## Re-review Scope

Re-reviewed `_bmad-output/planning-artifacts/architecture.md:302-316`, the Stack at lines `352-354`, the Secrets convention, and the Deferred row against the findings in `review-update-2026-07-19-technology-currentness.md`.

Primary sources rechecked:

- [Dapr OpenBao reference](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/) and [supported secret-store catalog](https://docs.dapr.io/reference/components-reference/supported-secret-stores/)
- [Dapr HashiCorp Vault component reference](https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/) and [v1.18.0 component source](https://github.com/dapr/components-contrib/blob/v1.18.0/secretstores/hashicorp/vault/vault.go)
- [Dapr component secret references](https://docs.dapr.io/operations/components/component-secrets/), [component scopes](https://docs.dapr.io/operations/components/component-scopes/), and [secret scopes](https://docs.dapr.io/developing-applications/building-blocks/secrets/secrets-scopes/)
- [Dapr sidecar health](https://docs.dapr.io/operations/resiliency/health-checks/sidecar-health/), [component schema](https://docs.dapr.io/reference/resource-specs/component-schema/), and [component reload behavior](https://docs.dapr.io/operations/components/component-updates/)
- [Dapr runtime/SDK support policy](https://docs.dapr.io/operations/support/support-release-policy/) and [.NET SDK v1.18.4 release](https://github.com/dapr/dotnet-sdk/releases/tag/v1.18.4)
- [Azure Container Apps managed-Dapr overview and limitations](https://learn.microsoft.com/en-us/azure/container-apps/dapr-overview) and [Azure Container Apps Dapr component schema](https://learn.microsoft.com/en-us/azure/container-apps/dapr-components)
- [OpenBao policies](https://openbao.org/docs/2.5.x/concepts/policies/), [auth methods](https://openbao.org/docs/auth/), and [TCP listener TLS](https://openbao.org/docs/2.5.x/configuration/listener/tcp/)

Repository evidence rechecked:

- `.github/workflows/integration.yml:23-24,55-58` pins Dapr runtime `1.18.0` for the live-sidecar lane.
- `deploy/README.md:217-256` uses `daprio/daprd:1.18.0` as the deployment seed.
- `references/Hexalith.Builds/Props/Directory.Packages.props:138-145` independently pins Dapr .NET SDK packages `1.18.4`.
- `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md:4-6,23-29,1567-1591,1679-1685` remains draft, proposed, and not authorized.
- The same specification at lines `969-997` proposes Azure Key Vault Premium RSA-HSM custody and explicitly rejects Dapr secret stores for production `pdenc-v2` KEK custody.

## Prior Finding Resolution

### C1 — Payload-protection approval status: resolved

AD-24 now says it “does not approve, replace, or modify” AD-23 or the **draft-not-authorized** specification's **proposed** Azure Key Vault Premium RSA-HSM backend. That matches the artifact's current frontmatter, pending approval record, ADR disposition, and Story 8.2 authorization state.

The boundary also remains technically correct: OpenBao supplies operational/application secrets only; the proposed payload adapter uses direct managed-identity Azure Key Vault key operations, and the security specification rejects Dapr secret stores as KEK custody. AD-24 no longer creates either an approval shortcut or a competing key-custody authority.

### H1 — Runtime versus SDK evidence: resolved

AD-24 and the Stack now distinguish:

- the OpenBao component floor: Dapr runtime `>=1.16`;
- the repository CI/deployment runtime seed: `1.18.0`; and
- Dapr .NET SDK packages: `1.18.4`, explicitly not runtime evidence.

Those claims match Dapr's catalog, support model, the official SDK release, the integration workflow, deployment seed, and central package props. Requiring each production profile to pin a compatible 1.18.x runtime closes the earlier gap in which a NuGet version could be mistaken for sidecar evidence.

### H2 — Bootstrap-token acquisition and rotation: resolved at architecture altitude

The rule now correctly states that the Vault-compatible component reads its token during initialization and does not assume automatic renewal or file reload. It places the token outside the Dapr/OpenBao dependency graph, prohibits committed inline tokens, and binds planned rotation to a controlled sidecar rollout/restart before expiry or revocation. Emergency replacement holds readiness false until sidecars restart with the new credential.

This matches Dapr v1.18.0's implementation, which accepts either `vaultToken` or `vaultTokenMountPath`, materializes the token at component initialization, and reuses it. Exact hosting-specific acquisition, TTL, and maintenance-window values remain deferred to the single platform overlay, with a clear revisit condition; independently built application/component slices cannot invent another lifecycle.

### H3 — Required-secret startup/readiness behavior: resolved

AD-24 no longer attributes proactive logical-secret validation to Dapr itself. Instead it requires every host to validate its declared contract through the Dapr Secrets API before becoming ready, and it distinguishes:

- missing store/bootstrap/`startup-only` inputs, which block deployment or startup; and
- `runtime-required` lookup failures, which fail closed, disable the dependent operation, and hold readiness false until a bounded successful recheck.

This is implementable with Dapr's outbound health/readiness sequence and application health checks. The canonical value-free contract gives the host an exact set to probe, while the platform overlay owns component/configuration composition and validation. Dapr component initialization remains fail-closed by default (`ignoreErrors: false`); even if a future manifest accidentally weakens that default, the required host probe and deployment validation still prevent readiness, so the architecture does not depend on an undocumented eager-secret check.

### M2 — OpenBao server-version fit: resolved as a bounded deployment gate

The spine does not invent an unsupported OpenBao version matrix. It defers the server release, HA/storage topology, endpoint, KV engine/prefix, bootstrap acquisition/TTL, and maintenance window to deployment hardening, while requiring a real-OpenBao release-evidence lane and preserving stable logical names, map shape, scopes, ACL paths, and lifecycle across environments.

That is the correct architecture altitude: Dapr currently certifies OpenBao via the Vault component without publishing a server-version matrix, so production deployability must be proved against the selected server release rather than asserted generically.

## Azure Container Apps Verification

AD-24's Azure Container Apps statement is current and correctly bounded.

Microsoft's managed-Dapr documentation says:

- only the Dapr APIs and Tier 1/Tier 2 components in its published matrix are supported;
- OpenBao and `secretstores.hashicorp.vault` are absent from that component matrix; and
- “Dapr Configuration spec: Any capabilities that require use of the Dapr configuration spec” are a managed-Dapr limitation.

Dapr secret scopes live in the per-application Dapr `Configuration` resource, so AD-24's required `defaultAccess: deny` plus `allowedSecrets` policy cannot be represented by the conforming managed-Dapr profile described by Microsoft. Azure Container Apps does support component `scopes` and component secret references, but those do not replace per-secret Configuration scopes.

The spine therefore correctly says **Azure Container Apps managed Dapr** is not currently an AD-24-conforming production profile. It does not claim that Azure Container Apps can never host such a topology; it requires a separately approved future profile with actual OpenBao component support and equivalent least-privilege scoping before compliance can be claimed. Managed-Dapr also controls its runtime rollout rather than giving this repository the explicit runtime pin AD-24 requires, reinforcing the exclusion.

## Remaining Medium Finding

### M1 — Configuration-key-only diagnostics remain an implementation evidence obligation

AD-24 still says diagnostics identify only the logical configuration key. This is a valid architecture requirement, but it is not a guarantee supplied by the Dapr/OpenBao components: current Dapr Vault errors can include non-secret operational details such as a configured token mount path, and provider/transport logs are outside EventStore's formatter.

**Disposition:** **Non-blocking at spine altitude.** Treat the statement as a required constructive output contract for EventStore-owned diagnostics and as an operations verification obligation for Dapr/OpenBao logs. The deployment-hardening and real-OpenBao lane must exercise bootstrap-file, TLS, denied-policy, missing-path, expiry, and provider-unavailable failures; verify sink access/retention/redaction; and prove no credential or secret value is emitted. If paths/endpoints are classified, suppress them in the log pipeline rather than assuming upstream components do so.

This does not reopen a critical/high architecture hole because AD-24 already assigns ownership to the platform overlay, requires a real integration lane, and fixes the externally safe diagnostic identity. It does remain required evidence before any profile is called production-conforming.

## Gate Result

- Critical findings: **0**
- High findings: **0**
- Medium findings: **1**, implementation/operations evidence only
- Technology-currentness recommendation: **pass AD-24 for architecture handoff**, retaining the named deployment-hardening and real-OpenBao verification gates.
