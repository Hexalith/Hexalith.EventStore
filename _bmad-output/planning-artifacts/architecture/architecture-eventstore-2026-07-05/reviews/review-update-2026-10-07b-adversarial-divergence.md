---
title: Architecture Update 2026-10-07b - Adversarial Divergence Review
date: 2026-10-07
lens: adversarial-divergence
subject: architecture.md working tree
assurance: tool-persona evidence only
---

# Architecture Update 2026-10-07b - Adversarial Divergence Review

> **Assurance label.** This report is `tool-persona` evidence under the PRD Assurance Control. It is not an
> approval or a ratification, it counts as no approval identity, and it changes no gate result, story status, or spine text.

## Anchors

| Item | Value |
| --- | --- |
| Subject | Working tree `_bmad-output/planning-artifacts/architecture.md`, SHA-256 `d3e5fcc5d0f6961c3976c31546793774f77e24993f704f8a7da663d012cb0b2a` (571 lines, LF, `eol=lf` per `.gitattributes`). |
| Base | `HEAD` (`4e4ee858`), spine bytes `1ff06c5ecc94003765a2fa6fd39491d9ccdb6d5b09419fcf7c8755c30d2d31e5`. That is the drift anchor named at memlog `:186`. |
| This run's delta | `git diff -- architecture.md`: 27 insertions and 26 deletions. The edits touch AD-5, 10, 11, 12, 17, 24, 26, 27, 28, 33, and 35, the preamble, the Stack row, and seven Implementation Status rows. `[ASSUMPTION]` occurrences fall from 14 to 4: the preamble, the AD-26 heading, the AD-26 Ratification text, and the G-BASELINE row. |
| Owner decisions | Memlog `:186-197`: all 11 inline assumptions are resolved, 6 of them with refinements. |
| AD-26 subject digest (evidence only) | Reading A, the most literal one, gives `16ab54f4e6a84dca2330df8646d0aabb48904765b06c8a93bca05f88bb2bc436` on the subject bytes. The alternative readings are in H2. This is a computation, not a recorded subject. |
| Level below, checked | `epics.md` working tree (Stories 2.15, 3.19, 3.21, 4.16, 5.7, 5.12-5.14, 7.22, 9.2, 9.3); `prd.md` working tree (Assurance Control `:201`, G-STATUS-ID `:680`); and the source paths cited inline. |

Line references `:NNN` point into the subject bytes unless another file is named.

## Verdict

**FAIL.** Every unit pair below obeys the adopted text to the letter and still builds something incompatible.

There are two critical pairs:

- The AD-33 refinement attaches one credential kind to a message key. A message, however, crosses hops of both kinds.
- The per-app actor-state rule still lets a reader component share the actor key space.

There are eight high pairs. Several come from this run's resolutions landing in the spine but not in the story acceptance criteria that build them (Stories 2.15, 5.7, 5.12, 9.2, 9.3). The AD-26 ratification subject also has a self-invalidation path and an ambiguous byte definition.

| Severity | Count |
| --- | ---: |
| Critical | 2 |
| High | 8 |
| Medium | 13 |
| Low | 3 |

Ratings follow the prior adversarial reviews:

- **critical:** data loss, an authorization bypass, or no conforming implementation of a primary flow.
- **high:** two owners or authorities for one fact, two validators computing different results, or a cycle that blocks a gate.
- **medium:** drift that a validator would catch late.
- **low:** wording.

---

## Critical

### C1 - One route entry per message cannot carry one credential kind across a two-hop flow (AD-33 refinement vs AD-36, AD-28, AD-25 facet, Story 5.14)

**Unit A: Story 5.12 catalog schema, built to AD-33 `:387`.**

- Commands and queries map by `(Domain, MessageType)` "to exactly one app ID, method, and contract version".
- Each entry "declares exactly one admitted credential kind … and the operation it requires from that kind's vocabulary".
- "a capability both kinds need is cataloged as two operations".
- Kind and operation are "declared in the `Contracts` route declaration".
- Activation `:389` fails readiness on "any duplicate … entry". Story 5.12's validator AC also fails on "a duplicate … key".

**Unit B: the hops that carry the same `(Domain, MessageType)`.**

- *Human command.* A generated REST host calls the EventStore gateway. This is a delegated-user call (AD-36 `:409`).
- *Dispatch.* `AggregateActor` calls the domain service's generic `/process`. This is a workload call with operation `domain-service:process` (`src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreWorkloadOperations.cs:9`), and that operation is shared by every command of every domain.
- *Trusted effect.* A domain service calls `api/v1/trusted-effects` (`src/Hexalith.EventStore/Controllers/TrustedEffectsController.cs:14-15`). This is a workload call, and it carries `TrustedEffectSubmission.CommandType`, which is the same `(Domain, CommandType)` a human can submit.
- *Operations.* Story 5.14 AC (`epics.md:4825`) has the Operations dead-letter routes admit "the human bearer relayed by Admin Server … or a workload assertion". That is one route with two kinds.

