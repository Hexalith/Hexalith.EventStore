# Architecture Reviewer Gate — Good-Spine Rubric Walker

- **Subject:** `_bmad-output/planning-artifacts/architecture.md`
- **Review date:** 2026-09-09
- **Intent:** validate
- **Lens:** independent good-spine checklist
- **Deterministic lint:** passed with 0 findings (input to this review)
- **Verdict:** **CHANGES REQUIRED**
- **Finding count:** 2 critical, 4 high, 9 medium, 3 low

The spine is unusually strong on its core event-sourcing, admission, release-evidence, projection, and secret-management invariants. It is not yet safe to accept as the canonical architecture, however, because two high-consequence cross-unit authorities remain undefined: the production runtime profile and canonical tenant identity. Several adopted rules also describe a target that the brownfield code does not currently implement without saying that the rule is prospective.

## Review basis

I read the complete canonical spine and its complete memlog, then checked the PRD, epics, sprint state, relevant approved specification, deployment material, live dependency catalog, and representative implementation paths. The memlog's original contract says the architecture owns component, integration, topology, and decision-record gates and must ratify the brownfield (`_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/.memlog.md:10-12`); its Admin decision also records the intended user/tenant/action/outcome/correlation audit boundary (`_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/.memlog.md:27-31`). I treated its prior 2026-09-08 review summary as historical context (`_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/.memlog.md:105`) and independently reconfirmed the findings below against the current files.

## Critical findings

### C1 — No production runtime profile selects the state provider, broker, deployment target, or evidence boundary

**Evidence**

- AD-9 says topology is one unit and requires AppHost, component YAML, scopes, ACLs, resiliency, and publish targets to change together (`_bmad-output/planning-artifacts/architecture.md:141-145`), but it does not select a production unit.
- AD-25 names `oq8-postgresql-v1` as a **production-equivalent evidence profile**, not as the production provider (`_bmad-output/planning-artifacts/architecture.md:451-460`).
- The append-fence deferral records that local Redis silently overwrote the same actor key and explicitly forbids inferring another provider's behavior (`_bmad-output/planning-artifacts/architecture.md:606`).
- Deployment guidance offers three state stores and four brokers with materially different proof status (`deploy/README.md:25-44`), tells an operator to “choose one” (`deploy/README.md:141-155`), and advertises Azure Container Apps (`deploy/README.md:173-180`). The AppHost also emits an ACA target (`src/Hexalith.EventStore.AppHost/Program.cs:374-384`), while AD-24 says managed ACA DAPR is nonconforming (`_bmad-output/planning-artifacts/architecture.md:394-398`).

**Why this fails the gate:** Independently built deployment, release, and data-integrity work can select incompatible combinations while each claims architectural conformance. The risk is not cosmetic: the only explicitly observed local-store behavior includes silent overwrite, while Kafka and Azure Service Bus ordering are unproven in the shipped configuration. There is no authoritative answer to which profile can receive production traffic.

**Disposition:** **discuss**

**Required resolution:** Adopt one named production runtime profile that fixes state provider, pub/sub provider, deployment target, app IDs, component/configuration overlays, secret source, resilience policy, and required proof. Classify every other shipped YAML/publisher target as local, example-only, candidate, or nonconforming. If selection cannot be made now, create an explicit production-traffic prohibition with owner, decision trigger, and evidence gate; a silent omission is not a safe deferral.

### C2 — Canonical tenant identity is not owned across claims, state, topics, admin filters, and SignalR

**Evidence**

- The only spine-wide convention says tenant identity remains “explicit”; it does not define the canonical source, normalization point, comparison semantics, or whether an omitted tenant is legal (`_bmad-output/planning-artifacts/architecture.md:466-469`).
- `AggregateIdentity` lowercases tenant and domain values and gives `system` special topic behavior (`src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs:15-36`, `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs:88-94`).
- Gateway tenant authorization uses exact ordinal comparison (`src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs:33-49`), while both gateway and Admin action filters may replace an omitted tenant with the first granted tenant (`src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs:27-48`, `src/Hexalith.EventStore.Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs:23-44`).
- Admin projection reads and wildcard matching use case-insensitive comparison (`src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:64-80`, `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:203-206`).
- NFR2 requires isolation through state keys, actor IDs, topics, admin queries, generated APIs, SignalR groups, and deployment configuration, including rejection of reserved `system` (`_bmad-output/planning-artifacts/prd.md:305-307`).

