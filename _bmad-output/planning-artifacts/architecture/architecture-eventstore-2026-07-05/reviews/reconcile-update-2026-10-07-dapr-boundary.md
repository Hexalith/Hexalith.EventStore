# Architecture Update Input Reconciliation — 2026-10-07 (Dapr Infrastructure Boundary)

> Read-only input-reconciliation review for the 2026-10-07 spine Update run. The review edits nothing except this
> file. Citations use **AD identifiers** first; `l.NNN` line anchors refer to the spine blob below and become
> unreliable once the Update run edits the file.

## Baseline

| Item | Value |
| --- | --- |
| Spine | `_bmad-output/planning-artifacts/architecture.md`, blob `739c6343`, SHA-256 `ca7898c7…0cab`, clean against HEAD `27ac3c62` |
| Direct spine edits reconciled | `9b525ba8` (2026-10-04, D-ARCH SQL adapter), `5badddbb` (2026-10-05, Dapr-only supersession), `23680543` (2026-10-05, PRD §8.4 rewrite and diagrams), `a6fc951e` (2026-10-06, AD-13 loader bullet) |
| Inputs (SHA-256 prefix) | SCP 2026-10-05 `06f25756…`; PRD `ca97f4ef…` (worktree; carries uncommitted 2026-10-07 correct-course edits that leave §8.4 unchanged); Dapr-only amendment `2c1dc2e8…`; trusted-code amendment `2eb79903…`; draft payload Dapr amendment `2a162836…`; metadata-adapter contract `a4abe9c2…` |
| Memlog | The brief says the last entry is 2026-09-23. The memlog now also has the 2026-10-07 Update-run entries (l.151–155). They recover the four commits "as adopted" and say they were "restructured without changing meaning". Several recommendations below add content that §8.4 and the amendments approved but the spine never landed, such as the no-fallback rule, credential isolation and the shared evolution service. The memlog must record those as **new decisions with their sources**, not as meaning-preserving restructuring. |

## Verdict

**CHANGES REQUIRED.** The direct edits land the core of PRD §8.4, which makes Dapr the required boundary, adds operation/component qualification and keeps actor ownership. They also keep the draft payload amendment non-authorizing. Four defects need fixing:

- §8.4's no-fallback rule is missing.
- The project-wide qualification rule sits inside the unratified AD-26 `[ASSUMPTION]`.
- The withdrawal of SQL authority applies "for Story 6.6" only.
- The retained Redis backplane conflicts with AD-26 and has no gate row.

Story 6.6 content is spread across AD-1, AD-13 and AD-26.

| Severity | Count | IDs |
| --- | ---: | --- |
| High | 4 | H1–H4 |
| Medium | 8 | M1–M8 |
| Low | 9 | L1–L9 |

## (a) Directive reconciliation

Status key: **LANDED** means it is in the spine and enforceable. **PARTIAL** means it is present but weakened, misplaced or incomplete. **MISSING** means it is absent. **CONTRADICTED** means the spine says otherwise. **HELD** means it is correctly not landed (non-authorizing).

### Input 1 — `sprint-change-proposal-2026-10-05.md` (approved P1–P7; §6 application disposition)