**Divergence.** Neither reading of "route entry" yields a conforming build.

1. **The entry describes the hop into its target app ID.** AD-33's "Each receiver's AD-36 workload audience is its activated catalog app ID" supports this reading.
   - Every command and query entry is then `workload` / `domain-service:process`, and the per-message declaration is redundant.
   - "Two operations" can never be expressed.
   - The gateway ingress, Admin Server, and Operations hops have no entry at all. AD-28's "the AD-36 credential kind it admits" and AD-36's "the kind its route does not admit" therefore have nothing to read.
2. **The entry describes ingress.** This is the only reading in which "two operations" means something.
   - A command that is both human-submittable and a trusted effect needs two entries under one `(Domain, MessageType)` key, which is a duplicate, so readiness fails.
   - The AD-25 idempotency facet, keyed by `(Domain, CommandType)` (`:335`), splits into two descriptors for one key.
   - Every domain-service receiver that checks its route's kind denies EventStore's workload dispatch of each delegated-user entry, so no command reaches a domain service.
3. **Under either reading,** a per-message kind cannot be enforced at a shared endpoint (`/process`, `/api/v1/commands`) before the body is bound. AD-36 requires denial "before binding or downstream work", and AD-10 requires it before disclosure.

**Closing wording (AD-33).**

> "A route entry's credential kind and operation govern only the hop into its target app ID and method; for command and query entries that hop is EventStore's workload call, so kind and operation follow the target method, not the message. Ingress hops (gateway command, query, and trusted-effect endpoints, Admin Server, Operations) are cataloged in an endpoint facet keyed by (app ID, method), each with exactly one kind and one operation; a capability both kinds need is two methods, and no two entries share a (Domain, MessageType), (Domain, ProjectionType), or (app ID, method) key."

### C2 - Per-app actor-state components still share one key space (AD-5 Append race, AD-26 Rule vs Story 5.7 and the AppHost `keyPrefix: none` precedent)

**Unit A: Story 5.7 (or 3.19), built to AD-5 `:150` and AD-26 `:341`.**

- EventStore's `statestore` is scoped to `eventstore` alone.
- "state that any other app reads lives in separately scoped components".
- The reader component for Admin's reads of EventStore-written state (admin indexes, checkpoints, read models, command status) is copied from the existing production template. `deploy/dapr/statestore-postgresql.yaml` uses the same `{env:POSTGRES_CONNECTION_STRING}`, sets no `tableName` (so the default table), and sets the AppHost's `keyPrefix: none`. The AppHost file states the reason (`src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`, keyPrefix comment): it is "Required so eventstore-admin reads the same EventStore state keys".
- The result is a component scoped to `eventstore` and `eventstore-admin`, with keyPrefix `none`, on the same database and table.

**Unit B: Story 4.16's envelope.** "component scopes and ACLs" form one falsifiable mechanism (`:150`).

**Divergence.**

- Dapr stores actor state under `<appId>||<actorType>||<actorId>||<key>`. A keyPrefix-`none` component on the same table lets the `eventstore-admin` sidecar write `eventstore||AggregateActor||…` rows through the state API.
- Every component is "scoped to it alone" or "separately scoped" to the letter. The key prefix AD-5 binds into the digest is the *actor* component's prefix, which governs nothing for the reader component.
- The Development profile has the same shape "in every profile": one Redis instance, with every component on database 0.
- Story 4.16's removal-of-a-mechanism falsification cannot fail when scopes exclude nothing, so the envelope is unprovable. Alternatively, the shared backend is accepted and the prior C2 (NFR7 class (c) second writer) reopens.
- The pair fails closed at G-APPEND, but only after Stories 5.7, 3.19, and 7.22 have built on it.

**Closing wording (AD-5).**

> "No other component addresses an actor-state component's backing table or Redis database: each actor-state component has its own table or database, and a component sharing a backend with one uses a key-prefix strategy that cannot produce `<appId>||` actor keys. The AD-26 digest binds each component's backend table or database and key-prefix strategy, and Story 4.16 falsifies the envelope by attempting a state-API write of an actor key from every other scoped app."

---

## High

### H1 - The ratification subject must change when ratification happens (AD-26 Ratification vs preamble, G-BASELINE row, Story 9.3 AC)

**Unit A: the owner and the spine run that records ratification.**