**Why this fails the gate:** The same tenant can normalize to one persistence identity while being rejected, defaulted, or grouped differently at another boundary. “First granted tenant” is also not a canonical meaning for a missing tenant. That permits cross-tenant ambiguity in security-sensitive routing and makes NFR2 unenforceable as a single platform contract.

**Disposition:** **discuss**

**Required resolution:** Name one platform-owned tenant identifier contract and normalizer, its input source at each boundary, canonical casing/character rules, exact comparison semantics, reserved-name rule, and explicit-vs-omitted behavior. Require claims, gateway/Admin filters, actor/state/topic keys, projection queries, generated APIs, and SignalR group construction to consume that authority; prove equivalent raw spellings cannot create or reveal different scopes.

## High findings

### H1 — AD-10 requires application-layer credentials but does not define one enforceable internal-host contract

**Evidence**

- AD-10 requires application-layer credentials and tenant authorization on public, internal, domain-service, projection-notification, and admin-computation endpoints (`_bmad-output/planning-artifacts/architecture.md:147-151`).
- AD-16 makes the deny-by-default rule conditional on a host that “introduces” a fallback policy, rather than requiring all conforming hosts to have one (`_bmad-output/planning-artifacts/architecture.md:243-251`).
- The gateway's internal handler trusts an allow-listed `dapr-caller-app-id` header and elevates the caller to `global_admin` (`src/Hexalith.EventStore/Authentication/DaprInternalAuthenticationHandler.cs:9-45`).
- The domain-service SDK maps command, replay, query, projection, rebuild, and admin-metadata endpoints without attaching authorization metadata or a platform credential middleware (`src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:198-225`, `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:240-330`).
- The Operations host uses a different, conditionally installed DAPR app-channel-token middleware (`src/Hexalith.EventStore.Operations/Program.cs:13-44`).

**Why this fails the gate:** The rule names an outcome but not the credential, trusted issuer/header boundary, host integration seam, or default-deny mechanism. Teams can implement three mutually incompatible notions of “application-layer credential”; a bare SDK host can map sensitive endpoints without a compile-time or runtime conformance gate.

**Disposition:** **discuss**

**Required resolution:** Choose one platform-owned internal authentication/authorization seam and state how authenticated caller identity and tenant scope reach every internal route. Require a default-deny policy on every conforming host, preserving only AD-16's explicit anonymous probes. Treat DAPR ACL/caller identity as defense in depth unless the selected contract explicitly and safely promotes it to credential authority.

### H2 — The implemented Operations/dead-letter workload is absent from the architecture

**Evidence**

- FR34 expressly requires poison/dead-letter handling and bounded deduplication (`_bmad-output/planning-artifacts/prd.md:264-273`). Story 7.1 says no production-proven end-to-end subscriber contract exists yet (`_bmad-output/planning-artifacts/epics.md:4695-4710`).
- A publishable `eventstore-operations` workload already exists (`src/Hexalith.EventStore.Operations/Hexalith.EventStore.Operations.csproj:1-8`) with a dead-letter actor and background reconciler (`src/Hexalith.EventStore.Operations/Program.cs:18-44`) plus default topic, caller, replay target, and replay budget (`src/Hexalith.EventStore.Operations/Configuration/EventStoreOperationsOptions.cs:11-27`, `src/Hexalith.EventStore.Operations/Configuration/EventStoreOperationsOptions.cs:43-59`).
- The structural seed and runtime diagram omit this workload (`_bmad-output/planning-artifacts/architecture.md:515-582`), the capability map reduces the area to generic “Admin surfaces” and “delivery docs” (`_bmad-output/planning-artifacts/architecture.md:595`), and the release workflow publishes only `eventstore` (`.github/workflows/release.yml:125-136`).

**Why this fails the gate:** This is not merely a future backlog concept: a deployable host, persisted state owner, caller contract, replay side effect, and container identity already exist. Without an architectural owner and topology/release classification, separate teams can change poison acknowledgement, tenant scope, retry identity, and recovery semantics independently.

**Disposition:** **discuss**

