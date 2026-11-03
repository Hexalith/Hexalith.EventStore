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
- [x] `docs/architecture/dapr-infrastructure-exceptions.yaml`, `docs/concepts/dapr-infrastructure-boundary.md` — record only the accepted operation/path disposition and review trigger; keep unresolved guarantees explicit.
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


## Local implementation handoff — 2026-11-03

Local source work is present for review, but Story 8.6 remains **in-progress**. The approved diagnostic gate found one unresolved failure, and no Azure environment authority or live conformance run exists. The unchecked implementation/test tasks above are not acceptance claims. No 8.7, production traffic, Server enablement, publication, release or G5 authority follows.

Implemented the non-packable companion and two non-packable test projects; exact-version metadata/profile validation; fixed remote RSA-OAEP-256 POSTs using API 2025-07-01; owned bounded request/response buffers; typed outcomes; monotonic current-selection freshness; retained-target wrapping; conservative Ambiguous reconciliation; and a fresh fixed-time readiness probe. Registration is passive and starts no hosted service or Azure request. Story 8.7 must explicitly invoke readiness together with PF-02/lifecycle/fence checks only for an Enabled engine. Contracts, Server, Parties, Interaction code, frozen authority/goldens and the 14-package release inventory are unchanged.

The source-bound register records only the approved scoped SDK exception and correction. Builds has exactly the two approved catalog additions, Core 1.64.0 and Keys 4.10.2; Identity remains 1.21.0. Catalog SHA-256 after those additions is `ccb8ba5c57c744bb9d29060c6e8bf7b930a765d2ee81ab7de42995031f0f6aa7`. The changes remain uncommitted in the owning repositories; no Git history, branches or pointers were mutated.

The standard HttpMessageInvoker and direct DelegatingHandler paths were rejected by loopback observers: the former emitted RequestStart with the URI/path and the latter still emitted ConnectionEstablished with the host. The owned transport instead uses bounded HTTP/1.1 over platform-validated TLS, no redirects/proxy/decompression, strict framing and exact origin/path checks. This is new security-sensitive transport code and has not been exercised against Azure. The named SDK/HTTP observers do not certify raw OS/network EventSources or dump/swap policy.

The pinned SDK also refreshes tokens in background and MSAL can retry token HTTP independently of the ordinary retry options. The credential wrapper preserves actual token expiry, sets RefreshOn to that expiry, and awaits foreground acquisition. An invocation-owned active scope permits one identity request, stops hidden retry work on typed failure, refuses missing/closed scopes and links cancellation. Both metadata and crypto cache paths have short-lived token/cancellation tests; an isolated subprocess using the real pinned managed-identity SDK and a loopback 503 endpoint verifies one physical identity request. Crypto CAE challenge replay is explicitly refused.

### Diagnostic blocker requiring disposition

The retained test `AzureManagedIdentityRetryTests.V081_ManagedIdentityHasOnePhysicalRequestPerAcquisition` fails its strengthened SDK observer assertion. With all applicable public flags disabled (`IsLoggingEnabled`, `IsLoggingContentEnabled`, `IsDistributedTracingEnabled`, `IsTelemetryEnabled`, `IsAccountIdentifierLoggingEnabled`, `IsUnsafeSupportLoggingEnabled`), a fake selected user-assigned client ID is emitted by **Azure-Identity.LogMsalAlways twice** and **Azure-Identity.ManagedIdentityCredentialSelected once**. All three matches are the public client identifier; the identity-header canary is false for every match. This reproduction observed no access token, identity-header secret, vault URI or key URI leak. It used only a loopback identity fixture and made zero Azure calls.

Exact pinned API/decompiled-source inspection finds `Azure.Identity.AzureIdentityEventSource.LogMsalAlways(string)` calls `WriteEvent(24, message)` directly. `ManagedIdentityCredentialSelected(string, string)` checks only EventSource informational enablement and calls `WriteEvent(26, credentialType, id)`. These event methods have no per-client diagnostic-option check. The inspected public credential diagnostic surface supplies no additional suppression beyond the flags already set. The observed raw identifier conflicts with the safe-fingerprints-only diagnostic boundary; it has not been exempted, user-assigned identity support has not been removed, and no reflection/global-event workaround or package substitution was made. The failing sentinel blocks local completion and the workflow's dependent authorized-service job. Owner disposition is outstanding.

For net10.0, Azure.Identity 1.21.0 is a facade forwarding identity types into Azure.Core 1.64.0; the inspected emitter implementations are therefore in Core's package/source identity `254a1b693af968afee40958e77d0762b44a1b13c`. Identity retains source identity `f7400e4471111bf7f2154834022786119acbdd77`; Keys retains `94d96d1ca05657c51326df85d11ec829a89b85fd`. Existing transitive MSAL is Microsoft.Identity.Client 4.92.0; no additional direct package reference or pin was added.

### Verification commands and observed results

Source base is `b46fa93e2926c9be39096ae76e920d38d338db8d` plus the uncommitted local changes. The owning original baseline remains unchanged. SDK 10.0.403; runtime 10.0.14; runtime source identity `0427b9abbead3fa64c6655eb4290c537397131fa`. Commands ran locally from the repository root. There is **no CI run URL**.