- The preamble `:111` says "`[ASSUMPTION]` marks a decision that awaits owner ratification".
- The AD-26 heading `:337`, `… [ASSUMPTION]`, is inside the subject ("from its heading"), as is "The target stays `[ASSUMPTION]` until the owner … records" (`:343`).
- Story 9.3 AC (`epics.md:7615`) rejects "open `[ASSUMPTION]`" and "any approval that predates the bytes it binds", and it binds the whole-file `architecture.md` SHA-256.

**Unit B: the Story 9.3 validator.** It recomputes the AD-26 subject digest, compares it with the records, and applies the AD-11 24-hour window `:210` "from the last authored change to the subject" (`:343`).

**Divergence.** Every ordering of the tag flip fails:

- Flipping `[ASSUMPTION]` to `[ADOPTED]` *after* the records changes the subject digest. The records then bind a stale digest, and the window restarts after their issuance.
- Never flipping leaves the tag open, and 9.3 rejects it.
- Flipping *before* the records makes the spine claim ADOPTED with no ratification, which contradicts `:111` and `:343`.

The subject also holds prose that must change while the ratified work proceeds:

- "The file is absent, so no profile currently authorizes promotion" (`:345`) must change when Story 3.19 publishes.
- The story routing (`Story 3.21 qualifies … Story 7.22 proves`, `:345`) changes at every correct-course.
- AD-5's "AD-26 and the Story 3.17 inventory enumerate them" (`:150`) forces a subject edit for every new reader component if "AD-26" means the section rather than the profile file.

Separately, Story 9.3's AC is still whole-file. AD-26's "An edit outside the subject … without invalidating a record" is therefore unimplementable as Story 9.3 is written. This is the prior adversarial H6 residual, now at the level below.

**Closing wording (AD-26 Ratification).**

> "Before hashing, the heading's trailing status tag is replaced by `[STATUS]`, so flipping `[ASSUMPTION]` to `[ADOPTED]` in the change that records ratification leaves the subject digest unchanged. The subject holds only normative requirements; artifact presence and story routing live in the Implementation Status table, and AD-5's enumeration duty falls on `deploy/dapr/production-profile.yaml`."

Propagate the following to Story 9.3: "the manifest binds the AD-26 subject digest beside the whole-file digest and evaluates ratification records only against the subject digest."

### H2 - Two people can compute two subject digests (AD-26 canonicalization, `:343`)

**Unit A:** the owner's issuance tooling. **Unit B:** the Story 9.3 validator, or the Story 9.2 assurance check. Both follow `:343` literally. On the current bytes:

| # | Ambiguity in the definition | Effect on the subject |
| --- | --- | --- |
| a | "followed by": no separator, or the Markdown block separator (one blank line) | A `16ab54f4…` vs B `20a43275641505022255875967b5b207df83f52f50d68491da3a2b85b26a328a` |
| b | Interior blank lines (3 inside AD-26): kept as empty LF-terminated lines, or dropped as non-content | A vs C `98f2b825c4f24140aa4c4b07dde529d543fa348c0aada8e576bb61ab861dd107` |
| c | Anchor "the AD-5 **Append race** paragraph": the source bold run is `**Append race (NFR7 class (c)).**` | A literal matcher for `**Append race**` finds nothing |
| d | "the next `###` heading": a prefix match also stops at `####`, a level match does not, a following `##` heading stops neither, and a `###` inside a fenced block stops both | Latent today (the next heading is `### AD-27` at `:347`) |
| e | "this section": the definition sits inside the subject, so quoting it in the Story 9.3 manifest or validator, or moving the paragraph into another AD, changes its referent | Undefined once quoted |
| f | Byte source: worktree or committed blob; trailing whitespace, CR, NFC/NFD, BOM | Latent: 0 trailing-whitespace lines, ASCII-only subject, `eol=lf` |
| g | Final LF: "each line terminated by LF" | Clear; a reader who misses it gets E `f38dc682050f61273facccbff8b63e89776451b729fbbc389dc8fecafb736241` |

**Divergence.** Story 9.3 must pick a reading, which means it "redefines" the digest, and `:343` forbids that. Two validators then disagree, and a correct record is rejected. This fails closed, but it does not converge.

**Closing wording (AD-26).**

> "The subject is computed from the committed blob by one tools-owned reference function: the AD-5 line beginning `**Append race`, then the AD-26 lines from `### AD-26 ` through the last non-blank line before the next line matching `^#{1,3} ` outside a fenced block, keeping interior blank lines, removing only a trailing CR, with no Unicode normalization, each line LF-terminated and no separator line. The Story 9.3 manifest records that function's digest and a fixed test vector, and validators call it rather than re-implement it."

### H3 - An "EventStore-issued" context cannot protect non-EventStore actor hosts (AD-28 `:357`, AD-24 `:309`)