| # | Directive | Status | Spine evidence / gap |
| --- | --- | --- | --- |
| 1.1 | Dapr is the required boundary project-wide, not only for 6.6 (P2) | LANDED | AD-1 l.101: "with Dapr as the required infrastructure boundary whenever the required capability is supported. Apply PRD §8.4 to all EventStore runtime packages and hosts." |
| 1.2 | Provider dependencies sit behind Dapr components. An unavailable operation needs a documented exception (P2). | LANDED (stricter) | AD-1: "an evidence-backed, owned, exact-path architecture exception. Unknown suitability stays unresolved." |
| 1.3 | A generic binding that carries SQL is not portability evidence and cannot bypass actor ownership (P2) | LANDED | AD-1, verbatim intent |
| 1.4 | Select the highest applicable abstraction and qualify its actual guarantees (P2) | LANDED | AD-1: "qualify its actual correctness, security, compatibility, and operational guarantees" |
| 1.5 | "Retain the existing Story 6.6 amendment" in AD-1 (P2; planning-reconciliation spec: "broaden AD-1 while retaining 6.6 constraints") | LANDED, poorly structured | AD-1 l.101 carries the supersession narrative and the story link. A restructure must leave a pointer in AD-1 so this instruction stays satisfied (see (b)). |
| 1.6 | AD-3 operational reads go through Dapr APIs, with no provider drivers or private actor keys/tables (P2) | LANDED | AD-3 l.118 |
| 1.7 | Every supported profile does per-operation/component qualification: ETags, transaction scope, TTL, ordering, failure. A shared backend gives no cross-component transactions. A backend change requires requalification (P2). | PARTIAL (placement) | Text is in AD-26 l.300. AD-26 is `[ASSUMPTION]` and production-only, while the rule covers "every supported profile" and was owner-approved (H2). |
| 1.8 | Diagram labels: Dapr state/actor access, service invocation, secrets/crypto/bindings behind the sidecar when qualified, any retained SignalR adapter as an explicit exception edge, and labels on public HTTP, browser SignalR and telemetry (P2) | LANDED, with label defects | l.59–82. Redis is rendered as "no accepted exception", which is correct because the register is empty. The secret node label is generic (L1) and the catalog edge over-asserts (L2). |
| 1.9 | Story 3.17 guard rejects invalid exception entries. Each entry carries owner, evidence, allowed paths and review/removal trigger. No global test-folder or provider-namespace exemption (P3.3). | PARTIAL | AD-26 l.300 names 3.17. AD-1 has owner/evidence/exact-path only. The no-blanket-exemption rule and the register location are missing (M1). |
| 1.10 | No application provider credentials in supported profiles where Dapr supplies the operation (P3.5) | MISSING | Absent from AD-1, AD-24 and AD-26 (M2) |
| 1.11 | 3.16: a catalog upgrade cannot establish an exception or introduce a direct provider integration (P3) | MISSING | The "Dependency patch/preview exits" gate row (l.488) does not mention it |
| 1.12 | 2.13: assess before extending the backplane. Retire Redis only after compatibility and rollback proof. Unfinished investigation is not a permanent exception (P4, Epic 2 impact). | PARTIAL / CONTRADICTED | Diagram l.80 and AD-26 l.300 say "Retained direct Redis is not an accepted exception". "Do not extend before 2.13" is missing, and the backplane's production status conflicts with AD-26 (H3). |
| 1.13 | AD-23 cites the current replacement authority. The Dapr change is a new unapproved amendment. Historical approvals stay with their bytes (P5). | LANDED | AD-23 l.261 (`de9ba886…`, `AR-20260913-01`, `AR-20260914-01`, historical `AR-20260801-01`) |
| 1.14 | A Dapr secret store is not a cryptographic key-operation component (P5) | LANDED | AD-23 l.261 |
| 1.15 | Do not replace `pdenc-v2`/AAD bytes with Dapr Crypto Scheme v1 (P5) | PARTIAL | AD-23 says only "Preserve `pdenc-v2`/AAD". The approved explicit non-replacement is in the guide but not in the spine (L6). |
| 1.16 | Unsupported operations return to Architecture/Security for a compatible design, a format migration or an exception (P5) | LANDED | AD-23 l.261 |
| 1.17 | The adapter consumes logical component/key identities and deployment supplies endpoint and credentials (P5, Story 8.6 text) | HELD | Correctly absent. It conflicts with the still-approved frozen §5/§11, and landing it would decide the draft amendment early. |
| 1.18 | Runtime business operations enter through Dapr. Backend diagnostics never substitute. 7.11 verifies logical values through the Dapr boundary (P6). | MISSING | AD-12 l.186 says "inspect persisted state" without naming the path (M4) |
| 1.19 | Routing through Dapr alone does not solve the append race (Epic 4 impact) | PARTIAL | Implicit in AD-5 l.130 and gate row l.481 (L7) |
| 1.20 | Dapr transport does not replace application authorization (Epic 5) | LANDED | AD-10 l.162, AD-28 l.314 |
| 1.21 | No approval, readiness or qualification is inferred, and the register is empty (§6) | LANDED | AD-26 l.300: "Existing AD-26 ratification and production gates still fail closed"; AD-23 "conveys no implementation authority" |
| 1.22 | Package identity/count changes stay in Story 8.8 (P5) | LANDED | Structural Seed l.411; AD-23 |

### Input 2 — PRD §8.4 (l.469–481), FR37 (l.352), §11.2.1 (l.627–633), glossary (l.188)

