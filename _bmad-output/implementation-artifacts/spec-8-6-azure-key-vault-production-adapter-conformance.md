---
title: 'Story 8.6: Azure Key Vault Production Adapter Conformance'
type: 'feature'
created: '2026-11-03'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '45d29a03d8be155d2251251a5bc6ba42035b672d'
story_key: '8-6-azure-key-vault-production-adapter-conformance'
approval_date: '2026-11-03'
approved_exception_sha256: 'fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394'
approved_correction_sha256: '360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-5-lifecycle-protocol-amendment.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-6-dapr-qualification.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-6-crypto-boundary-amendment.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-6-azure-core-pin-correction.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The shared engine has no production backend. The pinned Dapr runtime cannot expose the required key operations, and the proposed crypto amendment remains unapproved.

**Approach:** Resolve operation qualification and approve an exact design before adding the non-packable companion adapter. Prove custody, historical reads and failure behavior against an authorized isolated Azure service.

**Owner decision (2026-11-03):** Prepare the scoped Azure SDK exception. This selects the architecture route; exact exception/design approval remains the next checkpoint. Use the attached candidate's remote data-plane design and conservative pending outcome when wrap reconciliation is unknowable.

## Boundaries & Constraints

**Always:** Preserve pdenc-v2/AAD/goldens, exact historical KEK versions, RSA-OAEP-256, ordinary Premium vault and non-exportable provider-generated RSA-HSM-3072. Reuse lifecycle authority, bounded retries, typed outcomes and owned-buffer clearing. Obtain content-bound design/predecessor approval before affected implementation and separate authority before Azure mutations.