**Required resolution:** Add an Operations/dead-letter decision covering ownership, durable state and acknowledgement semantics, tenant scope, authorization/audit, replay identity/idempotency, topology, app ID, and container/release status. If Story 7.1 remains the design owner, explicitly mark the current workload non-production and bind an interim fail-closed contract; it cannot remain unmentioned.

### H3 — AD-19 mandates exact normalized types that do not exist in the brownfield implementation

**Evidence**

- AD-19 requires exactly `ProjectionDispatchResult(Version, Entries)`, `ProjectionDispatchResultEntry`, and `ProjectionCheckpointAdvanceState`, and rejects alternatives (`_bmad-output/planning-artifacts/architecture.md:286-309`).
- The current wire type is `ProjectionDispatchResponse(Version, Outcomes)` (`src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchResponse.cs:1-10`).
- The coordinator validates outcomes and advances durable state through internal control flow (`src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:172-191`, `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:522-600`), but a repository-wide search finds no declaration of any of the three mandated normalized types.

**Why this fails the gate:** An `[ADOPTED]` rule appears to ratify current architecture while actually specifying unowned future contract types. Reviewers cannot tell whether current code violates the decision, whether a missing story owns implementation, or whether the rule intended only observable semantics. That makes the rule non-enforceable and weakens brownfield fidelity.

**Disposition:** **discuss**

**Required resolution:** Either bind the exact types to a planned implementation owner and mark the rule prospective until that gate closes, or rewrite the rule around the currently observable normalized semantics and point to their existing authority. Preserve the status/checkpoint matrix either way.

### H4 — Hard-coded technology baselines are stale one day after the spine's claimed currentness point

**Evidence**

- AD-11 hard-codes SDK `10.0.400` and an ASP.NET/runtime security baseline of `10.0.11` (`_bmad-output/planning-artifacts/architecture.md:153-157`); the Stack repeats those values (`_bmad-output/planning-artifacts/architecture.md:486-506`).
- On 2026-09-09, Microsoft's official [.NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) lists SDK `10.0.401` and runtime/ASP.NET Core `10.0.12`, released 2026-09-08. Microsoft's [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) requires supported systems to remain current on servicing updates.
- The local SDK seed is still `10.0.400` with `latestPatch` (`global.json:1-4`) and the shared Builds catalog still pins ASP.NET packages at `10.0.11` (`references/Hexalith.Builds/Props/Directory.Packages.props:172-193`), so this is a repository/catalog maintenance gap, not just a table typo.
- The Stack says FrontComposer is `4.1.1` (`_bmad-output/planning-artifacts/architecture.md:506`), while the cited live catalog authority is already `4.4.0` (`references/Hexalith.Builds/Props/Directory.Packages.props:6-10`).

**Why this fails the gate:** The checklist requires named technology to be verified-current. A hard security baseline that is already superseded can cause a team to preserve a vulnerable patch under the mistaken belief that it is architectural authority; the FrontComposer row contradicts its own declared source of truth.

**Disposition:** **discuss**

**Required resolution:** Route the .NET/ASP.NET servicing move through AD-11's dependency-evidence process and Story 3.16, then refresh the dated rendering. Immediately reconcile the FrontComposer row with the live catalog. Prefer a policy plus evidence timestamp over embedding “current required” patch claims in an invariant that becomes stale between reviews.

## Medium findings

### M1 — AD-3 forbids direct Admin state-store access while the same spine and code require it

**Evidence:** AD-3 says Admin code does not call state stores directly (`_bmad-output/planning-artifacts/architecture.md:105-109`), but the runtime diagram draws `AdminServer --> StateStore` for operational reads (`_bmad-output/planning-artifacts/architecture.md:539-577`) and `DaprProjectionQueryService` reads the store directly (`src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:61-80`).

**Impact:** Teams cannot simultaneously conform to the prose rule and the documented/runtime design, so the policy boundary is ambiguous precisely where tenant-filtered support reads need strict governance.

**Disposition:** **autofix** — qualify AD-3 with the intended read-only, support-safe Admin exception, including tenant authorization and AD-12 evidence, while retaining gateway ownership of mutations; or remove the diagram/code path if no exception is intended.

### M2 — The structural seed is materially incomplete for the current repository