| # | Directive | Status | Spine evidence / gap |
| --- | --- | --- | --- |
| 2.1 | MUST use Dapr for persistence/actors, messaging, invocation, configuration, secrets, bindings, scheduling/workflows and crypto where they apply | LANDED (by reference) | AD-1 "Apply PRD §8.4" |
| 2.2 | MUST NOT add a database driver, broker client, cloud SDK, **direct provider HTTP call, connection string** or provider schema | PARTIAL | AD-1: "Provider dependencies belong behind Dapr components". Raw HTTP and connection strings are not named, yet 2.13 depends on them (`BackplaneRedisConnectionString`, `EVENTSTORE_SIGNALR_REDIS`). |
| 2.3 | Convenience, familiarity or unmeasured performance is not a gap, and a missing SDK helper does not justify a bypass | MISSING | Not in AD-1. Two units could otherwise disagree on whether a performance preference justifies an exception. |
| 2.4 | An exception records the missing guarantee and evidence, alternatives, isolated adapter, owner, exact paths and review/removal trigger, with the architecture-owner decision **before** the dependency is introduced | PARTIAL | AD-1: "evidence-backed, owned, exact-path". Alternatives, trigger, decide-before and the register location are missing (M1). |
| 2.5 | Unknown suitability requires qualification | LANDED | AD-1: "Unknown suitability stays unresolved." |
| 2.6 | **Never silently fall back to direct infrastructure after a Dapr failure** | MISSING | Fail-closed clause absent everywhere in the spine (H1). The historical adapter contract's "otherwise the selected adapter supplies it directly" shows why it matters. |
| 2.7 | Provisioning, component configuration and backup administration are platform operations. Pure computation and scoped test doubles/diagnostics are not runtime adapters, and neither may expose a bypass. | MISSING | No classification rule in the spine. AD-9 covers co-change only. |
| 2.8 | Exceptions are exact-purpose and exact-path, not blanket | PARTIAL | "exact-path" only |
| 2.9 | Public HTTP, browser SignalR and governed telemetry are distinct transport edges | LANDED (diagram) | l.60–61, l.81 |
| 2.10 | Every runtime operation maps to a qualified Dapr API/component or an accepted exception, with guarantees, exact versions and observed evidence | PARTIAL (placement) | AD-26 l.300 (H2) |
| 2.11 | 3.17 owns the inventory/guard. 2.13 Redis and 8.6 crypto are unresolved. The policy accepts no exception and changes no readiness verdict. | PARTIAL | AD-26 l.300 and AD-23 carry it. The Production Gates table (l.466–488) has **no rows** for 3.17, 2.13 or 8.6 (M6). |
| 2.12 | Accepted-exception register: `docs/architecture/dapr-infrastructure-exceptions.yaml` | MISSING | Not named in the spine or its frontmatter. The 3.17 guard and future exceptions need one agreed location. |
| 2.13 | FR37: production backend through a suitable Dapr crypto component, exceptions under §8.4, formats/custody/typed failure/rollback preserved | LANDED | AD-23 l.261: "PRD §8.4 requires Dapr key-operation qualification before provider SDK selection." |
| 2.14 | §11.2.1 supporting coverage (3.17 / 2.13 / 8.6) | PARTIAL (low) | The Capability map (l.454–464) has no boundary AD |
| 2.15 | §8.4 changes no actor-state authority or readiness gates | LANDED | AD-1 actor-ownership sentences; AD-26 |
| 2.16 | Scope includes samples, linked source and generated-host inputs (guide; 3.17 AC1) | PARTIAL | AD-1 says "runtime packages and hosts". Linked source matters because the SignalR Redis code is compiled into the released Gateway package (L8). |
| — | The glossary "DAPR Boundary" (l.188) omits secrets, bindings, scheduling/workflows and crypto | Out of scope | PRD-internal inconsistency. Route to the PM; no spine change. |

### Input 3 — `story-6-6-dapr-only-amendment.md` (owner 2026-10-05; current constraint)

| # | Directive | Status | Spine evidence / gap |
| --- | --- | --- | --- |
| 3.1 | Supersedes the D-ARCH PostgreSQL/provider-extension design for 6.6. The reviewed spec digest stays historical and is never repinned. | LANDED | AD-1 l.101; AD-26 l.298 ("superseded for Story 6.6") |
| 3.2 | Events, **metadata, snapshots, command results and outbox** are staged under `AggregateActor` and written in **one `SaveStateAsync`** | PARTIAL | AD-1 names "Aggregate/event/snapshot and existing actor drain-registration". Command result, outbox and single-save are missing. AD-5 covers append-coordinator ownership only. |
| 3.3 | An ambiguous save is reconciled by a fresh, addressed Dapr actor read, proven independent of staged state, before retry or acknowledgment | MISSING | No lost-ack rule in AD-1, AD-5 or AD-26 |
| 3.4 | No application SQL, Npgsql, PostgreSQL schema, Dapr private actor key or **provider fork** | LANDED (fork omitted) | AD-1: "Application code does not access PostgreSQL or Dapr private actor-state keys/tables/caches." |
| 3.5 | New non-actor control state uses Dapr state with ETags/transactions only after component proof, or a dedicated actor | LANDED | AD-1: "Other coordinator state may use Dapr state/actor APIs after capability qualification." |
| 3.6 | No transaction across actor state, separate actors, broker or external objects; **use durable intent, idempotency and reconciliation**; no atomicity from a shared database | PARTIAL | AD-26 l.300 has the negative half. The positive mechanism is missing. |
| 3.7 | The component may stay `state.postgresql` behind the sidecar. The application has **no PostgreSQL credential** or schema ownership. Backup is a platform job. | PARTIAL | The credential clause is missing (M2) |
| 3.8 | Evolution contract: stable type plus positive version, allow-listed aliases, contiguous bounded chain, in-memory upcast only, never rewrite history or change identity | LANDED (delegated) | AD-6, AD-13 spec-first, Conventions "Mutation" (l.354) |
| 3.9 | Original application bytes are immutable and actor-owned. Readback proves the logical value only, never physical bytes or provider attestation. | PARTIAL (placement) | AD-26 l.298: "no independent physical-byte or historical-generation receipt is claimed". This is evolution content, not production-profile content. |
| 3.10 | An operation that depends on an unavailable provider proof stays disabled with a **typed unavailable/hold outcome** | MISSING | Fail-closed outcome contract absent |
| 3.11 | A missing/ambiguous mapping, corrupt digest, unreadable payload, unsupported version or incomplete prefix fails closed **before domain code, checkpoint, publication marker or handler effect**; no arbitrary CLR loading or silent skip | MISSING | AD-8 "silent drop is not the default" covers unknown types only |
| 3.12 | **One shared evolution service** for actor replay, projections, subscriptions, reconstruction and inspection; **V2 admission fence kept** until the writer and every serving reader/consumer are compatible; V1 stays additive | MISSING | It is only in the epics 6.6 constraint ("one allow-listed evolution pipeline across consumers") (M3) |
| 3.13 | Live-component evidence; **never direct database reads as product evidence**; record the exact Dapr profile | PARTIAL | AD-12 tension (M4) |
| 3.14 | Does not complete 6.6 or authorize V2 writes, migration, deployment or promotion | LANDED | Implementation-status note l.84; AD-13 "does not prove delivery" |