**Never:** Infer approval from status; introduce provider dependencies before disposition; use automatic direct-provider fallback, plaintext fallback, production keys/data or a replacement encryption format. Preserve Contracts, Server, Parties, `Interaction*`, frozen authority, release inventory and packability. Provisioning and release are separately authorized.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Required behavior | Failure |
|---|---|---|---|
| Startup | Closed options, identity, current key | Validate profile and fresh 32-byte fixed-time wrap/unwrap probe | Fail readiness on ambiguity; clear probe buffers |
| Wrap/rewrap | Current selection or retained exact target | Bind validated version and owned wrapped bytes | Stale selection blocks new wraps |
| Historical unwrap | Recorded version and wrapped DEK | Use only that exact version; return owned 32-byte DEK | Preserve typed missing/denied/unavailable/consistency outcomes |
| Fault/cancellation | Provider, transport or state uncertainty | Shared total budget, cancellation propagation and clearing | No blind ambiguous replay or inferred absence/deletion |
| Restart/rotation/rollback | Persisted event, snapshot and key history | Read through qualified Dapr state/actor boundaries | Retain historical material and monotonic authority |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.PayloadProtection/PayloadProtectionBackend.cs`, `PayloadBackendResult.cs`, `PayloadBackendFailure.cs`: internal SPI, exact-target wrapping, constructive outcomes and clearing. A companion needs explicit internal visibility; avoid a public Contracts change.
- `PayloadBackendExecutor.cs`, `PayloadKeyResolver.cs`, `KeyReservationCoordinator.cs`, `PayloadKeyRotationCoordinator.cs` in that project: reuse total budgets, leases, retained version and stable reconciliation. Backend lookup failure cannot mean affirmative mutation absence.
- `PayloadProtectionCapabilityProbe.cs`: state-only qualification; does not prove Azure readiness. Preserve its PF-02 checks.
- `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/`: reuse isolated Dapr fixture and logical persisted-state assertions; its fake crypto backend supplies no Azure custody proof.
- `docs/architecture/dapr-infrastructure-exceptions.yaml`: crypto row unresolved. The October crypto draft stays historical/unapproved. The new candidate specifies exact SDK transport, paths, versions and the two proposed Builds catalog additions.

## Tasks & Acceptance

**Execution, in dependency order:**

- [x] `spec-8-6-crypto-boundary-amendment.md` beside this spec — obtain exact-content exception/design approval and record predecessor disposition; preserve original approval bytes. Its named central catalog additions require explicit scope approval before submodule edits.
- [ ] `docs/architecture/dapr-infrastructure-exceptions.yaml`, `docs/concepts/dapr-infrastructure-boundary.md` — record only the accepted operation/path disposition and review trigger; keep unresolved guarantees explicit.
- [ ] `src/Hexalith.EventStore.PayloadProtection.AzureKeyVault/` — add non-packable project, backend, closed options, validation and readiness types under the approved design; give the companion narrowly scoped internal access in the core project.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/` — cover the matrix, version/profile mismatches, typed failures, combined budgets, cancellation, zeroing and leak sentinels.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests/`, `.github/workflows/payload-protection-azure.yml` — add explicitly invoked authorized-service qualification, persisted history/fault/rotation/rollback checks, cleanup and retained-key reporting. Register projects in `Hexalith.EventStore.slnx` and lane guards in `ReleasePackageManifestTests.cs` without changing the release set.

**Acceptance Criteria:**

- Given required design and predecessor decisions, when preflight compares exact identities, then missing or changed authority blocks affected implementation.
- Given the authorized real Premium/HSM/private-network environment, when the complete conformance suite runs, then custody, persisted history, faults, compatibility, buffer ownership and rollback pass with exact versions and limitations recorded; mocks cannot close this gate.
- Given completion review, when source, commands, counts, CI and named owner decisions are assessed, then only explicit 8.7 authorization may follow; production traffic, release and G5 remain separately gated.

## Implementation Notes

- 2026-11-03: The interactive project owner, Administrator, answered “approve and stop” to the checkpoint covering this specification, the exact scoped Azure SDK exception and its two named Hexalith.Builds catalog additions. Approved exception normative SHA-256: `fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394`. Approved frozen-block SHA-256, including tag lines: `7fe1ee3af25c7199c16a510fc9e5cac210581fe94ef5030a364c04525c86cbff`. Both byte ranges are locked. This is a single-maintainer-attested owner decision, not independent review.
- Predecessor disposition: accept the candidate's exact bound 8.4/8.5 evidence for additive companion development, subject to its required affected regression checks; no CI or Azure conformance is inferred. Approval includes only Azure.Core 1.54.0 and Azure.Security.KeyVault.Keys 4.10.2 catalog additions; Azure.Identity 1.21.0 remains pinned. No catalog change has been applied. On implementation resume, record the accepted exception in the boundary register before introducing dependencies.
- Stop handoff: specification and sprint tracking are `ready-for-dev`; implementation has not begun. Resume this exact spec with bmad-build to enter implementation. Azure resource/identity/network/key/fault mutations, live conformance, production traffic, release, Server enablement and G5 retain their separate gates. Source HEAD at approval was `b18a07c8b19fcdfe38678c94ad34766720f7b979`; the original baseline is preserved and all incorporated approval subjects still match.
- Resume preflight, 2026-11-03: implementation stopped before dependency introduction. The approved Azure.Core 1.54.0 central addition produces NU1109 against the existing Aspire.Hosting.Azure.AppContainers 13.6.4 dependency, which requires Azure.Core >=1.62.0. The unchanged graph resolves 1.64.0: Azure.ResourceManager.KeyVault 1.5.0 requires >=1.63.0 and Azure.Security.KeyVault.Secrets 4.12.0 requires >=1.64.0. Amendment section 4 requires this incompatible graph to return to review. No adapter, accepted-register, catalog, solution, workflow or runtime change was applied. The approved normative range, frozen block, predecessor identities and catalog remain unchanged.
- An isolated alternate catalog using Azure.Core 1.64.0 restores successfully with versionless direct references to Azure.Core, Azure.Identity, Azure.Security.KeyVault.Keys and Aspire.Hosting.Azure.AppContainers. This is compatibility evidence only. Selecting 1.64.0 requires source/API/package/hash review and a new exact-content owner decision covering the revised exception and central catalog edit, while retaining the original approval subject. Neither the successful restore nor this implementation request authorizes replacing the approved 1.54.0 pin. Azure environment authority and live conformance remain separately outstanding.

- Dependency correction approved on 2026-11-03 by Administrator through “approve”: [Azure.Core pin correction](spec-8-6-azure-core-pin-correction.md), normative SHA-256 `360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e`. This supersedes only the original Core 1.54.0 dependency/package/source identities with Core 1.64.0 and expressly authorizes continuation of additive local implementation and the revised Builds catalog additions. Original approval bytes and predecessor disposition are preserved. The approved context files now jointly govern implementation; no further local dependency approval is required. Azure mutations and live conformance still lack authority.

## Spec Change Log

## Review Triage Log

## Design Notes

Irreversibles: Azure resource/identity/network/key/fault mutations require separate authority. Footprint: companion, two test projects, narrow core access, workflow, solution, boundary documents and two central catalog additions. The SDK pipeline avoids local RSA wrapping; lost wrap responses may stay pending without a new durable journal.

## Verification

Planning preflight: both golden verifiers passed V001–V003; base and lifecycle digests reproduced. The qualification assessment records the isolated API probe. No adapter tests or CI run exist.

After approval, build each affected test project in Debug/source-reference mode; invoke built xUnit v3 assemblies directly. Run the complete existing engine unit/live lanes, invariant V029, package/lane guards and workflow lint. Execute Azure conformance only against its separately authorized environment; record source SHA, commands, pass/fail/skip counts, CI URL and limitations here.

Resume preflight source: `b46fa93e2926c9be39096ae76e920d38d338db8d`; .NET SDK `10.0.403`. All probes below ran locally from the repository root. Scratch inputs, restore logs and selected dependency graphs are retained at `/tmp/story86-catalog-compat-yy973wev/`; these are disposable local observations, not a committed evidence packet or CI artifact.

Each isolated project targets net10.0 with central package management and transitive pinning enabled. `control/` copies the exact approved current catalog and references only the existing Aspire.Hosting.Azure.AppContainers. `candidate/` adds only the two approved catalog rows, retaining the same project. `alternate/` changes only the candidate catalog's Azure.Core pin to 1.64.0 and adds versionless direct Azure.Core, Azure.Identity and Azure.Security.KeyVault.Keys references to exercise the proposed companion dependency graph alongside Aspire. Tracked repository dependency/configuration files were never edited by these probes.

| Exact command | Result | Retained observation |
|---|---|---|
| `python3 /tmp/story86-catalog-compat-yy973wev/verify-authority.py` | PASS; 11 identity checks: base full/normative, lifecycle/effective, approved exception, owning frozen block, both predecessor specs, qualification, catalog and Builds gitlink | `verify-authority.py`, `authority.log`; all asserted identities are recorded in the approved exception |
| `dotnet restore /tmp/story86-catalog-compat-yy973wev/control/CatalogCompatibility.csproj --disable-parallel -p:NuGetAudit=false` | Exit 0; resolves Azure.Core 1.64.0 and Azure.Identity 1.21.0 | `control-restore.log`, `control-selected-graph.json`, `control/obj/project.assets.json` |
| `dotnet restore /tmp/story86-catalog-compat-yy973wev/candidate/CatalogCompatibility.csproj --disable-parallel -p:NuGetAudit=false` | Exit 1; NU1109: Azure.Core downgraded from 1.62.0 to centrally defined 1.54.0; `CatalogCompatibility -> Aspire.Hosting.Azure.AppContainers 13.6.4 -> Azure.Core (>= 1.62.0)` conflicts with `CatalogCompatibility -> Azure.Core (>= 1.54.0)` | `candidate-restore.log`, `candidate-selected-graph.json`, `candidate/obj/project.assets.json` |
| `dotnet restore /tmp/story86-catalog-compat-yy973wev/alternate/CatalogCompatibility.csproj --disable-parallel -p:NuGetAudit=false` | Exit 0; resolves Azure.Core 1.64.0, Azure.Identity 1.21.0 and Azure.Security.KeyVault.Keys 4.10.2 | `alternate-restore.log`, `alternate-selected-graph.json`, `alternate/obj/project.assets.json` |
| `node scripts/payload-protection/verify-golden-vectors.mjs` | Exit 0; PASS V001–V003; Node v26.4.0 / OpenSSL 3.5.7 | `node-goldens.log` |
| `python3 scripts/payload-protection/verify-golden-vectors.py` | Exit 0; PASS V001–V003; Python 3.14.4 / cryptography 46.0.5 / OpenSSL 3.5.5 | `python-goldens.log` |

Preflight totals: two successful restores, one rejected approved candidate, 11 passing identity checks and three passing golden vectors in each independent toolchain. No adapter tests exist or ran; existing engine/live/package/workflow regression lanes remain unrun because affected implementation stopped at dependency preflight. No CI run URL, Azure calls/mutations, custody proof or successor authorization exists for this resume.