| Exact command | Result |
|---|---|
| `python3 /tmp/story86-catalog-compat-yy973wev/verify-authority.py` followed by correction-marker SHA-256 verification before edits | PASS: 12 original approval/frozen/predecessor/catalog/gitlink/correction identity checks |
| `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive`; `aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive --format Json`; `aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` | Exit 0 each; lifecycle baseline inspected and stopped; not a whole-topology readiness proof |
| `dotnet build tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` | PASS, zero warnings/errors |
| `dotnet tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests.dll` | 138 total: **137 passed, 1 failed**, zero skipped; SDK identity-event sentinel above remains failing |
| `dotnet build tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` | PASS, zero warnings/errors |
| `dotnet tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests.dll -preEnumerateTheories -list tests` | 12 discovered, **not executed**: 11 healthy/boundary cases plus one prepared-real-fault case |
| `dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1`; `dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll` | PASS: 1,021 total, zero failed/skipped |
| `dotnet build tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1`; `dotnet tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests.dll` | PASS: 31 total, zero failed/skipped; real isolated Dapr PF-02/lifecycle lane, fake crypto backend |
| `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll -method Hexalith.EventStore.PayloadProtection.Tests.AadPathTests.V029_InertPlatformNormalizer_FailsClosedThroughCoreWriteSeams` | PASS: 1 total, zero failed/skipped |
| `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1`; `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests` | PASS: 149 package/lane/workflow guards, zero failed/skipped |
| `dotnet build src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` | PASS: affected existing Azure consumer graph, zero warnings/errors |
| `node scripts/payload-protection/verify-golden-vectors.mjs`; `python3 scripts/payload-protection/verify-golden-vectors.py` | Each PASS V001–V003; original envelope/wrapper hashes preserved |
| `actionlint .github/workflows/payload-protection-azure.yml`; `git diff --check` | PASS |

Resolved graphs are retained at `/tmp/story86-dependencies/{adapter,unit,live}-graph.json`, produced with `UseHexalithProjectReferences=true dotnet list <owning-project.csproj> package --include-transitive --format json --no-restore`. A local CycloneDX 1.6 package inventory of 65 resolved components is `/tmp/story86-dependencies/companion-test-sbom.cdx.json`; it is implementation dependency evidence, not release provenance. The script and package-hash/API/library observations remain beside it. Approved nupkg SHA-256 values for Core, Identity and Keys were reproduced exactly. Selected assembly/file versions are Core `1.64.0.0` / `1.6400.26.52605`, Identity `1.21.0.0` / `1.2100.26.21009`, Keys `4.10.2.0` / `4.1000.226.50206`. Temporary observations are disposable, not a committed evidence packet or CI artifact.

The manually invoked Azure job requires a protected private ephemeral runner group, exact daprd hash, an unexpired source-bound authority file, a matching custody receipt and externally prepared fault windows. It runs healthy and real-fault phases sequentially and fails on skips. Fixtures preserve Azure keys, report isolated Dapr cleanup and retained-key limits, and distinguish synthetic engine boundary outcomes from actual provider status/dispatch observations. These fixtures have only been built/discovered. Real custody, private networking/RBAC, service failures, persisted Azure history, rotation and rollback remain unproven until separate environment authority and an actual successful run are supplied. No live pass count, all-copy erasure claim, production eligibility or successor authorization exists.


### Parent checkpoint and external history reconciliation — 2026-11-03

The earlier statements that all source changes were uncommitted and that no history changed describe the implementation agents' actions and an earlier observation. Another actor advanced this repository through `1aec4a7f466cfae7f590703fbde222d39f00d659` to full current HEAD `692da20b403e9fc5e3bb101660cbaf5dab97862c`; the latter now contains the adapter/test/workflow source. The parent and implementation agents made no commit, push, staging, branch or submodule-pointer mutation. Those external commits, including unrelated Aspire work, are preserved. The owning baseline stays `45d29a03d8be155d2251251a5bc6ba42035b672d`; prior test records describe the source states at execution, not a blanket verification of the new unrelated commit. Builds still has only the authorized two-row working-tree catalog delta.

Parent preflight revalidated the original/follow-on normative digests, frozen block, predecessor/qualification identities and the exact two-row catalog delta against the original catalog hash. The native managed-identity sentinel was independently rebuilt and reproduced. The parent removed its fixed `/tmp` debug-file write, retained the failing assertion, added first-chance observation in the isolated child and kept only safe diagnostic categories in parent failure output. The additional observation still found exactly the three client-ID EventSource matches and no client-ID/header-canary matches in first-chance exception output for this path. Builds pass; the diagnostic assertion remains unsatisfied.

A precise [managed-identity diagnostic disposition candidate](spec-8-6-identity-diagnostics-disposition.md) is prepared for owner decision. It would allow only the configured client identifier in the two named pinned vendor EventSource types. It is not approved or applied. All other diagnostic prohibitions, test obligations and external gates remain. Workflow step 3 acceptance/matrix completion and step 4 review have not been claimed while this sentinel and real Azure conformance remain unresolved.


Parent final commands: `dotnet build tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` passed with zero warnings/errors. `dotnet tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests.dll` after the parent sentinel cleanup/first-chance observation again produced 138 total / 137 passed / one failed / zero skipped. The required failing SDK identifier case is preserved. Logs: `/tmp/story86-parent-build.log`, `/tmp/story86-parent-full-unit.log`. No production code behavior changed during this parent verification.
