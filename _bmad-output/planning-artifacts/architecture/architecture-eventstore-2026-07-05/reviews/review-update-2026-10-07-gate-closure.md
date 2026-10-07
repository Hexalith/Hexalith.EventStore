---
review: gate-closure
run: architecture spine update 2026-10-07 (gate-fix batch)
reviewer-label: tool-persona
reviewer-mode: read-only (only this report written; no git mutation, build, or test)
created: 2026-10-07
---

# Gate-Closure Review: 2026-10-07 Gate-Fix Batch

> **Assurance label.** This review is `tool-persona` evidence under the PRD Assurance Control. It is not an
> approval, a ratification, or an `independent` review, and it changes no gate result. The owner-role registry
> names one human, who holds every role.

## Anchors

| Item | Value |
| --- | --- |
| Review subject | `_bmad-output/planning-artifacts/architecture.md`, SHA-256 `31ed32597669bec31a7ce23d3cff53d19dfd53df2d830e00eac72c429a96411c` (570 lines, `status: draft`). The workspace symlink `ARCHITECTURE-SPINE.md` resolves to the same bytes. |
| Prior reviewed version | SHA-256 `e15b079f475c03603ac895213bfdd8ce0f68d0fdb90985d23e38d7874ac3f6cf`. It was reconstructed byte-exactly by applying the session scratchpad `arch.diff` to `architecture.pre-update-2026-10-07.md` (`ca7898c7…`). The result hashed to `e15b079f…f6cf`, so the diff below is exact. |
| Gate-fix delta | 62 insertions and 44 deletions across AD-5, 8, 10, 11, 12, 13, 17, 18, 24, 25, 26, 27, 28, 33, 34, 35, and 36, plus Conventions, Stack, and gate rows. Neither diagram changed. |
| Prior findings | The four `review-update-2026-10-07-*.md` reports (adversarial-divergence, rubric-walker, technology-reality, reconcile-closure). |
| Dispositions | `.memlog.md` 2026-10-07 gate entries (the "Gate fix …" decisions and the deferred-question entry). |
| Level below, checked | `prd.md` glossary Assurance Control (`:200`) and §11.4 READY rule (`:670`); `epics.md` Stories 9.2–9.4 (`:7350-7468`); source files named inline. |

Line numbers `:NNN` refer to the subject bytes unless stated otherwise.

## Verdict

**CHANGES REQUIRED: not yet closure-clean.** Of the 22 prior critical or high findings, 18 are CLOSED and 4 are
PARTIAL. One of the four is the critical adversarial C1. None is OPEN. The batch also introduces **2 new high**
divergences, both inside new inline `[ASSUMPTION]` clauses. Lint is clean, and both diagrams are unchanged and
still parse. AD-26 stays `[ASSUMPTION]` and the spine stays `draft`. Nothing here changes a gate posture.

| Bucket | Count |
| --- | ---: |
| Critical/high CLOSED | 18 |
| Critical/high PARTIAL | 4 (adv C1, adv H6, rubric H1, rubric H2) |
| Critical/high OPEN | 0 |
| New critical | 0 |
| New high | 2 (N1, N2) |
| New medium | 8 |
| New low | 10 |

## 1. Critical and high closure

### Adversarial-divergence