### Input 4 — `story-6-6-trusted-code-amendment.md` (owner 2026-10-06)

| # | Directive | Status | Spine evidence / gap |
| --- | --- | --- | --- |
| 4.1 | Replaces the spec's universal before-effect loader refusal. Historical spec and failed probe stay unchanged. | LANDED | AD-13 l.193. Its referent, the "universal before-effect loader assurance", is not defined in the spine (L4). |
| 4.2 | Catalog implementations and transitive dependencies are trusted application code under deployer review/release control | PARTIAL | Implied by "without granting … hostile-code confinement" |
| 4.3 | **Tenant input never supplies executable code, assembly paths or arbitrary CLR type selection** | MISSING | Security invariant absent from AD-10 and AD-13 (M8) |
| 4.4 | If the trust assumption fails, the evolution capability stays unavailable. Restricted workers need a separate design. No new process or service. | MISSING | Fail-closed clause absent (M8) |
| 4.5 | Admit the immutable manifest, options and bytes **against gateway and serving-peer pins**; refuse **before catalog callbacks** | PARTIAL | "immutable declared artifact/pin admission". The cross-unit agreement and refusal timing are missing. |
| 4.6 | Execution uses the same checked artifacts; hashing a path and then loading a replaceable file is insufficient | PARTIAL | "immutable" only |
| 4.7 | Undeclared explicit, reflection or nested loads are prohibited. Dynamic-loading exceptions are declared in the catalog. | PARTIAL | "declared artifact" only |
| 4.8 | Detection removes the process capability. In-flight routes refuse uncommitted success. Committed truth keeps Dapr reconciliation. | PARTIAL | "detection with subsequent capability loss" |
| 4.9 | No hostile-code confinement; hashes give identity, not safety | LANDED | AD-13 l.193 |
| 4.10 | Activation fences hold until consumer, compatibility, Dapr recovery and fleet qualification and the AD-26 profile | LANDED | "without granting activation" |

### Input 5 — `spec-shared-payload-protection-dapr-amendment-2026-10-05.md` (draft; must stay non-authorizing)

| # | Directive / risk | Status | Spine evidence |
| --- | --- | --- | --- |
| 5.1 | Draft/unapproved/non-authorizing; shared authority bytes unchanged; old approvals never reused | LANDED (non-authorization preserved) | AD-23 l.261: "draft/unapproved: it rewrites no shared authority bytes, conveys no implementation authority, and cannot reuse old approvals for new bytes"; diagram l.78 uses a dashed "Candidate crypto component; 8.6 unresolved" |
| 5.2 | Draft-only content: options/responsibility split, RSA-HSM profile table, PF-01 delta, §16 reapproval matrix | HELD | Correctly absent from the spine |
| 5.3 | Clauses with an **approved** source that can land without leaning on the draft: no direct-SDK fallback (§8.4), Crypto Scheme v1 never replaces `pdenc-v2` (P5), secret store ≠ crypto (P5), no provider SDK before qualification (§8.4) | Mixed | The last two are LANDED in AD-23. The first is MISSING (H1). The second is PARTIAL (L6). Cite PRD/P5 as the source, never the draft. |
| 5.4 | Structural Seed l.411: "The draft Dapr amendment proposes qualification before the adapter dependency choice." | Low risk | Non-authorizing as worded. Better to cite PRD §8.4 as the binding source so the sentence cannot be read as authority (L3). |