**Unit A: AD-28 as resolved.** Until AD-34 qualifies actor-invocation restriction (Story 5.7 on the Story 3.21 runtime), "every actor method that discloses, admits, or mutates validates one EventStore-issued execution context, integrity-protected with a dedicated AD-24 key so no peer can mint one". Memlog `:191` calls it "a dedicated EventStore key".

**Unit B: actor hosts other than EventStore.** AD-5 `:150` itself names them:

- **Domain service.** The domain-service `ReminderActor.ConvergeAsync` mutates (`src/Hexalith.EventStore.DomainService/ReminderActor.cs:30`). The domain service invokes it on itself (`DaprReminderActorInvoker.cs:27`).
- **Operations.** Operations' `IDeadLetterDrainActor` capture, list, retry, skip, and archive methods are invoked by Operations itself (`DeadLetterOperationsEndpointExtensions.cs:188`, `DeadLetterBacklogReconciler.cs:37`).
- **Application-implemented validator actors.** EventStore calls application-implemented `ITenantValidatorActor` and `IRbacValidatorActor` actors (`src/Hexalith.EventStore/Authorization/ActorTenantValidator.cs:44`, `ActorRbacValidator.cs:48`). The interface remarks say "Applications implement this interface".

**Divergence.** None of the available designs works:

- To validate an EventStore-issued context, these hosts need the key. If the context is a MAC under a symmetric key (AD-24 inventories secrets, and the existing internal proof is an HMAC), every such host holds a key that also mints contexts `AggregateActor` accepts, which contradicts "no peer can mint one".
- If the context is asymmetric, AD-24 inventories no verification key.
- For self-invocations, EventStore is not on the path at all, so there is no context to validate. A letter-compliant host either rejects its own calls or skips the check.

The story that issues the context and Stories 5.5 and 5.14, which validate it, cannot agree.

**Closing wording (AD-28).**

> "Each actor host issues and validates its own execution contexts with a dedicated AD-24 key held only by that app ID, so an actor accepts only contexts its own host issued and EventStore's actors accept only EventStore-issued contexts. Until AD-34 qualifies the restriction, a cross-app actor method (such as an application-implemented validator actor called by EventStore) is reached only through a service-invocation route admitting the caller's AD-36 workload assertion, never by raw actor invocation."

### H4 - The MessageId declaration digest has no canonical form and no comparer (AD-17 `:252` vs Stories 5.12, 5.13, 3.18)

**Unit A.** `RestApi.Generators`, a Roslyn compile-time generator, runs in consumer repositories such as Tenants and embeds "its digest" of the `Contracts` declaration.

**Unit B.** The Story 5.12 catalog codec derives the entry from the declarations and "carry[s] its digest". The Story 3.18 inventory entry carries the digest too.

**Divergence.**

- AD-17 never says what bytes the digest covers. It could cover the version alone, or the whole declaration, including the AD-33 kind and operation, which `:387` says are declared "like the AD-17 MessageId version". It could be per declaration or per assembly. AD-17 also never names the function that computes it.
- A generator hashing attribute syntax and a catalog tool hashing its codec JSON get different digests for the same declaration. Activation then fails for every command, or each tool defines its own digest.
- "activation fails when a generated host's embedded digest differs" has no comparer. Story 5.13's activation set (AppHost, ACLs, gateway, admission, domain and projection dispatchers) excludes generated API hosts, which deploy from other repositories.

**Closing wording (AD-17).**

> "The declaration digest is SHA-256 of the AD-33 canonical codec's encoding of one declaration record (domain, message type, MessageId version, contract version), computed only by a `Contracts` function that the generator, catalog builder, and Story 3.18 inventory call. A generated API host sends its embedded digest with each command, and the gateway refuses a command whose digest differs from the activated entry."

### H5 - Two homes for the MessageId version and its NFR12 classification (AD-17, AD-11 vs Story 2.15 AC, PRD G-STATUS-ID, Implementation Status `:551`)

**Unit A: Story 2.15, built to its AC.**

- `epics.md:2104` requires a versioned contract manifest that "assigns MessageId grammar v1 or v2 to every affected endpoint and generated contract".
- `epics.md:2115` adds that "the manifest records an NFR12 compatibility classification".
- PRD G-STATUS-ID (`prd.md:680`) requires "a versioned contract manifest assigning MessageId grammar v1 or v2".
- Row `:551` has Story 3.18 "fed by Story 2.15's classifications", which orders 2.15 before 3.18.
- Story 2.15's dependency note still waits for "the owner [to resolve] the AD-17 carrier assumption", which happened today.

**Unit B: Story 3.18, built to AD-11 `:194`.**

- Its inventory is "the sole compatibility authority", and classifications are "written into its schema rather than a second manifest".
- AD-17 `:252` makes the declaration "the only source of that version".