| ID | Verdict | Closing spine text (≤30 words) | Residual / fix |
| --- | --- | --- | --- |
| C1 | **PARTIAL** | AD-36 `:408`: "Every sidecar-routed call into a protected EventStore or domain-service endpoint is either a delegated-user call or a workload call." | Two adopted rules still require workload credentials on human-initiated internal hops. AD-31 `:374` requires "AD-28 channel and AD-36 workload authentication for invoked operations, AD-29 audit", and operator replay through Admin is a delegated-user call. AD-27 `:350` says internal platform-operation scope "uses … AD-28 channel and AD-36 workload authentication/authorization", which rules out Admin-delegated platform operations. Design Paradigm `:75` still says "internal calls carry a channel token and a scoped workload assertion". **Fix:** in AD-31 and AD-27, replace "AD-36 workload authentication" with "the AD-36 credential kind the route admits", and make the same correction at `:75`. |
| C2 | CLOSED | AD-5 `:149`: "Component scopes and ACLs act per component, not per key…" plus `[ASSUMPTION]` "…AD-9 parity is reached by moving the AppHost to that posture, never by widening production." | The closure itself introduces N2 (several app IDs host actors). |
| H1 | CLOSED (assumption-scoped) | AD-10 `:185`: "`[ASSUMPTION]` The contract defines versioned validation profiles … human bearer, AD-36 workload assertion (tenant, role, and administrator claims forbidden), and resource-bound delegation." | Medium residuals NM-2 (mapping families to profiles) and NM-3 (adopted text that depends on the assumption). |
| H2 | CLOSED | AD-36 `:410`: "today the families are projection-change provenance and trusted-effect delegation"; AD-24 `:308`: "`[ASSUMPTION]` internal proofs move to dedicated keys inventoried here rather than the AD-25 digest ring." | Low NL-9: the HMAC format of the gateway proof is not named as a platform-owned contract. |
| H3 | CLOSED | AD-8 `:169`: "`PubSub` and any Story 2.13 fan-out become production-eligible only through an AD-26 profile change that names a binding-capable issuer." | Medium NM-1: the drop policy conflicts with the Delivery-failure paragraph. |
| H4 | CLOSED | AD-17 `:251`: "declares exactly one version in its `Contracts` declaration, the only source of that version"; AD-11 `:193`: "written into its schema rather than a second manifest." | — |
| H5 | CLOSED (spine side) | AD-12 `:217`: "Live-evaluation results are evidence only and never seal." plus `[ASSUMPTION]` "That run is a dedicated required transition workflow, owned by Story 9.2…" | Medium NM-6: the transition list differs from the AD-11 states and from Story 9.2's AC. |
| H6 | **PARTIAL** | AD-26 `:342`: "`[ASSUMPTION]` Ratification binds the digest of AD-26 and of the clauses it names in the Story 9.4 clause ledger, not the whole file…" | The binding target does not exist. Story 9.4's Stable Clause Ledger (`epics.md:7434-7450`) lists **PRD §7.1 requirement clauses** with evidence-identity fields. It holds no architecture clauses and no clause-byte digests. Story 9.3 also "rejects … any approval that predates the bytes it binds" over the whole `architecture.md` SHA-256 (`epics.md` 9.3 AC 1). An edit outside AD-26 therefore still invalidates the ratification, and the loop does not converge. **Fix:** bind the ratification to a canonical digest of AD-26 and the AD clauses it names, recorded in an architecture-clause section of the Story 9.3 baseline manifest, and have 9.3 check that approval against those clause bytes. |
| H7 | CLOSED | AD-34 `:394`: "The Story 3.17 inventory … is the only operation/component matrix … a gitlink change that alters a bound version fails the Story 3.17 guard until requalified." | Low NL-8: the inventory file is absent but not declared absent. |
| H8 | CLOSED (wording) | AD-28 `:356`: "`[ASSUMPTION]` … every actor method that discloses, admits, or mutates validates an EventStore-issued execution context bound to tenant, operation, and fence…" | The closure introduces N1 (fence binding cannot be met for admission and reads). |

### Rubric-walker

