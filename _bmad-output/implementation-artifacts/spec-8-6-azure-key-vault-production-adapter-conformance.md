---
title: 'Story 8.6: Azure Key Vault Production Adapter Conformance'
type: 'feature'
created: '2026-11-03'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '45d29a03d8be155d2251251a5bc6ba42035b672d'
story_key: '8-6-azure-key-vault-production-adapter-conformance'
approval_date: '2026-11-03'
approved_exception_sha256: 'fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-5-lifecycle-protocol-amendment.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-6-dapr-qualification.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-6-crypto-boundary-amendment.md'
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

## Spec Change Log

## Review Triage Log

## Design Notes

Irreversibles: Azure resource/identity/network/key/fault mutations require separate authority. Footprint: companion, two test projects, narrow core access, workflow, solution, boundary documents and two central catalog additions. The SDK pipeline avoids local RSA wrapping; lost wrap responses may stay pending without a new durable journal.

## Verification

Planning preflight: both golden verifiers passed V001–V003; base and lifecycle digests reproduced. The qualification assessment records the isolated API probe. No adapter tests or CI run exist.

After approval, build each affected test project in Debug/source-reference mode; invoke built xUnit v3 assemblies directly. Run the complete existing engine unit/live lanes, invariant V029, package/lane guards and workflow lint. Execute Azure conformance only against its separately authorized environment; record source SHA, commands, pass/fail/skip counts, CI URL and limitations here.