**Divergence.**

- Story 2.15 ships an assigning manifest with classifications before 3.18's schema exists.
- Story 3.18 then treats that manifest as a foreign second authority.
- When the two disagree, the generator (which reads declarations) and the 2.15 manifest validator (which reads the manifest) emit or refuse `Location` differently.

**Closing wording (AD-11, propagated to Story 2.15, G-STATUS-ID, and row `:551`).**

> "Story 3.18 publishes its inventory schema before any story writes a classification; Story 2.15's manifest is a generated, non-authoritative view of the `Contracts` declarations that assigns nothing, and its classifications are rows in the Story 3.18 inventory."

### H6 - The guarded-transition workflow AD-12 assigns to Story 9.2 is not the check Story 9.2 builds (AD-12 `:218` vs Story 9.2 AC, PRD Assurance Control (1), AD-11 `:210`)

**Unit A: Story 9.2, built to its AC.**

- `epics.md:7586`: the matrix validator "runs in a blocking, required check … for the exact head SHA".
- `epics.md:7590` guards only "a high-risk gate or a story recorded against a high-risk NFR" going to PASS or `done`.
- PRD `:201` (1) still says "a required, blocking run".

**Unit B: AD-12.** A dedicated transition workflow, owned by Story 9.2, is triggered for each of six guarded transitions, so a truthful FAIL "blocks only that transition and never `main`".

**Divergence.**

- Story 9.2, built to its AC, puts a required check on `main` that truthfully fails today, because the matrix is absent. That blocks every merge and breaks AD-12's truthful-FAIL rule.
- An implementer who follows AD-12 instead fails Story 9.2's AC.
- No AC covers readiness, `release-available`, `production-promoted`, or consumer removal, so no story builds their seal.
- AD-11 `:210` rejects any result "not retrieved from a sealed AD-12 CI run". `release-available` and `production-promoted` can therefore never validate.
- The memlog records the PRD disagreement (`:190`), but `epics.md` carries no marker for it.

**Closing wording (AD-12).**

> "Story 9.2 owns two deliverables: the matrix validator under the truthful-FAIL rule (its fixture job blocks merges, its live job never does), and the transition workflow, run only on a request for one of the six guarded transitions, whose failure blocks that request and no merge. In PRD Assurance Control (1), 'blocking' means blocking the transition."

### H7 - Operation identifiers have two owning packages (AD-27 `:351`, AD-33 `:387` vs AD-36 `:409`, AD-10 `:186`)

**Unit A: `Contracts`.**

- AD-27: the platform-operation namespace is "declared once in `Contracts`".
- AD-33: "kind and operation are declared in the `Contracts` route declaration".
- Domain route declarations live in domain Contracts packages, such as `Hexalith.Tenants.Contracts`.

**Unit B: `ServiceDefaults`.**

- AD-36: "`Hexalith.EventStore.ServiceDefaults` owns the versioned assertion contract (… operation vocabulary …)".
- AD-10: "the human-bearer operation claim this contract owns".
- The vocabulary exists today in `ServiceDefaults/Authentication/EventStoreWorkloadOperations.cs`.
- `Hexalith.EventStore.ServiceDefaults.csproj` references no `Contracts`, and `Contracts` references only `Hexalith.Commons.UniqueIds`.

**Divergence.**

- A domain Contracts declaration must name an operation that ServiceDefaults owns. It can reference ServiceDefaults, which pulls JwtBearer and OpenTelemetry into a contracts package and breaks AD-2. Or it can copy the string.
- Once the string is copied, a ServiceDefaults vocabulary change silently desynchronizes the catalog from the assertion. "catalog, ACL, and credential compare one value" then compares two copies.
- The owner deferred adversarial M1 to the Story 5.12 spec freeze (memlog `:188`). This run adopted both ownership claims, so the deferral now covers a contradiction between two pieces of adopted text.

**Closing wording.**

> "`Contracts` owns every operation identifier (platform-operation namespace, human-bearer operations, AD-36 workload operations) in one versioned registry; ServiceDefaults owns claim types and validation and references that registry. A route declaration names a registry entry, never a string literal."

### H8 - AD-9 parity under per-app actor stores: Story 5.7 still converges the wrong way, and SDK defaults name EventStore's store (AD-5, AD-9, AD-26 vs Story 5.7 and library defaults)

**Unit A: Story 5.7, built to its text.**

- Its reconciliation (`epics.md:4482`) still lists, as a parity gap, that "production state-store templates omit the local `keyPrefix: none` posture".
- Its constraints (AD-9, 10, 12, 28) omit AD-5 and AD-26.
- Its AC compares component name, `actorStateStore`, key prefix, and app IDs across the AppHost and production.