| ID | Verdict | Closing spine text (≤30 words) | Residual / fix |
| --- | --- | --- | --- |
| H1 | **PARTIAL** | AD-36 `:408` call classes (as in C1); AD-33 `:386` `[ASSUMPTION]` "Each route entry also declares the credential kind it admits (delegated-user or workload)…" | Two parts of the proposed fix did not land. (a) Diagram edge `:81` still shows REST/UI/Admin/McpCli only as "public HTTP; authorization at edge", with no delegated-user service-invocation edge, and `:75` contradicts AD-36. (b) AD-28 `:356` "Every non-Development DAPR app endpoint" still does not say whether a public controller reachable both through ingress and through the app channel must require the channel token. **Fix:** add one AD-28 sentence scoping the token check to routes mapped only for sidecar delivery (dual-reachable routes authenticate by credential kind), and relabel `:81`. |
| H2 | **PARTIAL** | Gate row `:543`: "The fallback policy exists only in the uncommitted DomainService SDK…"; AD-28 `:356`: "the AD-16 fallback is never satisfiable by the channel scheme." | The conflict between UI-host static assets and login and PRD §9.2 is recorded nowhere. AD-16 `:243` allows exactly three anonymous endpoints. Memlog `:114` (an older AD-16 decision) still mentions "static-public/OIDC callback endpoints", which contradicts the spine. **Fix:** add to the `:543` safe posture that interactive UI static assets and login callbacks conflict with AD-16 under PRD §9.2, and that the product owner decides before readiness. |
| H3 | CLOSED (routed) | Gate row `:551`: "correct-course extends its scope to the gateway `ClaimsTenantValidator` … and decides the migration of the existing `system` tenant and `RestTenantSource.System`." | Low NL-10: the Admin Server and SignalR hub boundaries are not named. |
| H4 | CLOSED (routed) | AD-33 `:386`: "until the envelope activates, production readiness fails and Development resolves routes from the `Contracts` declarations"; row `:557`: "correct-course assigns it a story ahead of Stories 2.14 and 2.15." | — |
| H5 | CLOSED | Row `:544`: "Under `Direct`, a lost regeneration may return `304 Not Modified` on changed data; no freshness claim beyond read-model evidence." | — |
| H6 | CLOSED | AD-28 `:356`: "it never distinguishes sidecar delivery from peer invocation"; row `:543`: "`/**` ACLs let peers reach channel-only routes and actor methods … Story 5.7 as the candidate owner…" | — |
| H7 | CLOSED | AD-26 `:344` binds "the production AD-36 workload-assertion issuer and global-administrator bootstrap credential"; row `:545`. | — |
| H8 | CLOSED | AD-34 `:396`: "…and Admin's `DaprInfrastructureQueryService` reads of Dapr private actor-state keys. The list is non-exhaustive until the Story 3.17 inventory completes." | — |

### Technology-reality

| ID | Verdict | Closing spine text (≤30 words) |
| --- | --- | --- |
| H1 | CLOSED | Stack `:441`: "`1.14.x` is unsupported … `1.18.3` fixed a placement mass-disconnect, so AD-26 requires one tested pin of at least `1.18.3`"; row `:566`: "while the Kubernetes guide pins unsupported `1.14.4`". |
| H2 | CLOSED | AD-18 `:257`: "that outbound token is the caller's own `DAPR_API_TOKEN` for its sidecar, never the receiver's AD-28 `APP_API_TOKEN`"; AD-24 `:308` and AD-26 `:344` ("`DAPR_API_TOKEN` enablement"). |
| H3 | CLOSED (see N2) | AD-5 `:149`: "Any remaining writer is proven absent at code level through the Story 3.17 inventory"; the key prefix is bound into the AD-26 digest. |
| H4 | CLOSED | AD-11 `:205`: "`ReleaseEvidenceCodec` does not yet exist in EventStore or in Builds at the pinned gitlink…"; row `:546`. |

### Reconcile-closure