### Input 6 — `6-5-integration/metadata-adapter-contract.md` (superseded for 6.6; historical)

| # | Directive | Status | Spine evidence / gap |
| --- | --- | --- | --- |
| 6.1 | Superseded: historical approval evidence that grants no SQL authority | PARTIAL | AD-26 l.298 scopes the withdrawal "for Story 6.6" only. PRD §8.4 is project-wide and the register is empty, so the contract grants **no** story application-SQL, schema, credential or direct-fallback authority. Scoping it to 6.6 leaves a reading in which the 6.5 handoff still authorizes it elsewhere (H4). |
| 6.2 | IActorStateManager is the sole authority; never touch private actor tables/keys/caches | LANDED | AD-1 |
| 6.3 | Lost acknowledgement uses complete fresh addressed readback, never a cached value, affected count or invented receipt. Unavailable/mixed readback keeps the original owner/intent and evidence hold without dispatch. | MISSING | Survives the transport change (restated in 3.3) |
| 6.4 | **Cancellation/timeout cannot establish absence or authorize a second effect**; one monotonic recovery budget | MISSING (first half) | Architecture-level fail-closed clause. Budget values stay spec-level. |
| 6.5 | Pins, external objects, actor state and broker are separate intent/readback phases; no atomicity | LANDED | AD-26 l.300 |
| 6.6 | "A supported Dapr ETag transaction may be used only when qualification proves … otherwise the selected adapter supplies it directly" | CONTRADICTED by §8.4; correctly withdrawn | Withdrawn by AD-26 l.298, but nothing in the spine forbids the fallback pattern in general (H1) |
| 6.7 | "A separate ledger/registry backend fails readiness" (same-database rule) | Withdrawn | Replaced by "Sharing a physical backend does not imply transactions" (AD-26 l.300). The pre-existing production-proof term "two-host/shared-backend" (l.302) means two hosts on one backend, not transactions. Keep it. |
| 6.8 | Distinct OpenBao roles for metadata DML and migration | Withdrawn | AD-24 needs no change. The M2 credential clause stops it coming back. |

## (b) Structural recommendations and proposed wording

### Placement decision

| Current text | Problem | Destination |
| --- | --- | --- |
| AD-1 l.101: "For Story 6.6, the owner's 2026-10-05 Dapr-only direction supersedes …" through the amendment link | Story history in the root invariant. It makes "does not access PostgreSQL" ambiguous: is it 6.6-only or project-wide? | Durable actor-ownership sentences **stay in AD-1**. The 6.6 supersession and stricter scope **move to AD-13**, which already holds the other 6.6 amendment. AD-1 keeps a one-clause pointer so P2's "retain" instruction is met. |
| AD-26 l.298 "AD-13 metadata handoff … superseded" | Evolution and control-state history inside the production-profile AD | **Move to AD-13** (merged into the Dapr-only bullet). AD-26 keeps one sentence saying the profile binds no application-owned schema or credential. |
| AD-26 l.300 "Operation/component qualification" | A project-wide, owner-approved rule housed in an unratified `[ASSUMPTION]` AD. It covers Development/test, which AD-26 does not. It names stories that belong in the gate table. | **New AD-34 `[ADOPTED]`**. Amending AD-1 instead would turn the root invariant back into a procedure list. AD-34 keeps IDs stable and decouples the rule from AD-26 ratification, so Stories 3.17, 4.16 and 2.13 can cite an adopted decision. |
| AD-1 l.103–106 "Epic 3 encodes Story 3.13 …" | Release history under AD-1 | Move to AD-11 or the gate table (optional, L5) |

### Proposed AD-1 Rule (replaces l.101; 4 sentences)

> **Rule:** The platform uses CQRS, DDD, and event sourcing with Dapr as the required infrastructure boundary for every EventStore runtime package and host, including samples, linked source, and generated-host inputs, under PRD §8.4: an operation that a suitable Dapr API/component supports is reached only through the highest applicable Dapr abstraction, and application code adds no database driver, broker client, cloud SDK, direct provider HTTP call, provider connection string or credential, or provider schema for it. An operation Dapr cannot supply requires an AD-34 accepted exception before the dependency is introduced; unknown suitability stays unresolved, convenience, familiarity, a missing SDK helper, or unmeasured performance is not a capability gap, a generic binding carrying application-owned SQL or provider protocols is not portability evidence and cannot bypass actor ownership, and a Dapr failure never falls back to direct infrastructure. Aggregate/event/snapshot and existing actor drain-registration mutations remain solely `IActorStateManager`-owned, actor-owned state is addressed only through its actor boundary, application code never reads or writes Dapr private actor-state keys, tables, or caches, and domain modules receive no direct persistence authority. Aspire owns the local orchestration seed and production is governed by AD-26; Story 6.6's stricter Dapr-only amendment, which supersedes the 2026-10-04 PostgreSQL metadata-adapter permission, is recorded under AD-13.