**Unit B: AD-5 and AD-26.**

- AD-5 `:150`: "AD-9 parity is reached by moving the AppHost to that posture, never by widening production".
- AD-26 `:341`: EventStore's component is `statestore`, scoped to EventStore alone.

**The library defaults.** Every one of these resolves to `"statestore"`:

- `EventStoreReminderOptions.StateStoreName` (`DomainService/EventStoreReminderOptions.cs:30`)
- `EventStoreDataProtectionOptions.StateStoreName` (`:30`)
- `EventStoreDomainEventsOptions.MarkerStateStoreName` (`Client/Subscriptions/EventStoreDomainEventsOptions.cs:22`)
- `ProjectionOptions` checkpoint and read-model names (`Server/Configuration/ProjectionOptions.cs:12,19`)
- `AdminServerOptions.StateStoreName` (`:16`)
- the Aspire extension's `AddDaprComponent("statestore", …)` (`Aspire/HexalithEventStoreExtensions.cs:187`)

Kubernetes Component names are unique per namespace. Per-app actor stores in one namespace therefore cannot all be called `statestore`, although the AppHost can give each sidecar its own `statestore`.

**Divergence.** Each path fails:

- Story 5.7 as written imports `keyPrefix: none` into production, which widens production.
- If the AppHost names every store `statestore`, the AppHost and production break name parity. In production, every SDK default then points at EventStore's component, which is not scoped to the caller, so startup fails, unless someone re-scopes it and reopens C2.

**Closing wording (AD-5 and AD-26).**

> "Actor-state and reader components follow one naming convention, unique per namespace and identical in AppHost and production (EventStore's stays `statestore`), and no SDK, Server, Admin, or Aspire default names another app's component: each defaults to the caller's own declared component and fails startup when it is absent."

Propagate to Story 5.7: reverse the `keyPrefix` gap and add AD-5 and AD-26 to its constraints.

---

## Medium