| ID | Verdict | Evidence |
| --- | --- | --- |
| DR-1 | CLOSED | AD-12 `:217` quotes the PRD seal definition outside the assumption, including "under the platform's authenticated CI identity". |
| DR-2 / LC-2 | CLOSED | AD-13 `:224`: "Story 6.6 permits no application SQL, Npgsql, PostgreSQL schema, Dapr private actor key, or provider fork, and has no AD-34 exception path…" |
| DR-3 | CLOSED | AD-26 `:342`: "one authenticated, content-bound ratification". |
| DR-4 | CLOSED | AD-34 `:394`: "never expose a bypass to application code". |
| DR-5 | CLOSED | AD-26 `:344`: "OpenBao contract digest and server floor"; recorded in the memlog. |
| DR-6 | CLOSED (generic) | Preamble `:534`: Story 9.1 record, and Architecture/Security "wherever an AD names them". Low residual: "Story 5.10 keeps the reserved-`system` guard" and "Story 7.1 production acceptance" (broker) were not restored. |
| DP-1 | PARTIAL (low) | Only `cryptography-overview` was added. The other SCP 2026-10-05 §1 URLs (`supported-bindings/signalr/`, `supported-state-stores/`, `cryptography_api/`, `supported-cryptography/azure-key-vault/`) are absent. |
| DP-2 | CLOSED | `v1.18.2` and `fluentui-blazor v5.0.0` are in `sources`. |
| LC-1 | CLOSED | AD-34 `:394`: "required correctness, security, compatibility, and operational guarantees". |
| LC-3 | CLOSED | AD-35 `:402`: "including for infrastructure administration". |

### Mediums and lows the batch addressed (spot-verified)

| Finding | Verdict |
| --- | --- |
| Adv M3, M4, M5, M7, M9, M13 | CLOSED (AD-28 channel policy; AD-8 Contracts ceilings; AD-11 derived count; AD-33 activated-catalog audience; AD-25 per-form verification; AD-34 fault-injection rows) |
| Adv M6 | CLOSED under `[ASSUMPTION]` (AD-35 Admin Server termination) |
| Rubric M1–M5, M9, M11–M17 | CLOSED |
| Rubric M7 | PARTIAL: in-text scoping, but the heading tag remains (NM-4) |
| Rubric M8 | PARTIAL: AD-36 now says "until that story reaches `done`", but gate row `:542` still says "while … Story 5.5 review" and "when Story 5.5 exits review" (NL-2) |
| Tech M1–M6, L1, L2, L6 | CLOSED |
| Tech L3 | PARTIAL: `placement:latest` is recorded; the deprecated `-components-path` flag and the missing `APP_API_TOKEN` in the `deploy/README.md` override are not |
| Tech L5 | PARTIAL: six URLs were added; the Aspire 13.6.0, CommunityToolkit NuGet, and actors-concepts pages are absent |

Deferred items (adv M1, M2, M8, M10, M11, M12; rubric M6, M10; tech L7) are recorded in the memlog
deferred-question entry with triggers. That disposition is acceptable.

## 2. New divergences introduced by the batch

### N1 (high): The AD-28 execution context cannot be bound to a fence for admission, reads, or freshness, and the spine does not say whether it is the AD-5 context (AD-28 `[ASSUMPTION]` vs AD-5)

- **Unit A (AD-5 `:147`):** `AggregateActor` "accepts only an internal fenced execution context". "`null`, empty, unknown, or stale fences never authorize work". The requirement "does not apply to reads".
- **Unit B (AD-28 `:356`, `[ASSUMPTION]`):** "every actor method that discloses, admits, or mutates validates an EventStore-issued execution context bound to tenant, operation, and fence".
- **Divergence:** `IdempotencyAdmissionActor` *issues* the fence, so a call that asks it to admit cannot carry one. Disclosure methods (`GetEventsAsync`, `ReadEventsRangeAsync`) have no fence under AD-5. `ETagActor.RegenerateAsync`, which is the whole production-eligible `Direct` freshness path, has no admission. If one context is meant, AD-5's mutation-only fenced context is stretched to reads and to admission, which is unsatisfiable. If two are meant, the second is unnamed, has no issuer key, and `ProcessFencedCommandAsync` validates both. Implementers will either synthesize a fence, which violates AD-5, or skip the check. Read literally, the clause leaves no conforming command-admission path, which would be critical if confirmed as written.
- **Closing wording (AD-28):** "The AD-28 execution context is the AD-5 fenced execution context for mutating aggregate methods. Admission, read, and freshness methods validate the same EventStore-issued context bound to tenant and operation without a fence, and no method accepts a synthesized fence."