Preserved: required boundary, §8.4 scope, exception required, unknown stays unresolved, SQL binding is not portability, highest abstraction plus qualification (via AD-34), actor ownership, no private keys, domain authority, Aspire/AD-26, and the 6.6 supersession. Added from §8.4: no-fallback, the named prohibited classes, the not-a-gap rule, and decide-before-introduce.

### Proposed AD-13 bullets (append after the Rule)

**New bullet, Story 6.6 Dapr-only amendment (4 sentences):**

> - **Story 6.6 Dapr-only amendment (owner, 2026-10-05):** The [Dapr-only amendment](../implementation-artifacts/story-6-6-dapr-only-amendment.md) controls Story 6.6 wherever the approved spec conflicts; the [2026-10-04 metadata-adapter contract](../implementation-artifacts/6-5-integration/metadata-adapter-contract.md) is historical approval evidence only and grants no application SQL, schema, credential, direct-adapter fallback, or provider-proof authority to any story, and Story 6.6 has no AD-34 exception path without a separate owner decision. Events, metadata, snapshots, command results, and outbox are staged under `AggregateActor` and saved once through `IActorStateManager`; an ambiguous save is reconciled by a fresh addressed Dapr actor read before retry or acknowledgment, and a new non-actor control record uses an AD-34-qualified Dapr state operation or a dedicated actor. Readback proves only the logical value Dapr returns—never physical bytes, a provider-signed receipt, or an older committed generation—and an operation whose safety needs an unavailable provider proof stays disabled with a typed unavailable/hold outcome. One shared allow-listed evolution service serves actor replay, projections, subscriptions, reconstruction, and inspection; a missing or ambiguous mapping, corrupt digest, unreadable protected payload, unsupported version, or incomplete contiguous prefix fails closed before domain code, checkpoint, publication marker, or handler effect, and the V2 admission fence stays until the writer and every serving reader and consumer are compatible.

**Replacement loader bullet (replaces l.193; 4 sentences):**

> - **Story 6.6 loader policy (owner, 2026-10-06):** Catalog implementations and their transitive dependencies are trusted, deployer-reviewed application code; tenant input never supplies executable code, assembly paths, or CLR type selection, and a deployment that cannot meet this assumption keeps evolution capability unavailable. Before a catalog route runs, its immutable manifest, options, and artifact bytes are admitted against gateway and serving-peer pins, execution uses those same artifacts, and any missing, changed, ambiguous, mismatched, or undeclared dynamic load refuses admission before catalog callbacks. An observed loader-policy violation removes that process's evolution capability: later calls are refused, an in-flight route refuses uncommitted success, and committed truth keeps Dapr reconciliation and idempotency. Per the [reviewed trusted-code amendment](../implementation-artifacts/story-6-6-trusted-code-amendment.md), this replaces the AD-13 spec's universal before-effect loader assurance and grants neither activation nor hostile-code confinement.

### Proposed AD-26 edits

- **Delete** both floating paragraphs (l.298, l.300). Insert one sentence before **Production proof**:
  > **Control state.** The profile binds no application-owned database schema, DDL, or provider credential; Story 6.6 control state follows AD-13 and every infrastructure operation follows AD-34.
- In **Production proof**, extend the digest-binding list (fail-closed strengthening; M6):
  "… route/idempotency catalog digests, **the AD-34 operation/component matrix and exception-register digest**, restore posture, and required evidence."

### Proposed new AD-34 (after AD-33)

> ### AD-34 - Infrastructure Operations Are Dapr-Qualified [ADOPTED]
>
> - **Binds:** FR5, FR8, FR16, FR32, FR34, FR37, NFR12, NFR17, NFR19 (PRD §8.4)
> - **Prevents:** hosts choosing direct provider clients, assuming guarantees a component does not supply, or inferring atomicity from a shared backend.
> - **Rule:** Every supported profile, including Development and test, maps each runtime infrastructure operation either to the Dapr API/component that performs it—with its required guarantees, exact runtime/client/component versions, and observed acceptance evidence—or to an entry in the accepted-exception register `docs/architecture/dapr-infrastructure-exceptions.yaml` that records the missing guarantee and evidence, alternatives considered, isolated adapter, owner, exact purpose and paths, review/removal trigger, and the architecture-owner decision taken before the dependency is introduced; unknown, missing, stale, or overbroad rows stay unresolved and confer no conformance. ETags, transactional scope, TTL, ordering, cancellation, retries, and failure classification are qualified per component; sharing a physical backend implies no transaction across actors, state components, pub/sub, or external systems, so those boundaries use durable intent, idempotency, and fresh addressed Dapr readback, and a cancellation, timeout, or unavailable readback never establishes absence or authorizes a second effect. A backend, component, or catalog-version change requires configuration, data migration where needed, and requalification even when application code is unchanged, and cannot by itself establish an exception; provider credentials never reach application processes for an operation Dapr supplies, and provisioning, component configuration, test doubles, and diagnostics are classified separately with no blanket test-folder or provider-namespace exemption and never substitute for Dapr-path correctness evidence. This rule accepts no exception or qualification and changes no readiness verdict: the register is empty, the retained direct Redis SignalR backplane and production key operations are unresolved non-conformances rather than exceptions and may not be extended or selected before their owning qualification, and AD-26 ratification and production gates still fail closed.