| ID | Unit A | Unit B | Divergence | Closing wording |
| --- | --- | --- | --- | --- |
| M1 | A later edit to AD-24, 33, 34, or 36 (each bound by AD-26 Production proof `:345`), judged by its author not to change "what AD-26 requires" | The Story 9.3 drift guard, or a reviewer, judging that it does | The `:343` trigger is not mechanical: the memlog says "reviewer gate checks". This run itself edited AD-24 (dedicated keys now in the OpenBao contract that the profile binds) and AD-33 (route-entry fields in the catalog digest that the profile binds). A ratification can therefore silently cover changed obligations. | "An edit to any AD that AD-26 or the Append race paragraph cites by ID is subject-affecting unless its memlog entry says why not and the next reviewer gate confirms; Story 9.3 records each cited AD's text digest beside the subject digest and fails when one changes without such an entry." |
| M2 | A validator that measures the 24-hour window from the git author or committer date of the last commit that changed the subject | A validator that measures from the CI-platform push or run time | The sole maintainer sets both git dates. A rebase or amend rewrites them. Uncommitted edits, such as this run's, have no timestamp. "Last change to the subject" also needs the sub-file digest recomputed at every commit. | "The window opens at the CI-platform timestamp of the first run whose commit yields the current subject digest and closes at the record's issuance time read from its authenticated store; author- and committer-supplied dates never count." |
| M3 | A concurrent bmad-loop, or the owner, pushes directly to `main` under the ruleset bypass and records a story `done` against a high-risk NFR, or a gate PASS | The Story 9.2 "required" transition run (`:218`) | A bypass push records the transition with no run at all. "Required" is a branch-protection notion, and the bypass skips it silently. | "A guarded transition is effective only when the consuming validator retrieves a passing sealed run for the exact commit that records it; a recorded transition without one is void, whatever branch protection did." |
| M4 | An Admin Server platform operation in the AD-27 namespace, which carries no request tenant ("never forwarded as a request tenant") | The Story 2.14 boundary ("Each boundary requires exactly one explicit tenant … Missing … fail"), the AD-10 human-bearer rule "tenant validation … mandatory", and the AD-28 context "bound to tenant" | A platform operation fails the tenant boundary, or each implementer invents its own exemption. | "Each boundary requires exactly one explicit scope: one canonical tenant, or, on a route cataloged in the platform-operation namespace, that namespace and no tenant; AD-10 profiles and AD-28 contexts bind that scope, and a request carrying both fails." |
| M5 | The owner's AD-26 record, which names the broker, runtime pin, and restore posture by name (`:343`) | Story 3.19's validator, which compares content: the runtime image and broker component digests from 3.21 and the posture digest from 7.22 (`epics.md` 7.22 AC) | A component reconfigured under the same name passes or fails depending on the validator. Story 7.22 drills "on the production-equivalent profile", which is undefined, and Story 3.19 cannot start its draft before ratification. | "The ratification record binds each named input by content digest; Story 7.22 drills the named posture on the Story 3.19 draft profile after ratification, and a drill that changes the posture requires an approved replacement record." |
| M6 | Story 7.22 AC (`epics.md:6664`): a restore posture for "actor state in `statestore`" only | AD-5 and AD-26 per-app stores: the Operations AD-31 sink in `DeadLetterDrainActor` actor state (`DeadLetterDrainActor.cs:90-97`), the domain reminder stores, and the separately scoped reader components | Restoring `statestore` alone rewinds nothing in the sink, and restoring the sink to another point breaks AD-8 capture-before-ack. Reminder stores restored to a different point re-fire or lose reminders. | "The restore posture covers every component the AD-26 profile binds, names one consistency point across them or a per-pair restore order and prohibition, and never restores the AD-31 sink behind the subscriptions it acknowledged." |
| M7 | Story 5.11 conformance keyed by host ("against its registered fingerprint") | Story 5.5 receivers registering per-profile fingerprints (AD-10: "each with its own fingerprint"), alongside the ServiceDefaults "host/config fingerprint inventory" | A host that validates three profiles has either one registered fingerprint or three, and the two harnesses disagree. | "The fingerprint registry is keyed by (host, profile); each fingerprint is SHA-256 of the canonical serialization of that profile's effective validation parameters on that host, computed by one ServiceDefaults function." |
| M8 | A Story 3.19 profile built from AD-5's parenthetical "(EventStore, Operations, and domain services that use typed reminders)" | Application hosts of `ITenantValidatorActor` and `IRbacValidatorActor`, which are stateful actors ("managed at runtime via actor state") | The profile omits their actor store. The validator actors then fail closed with 503, or they get scoped onto EventStore's store, which reopens C2. | "Every app ID that registers any actor type is an actor-hosting app ID; the Story 3.17 inventory derives the list from actor registrations, not from this sentence." |
| M9 | A trusted-effect receiver that classifies the body `TrustedEffectSubmitRequest.DelegationToken` (resource-bound delegation profile) as the delegated-user kind | A receiver that treats it as payload | AD-33 admits "exactly one" kind, and AD-36 says "reject a request carrying both kinds". The first receiver rejects every trusted effect. The second accepts a credential that no route declares. | "A resource-bound family is a binding carried inside a call of one kind, never a kind; the route declares its kind plus the resource-bound family it requires, and the receiver validates both through their AD-10 profiles." |
| M10 | Story 5.12 built to its AC. Its assumption boundary (`epics.md:4737`) says "adds none of them as a field until the owner resolves", and the owner resolved them today. Its validator keys uniqueness by `(Domain, MessageType)` only. | Stories 2.14, 2.15, and 5.14, which consume kind, operation, namespace, and digest fields | 5.12 ships without the fields its consumers need, and no AC defines their shape. | "Story 9.3's repin change also rewrites the retired assumption-boundary notes in Stories 2.15 and 5.12 and adds the resolved fields and the H4/C1 key rules to 5.12's acceptance criteria." |
| M11 | The Story 3.19 validator, which rejects a predecessor result "not retrieved from a sealed AD-12 CI run" (AD-11 `:210`) | Story 3.15 and G-RUNTIME-PARITY's `evidence-validated` result (AD-11 `:208`), which none of the six AD-12 transitions covers | `release-available` consumes an `evidence-validated` predecessor that can never be sealed. | "A predecessor validator result consumed by a guarded transition is re-run inside that transition's sealed run; `evidence-validated` is recorded but never sealed on its own." |
| M12 | Story 5.7 rescopes `statestore` to `eventstore` alone, as AD-26 `:341` requires | Admin Server and the other readers of EventStore-written state (`AdminServerOptions.StateStoreName`, command status `CommandStatusConstants.cs:8`, checkpoints, markers, DataProtection keys) | AD-5 says only that AD-26 and Story 3.17 "enumerate them with their data migration"; no story executes the migration. Admin breaks, or `eventstore-admin` stays scoped, which reopens C2. | "Story 5.7 owns the split as one AD-9 slice: it creates the reader components, migrates each enumerated record set through Dapr reads and writes with read-back, and rescopes `statestore` last." |
| M13 | A Story 3.19 profile validator that requires the production state component to equal the component that OQ8 profile `oq8-postgresql-v1` was proven on | A validator that treats the OQ8 evidence as topology-agnostic | `tools/validate-oq8-platform-evidence.py:1433-1444` and `:4904-4909` bind the SHA-256 of `deploy/dapr/statestore-postgresql.yaml` at the v1 closure commit. That is the two-scope component, which the per-app split rescopes. AD-26 `:341` names the OQ8 profile without saying which component digest it carries. | "AD-26 binds the OQ8 evidence to the state-component digest it was captured on; after the per-app split, the profile names `oq8-postgresql-v1` only with evidence captured on the split component under the Story 4.15/4.17 rules." |