### N2 (high): Several app IDs host actors, but the AD-5 assumption and AD-26 assume one actor host and one `statestore` (AD-5 `[ASSUMPTION]` vs AD-26, AD-31, DomainService SDK)

- **Unit A (AD-5 `:149`, `[ASSUMPTION]`):** "the `actorStateStore: true` component is scoped to the actor-host app ID alone in every profile". AD-26 `:340` names exactly one component: "`statestore` with stable `state.postgresql` v1 and `actorStateStore: true`".
- **Unit B (actor hosts that exist today):**
  - `eventstore` hosts `AggregateActor` and others (`Server/Configuration/ServiceCollectionExtensions.cs:240-253`).
  - `eventstore-operations` hosts `DeadLetterDrainActor` (`Operations/Program.cs:30`). This is the AD-31 sink owner.
  - Every domain service that calls `AddEventStoreReminders` registers `ReminderActor` (`DomainService/EventStoreReminderServiceCollectionExtensions.cs:103-114`).
  - The DomainService SDK also defaults its DataProtection and reminder `StateStoreName` to `"statestore"`, "the component every Hexalith domain already binds" (`EventStoreDataProtectionOptions.cs:27-30`, `EventStoreReminderOptions.cs:30`). This contradicts `deploy/dapr/statestore-postgresql.yaml:12-14` ("Domain services have zero state store access").
- **Divergence:** Story 4.16 or 5.7 scoping the single `statestore` to `eventstore` alone removes the actor runtime from Operations and from every typed-reminder domain service, and breaks SDK DataProtection key storage. Widening the scope instead reopens C2. Dapr allows one actor state store per sidecar, so each actor host needs its own component. The batch also leaves unowned the data migration that the assumption implies: projection checkpoints, command and stream activity, the admin operational index, and snapshot policy are written to `statestore` today through `DaprClient` (for example `Server/Projections/ProjectionCheckpointTracker.cs` and `Indexes/AdminOperationalIndexHostedService.cs`). AD-34 requires migration and requalification for that move.
- **Closing wording (AD-5 and AD-26):** "Each actor-hosting app ID (EventStore, `eventstore-operations`, and each domain service that enables typed reminders) has its own `actorStateStore: true` component scoped to that app ID alone, and state any other app ID reads or writes lives in separately named, separately scoped components. The AD-26 profile and the Story 3.17 inventory enumerate every such component, its key prefix, and its data migration, and no SDK default resolves to another app's actor store."

### Medium