**Evidence:** The seed omits `AppHost`, `SignalR`, `Testing`, `Testing.Integration`, `Admin.Server.Host`, and `Operations` (`_bmad-output/planning-artifacts/architecture.md:515-537`). The brownfield inventory identifies the first five as distinct architectural parts (`docs/brownfield/architecture.md:58-80`), and Operations is a distinct publishable workload (`src/Hexalith.EventStore.Operations/Hexalith.EventStore.Operations.csproj:1-8`).

**Impact:** A new contributor cannot reconstruct the actual unit boundaries or distinguish packages, hosts, and deployment-only assets from the seed.

**Disposition:** **autofix** — add the missing projects and the relevant `samples/dapr-components` and `samples/deploy` topology roots, with clear library/host/release classifications. Resolve Operations semantics under H2 rather than duplicating them here.

### M3 — `ProjectionVersion` is declared authoritative without defining a comparable version authority

**Evidence:** AD-15 allows `ProjectionVersion` to drive freshness only when sourced from persisted `IReadModelFreshness` (`_bmad-output/planning-artifacts/architecture.md:227-241`), but it does not define the version's type, monotonicity, scope, producer, comparison semantics, or behavior across rebuild/promotion.

**Impact:** Projection implementations can emit incompatible strings or counters while all technically implement the seam; UI and API consumers can then make incompatible `Current`/`Stale` decisions.

**Disposition:** **discuss** — define one platform-owned version contract per named projection scope, including rebuild/promotion behavior, or explicitly defer version comparison and prohibit consumers from ordering/interpreting the value meanwhile.

### M4 — Erasure has multiple owners but no ordering/failure rule across projections and payload keys

**Evidence:** AD-7 treats read-model plus checkpoint deletion as one logical operation (`_bmad-output/planning-artifacts/architecture.md:129-133`), while AD-23 assigns generic `IErasureStateProvider` mechanics to EventStore and legal erasure orchestration to consuming domains (`_bmad-output/planning-artifacts/architecture.md:370-378`). AD-20 separately requires rebuild output to equal canonical replay (`_bmad-output/planning-artifacts/architecture.md:311-315`).

**Impact:** Key invalidation, read-model/checkpoint removal, replay, and projection lifecycle can race or fail partially. One implementation may advance past now-unreadable history; another may keep retrying or resurrect an erased view.

**Disposition:** **discuss** — name the orchestration authority and ordering between projection erasure and key invalidation, plus the typed unreadable-event/checkpoint/rebuild outcome. If this remains post-MVP, add an interim prohibition against claiming full erasure completion.

### M5 — Correlation and Admin audit identity are not one platform contract

**Evidence:** The spine requires ULID-safe correlation handling (`_bmad-output/planning-artifacts/architecture.md:462-468`) and says Admin mutations are attributable (`_bmad-output/planning-artifacts/architecture.md:147-151`). The gateway accepts bounded identifier syntax and generates sortable IDs (`src/Hexalith.EventStore/Middleware/CorrelationIdMiddleware.cs:7-45`), while the Admin host uses `Guid.TryParse` and `Guid.NewGuid` (`src/Hexalith.EventStore.Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs:6-27`). Story 7.3 confirms that no shared durable audit protocol or complete mutation inventory exists (`_bmad-output/planning-artifacts/epics.md:4816-4842`).

**Impact:** The current Admin host violates the stated identity convention, and “attributable” can degrade to unrelated logs rather than durable subject/tenant/action/outcome/correlation evidence.

**Disposition:** **discuss** — make the gateway correlation contract reusable and authoritative across hosts, and bind Admin mutation audit to one durable admission/end-state seam with unresolved subjects rejected. Mark Story 7.3 as the implementation owner if the rule is prospective.

### M6 — Several operational dimensions are neither decided nor safely deferred

**Evidence:** The spine names telemetry packages and a ServiceDefaults project (`_bmad-output/planning-artifacts/architecture.md:486-513`, `_bmad-output/planning-artifacts/architecture.md:515-528`) but no log/trace/metric correlation, redaction, exporter, or cardinality authority. It also does not decide or defer event/snapshot/backup retention, restore authority, scale-out/multi-region posture, or production environment promotion. The Deferred table mentions quantitative UI budgets and out-of-MVP physical backup erasure, but not these operating contracts (`_bmad-output/planning-artifacts/architecture.md:598-609`).

