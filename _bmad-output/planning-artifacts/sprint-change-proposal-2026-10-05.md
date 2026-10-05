# Sprint Change Proposal: Dapr Infrastructure Boundary

Date: 2026-10-05  
Project: Hexalith.EventStore  
Requested by: Administrator  
Status: Initial planning/documentation slice applied — runtime qualifications and detached crypto-amendment approval remain pending
Review mode: Incremental, selected by the user after initial draft preparation  
Scope: Moderate — direct adjustment within existing epics

## 1. Issue Summary

The owner requires EventStore to use the Dapr layer whenever it can provide the required infrastructure capability, and to avoid direct infrastructure dependencies when a suitable Dapr component exists. This applies across the project, beyond the existing Story 6.6 amendment.

The trigger is the owner's architectural direction on 2026-10-05. Story 6.6 is the immediate implementation context, not the limit of the policy. The correction strengthens an existing architecture principle rather than changing the event-sourcing product goal.

### Evidence and baseline

Inspected checkout: `main`, HEAD `f1662b9c09c06223a06b620b4cfc6efdd6e8952a`, with pre-existing uncommitted Story 6.6 source and test changes. Findings describe that working tree, not a clean release baseline. This proposal does not certify runtime behavior.

| Evidence | Finding | Disposition |
| --- | --- | --- |
| [Architecture AD-1](architecture.md#ad-1---dapr-backed-hexagonal-event-sourcing-adopted) and [Story 6.6 amendment](../implementation-artifacts/story-6-6-dapr-only-amendment.md) | Core state, actors, pub/sub and invocation already use Dapr. The explicit prohibition on application SQL is scoped to Story 6.6. | Generalize the infrastructure boundary while retaining the stricter actor-ownership rules. |
| [Story 6.6 implementation spec](../implementation-artifacts/spec-6-6-event-versioning-and-upcasting-implementation.md) | Records removal of the SQL adapter/Npgsql footprint; shared consumer integration and qualification remain open. | Continue the Dapr design; do not reopen the withdrawn SQL solution or claim completion. |
| [Epics, Story 8.6](epics.md) and [payload-protection spec, sections 5 and 11](../implementation-artifacts/spec-shared-payload-protection-engine.md) | Explicitly prescribe direct `Azure.Identity`, `Azure.Security.KeyVault.Keys`, and `CryptographyClient` integration. | Replace the unconditional SDK choice with Dapr capability qualification and an explicit gap decision. |
| [SignalR registration](../../src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs) | `ConfigureBackplane` parses a Redis connection string and calls `AddStackExchangeRedis`. Both EventStore and Gateway projects reference that integration. | Existing runtime dependency requiring assessment, not automatic deletion. |
| [SignalR proof](../../tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs) | Exercises delivery between two application processes with a Redis backplane. | Preserve cross-instance behavior in any replacement. Existing test code is not evidence that this run passed. |
| [Architecture overview](../../docs/concepts/architecture-overview.md) | Says every infrastructure connection uses Dapr and every backend switch needs only YAML. | Qualify those claims against existing exceptions and component semantics. |
| [PRD](prd.md), [epics](epics.md), [architecture](architecture.md), [UX](ux.md) | No uniform operation-level decision rule or project-wide enforcement story. | Add the rule, owners, acceptance criteria and verification. |

A source/package-reference scan found no current Npgsql dependency in the EventStore production projects and no implemented Azure Key Vault adapter. Direct Redis dependencies in tests and Aspire resource integrations also exist; their purpose must be classified rather than treated as production persistence clients.

Planning input byte digests observed during analysis:

- `prd.md`: `b3febbf2794c5cfca07c4a12ba0edd25a8f2b5e1a25b99651accf83e42182262`.
- `epics.md`: `acab4578821b4843ac10ea6a24de5231e3e3503a11618fd89d46c84d47f8e762`.

These are observation identifiers, not approval receipts.

### Dapr capability evidence

Official documentation checked on 2026-10-05:

- Dapr provides `crypto.azure.keyvault`. This establishes a candidate abstraction, not conformance to EventStore's frozen encryption contract. [Azure Key Vault component](https://docs.dapr.io/reference/components-reference/supported-cryptography/azure-key-vault/).
- The documented encryption interface uses Dapr Crypto Scheme v1; it cannot simply replace `pdenc-v2`. Qualification must inspect the exact runtime/SDK APIs for required key operations and preserved wire semantics. [Cryptography overview](https://docs.dapr.io/developing-applications/building-blocks/cryptography/cryptography-overview/), [API reference](https://docs.dapr.io/reference/api/cryptography_api/).
- `bindings.azure.signalr` is an output binding with broadcast, group and user targeting. Its existence does not establish equivalence to a self-hosted ASP.NET Core SignalR Redis backplane, including authenticated connections, negotiation and group membership. [SignalR binding](https://docs.dapr.io/reference/components-reference/supported-bindings/signalr/).
- State-store support varies by component, including concurrency and transaction support. Select and test the required capabilities for each supported profile. [State component matrix](https://docs.dapr.io/reference/components-reference/supported-state-stores/).

The equivalence cautions above are analysis conclusions. No live cryptography or alternate SignalR conformance test was performed for this proposal, and no new runtime/package version is selected here.

## 2. Impact Analysis

### Epic and story impact

| Epic | Impact and proposed work |
| --- | --- |
| 1 — Domain services | Preserve completed SDK work. Apply the common boundary to future changes in read models, batching, projection lifecycle and domain hosting. Domain guardrails in 1.11 are useful inputs but do not cover the whole platform. |
| 2 — Integration surfaces | Add Story 2.13 to qualify Dapr-based notification distribution and resolve the existing Redis backplane dependency. Preserve Story 2.8's public notification contracts. |
| 3 — Release and repository | Add Story 3.17 for dependency inventory, explicit exceptions and automated boundary enforcement. Story 3.16 dependency refresh must consult it before adding/upgrading provider integrations. |
| 4 — Event integrity | Keep aggregate actor mutation ownership, admission fences, outbox recovery and the unproven provider write-once gate. Dapr use alone does not solve the observed append race. No story rollback. |
| 5 — Security and topology | Stories 5.6–5.9 carry qualified component scopes, profiles, credentials and deployment parity. Dapr transport does not replace application authorization. |
| 6 — Cost and evolution | Story 6.6 remains in progress under its existing Dapr-only amendment. General policy cannot reauthorize the withdrawn provider SQL, private actor keys or provider-attestation claims. |
| 7 — Operations | Preserve 7.6 OpenBao/Secrets API and 7.8 resiliency direction. Story 7.11 evidence helpers must distinguish logical application readback from backend diagnostics. Admin access remains typed and authorized. |
| 8 — Payload protection | Revise the adapter selection in 8.6 after a reviewed successor/amendment to the frozen spec. Assess effects on 8.3–8.5 and rebind affected 8.7–8.11 evidence. Keep this work post-MVP. |
| 9 — Readiness governance | Map the new work to the existing corrective-work and gate processes; no new epic and no readiness pass inferred from this proposal. |

No epic is obsolete. Prioritize the policy and Story 3.17 before new infrastructure decisions; perform the Story 2.13 assessment before extending the backplane; resolve the payload adapter decision before Story 8.6 implementation. The existing Story 6.6 work can follow its already authorized Dapr constraint.

### Artifact and technical impact

- PRD: add a project-wide constraint and acceptance evidence; clarify FR37's backend selection without changing its encryption or compatibility requirements.
- Architecture: extend AD-1; clarify AD-3's operational-state reads; add qualification language to AD-23 and AD-26; show Dapr on infrastructure edges and label any accepted exception.
- Epics and story specs: add 2.13/3.17 and their traceability, update 8.6 selection criteria, carry the rule into future infrastructure changes, and reconcile affected predecessor evidence.
- Sprint tracking: add only the new backlog entries after approval; preserve current completion and in-progress states. Record dependencies without marking any qualification complete.
- Documentation: correct unconditional portability claims and document supported component capabilities and exceptions.
- UX: no new screen or route. `/projections`, `/dapr`, `/services` and `/health` continue to show existing evidence-based states. Transport failure must preserve unavailable/stale behavior and tenant-safe group delivery.
- Deployment: provider-specific provisioning and component YAML remain necessary. Move runtime provider credentials and configuration behind Dapr where qualified. Update AppHost, profiles, scopes, health and tests together for any selected replacement.
- Tests: retain unit fakes and deliberate backend observation/fault injection. Runtime business operations must enter through Dapr; backend diagnostics cannot substitute for Dapr-path acceptance evidence.

### Frozen evidence conflict

Architecture AD-23 and the Story 8.1 summary still cite historical digest `0f841d5a...`. The payload specification records replacement normative digest `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`, approval `AR-20260913-01`, and Story 8.3 authorization `AR-20260914-01`. Reconcile the planning references against that source before authoring a successor. Do not rewrite frozen bytes or repin validators to manufacture earlier approval for the Dapr change.

## 3. Recommended Approach

Choose **Direct Adjustment** within the existing epics.

| Option | Assessment | Effort / risk |
| --- | --- | --- |
| Direct adjustment | Extends the established architecture, resolves specific conflicts and adds enforcement. Recommended. | Medium overall; crypto and transport feasibility need focused qualification. |
| Rollback | Reverting completed Dapr-based work would not resolve the cross-cutting policy gap. Removing the existing backplane before replacement proof risks lost notifications. | Unjustified effort and compatibility risk. |
| MVP reduction | The request changes how infrastructure is accessed. It does not remove user capabilities; Epic 8 remains post-MVP. | No scope reduction proposed. |

Planning estimate, not a delivery commitment: 1–2 engineering days for policy/backlog reconciliation, 2–4 for initial inventory and enforcement, 2–4 for SignalR qualification, and 2–4 for crypto qualification. Some work may overlap. Replacement implementation and real-service evidence are estimated after qualification; no reliable completion date follows from the current evidence.

Principal risks are encryption-format incompatibility, changing key-custody/error semantics, notification loss or tenant leakage, overly broad exceptions, and false confidence from dependency-name scans. Address them through operation-level qualification, existing compatibility vectors, live multi-instance tests and an explicitly bounded guardrail.

MVP functionality stays intact. Boundary enforcement and any required correction to existing MVP behavior add work before claiming conformance. Existing readiness failures remain separate. The project-wide direction is supplied by the user; approval of this proposal selects the concrete edits and backlog below.

## 4. Detailed Change Proposals

All NEW text below forms the proposed change set. Only this proposal file is written during this review stage. P1–P7 were individually approved by the user on 2026-10-05 and are retained without revision. Incremental review is complete; the complete proposal now awaits final review and implementation approval under Steps 4–5.

### P1 — PRD: project-wide infrastructure rule

**Review decision:** Approved by the user on 2026-10-05 (reply: “approve” to Edit 1 of 7).

Target: `prd.md`, section 8, new subsection **8.4 Dapr Infrastructure Boundary**.

**OLD:** No equivalent project-wide subsection; the glossary describes Dapr's abstraction role.

**NEW:**

> EventStore application and shared runtime code MUST use Dapr building blocks and components whenever they support the required infrastructure operation. This includes persistence and actors, messaging, service invocation, configuration, secrets, bindings, scheduling/workflows and cryptographic provider operations where applicable. Application code MUST NOT add a database driver, broker client, cloud SDK, direct provider HTTP call, connection string, or provider schema dependency for a capability available through a suitable Dapr interface.
>
> Suitability is evaluated against the required operation and its correctness, security, compatibility and operational guarantees on an identified runtime/component profile. Convenience, familiarity or an unmeasured performance preference is not a capability gap. A missing SDK convenience method does not justify bypassing an available supported Dapr API.
>
> If Dapr cannot provide a required operation, record the missing guarantee and evidence, alternatives considered, the narrowly isolated adapter, its owner and review/removal trigger in an architecture exception. Obtain the architecture owner's decision before introducing that dependency. Unknown suitability requires qualification. Never silently fall back to direct infrastructure after a Dapr failure.
>
> Provider-specific provisioning, Dapr component configuration and deployment/backup administration remain platform operations. Local pure computation and deliberately scoped test doubles/diagnostics are not runtime infrastructure adapters. Neither category may expose a bypass to application code. Approved exceptions are exact-purpose and exact-path, not blanket permissions.

Add acceptance evidence: every runtime infrastructure operation maps to a qualified Dapr interface or a documented exception; Story 3.17 checks the dependency boundary; changed paths pass their relevant live Dapr and compatibility tests. Preserve FR/NFR identities and link this constraint to FR5, FR8, FR32, FR34, FR37 and NFR12/NFR17/NFR19.

Rationale: turn the owner's direction into a reusable decision rule across the project.

### P2 — Architecture: broaden AD-1 and clarify its edges

**Review decision:** Approved by the user on 2026-10-05 (reply: “approve” to Edit 2 of 7).

Target: `architecture.md`, AD-1, retain the existing Story 6.6 amendment after the following addition.

**OLD:** “The platform uses CQRS, DDD, and event sourcing over DAPR state, actors, pub/sub, and service invocation.”

**NEW:**

> The platform uses CQRS, DDD and event sourcing with Dapr as the required infrastructure boundary whenever the required capability is supported. Apply PRD section 8.4 to all EventStore runtime packages and hosts. Provider dependencies belong behind Dapr components; any unavailable operation requires a documented architecture exception. A generic binding that carries application-owned SQL or provider protocols is not evidence of backend portability and cannot bypass actor state ownership. Select the highest applicable Dapr abstraction and qualify its actual guarantees.

Target: AD-3 operational-state read clause.

**OLD:** “Direct state-store reads are allowed only through named, tenant-authorized, support-safe adapters over platform-owned operational state.”

**NEW:**

> Reads of platform-owned operational state use named, tenant-authorized, support-safe adapters through Dapr APIs. This permission does not allow provider drivers or reads of Dapr private actor keys/tables. Actor-owned state is addressed through its actor boundary. External callers never receive generic-key access or direct mutation authority.

Target: AD-26, append after the existing Story 6.6 handoff.

**OLD:** No general operation-to-component qualification rule beyond existing profile gates.

**NEW:**

> Every supported profile identifies the Dapr API/component used by each infrastructure operation, the guarantees required, exact runtime/component versions, and observed acceptance evidence. ETags, transactional scope, TTL, ordering and failure behavior are qualified per component. Sharing a physical backend does not imply transactions across actors, state components, pub/sub or external systems. A backend change includes configuration, data migration where needed and requalification, even when application code remains unchanged.

Diagram edits: label read models/checkpoints and catalogs with their Dapr state/actor access; label domain calls with service invocation; depict configured secrets/crypto/bindings behind the sidecar when qualified. Depict any retained SignalR adapter as an explicit exception edge. Keep public HTTP, browser SignalR and telemetry edges accurately labelled.

Rationale: make the boundary architectural rather than a list of NuGet exclusions.

### P3 — New Story 3.17: Dapr Boundary Inventory and Enforcement

**Review decision:** Approved by the user on 2026-10-05 (reply: “approve” to Edit 3 of 7).

Target: Epic 3 in `epics.md`; new implementation story after approval.

**OLD:** No platform-wide infrastructure-boundary story. Story 1.11 checks domain boilerplate and has documented coverage limits.

**NEW:**

> As a maintainer, I want runtime infrastructure dependencies mapped to Dapr capabilities or explicit exceptions, so new work cannot silently introduce provider coupling.
>
> Coverage: PRD 8.4; FR8/FR32/FR34, NFR12/NFR17; AD-1/AD-9/AD-11/AD-12.
>
> 1. Inventory evaluated production dependency graphs and call sites across libraries, hosts, samples and generated-host inputs, including transitive provider integrations and linked source. Classify runtime, orchestration, test tooling and approved exception use separately. A central package catalog row alone does not prove runtime use.
> 2. For each runtime integration record the operation, Dapr API/component candidate, required guarantees, qualification evidence, current disposition and exact paths. Unknown rows remain unresolved. Name scans are only one input; include raw provider HTTP and configuration-based integration.
> 3. Add a deterministic guard to the existing architecture/packaging test lane. It rejects prohibited dependencies/call sites and invalid exception entries; exception scope includes owner, evidence, allowed paths and review/removal trigger. It has no global test-folder or provider-namespace exemption that can leak into shipped runtime assets.
> 4. Demonstrate failure using a prohibited database/broker reference and a direct-provider HTTP fixture; demonstrate valid Dapr use and narrowly scoped tooling/exception cases. Report static-analysis limits without claiming complete network enforcement.
> 5. Prevent application provider credentials from entering supported profiles where Dapr supplies the operation. Keep AppHost provisioning dependencies and deliberate test fault injection classified and bounded.
> 6. Publish the inventory and exception documentation with the guard. Carry every unresolved migration into an owned story, including 2.13 and 8.6. Do not call an unresolved inventory fully conformant.

Proposed outputs: `docs/concepts/dapr-infrastructure-boundary.md`, `docs/architecture/dapr-infrastructure-exceptions.yaml`, and guard tests in `tests/Hexalith.EventStore.Contracts.Tests/Packaging/`. Choose the final guard implementation in the story spec after inspecting the evaluated project graph.

Story 3.16 addition: “Before accepting an infrastructure dependency change, consult Story 3.17's inventory and PRD 8.4. A catalog upgrade cannot establish an exception or introduce a direct provider integration.”

Rationale: enforce the rule where platform dependencies enter the build.

### P4 — New Story 2.13: Dapr Notification Distribution Qualification

**Review decision:** Approved by the user on 2026-10-05 (reply: “approve” to Edit 4 of 7).

Target: Epic 2 in `epics.md`; new implementation story after approval.

**OLD:** Story 2.8 has delivered notification contracts; current scale-out registration directly depends on Redis. No scoped Dapr replacement/exception assessment exists.

**NEW:**

> As an operator, I want notification distribution to use a qualified Dapr path wherever feasible, so infrastructure choice does not leak into application delivery contracts.
>
> Coverage: FR16, NFR5/NFR12/NFR15/NFR16, PRD 8.4; AD-8/AD-9/AD-10/AD-12.
>
> 1. Compare Dapr pub/sub fan-out to locally connected hubs and the Azure SignalR output binding against the current self-hosted topology. Document connection/negotiation, group membership, replica delivery, scaling, ordering, duplicates and tenant authorization. Competing-consumer delivery to one replica is not proof of delivery to all relevant hubs.
> 2. Select and prove a supported Dapr design if it meets the required behavior. If it cannot, produce the specific gap evidence and an architecture-owner decision for a bounded Redis backplane exception. Lack of completed investigation is not a permanent exception.
> 3. Preserve `ProjectionChanged`, `ProjectionChangedDetail`, scoped groups, metadata bounds and projection-confirmed UI behavior. Prove two-host delivery, reconnect/rejoin, duplicate handling, outage behavior and tenant-denial cases using real processes and sidecars for the selected Dapr path.
> 4. For a qualified replacement, update both host dependency graphs, registration/configuration, AppHost/deployment assets, documentation and tests together. Retire direct Redis runtime packages/configuration only after compatibility and rollback proof. Preserve existing behavior until that transition is qualified.
> 5. Completion records either the proven Dapr replacement or the accepted, evidence-backed exception with exact scope and review trigger. No exception is approved by this proposal alone.

Rationale: remove avoidable coupling while retaining the concrete cross-instance behavior users depend on.

### P5 — Payload protection: qualify Dapr before choosing the adapter

**Review decision:** Approved by the user on 2026-10-05 (reply: “approve” to Edit 5 of 7).

Targets: `prd.md` FR37, `architecture.md` AD-23, `epics.md` Story 8.6, and a successor/amendment to `spec-shared-payload-protection-engine.md`.

**OLD, FR37 excerpt:** “include at least one integration-proven production backend”.

**NEW:** “include at least one integration-proven production backend accessed through a suitable Dapr cryptography component; any operation unavailable through Dapr requires a documented, narrowly scoped architecture exception under section 8.4. Preserve the approved durable formats, custody, typed failure and rollback contracts.”

**OLD, Story 8.6 selection:** “stable non-preview `Azure.Identity` and `Azure.Security.KeyVault.Keys` versions supporting the repository target are centrally pinned”.

**NEW:**

> Before selecting application provider SDKs, qualify the pinned Dapr runtime, client API and `crypto.azure.keyvault` component against the approved key-operation contract. Record support and gaps for exact-version wrap/unwrap, current-version discovery, RSA-HSM profile and operation validation, identity selection, cancellation, retry budgets, typed failure classification, key custody, buffer ownership and historical decryptability. Component existence or an encrypt/decrypt happy path is insufficient evidence.

**OLD, Story 8.6 project responsibilities:** Engine plus Azure SDK dependencies; application-owned Azure credential/client construction and provider operations.

**NEW:**

> Implement supported provider operations through Dapr. The adapter consumes logical component/key identities; deployment supplies provider endpoint, credentials, scopes and identity configuration. Do not replace the frozen `pdenc-v2` envelope or its authenticated-data bytes with Dapr Crypto Scheme v1. Unsupported required operations return to architecture/security review for a compatible Dapr design, a separately reviewed format migration, or an isolated exception. Do not introduce the direct SDK merely because the earlier story prescribed it.

**OLD, Story 8.6 options and credential acceptance:** Application `VaultUri`/`ManagedIdentityCredential` construction is mandatory.

**NEW:**

> The amended spec defines logical application options and the separately governed component/profile contract. Qualified Dapr components own provider authentication. Preserve required least privilege, deterministic identity, private connectivity, key attributes and lifecycle restrictions; prove these at the component/provider boundary. Only a documented unsupported operation may retain provider-specific application options inside its approved exception.

**OLD, AD-23 approval reference:** Historical `0f841d5a...` / `AR-20260801-01` is described as current prerequisite authority.

**NEW:**

> Cite the current replacement authority recorded in the payload specification and identify this Dapr change as a new, unapproved amendment until reviewed. Apply the exact-digest approval and predecessor rules to all affected stories and evidence. Retain historical approvals as evidence of their original bytes.

Apply the same selection change to spec sections 5, 11 and 16. Package identity/count changes, if needed, remain Story 8.8's atomic release concern. Existing key-custody, golden-vector, typed-outcome, no-leak and rollback acceptance criteria remain required and must be rebound to the selected design. A Dapr secret store is not a replacement for a cryptographic key-operation component.

Rationale: remove the planned automatic SDK bypass without weakening the security or compatibility contract. This proposal authorizes no frozen-spec byte rewrite or reuse of old approval for new bytes.

### P6 — Documentation, testing evidence and UX alignment

**Review decision:** Approved by the user on 2026-10-05 (reply: “approve” to Edit 6 of 7).

Target: `docs/concepts/architecture-overview.md`, “Infrastructure Portability”.

**OLD:** “Because all infrastructure access goes through DAPR building blocks, switching backends is a configuration change — not a code change.”

**NEW:**

> EventStore uses Dapr for infrastructure operations supported by qualified components. Backend selection is expressed through component configuration; each change must preserve required semantics and pass the supported-profile tests, with data migration where necessary. Documented operation-specific exceptions are listed in the infrastructure-boundary inventory. Domain behavior remains independent of backend clients.

Reconcile the similar unconditional claims in “What Is DAPR?” and the building-block introduction. Link the boundary guide from the architecture overview and Dapr component reference.

Target: Epic 7, Story 7.11 persisted-state evidence acceptance.

**OLD:** Existing backend readback helpers are planned without this project-wide classification rule.

**NEW:**

> Acceptance exercises application operations through Dapr and verifies logical persisted values via the appropriate Dapr state/actor boundary. Direct backend tooling may support isolated setup, deliberate fault injection or physical diagnostics only when explicitly identified and kept outside shipped runtime paths. It cannot substitute for Dapr-path correctness evidence or claim access to private actor storage as an application contract. Story 6.6 retains its stricter prohibition on direct-database product evidence.

UX disposition: no wireframe change. Preserve the canonical UX states and routes; test that transport/component failures retain honest freshness and availability, and that switching distribution does not change authorization or group isolation. UI copy does not expose backend credentials, internal endpoints or exception mechanics.

### P7 — Backlog, tracking and handoff synchronization

**Review decision:** Approved by the user on 2026-10-05 (reply: “approve” to Edit 7 of 7).

**OLD:** Epic 2 ends at 2.12; Epic 3 ends at 3.16. The current sprint tracker has no 2.13 or 3.17.

**NEW, after approval:**

```yaml
2-13-dapr-notification-distribution-qualification: backlog
3-17-dapr-boundary-inventory-and-enforcement: backlog
```

Update the epic lists and FR/NFR coverage references together. Keep 6.6 and 8.3 in progress and 8.6 in backlog; record new qualification dependencies without erasing earlier completion evidence. Amend affected story text and specs before implementation handoff. Planning constraints and cross-links must agree with the revised PRD/architecture.

Rationale: prevent a policy-only change from disappearing before implementation.

## 5. Implementation Handoff

Classification: **Moderate**. Product Owner/Developer coordination is needed, with Architecture/Security decisions for the crypto and notification capability gaps. A fundamental product replan is unnecessary.

| Recipient role | Responsibility | Required output |
| --- | --- | --- |
| Product Owner | Approve and apply planning deltas, add 2.13/3.17, maintain scope and sequencing | Consistent PRD, epics, story specs and sprint tracker |
| Architect | Own PRD 8.4/AD-1 boundary, capability decisions and exception disposition | Operation/component matrix and bounded decisions |
| Developer | Implement 3.17; qualify and implement approved 2.13/8.6 paths; continue authorized 6.6 work | Reviewable changes with focused regression evidence |
| Security and Operations | Review crypto custody/format compatibility and component credentials/scopes; qualify selected profiles | Bound design and live environment evidence |
| Test owner | Validate guardrail negatives, multi-host delivery and exact crypto/state semantics | Reproducible commands/results and declared limits |

Sequence:

1. Review this proposal, then obtain explicit approval of its concrete change set.
2. Apply P1/P2/P6/P7 and author the new story specs. Reconcile current payload authority references before drafting its amendment.
3. Inventory dependencies and enforce the new boundary in Story 3.17. Qualify SignalR and crypto paths before selecting migrations or exceptions.
4. Apply approved story/spec changes, preserve frozen evidence, and satisfy existing corrective-work or successor gates where applicable. The PRD's existing readiness controls remain in force; no implementation handoff is claimed here.
5. Implement each authorized slice with its narrow relevant checks; run live/profile and compatibility gates for changed runtime behavior.

Success criteria:

- The project-wide rule consistently requires Dapr for available suitable infrastructure operations.
- Every runtime provider integration has either a qualified Dapr replacement or an explicit operation-specific gap decision; unresolved cases are visibly tracked.
- Automated checks reject representative prohibited dependencies and bypasses and document their limits.
- Story 6.6 keeps actor ownership, logical readback and consumer qualification; it gains no SQL or physical-attestation permission.
- Payload-protection selection considers Dapr first while retaining frozen-format, custody and historical-read requirements.
- Notification delivery retains public contracts, tenant isolation and multi-instance behavior.
- Component/profile changes carry exact live evidence; no approval, readiness or delivery state is inferred from adopting this policy.

### Checklist execution record

`[x]` assessed/completed; `[N/A]` not applicable; `[!]` outstanding action.

| Checklist item | Status | Disposition |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Owner direction; Story 6.6 provides immediate context. |
| 1.2 Core problem | [x] | Inconsistent project-wide infrastructure boundary. |
| 1.3 Evidence | [x] | Source, package, planning and official component evidence above. |
| 2.1 Current epic | [x] | Epic 6 can continue under its existing amendment. |
| 2.2 Epic changes | [x] | Add stories in 2/3; revise 8.6 and cross-cutting constraints. |
| 2.3 Remaining epics | [x] | All nine epic areas assessed above. |
| 2.4 Obsolete/new epics | [N/A] | No removal or new epic required. |
| 2.5 Order and priority | [x] | Boundary/inventory before new integrations; qualification before replacement. |
| 3.1 PRD | [x] | P1 and FR37 clarification; no feature reduction. |
| 3.2 Architecture | [x] | P2/P5, component semantics and diagram changes. |
| 3.3 UX | [x] | Preserve current routes, evidence states and notification contracts. |
| 3.4 Other artifacts | [x] | Profiles, docs, tests, packages and frozen specs identified. |
| 4.1 Direct adjustment | [x] | Recommended; medium effort/risk with qualification work. |
| 4.2 Rollback | [x] | Evaluated and rejected. |
| 4.3 MVP review | [x] | Evaluated; reduction unnecessary. |
| 4.4 Selected path | [x] | Direct adjustment within current epic structure. |
| 5.1 Issue summary | [x] | Section 1. |
| 5.2 Impact | [x] | Section 2. |
| 5.3 Recommendation | [x] | Section 3. |
| 5.4 MVP/action plan | [x] | Sections 3–5. |
| 5.5 Handoff plan | [x] | Named roles and deliverables; not yet executed. |
| 6.1 Checklist review | [x] | Outstanding approval and implementation work explicit. |
| 6.2 Proposal accuracy | [x] | Source anchors and scope checked; runtime claims remain unproven. |
| 6.3 Explicit approval | [!] | All seven edits individually approved; complete-proposal implementation approval remains pending. |
| 6.4 Sprint updates | [!] | Proposed rows prepared; apply after approval. |
| 6.5 Handoff confirmation | [!] | Plan prepared; final implementation routing follows approval. |

Workflow state: Step 3 complete with P1–P7 approved and retained. The complete proposal is saved and presented at Step 4 for **Continue** (proceed to final approval) or **Edit** (revise the proposal). Step 5 complete-proposal implementation approval has not been obtained. No runtime, dependency, frozen-spec, sprint-status or implementation-authorization record was changed; individual review decisions are recorded here.


## 6. Subsequent Approval And Application Disposition — 2026-10-05

The user explicitly instructed implementation of `_bmad-output/implementation-artifacts/spec-dapr-infrastructure-boundary-planning-reconciliation.md`, with that specification as the sole source of truth. This authorizes its bounded initial planning/documentation slice. It does not approve a crypto amendment, qualification result, provider dependency, exception, provisioning, runtime/topology change, release, production deployment, consumer migration or readiness claim.

The earlier header status (“Complete proposal — P1–P7 individually approved; final review and implementation approval pending”), checklist outstanding items and Step 4–5 workflow state above are retained as the prior review checkpoint. The subsequent implementation instruction and application below supersede the pending application/handoff disposition only for this slice. Individual P1–P7 decisions remain historical approvals of the stated policy/planning changes; no approval is transferred to new frozen bytes or evidence.

| Proposal | Applied output | Remaining owned gate |
| --- | --- | --- |
| P1 | PRD §8.4, FR37 Dapr qualification and supporting FR/NFR traceability | 3.17 final inventory/guard and relevant live/profile correctness; existing readiness failures remain. |
| P2 | AD-1/AD-3/AD-23/AD-26 and accurately labelled infrastructure/transport diagrams | Actor ownership, 6.6 stricter amendment and all AD-26 production/ratification gates remain. |
| P3 | Backlog 3.17 epic criteria and actionable implementation specification; 3.16 dependency policy; preliminary guide/register | No guard implemented; complete evaluated/transitive/linked/generated/HTTP/credential inventory, negatives and analysis limits in 3.17. |
| P4 | Backlog 2.13 epic criteria and actionable implementation specification; retained Redis explicitly unresolved | Real two-host/sidecar delivery/reconnect/duplicates/outage/tenant-denial qualification, then approved replacement or separate exact-scope exception. |
| P5 | Current payload authority reconciled in architecture/epics/8.1 wrapper; conditional 8.6 criteria; detached draft amendment for §§5/11/16 and PF-01 | Amendment draft/unapproved, operation suitability unresolved, affected 8.3–8.11 impact and exact-content reapproval/predecessor gates. Shared authority/packets/fixtures/validators unchanged; no provider SDK selected. |
| P6 | Public portability claims qualified and guide links added; 7.11 Dapr-path evidence and UX transport/tenant obligations recorded | Routes/states/freshness/authorization unchanged; backend tools supply isolated setup/fault/physical diagnostics only, never replacement Dapr correctness evidence. |
| P7 | Exact 2.13/3.17 backlog rows and synchronized update dates; epic lists/coverage/handoffs agree | All pre-existing tracker values and guarded blocks retained, including 6.6/8.3 in progress and 8.6 backlog; no qualification/completion inferred. |

The initial planning/documentation handoff is applied. Implementation and qualification follow the new owned backlog specifications and existing authority controls; the current accepted-exception register is empty. No input/evidence digest is repinned, frozen payload authority rewritten, provider chosen, service provisioned or runtime/topology/submodule altered by this slice. Verification of changed documentation, tracker preservation, current payload hashes, frozen vectors and the individual Contracts lane is recorded with the implementation handoff; it supplies no live qualification or readiness approval.