| ID | Divergence | Closing wording (≤2 sentences) |
| --- | --- | --- |
| NM-1 | AD-8 freshness drop (`:169`) conflicts with AD-8 Delivery failure (`:171`). Delivery failure says "Every production subscriber … acknowledges a poison or terminally rejected message only after … the configured AD-31 sink" accepts a durable record, and it allows a cataloged policy only for `SkippedUnknownEventType` and `SkippedNoHandlers`. Once `PubSub` becomes eligible, the hub subscriber therefore cannot drop. The drop set ("denied, expired, or unbound") also omits oversized and malformed signals, which AD-8 says the receiver rejects. Those fall back to AD-31 capture and would store the provenance credential that AD-10 bans from evidence. | "Projection-change freshness signals are excluded from the Delivery-failure capture rule. Every rejected freshness signal, whether denied, expired, unbound, oversized, or malformed, follows the freshness-only policy." |
| NM-2 | The AD-10 profiles do not map to credential kinds. The workload profile forbids tenant claims (`:185`), yet projection provenance is issued by the workload issuer with tenant, projection, and topic bindings (`:410`; spec-5-5 `:22`). The AD-29 delegation that AD-36 admits for delegated-user calls has no listed profile, so "a validator outside them … fails the host gate". | "Each AD-36 resource-bound family and the AD-29 delegation is validated through the AD-10 resource-bound delegation profile, whose binding claims are claim types distinct from the forbidden tenant-grant claim." |
| NM-3 | Adopted text depends on assumptions. In AD-10, "A host validates a credential only through a registered profile…", "the human-bearer operation claim…", and "For the human-bearer profile, … mandatory" are untagged but presuppose the `[ASSUMPTION]` profiles. If those are rejected, the universal tenant/role rule is gone. AD-36's adopted "the kind its route does not admit" presupposes the AD-33 `[ASSUMPTION]` route-kind field. The AD-11 split (`:193`) moved "The release lane fails … rather than a second manifest" out of the assumption, where it was part of one `;`-joined sentence in `e15b079f`, so it is now adopted. | Either tag each dependent sentence, or state in AD-36 that "each route declares the kind it admits in its `Contracts` route declaration until the AD-33 envelope activates" and keep AD-10's dependent sentences conditional on the profile set. |
| NM-4 | AD-26 is still tagged `[ASSUMPTION]` in its heading (`:336`), while the text says "Only this target selection is the assumption; the Ratification and Production proof mechanics below are adopted" (`:340`). The preamble (`:110`) defines only the inline tag (rubric L4 open). "This target selection" leaves `:340`'s Redis/Cosmos and no-schema sentences ambiguous. The lint does not inspect tags: a negative control that removed AD-30's `[ADOPTED]` went undetected. A mechanical Story 9.3 or 9.4 reader will treat all of AD-26 as an assumption. | Add to the preamble: "AD-26's heading `[ASSUMPTION]` covers only its first Rule sentence (target selection)", and end that sentence with the scope statement. |
| NM-5 | AD-28 `[ASSUMPTION]` clause (b), "the channel alone admits only Dapr runtime callbacks (activation, timers, reminders) and subscription delivery", narrows the adopted sentence before it, which lists "subscription discovery … actor callbacks" as channel-admitted. Subscription discovery (`/dapr/subscribe`), actor configuration (`/dapr/config`), and deactivation drop out. A literal implementation would break sidecar discovery. | "…admits only Dapr runtime callbacks (actor configuration, activation, deactivation, timers, reminders), subscription discovery, and subscription delivery." |
| NM-6 | The AD-12 `[ASSUMPTION]` transitions (readiness, `release-available`, `production-promoted`, consumer removal) differ from Story 9.2's guarded transitions (high-risk gate PASS and story `done`, `epics.md:7384-7386`). They also omit AD-26 ratification and `evidence-validated`, while AD-11 `:209` rejects any validator result "not retrieved from a sealed AD-12 CI run". Story 9.2's AC owns no transition workflow, and the preamble propagation note (`:534`) covers only AD-34–36 IDs. | "The seal workflow covers every guarded transition named by AD-11, AD-22, AD-26, and Story 9.2." Extend the `:534` propagation note to AD changes that reassign story ownership (AD-12, AD-26). |
| NM-7 | AD-18 ¶2 (`:259`) permits "any inbound bearer or header-forwarding handler" on gateway clients, and the Dapr handler replaces only `dapr-app-id` and `dapr-api-token`. AD-18 ¶4 and AD-36 forbid a bearer on workload calls and an assertion on delegated-user calls, but no platform handler is assigned to remove the credential that does not belong. A host-added forwarder on a workload client then produces a both-kinds request that AD-36 rejects. | "Platform handlers enforce the route's credential kind: they remove `Authorization` on workload clients and any assertion on delegated-user clients." |
| NM-8 | A delegated-user call "forwards only that human's AD-10 bearer … for the receiver's human audience" (`:408`). With no exchange step, this works only if the IdP mints a token whose `aud` includes every relay receiver. AD-10 validates per-host audiences (`JwtBearerAuthenticationContract.cs:157-170`), and neither token exchange nor a multi-audience token is decided. | "A forwarded bearer's audience set must include the receiver's human audience; a token exchange requires an owner-approved AD-10 profile." |

### Low