**Impact:** Host and deployment teams can make divergent operational choices without violating any AD; this fails the rubric's explicit operational/environment-dimension check.

**Disposition:** **defer** — add one row per intentionally unresolved dimension with current safe posture, owner, trigger, and forbidden interim claims. Promote any dimension already needed for production traffic into an AD, especially retention/restore and environment promotion.

### M7 — The payload-protection authorization state in AD-24 is stale

**Evidence:** AD-24 calls the payload-protection specification “draft-not-authorized” (`_bmad-output/planning-artifacts/architecture.md:398`) and AD-23 conditions Story 8.2 on approval (`_bmad-output/planning-artifacts/architecture.md:378`). The referenced specification is `approved-authorized`, records `story_8_2_authorized: true`, and includes named approvals (`_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md:1-10`, `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md:61-75`) plus a final exact-digest authorization (`_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md:2253-2272`).

**Impact:** The spine misstates the authority order and can block authorized work or lead reviewers to inspect the wrong version state.

**Disposition:** **autofix** — state that the prerequisite is satisfied by `AR-20260801-01` for the recorded digest while retaining every successor evidence gate. Do not modify the frozen normative body.

### M8 — Story schedules and evidence procedures obscure the stable architectural contract

**Evidence:** The paradigm calls Stories 4.9-4.15 active and embeds live tracker status (`_bmad-output/planning-artifacts/architecture.md:60-71`). AD-11 occupies thirteen lines of release-procedure and dated corrective-story detail (`_bmad-output/planning-artifacts/architecture.md:153-165`); AD-22 similarly contains dated exception/erratum mechanics (`_bmad-output/planning-artifacts/architecture.md:323-368`); the Release convention embeds a 14-to-16 package schedule (`_bmad-output/planning-artifacts/architecture.md:484`).

**Impact:** A “final” spine now changes whenever a story or package count changes, making stable invariants harder to locate and increasing stale contradictions such as M7/H4.

**Disposition:** **discuss** — retain the stable ownership, identity, immutability, and proof rules in ADs; move time-bound status, rejected-candidate chronology, exact receipts, and package-count migration steps into the memlog/spec/story evidence with stable references.

### M9 — The conventions table duplicates decisions and already introduces independent drift

**Evidence:** Idempotency, probes, projection persistence/rebuild, payload protection, secrets, headers, UI, topology, and release rows repeat AD-7/8/11/16/18/19/20/21/23/24/25 (`_bmad-output/planning-artifacts/architecture.md:462-484`). The Release row adds a schedule-specific inventory rule, and the UI/Stack rows disagree with the live FrontComposer catalog noted in H4.

**Impact:** Reviewers cannot tell whether the AD or convention row is authoritative when one changes without the other.

**Disposition:** **autofix** — keep only conventions that are not already AD rules, and replace duplicated prose with short `See AD-x` references.

## Low findings

### L1 — Editorial placeholder remains in a final deferral

**Evidence:** The append-fence row begins `**ADD, deferred ...**` (`_bmad-output/planning-artifacts/architecture.md:606`).

**Disposition:** **autofix** — remove `ADD,` and retain the substantive deferral.

### L2 — The spine lists itself as a companion artifact

**Evidence:** Frontmatter includes `_bmad-output/planning-artifacts/architecture.md` under `companion_artifacts` (`_bmad-output/planning-artifacts/architecture.md:50-51`).

**Disposition:** **autofix** — remove the self-reference unless a consuming tool has a documented requirement for it.

### L3 — The capability map overstates direct binding for FR17/FR18

**Evidence:** The map assigns FR17-FR22 and FR25 to AD-9/11/12 (`_bmad-output/planning-artifacts/architecture.md:584-591`), but AD-11's explicit `Binds` omits FR17-FR20 and AD-12 binds no FR IDs (`_bmad-output/planning-artifacts/architecture.md:153-171`). AD-1's blanket binding technically covers them, but the detailed row implies more direct traceability than the decisions declare.

**Disposition:** **autofix** — add the intended direct bindings or narrow the map to the actual governing ADs.

## Good-spine checklist result