Every fail-closed clause in AD-26 l.300 carries over: per-component qualification, no shared-backend transactions, requalification on backend change, "Retained direct Redis is not an accepted exception", and "AD-26 ratification and production gates still fail closed". The story-ownership sentence moves to the gate rows below.

### Proposed Production Gates rows (story ownership moved out of the AD text)

| Item | Safe posture | Owner and trigger |
| --- | --- | --- |
| Dapr boundary inventory and guard (AD-34) | No PRD §8.4 conformance claim; the register is empty; unresolved rows block conformance | EventStore maintainer with Architecture, Story 3.17, before any infrastructure dependency change is accepted or conformance is claimed |
| SignalR cross-replica distribution | The retained direct Redis backplane is an AD-34 non-conformance, not an exception. It is not extended and not in any AD-26 profile, and no production multi-replica notification claim is made. | SignalR transport owner, Story 2.13, before backplane extension or retirement, or AD-26 profile proof |
| Production payload key operations | No provider SDK or Dapr crypto component is selected; the detached amendment stays draft and non-authorizing | Payload owner with Architecture and Security, Story 8.6, before adapter dependency selection |

### Ancillary structure

- Theme table l.92: "AD-1 through AD-4, AD-34". Add a Consistency Conventions row: "Infrastructure access | Dapr boundary, qualification, and exceptions follow AD-1 and AD-34."
- Capability map: add AD-34 to the FR1–FR10, FR11–FR16, FR26/28/32, FR34–FR35 and FR37 rows.
- Downstream references, routed and not edited here: epics Story 2.13, 3.17, 4.16 and 8.6 "Architecture constraints" should add AD-34. `docs/concepts/dapr-infrastructure-boundary.md` ("architecture AD-1/AD-3/AD-23/AD-26") should cite AD-34. The 3.17 spec ("AD-1/AD-9/AD-11/AD-12") should add AD-34.
- Frontmatter (L3): set `updated` to the Update-run date (currently `2026-10-05`, but `a6fc951e` edited on 2026-10-06). Add the following to `sources`:
  - `sprint-change-proposal-2026-10-05.md`
  - `story-6-6-dapr-only-amendment.md`
  - `story-6-6-trusted-code-amendment.md`
  - `docs/architecture/dapr-infrastructure-exceptions.yaml`
  - the Dapr crypto, SignalR binding and state-matrix documentation URLs cited in SCP §1

  Do **not** list the draft payload amendment as a source; AD-23 already links it with non-authorizing wording.

## (c) Contradictions and tensions introduced or exposed by the direct edits