| ID | Finding |
| --- | --- |
| NL-1 | Capability map `:528`: the FR34-FR35 row now omits AD-24 and AD-36. Both gained FR34 in this batch (Binds script delta, old vs new). |
| NL-2 | Gate row `:542` still says "literal contract still in Story 5.5 review" and "when Story 5.5 exits review", while AD-36 `:410` says "until that story reaches `done`". The tracker shows 5.5 `in-progress`. |
| NL-3 | AD-35 `:402`: the inline tag is mid-sentence before a `;`, so it is ambiguous whether "it reaches no stream…" falls under it. AD-35 routes McpCli over public edges, while AD-36 `:408` lists McpCli among sidecar-routed delegated-user callers. |
| NL-4 | AD-24 states two digest-key retirement conditions (Rule: "an AD-25 catalog proves that no live references remain"; Secret contract: "every registered consumer, including trusted-effect gateway proofs") without saying both apply. |
| NL-5 | Gate row `:540` says "Story 3.19 then authors" the profile, while AD-26 `:344` says "Story 3.19 drafts". |
| NL-6 | AD-12's "required" transition workflow must report a no-op pass on PRs that propose no transition. Otherwise a path-filtered required check stalls `main`, which contradicts "never `main`". |
| NL-7 | Open `[ASSUMPTION]` tags rose from 4 inline to 11 inline plus the AD-26 target. Story 9.3 rejects every open tag, and none has a per-assumption owner or trigger beyond the G-BASELINE row `:538`. |
| NL-8 | `docs/architecture/dapr-infrastructure-inventory.yaml` (AD-34 `:394`, bound by AD-26) does not exist and is not declared absent. This is the same class as tech H4. |
| NL-9 | AD-36 forbids host-defined signing protocols, but `TrustedEffectGatewayProof`'s HMAC format is not named as a ServiceDefaults- or Contracts-owned contract. |
| NL-10 | Rubric H3 residual: row `:551` names the gateway validator but not the Admin Server or SignalR hub tenant boundaries. |

### Requested lenses, result

| Lens | Result |
| --- | --- |
| AD-36 classes vs AD-18 ¶2 and AD-29 | Consistent on handler order. Gaps: NM-7 (removing the wrong credential), NM-2 (AD-29 delegation has no profile), and the C1 residual in AD-31/AD-27. |
| AD-28 execution context vs AD-5 | **N1**. |
| AD-5 actor-store scoping vs AD-3 Admin reads and AD-31 | **N2**. Admin reads of actor-owned state must go through the actor boundary, which under the AD-28 assumption means through EventStore, so `DaprInfrastructureQueryService` needs a new path. Covered by the AD-34 non-conformance. |
| AD-26 "target selection alone" vs heading tag | NM-4. |
| AD-26 clause-ledger binding vs PRD "bound to the subject digest" | PRD `:200` allows a clause-scoped subject. The binding target (Story 9.4) and the Story 9.3 check do not support it: adv H6 PARTIAL. |
| AD-12 seal workflow vs AD-11 assurance | NM-6 and NL-6. The seal definition matches PRD `:200` verbatim. |
| AD-8 drop policy vs Delivery failure and AD-31 | NM-1. |

## 3. `[ASSUMPTION]` inventory and scope check

There are 11 inline tags plus the AD-26 heading target, which is the expected count. "Scope" means whether the tag covers exactly its own clause and no adopted text.