| # | Checklist item | Result | Judgment |
| --- | --- | --- | --- |
| 1 | The spine fixes the real divergence points and misses none | **Fail** | Production runtime selection, tenant identity, internal credentials, and the existing Operations workload are material unowned divergence points (C1, C2, H1, H2). |
| 2 | Every AD Rule is enforceable and prevents its stated divergence | **Fail** | Many rules are precise, but AD-10 does not name a uniform credential/default-deny seam, AD-19 mandates absent types without a prospective owner, and AD-3 contradicts the documented Admin read path (H1, H3, M1). |
| 3 | Nothing Deferred could still let implementation units diverge | **Pass, with caveat** | The items actually listed under Deferred have safe interim constraints or are cleanly assigned to another planning artifact. The unsafe gaps are omissions from that table, recorded under C1/M6, rather than weaknesses in its existing rows. |
| 4 | Named technology is verified-current | **Fail** | OpenBao/DAPR claims check out, and most package rows match the shared catalog, but the .NET/ASP.NET servicing baseline and FrontComposer rendering are stale (H4). |
| 5 | The document ratifies the brownfield rather than designing a parallel system | **Partial — gate fail** | Core gateway/actor/projection/release structures are brownfield-grounded. AD-19's phantom types, omitted Operations host, incomplete seed, and Admin read contradiction are parallel or incomplete descriptions (H2, H3, M1, M2). |
| 6 | The spine covers the driving specification capabilities | **Partial — gate fail** | Every FR1-FR37 and NFR1-NFR19 identifier is traceable, and most driving capabilities have strong decisions. FR34's poison/dead-letter path and Admin-audit contract remain inadequately governed despite implemented scaffolding (H2, M5). |
| 7 | Inherited parent decisions are not weakened | **Pass / N/A** | No inherited parent architecture spine is identified. The repository Hexalith baseline is strengthened, not weakened, by the domain-centric/DAPR/Aspire decisions. |
| 8 | Every altitude dimension is decided, safely deferred, or explicitly open | **Fail** | Boundaries, data ownership, integration, release, and most security concerns are decided; production environment/runtime selection, operational evidence policy, retention/restore, and scale posture are not (C1, M6). |

## Explicitly passed areas

- The required design paradigm, invariant/rule set, conventions, stack, structural seed, capability map, and Deferred section are present and lint-clean.
- AD-1 through AD-9 provide a coherent DAPR-backed hexagonal/event-sourcing core with actor-owned mutation, stable event identity, at-least-once projection semantics, and topology co-change rules (`_bmad-output/planning-artifacts/architecture.md:56-145`).
- Query metadata/provenance, command-status location, outbound sidecar headers, and rebuild equivalence are detailed enough to prevent common cross-host divergence (`_bmad-output/planning-artifacts/architecture.md:181-315`).
- Release identity and exact-SHA consumer-parity decisions are unusually explicit about immutable evidence and fail-closed acceptance (`_bmad-output/planning-artifacts/architecture.md:153-171`, `_bmad-output/planning-artifacts/architecture.md:323-368`).
- OpenBao is a current DAPR-supported stable component, and AD-24 clearly separates operational secret retrieval from payload-encryption key custody (`_bmad-output/planning-artifacts/architecture.md:380-398`).
- AD-25 and its evidence profile align with the OQ8 platform packet rather than claiming generic exactly-once behavior; the append-fence limitation is stated honestly (`_bmad-output/planning-artifacts/architecture.md:400-460`, `_bmad-output/planning-artifacts/architecture.md:606`).
- The 2026-09-08 PRD ratification of the shared JWT contract does not require a competing architecture decision: the spine correctly inherits the fail-closed posture through AD-10/16/18, and the implementation centralizes JWT validation in `JwtBearerAuthenticationContract`.
- All FR1-FR37 and NFR1-NFR19 identifiers appear in the architecture's binding/traceability surface; no template tokens, TODO-only ADs, or duplicate AD identifiers were found.

## Gate recommendation

Do not accept the current `final` spine. Resolve C1 and C2 before any production-readiness claim. Resolve H1-H4 before validation acceptance, either by making the current brownfield contract explicit or by marking target rules prospective with named implementation/evidence owners. Assign every medium item to an autofix, discussion outcome, or explicit safe deferral; batch the low editorial fixes with that update. Re-run deterministic lint and this reviewer gate after the canonical file changes.