## Low

| ID | Finding | Closing wording |
| --- | --- | --- |
| L1 | AD-35 `:403`, now adopted, routes McpCli over the public AD-3 and AD-21 edges. AD-36 `:409` still lists McpCli among the sidecar-routed delegated-user callers. This is prior NL-3, and both texts are now adopted. | "…a UI host, the Tenants API, or an in-cluster McpCli head…", or drop McpCli from AD-36. |
| L2 | The AD-28 context binds only tenant and operation. Under any key-sharing design, a host that receives contexts can replay one to another actor method with the same tenant and operation. | "…bound to tenant, operation, target actor type and ID, and a short expiry." |
| L3 | AD-24 `:309`'s "namely the trusted-effect gateway proof and the AD-28 execution context" closes the list of internal proofs. A future internal proof, such as an Admin Server-to-Operations relay attestation, falls under no rule. | "Every internal proof uses a dedicated key inventoried here; today these are …" |

## Requested probes → findings

| Probe | Result |
| --- | --- |
| Can two people compute different AD-26 subject digests (paragraph boundaries, heading line, interior blanks, trailing whitespace, final LF, Unicode, "this section")? | **Yes.** See H2, which lists 3 live ambiguities and 4 latent ones with measured digests. H1 adds that the heading line itself must change when ratification lands. Unicode, trailing whitespace, and CR are latent only: the subject is ASCII with 0 trailing-whitespace lines and `eol=lf`. |
| Does "a capability both kinds need is cataloged as two operations" collide with "(Domain, MessageType) maps to exactly one app ID, method, and contract version"? | **Yes.** See C1. Under either reading of "route entry", one hop is non-conforming or the key is duplicated. Story 5.14's dual-kind Operations route already instantiates the collision. |
| Who issues and who verifies the AD-28 context key? Can domain-service actor hosts (typed reminders) validate an "EventStore-issued" context? | **They cannot without becoming minters.** See H3. Self-invocations by the domain service and by Operations have no issuer on the path, and application validator actors need EventStore's key. L2 covers replay scope. |
| Does AD-12's "owner record checked by the consuming transition's sealed run" conflict with AD-11 Assurance level or PRD Assurance Control (1)? | **With AD-11, it is compatible in principle.** Attestations need the 24-hour window, and validator results need seals. M11 covers `evidence-validated`, which is a predecessor result that is never sealed. **With PRD (1) and Story 9.2's AC, it conflicts** ("required, blocking"; two transitions; see H6). M2 and M3 cover the clock source and bypass pushes. |
| Do per-app actor-state components conflict with AD-9 parity, AD-31 Operations, or AD-34 inventory ownership? | **AD-9:** H8, through the Story 5.7 direction, the SDK default names, and namespace-unique names. **Key space:** C2. **AD-31:** M6, where the sink lives in Operations' actor store and restore splits it. **AD-34 / Story 3.17:** M8 and M12, where the enumeration is incomplete and the migration has no owner. **OQ8:** M13. |

## Checked and consistent

These items were checked and found consistent:

- **Inline tags.** All 11 inline `[ASSUMPTION]` tags were removed, matching the 11 memlog resolutions. The remaining tags sit only on AD-26 and in rows that describe it.
- **AD-28 channel-admitted set.** It now includes actor configuration, deactivation, and subscription discovery (prior NM-5 closed).
- **AD-28 fence.** The context is unfenced for admission, read, and freshness methods (prior N1 closed).
- **Stack gitlink.** The Stack row's Builds gitlink `397c94a4` equals the root gitlink at `HEAD`.
- **Code coverage pin.** `18.12.0` matches `references/Hexalith.Builds/Props/Directory.Packages.props:267`.
- **Stories 3.21 and 7.22.** Both keep broker, pin, and posture selection inside the AD-26 record, consistent with row `:540`.
- **Release evidence codec.** Row `:547` is consistent with the AD-11 Release evidence paragraph.

## Disposition

- This is the only file written. No spine, memlog, epics, PRD, tracker, or source edit was made, and no git command mutated state.
- The closing wordings above are inputs to the owner's next spine update and to the Story 9.3 repin propagation. None is adopted here.
- AD-26 stays `[ASSUMPTION]` and the spine stays `draft`. Every Implementation Status row keeps its posture. Closing these findings ratifies nothing; ratification remains the owner's time-separated attestation at the AD-11 assurance level.