| # | Location | Clause | Scope |
| --- | --- | --- | --- |
| 1 | AD-5 `:149` | Actor store scoped to the actor host alone; other readers use a separate component; AD-9 parity moves the AppHost | One sentence with three coordinated parts. Scope is clean, but the content is wrong (N2). |
| 2 | AD-10 `:185` | Versioned validation profiles | One sentence, but the next two adopted sentences depend on it (NM-3). |
| 3 | AD-11 `:193` | Story 3.18 inventory is the sole compatibility authority | Scope **narrowed** by the split: the release-lane and 2.15 sentence is now adopted (NM-3). |
| 4 | AD-12 `:217` | Seal run is a Story 9.2 dedicated transition workflow | Clean. Transition list is incomplete (NM-6). |
| 5 | AD-17 `:251` | Route entry, generator metadata, and inventory entry are derived with a digest; activation fails on mismatch | Clean. |
| 6 | AD-24 `:308` | Internal proofs move to dedicated keys | Clean (final clause, mid-sentence). |
| 7 | AD-26 `:342` | Ratification binds AD-26 and the named clauses in the Story 9.4 ledger | Clean. Target is wrong (adv H6). |
| 8 | AD-27 `:350` | Platform namespace in Contracts and AD-33, authorized by the AD-10 human-bearer operation claim | Clean. |
| 9 | AD-28 `:356` | Execution context until AD-34 qualifies actor-invocation restriction; channel-only set | One sentence, two clauses. Content conflicts with AD-5 (N1) and with the preceding adopted sentence (NM-5). The following "mTLS, ACLs…" sentence is pre-existing adopted text and sits outside the tag. |
| 10 | AD-33 `:386` | Route entry declares credential kind and AD-36 operation | Clean, but AD-36 adopted text depends on it (NM-3). For delegated-user routes the "AD-36 operation" should read "the required operation". |
| 11 | AD-35 `:402` | Admin operations terminate at Admin Server | Mid-sentence; the end of the scope is ambiguous (NL-3). |
| — | AD-26 heading `:336` | Production target selection | Heading tag versus in-text scope (NM-4). |

## 4. Structural checks

| Check | Result |
| --- | --- |
| Theme table `:112-117` | Every AD-1 through AD-36 appears exactly once. AD-34 and AD-35 are under Platform; AD-36 is under Runtime/security. **Pass.** |
| Consistency Conventions `:414-428` | The Identity (correlation never ULID/GUID) and Serialization (`EventStorePayloadSerialization`, which exists at `Contracts/Serialization/EventStorePayloadSerialization.cs`) rows match the ADs. The HTTP-security row cites AD-10, 16, 18, 28, and 36. **Pass.** Optional: name delegated-user credentials in the HTTP-security row. |
| Capability map `:522-530` | The only new inconsistency is NL-1. Pre-existing padding (rubric M10) is deferred. |
| Diagram 1 `:79-104` | Byte-identical to the `e15b079f` block. The mermaid 11.12.3 flowchart grammar parses it (46 vertices / 23 edges). The negative control (`bad.mmd`) is rejected and the `;`-in-label control parses. **Semantic drift:** there is no delegated-user service-invocation edge, and `:75` prose contradicts AD-36 (rubric H1 PARTIAL). |
| Diagram 2 `:479-518` | Byte-identical. Parses (47 vertices / 16 edges / 3 subgraphs). It shows the current topology, so it does not conflict with N2. The production target omits the Scheduler, which AD-26 now binds (cosmetic). |
| Lint | `uv run .claude/skills/bmad-architecture/scripts/lint_spine.py --workspace …/architecture-eventstore-2026-07-05` returns `ok: true`, `total_findings: 0`, exit 0. Negative control (scratch copy with AD-29 `Prevents` removed and AD-30 `[ADOPTED]` removed): `ok: false`, 1 high (`AD-29 missing required field(s): prevents`), **exit 0**. So `ok` is the only signal, the exit code is not, and status tags are not linted. |
| Reserved labels | `independent` appears only as the registry-computed label (AD-11 `:209`, Conventions `:418`). No second-reviewer language appears. **Pass.** |

## Disposition

- This is the only file written. No spine, memlog, epics, tracker, or code edit was made.
- To close the gate: apply the C1 residual wording (AD-31, AD-27, `:75`); retarget the AD-26 ratification binding to a Story 9.3 architecture-clause digest (adv H6); add the AD-28 dual-reachable-route sentence and the diagram edge (rubric H1); record the UI-host conflict with PRD §9.2 (rubric H2); resolve N1 and N2. Then re-run the adversarial lens against the new digest.
- AD-26 stays `[ASSUMPTION]`, the spine stays `draft`, and every Implementation Status row keeps its posture. Closure of these findings ratifies nothing; that remains the owner's time-separated attestation under the AD-11 assurance level.