| ID | Conflict | Evidence | Resolution |
| --- | --- | --- | --- |
| C1 (H3) | **Redis backplane vs AD-26.** The paradigm diagram, which is not split into current and target, shows a retained direct Redis SignalR backplane with no profile qualifier. AD-26 says "Redis is Development/test only", gate row l.472 says "Redis pub/sub is Development/test only", and PRD §8.4 forbids provider connection strings. A production multi-replica hub would therefore need either a non-conforming direct Redis or no cross-replica delivery, and the spine picks neither. | l.80; AD-26 l.296; l.472; source `SignalRServiceCollectionExtensions.cs:46-50` (`AddStackExchangeRedis`, linked into the released Gateway package) | The AD-34 sentence plus the SignalR gate row: excluded from every AD-26 profile, not extended, no production cross-replica claim until 2.13. Relabel l.80 (L1/L2 block below). |
| C1b (M7) | **AD-8/AD-31 consequence of 2.13.** AD-8 and AD-31 never mention Redis, so there is no textual conflict. If 2.13 selects Dapr pub/sub fan-out, every hub becomes a "production subscriber" under AD-8 *Delivery failure*. Poison notifications would then need AD-31 capture-before-ack, or an explicit cataloged policy, because silent drop is not the default. The 2.13 spec does not mention poison or dead-letter handling. | AD-8 l.150; AD-31 l.332; `2-13-…qualification.md` (no poison/dead-letter text) | Route to 2.13 and Architecture: either a notification topic is an AD-8 subscriber with AD-31 capture, or AD-8 gains a cataloged freshness-only policy. Decide before 2.13 selects a design. |
| C2 (M5) | **AD-7 "same-store batch transaction" vs AD-26 l.300.** AD-26's production profile is one `statestore` with `actorStateStore: true`, where actor keys and read-model keys share a component. "Same-store" can be read as allowing a multi-key transaction that spans actor-owned keys, which AD-1 and AD-3 forbid, or as same-database, which l.300 rejects. | AD-7 l.142; AD-26 l.296, l.300 | Replace "a same-store batch transaction" with "one AD-34-qualified transaction within a single Dapr state component—never spanning actor-owned state, another component, pub/sub, or external effects—". |
| C3 (L1) | **AD-24 naming vs diagram.** The diagram node is "Configured secret component", while AD-24 and NFR17 require the component named `openbao` in production. The generic label suggests the name is free to choose. | l.77; AD-24 l.267 | Relabel: `Secrets[(Secret component; openbao in production — AD-24)]` |
| C4 (M4) | **AD-12 vs Dapr-path evidence.** AD-12 says "inspect persisted state/read-model/CloudEvent data" without naming the path. The 6.6 amendment ("Never use direct database reads as product evidence") and P6/7.11 require logical values read through the Dapr boundary. | AD-12 l.186 | Append to AD-12: "Persisted state is read through its owning Dapr state/actor boundary; direct backend reads are labelled diagnostics and never substitute for Dapr-path evidence." |
| C5 (L2) | **AD-33 vs the diagram catalog edge.** `Catalog -->|Dapr state API; catalog activation| Sidecar` asserts where activation state is stored. AD-33 never decides this: it fixes a file-based signed envelope and an owner-committed generation. | l.64; AD-33 l.344–346 | Either add an AD-33 clause ("committed generation state is held through an AD-34-qualified Dapr state/actor operation") or relabel the edge "catalog activation (AD-33)". |
| C6 (H4) | **AD-1 PostgreSQL ban scope vs the §8.4 exception path.** "Application code does not access PostgreSQL" follows "For Story 6.6 …", so its scope is ambiguous. Read as project-wide it is absolute, while §8.4 allows an accepted exception. Read as 6.6-only, it leaves the metadata contract usable elsewhere. | AD-1 l.101; AD-26 l.298 | Proposed AD-1 S1/S2 make the general rule "no provider schema or driver without an AD-34 exception" (the register is empty, so the effect is absolute today). The AD-13 bullet makes 6.6 absolute and withdraws the contract for every story. |
| C7 (H2) | **Adopted policy inside an assumption.** PRD §8.4 and P2 are owner-approved, but their operative rule sits in AD-26 `[ASSUMPTION]`, which the 2026-10-07 correct-course says remains unratified. Stories 3.17 and 4.16 would cite an unratified AD for an adopted rule. | AD-26 l.292, l.300; SCP 2026-10-07 l.58, l.560 | New AD-34 `[ADOPTED]` |
| C8 (H1) | **Missing no-fallback clause vs the withdrawn adapter text.** The historical contract's "otherwise the selected adapter supplies it directly" is exactly the fallback §8.4 forbids. The spine withdraws that contract for 6.6 only and never states the general prohibition. | metadata-adapter-contract l.78; PRD l.475 | AD-1 S2 ("a Dapr failure never falls back to direct infrastructure") |

### Low-severity items

- **L1/L2 diagram relabels:**
  - l.80 becomes `Hub -.->|direct backplane: AD-34 non-conformance, Story 2.13; excluded from AD-26 profiles| Redis[(Redis backplane, Dev/test)]`.
  - l.77 and l.64 as in C3 and C5.
- **L3** frontmatter, as above.
- **L4** The AD-13 loader bullet refers to a "universal before-effect loader assurance" that the spine never defines. The proposed bullet names its location (the AD-13 spec).
- **L5** The Epic 3 release-history paragraph under AD-1 should move to AD-11.
- **L6** AD-23: append "Dapr Crypto Scheme v1 never replaces `pdenc-v2` or its AAD bytes." The source is approved P5, not the draft.
- **L7** AD-5, optional: append "Routing writes through Dapr does not by itself close the append race." The source is the SCP Epic 4 impact.
- **L8** Structural Seed: the `Gateway` "released package seam" compiles the linked SignalR Redis registration. Note it as a 2.13/3.17 subject so a package-level conformance claim cannot ignore it.
- **L9** The PRD glossary "DAPR Boundary" is narrower than §8.4. Route to the PM.

## Not landed by design

- None of the draft payload amendment's operative content (options split, RSA-HSM/PF-01/§16 deltas, logical-identity adapter contract) should enter the spine before exact-content approval. AD-23's current wording is the correct ceiling.
- Spec-level values (30-second recovery budget, 128 MiB scratch, 64 MiB manifest/65,536 rows, queue caps) stay in the AD-13 spec and amendments. The spine binds only the fail-closed semantics above.
