---
title: Architecture Update 2026-10-07b Review - Gate Closure
date: 2026-10-07
lens: gate-closure
subject: architecture.md working tree, SHA-256 09a613dfdbb0f61d3f73b0d2e0a87be690b0f53889d3e3172ad3578cbd2fd4f8
assurance: tool-persona evidence only
---

# Architecture Update 2026-10-07b Review - Gate Closure

> **Evidence class: `tool-persona`.** This review is evidence only. It is not an approval, a ratification, or an attestation. It records no assurance level for any gate, changes no gate result, and counts as no approval identity. The owner holds every role.

## Anchors

| Item | Observed value |
| --- | --- |
| Subject | `_bmad-output/planning-artifacts/architecture.md` working tree, SHA-256 `09a613dfdbb0f61d3f73b0d2e0a87be690b0f53889d3e3172ad3578cbd2fd4f8`, 575 lines, LF only, no BOM. Re-checked at the end of the review: unchanged. |
| Bytes before the gate fixes | Scratch copy `spine-before-gate-fixes.md`, SHA-256 `d3e5fcc5…cb0b2a`. This is the subject all three closed reviews bound. The gate fixes are +23/−19 lines (`git diff --no-index`). |
| Commit state (observed during this review) | The working-tree spine is **already committed**. At session start `HEAD` was `4e4ee858` (spine `1ff06c5e…`). A concurrent session then committed the spine, the memlog, `epics.md`, `prd.md`, `sprint-status.yaml`, the routing proposal, and the three 2026-10-07b reviews inside `4b1377a7`, titled "feat(tests): add comprehensive tests for event managed artifacts and private image registry callbacks". It is on `origin/main`. The repository Activity API records the push at `2026-10-07T10:56:02Z`, and the push-triggered runs were created at `10:56:05Z`-`10:56:07Z` (`CI` concluded `failure`). The working-tree spine equals the `4b1377a7` blob. This is the `concurrent-bmad-loop-git` trap again. |
| Owner decisions | Memlog entries after "Update run 2026-10-07b opened". The last four entries are the gate results and three owner follow-ups: signed, allow-listed AD-28 contexts; AD-33 hop scoping now, with ingress cataloging deferred to the Story 5.12 spec freeze together with adv M1; and the AD-26 window anchored at the CI-recorded first push to `main`. The operation-registry question (adv M1 / adv H7) is owner-deferred and is **not counted as unclosed**. |
| Lint and whitespace | `python3 -I .claude/skills/bmad-architecture/scripts/lint_spine.py --workspace <run folder>`: `total_findings: 0`. `ARCHITECTURE-SPINE.md` is a symlink to the subject. `git diff --check`: clean. |
| Method | Read-only. Spine and reviews were read in full for the changed ADs. Code spot checks: actor-proxy call sites in `src/`, both `AdminTenantAuthorizationFilter` copies, the `RestApi.Generators` and `Contracts` target frameworks, the OQ8 validator's state-component binding, `DaprReadModelStore`, and the `deploy/dapr/statestore-postgresql.yaml` header. Upstream: components-contrib `release-1.18` `common/authentication/postgresql/metadata.go`, fetched into the scratchpad. The only file written is this one, plus the scratch script `closure_digest.py`. |

## Verdict

**The prior gates are closed in substance, with three new high divergences in the gate-fix text and one high residual.**

Of the 21 prior critical and high findings:

| Status | Count |
| --- | ---: |
| CLOSED | 16 |
| PARTIAL | 4 |
| OPEN | 0 |
| Owner-deferred and recorded in the spine (adv H7) | 1 |

The PARTIAL findings are rubric H8, technology H3, adversarial H1, and adversarial H5. Technology H3 keeps a high residual.

The gate-fix text adds 3 new high findings (NH1-NH3), 10 new medium findings, and 7 low findings. Every new finding fails closed except NH3, which silently corrupts read models after a restore. None authorizes production today.

The AD-26 subject digest is now single-valued on the current bytes, and flipping the ratification tag leaves it unchanged (section "Task 2").

## Task 1 - Prior critical and high findings

Line numbers point into the subject.

| Prior finding | Status | Closing text in the subject (quoted) and residual |
| --- | --- | --- |
| Rubric H1, subject byte readings | CLOSED | `:343` "the SHA-256 of these UTF-8 lines, each ending in exactly one LF and otherwise unnormalized: the line beginning `**Append race (NFR7 class (c)).**` through the line before the next empty line; then, with no separator, the line beginning `### AD-26 ` with its trailing status tag (` [ASSUMPTION]` or ` [ADOPTED]`) removed, through the last non-empty line before the next line beginning `### ` or `## `, with the empty lines between them included." One value on the current bytes (Task 2). |
| Rubric H2, ratification edits the subject | CLOSED | Tag excluded (as above). `:341` "binds only through the Ratification below". `:343` "The target binds only once the owner … records". "The file is absent" was removed from `:345`; row `:545` now carries "No profile currently authorizes promotion". |
| Rubric H3, Story 9.3 rejects records | CLOSED (routed) | `:343` "Story 9.3 records the subject digest in its manifest, checks each record against it rather than the whole-file digest, and never redefines it." Row `:541`: "correct-course extends … Story 9.3 (AD-26 subject digest and the memlog AD-26-impact record), before either spec freezes". |
| Rubric H4, AD-12 seal workflow vs Story 9.2 and the PRD | CLOSED (routed) | `:218` "Required means the guarded transition is effective only when its validator finds that sealed run; it is never a branch-protection or ruleset check on `main`, which a bypass push skips." Row `:541` routes PRD item 7.1 and the Story 9.2 extension. |
| Rubric H5, credential-kind key collision | CLOSED | `:387` "A route entry's kind and operation govern only the hop into its target app ID and method, so message and projection entries admit the workload kind; a capability both kinds need is cataloged as two operations with distinct keys, and no two entries share a key." Ingress cataloging is owner-deferred (row `:562`). |
| Rubric H6, per-app actor-state components unowned | CLOSED (routed) | Row `:542` "correct-course extends Stories 5.6 and 5.7 to one component per actor-hosting app and assigns the reader data migration …". Row `:540` supersedes "Story 5.7's … `keyPrefix: none` and zero-actor-state targets". |
| Rubric H7, internal-proof keys and NFR12 | CLOSED (routed) | `:309` "Every internal proof uses dedicated keys … today the AD-5 fenced and AD-28 execution contexts, …, the trusted-effect gateway proof, and the trusted-effect erasure capability. A digest-key generation retires only after every registered consumer, including any internal proof not yet moved to its dedicated key, shows no live reference." Row `:543` adds "NFR12-classified through Story 3.18 because released `Server` actor interfaces change". |
| Rubric H8, resolutions not reaching stories | **PARTIAL** | Row `:540` lists the superseded texts. Row `:562` reads "Its fields include each command's MessageId version and declaration digest, the platform-operation namespace, and each entry's credential kind and operation". Residual (NM8): the trigger "before the Story 5.12 spec freezes" does not gate Stories 5.7, 5.14, and 7.22, none of which depends on 5.12. |
| Tech H1, `keyPrefix` does not partition actor state | CLOSED | `:150` "with its app ID, namespace, and physical target (database and schema, `tableName`, and its own `metadataTableName`) bound into the AD-26 digest. Dapr `keyPrefix` never applies to actor state". `:345` "every actor-state component's app ID, namespace, and physical target". New divergences in this text: NH1 and NM6. |
| Tech H2, three digest-ring proofs | CLOSED | `:309`, as quoted for rubric H7. |
| Tech H3, AD-28 exit relies on a nonexistent control | **PARTIAL (high residual)** | `:357` "Service-invocation access control, the API allowlist, and workflow access policies do not restrict invocation of a user-defined actor type, and DAPR 1.18 offers no control that does. Until a Story 3.17 inventory row for the profile and runtime shows a runtime-denied cross-app call to each hosted actor type, …". Residual: see "Technology H3 residual" below. |
| Adv C1, one kind across a two-hop flow | CLOSED | `:387` hop scoping, as quoted for rubric H5. The ingress question is recorded as an owner decision: row `:562` "before the Story 5.12 spec freezes the owner decides … whether sidecar-reachable ingress endpoints are cataloged". |
| Adv C2, per-app components still share a key space | CLOSED | `:150` "no other component, namespace, or environment addresses that physical target". The falsification clause added with it is defective (NH1). |
| Adv H1, subject must change at ratification | **PARTIAL** | The tag flip is neutral, and artifact presence and the 3.21/5.12/5.13/7.22 routing moved out. Residual: story routing that a correct-course can change remains inside the subject: Story 4.16 ×3 and Story 3.17 ×3 in AD-5, plus Story 6.6, Story 9.3, and Story 3.19 in AD-26. The gate fix also **added** a present-state list inside the subject: "(today EventStore, Operations, and every application or domain service that hosts reminder or validator actors)". Re-routing G-APPEND, the inventory, or the profile story re-digests the subject and restarts the 24-hour window. Closing wording: move routing and the "today" list to Implementation Status, leaving role nouns ("the G-APPEND proof", "the AD-34 inventory") in the subject. |
| Adv H2, two digests | CLOSED | As quoted for rubric H1. |
| Adv H3, "EventStore-issued" context for non-EventStore hosts | CLOSED | `:357` "The invoking app signs it with its own dedicated AD-24 key; the hosting app verifies it with verification-only material and accepts only issuers allow-listed for that actor type (EventStore for EventStore-hosted actors and for the tenant and RBAC validator actors it calls; a self-hosted reminder or drain actor only its own host), so no verifier can mint one." Code check: every `CreateActorProxy<…>` in `src/` is in EventStore (gateway + `Server`), Operations (its own `IDeadLetterDrainActor`), or DomainService (its own `IReminderActor`). Admin Server creates none. The allow-list matches today's callers. |
| Adv H4, declaration digest canonical form and comparer | CLOSED | `:252` "The declaration digest is the SHA-256 of the declaration's canonical bytes from the Story 5.12 `Contracts` codec, and every consumer reads it from that codec." `:389` "every required host, including each generated API host". New feasibility issue: NM4. |
| Adv H5, two homes for the MessageId version | **PARTIAL** | `:194` "Until the Story 3.18 schema exists, Story 2.15's manifest is a projection derived from the `Contracts` declarations with no compatibility authority; Story 3.18 imports and retires it". Residual (NM8): row `:540` declares "Story 2.15's interim carrier clause" "superseded and do not bind". That clause is the only Story 2.15 text making its manifest declaration-derived, so the remaining AC ("it assigns MessageId grammar v1 or v2", `epics.md:2104`) reads as an assigning manifest again. |
| Adv H6, Story 9.2 builds a check on `main` | CLOSED (routed) | `:218` "Story 9.2 owns that workflow beside its truthful-FAIL matrix validator" + "Required means …". Row `:541`. |
| Adv H7, two owning packages for operation identifiers | OWNER-DEFERRED (recorded) | Row `:562` "before the Story 5.12 spec freezes the owner decides the single owner of the operation vocabularies (ServiceDefaults does not reference `Contracts`), the AD-10 human-bearer operation claim type, …". This is recorded as an owner decision due before the 5.12 spec freeze, as required. |
| Adv H8, AD-9 parity and SDK defaults | CLOSED | `:150` "EventStore's component keeps the name `statestore`; every other component name follows one convention, unique per namespace and identical in the AppHost and production, and no SDK or host default names another app's component." Row `:540` supersedes 5.7's `keyPrefix: none` target. Residual medium: NM10. |

### Technology H3 residual (high)

The exit condition "a runtime-denied cross-app call to each hosted actor type" can be met by a control that the preceding sentence says restricts nothing.

- Story 5.7 can deny the whole `actors` API to Sample API, which hosts no actors, through Sample API's own API allowlist.
- A `CallActor` from Sample API to `AggregateActor` is then runtime-denied, so a Story 3.17 row "shows" the denial for every hosted actor type.
- The execution-context requirement ends. Meanwhile Operations and reminder-hosting domain services, which cannot deny themselves the actors API, can still invoke `AggregateActor` with no context.

**Closing wording (AD-28):** "Until a Story 3.17 inventory row for the profile and runtime shows that the runtime denies a cross-app call to each hosted actor type from every app ID in the profile other than that type's allow-listed issuers, including apps that host actors, beside a positive control from each allow-listed issuer, … A denial by the caller's own API allowlist, ACL, or network policy does not qualify."

## Task 1 - Mediums

| Finding | Status |
| --- | --- |
| Rubric M1 (AD-26 same-change rule has no enforcer) | PARTIAL: `:343` "each architecture update records in its memlog whether a change outside the subject changes what AD-26 requires", routed to Story 9.3 (row `:541`). The judgment is still self-assessed. |
| Rubric M2 ("last authored change" undefined) | CLOSED: the CI first-push anchor. New issue: NM1. |
| Rubric M3 (achieved level undefined) | CLOSED in AD-12. AD-11 `:210` is not aligned (NM2). |
| Rubric M4 (operation vocabularies) | Owner-deferred, recorded in row `:562`. |
| Rubric M5 (AD-17 digest form) | CLOSED: `:252`. |
| Rubric M6 (AD-11 vs Story 2.15 sequencing) | CLOSED: `:194`. |
| Rubric M7 (McpCli admin path) | CLOSED: `:403`. |
| Rubric M8 (minting by non-EventStore hosts) | CLOSED: signed with verification-only material (`:357`, `:309`). |
| Rubric M9 (Story 7.22 restore scope) | CLOSED as written. The narrowed wording creates NH3. |
| Rubric M10 (AD-10 profiles in Story 5.11 scope) | CLOSED: row `:546`. |
| Rubric M11 (`[ASSUMPTION]` tag scope) | CLOSED: `:111`. |
| Tech M1 (subject includes the status marker) | CLOSED: tag excluded. |
| Tech M2 (Admin tenant defaulting) | PARTIAL: row `:556` names only Admin Server. The gateway copy `src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs:34-40` also fills `tenantId` from the first grant (verified), and Story 2.14's listed gateway surface is `ClaimsTenantValidator` only. |
| Tech M3 (operation vocabulary has no code or owner) | Owner-deferred, recorded in row `:562`. |
| Tech M4 (D4 zero-state posture) | CLOSED in the spine (`:150`, row `:540`). Template headers and guides are not named, and the sentence admits two readings (NM5). |
| Tech M5 (stories only in uncommitted epics) | PARTIAL: the subject no longer names 3.21, 5.12, 5.13, or 7.22. The epics are now committed in `4b1377a7`, under an unrelated `feat(tests)` subject. |
| Adv M1 (cited-AD edits are subject-affecting) | PARTIAL: memlog self-record only; there is no digest of the cited ADs. |
| Adv M2 (clock source) | CLOSED. New issue: NM1. |
| Adv M3 (bypass push) | CLOSED: `:218`. |
| Adv M4 (platform operation vs tenant boundary) | CLOSED: `:351`. The adopted wording creates NH2. |
| Adv M5 (records name inputs rather than binding content digests) | OPEN: `:343` still says "naming the broker, DAPR runtime pin, restore posture, and NFR7 class (c) envelope path". |
| Adv M6 (restore consistency across components) | PARTIAL: actor-state only. See NH3. |
| Adv M7 (fingerprint registry key) | PARTIAL: `:186` "registered per host and profile". Whether the fingerprint is of the contract or of the host's effective configuration is undefined. |
| Adv M8 (actor-host enumeration) | CLOSED: `:150` "that the Story 3.17 inventory enumerates (… application or domain service that hosts reminder or validator actors)". |
| Adv M9 (resource-bound family vs kind) | CLOSED: `:387`. New issue: NM7. |
| Adv M10 (Story 5.12 fields) | CLOSED: rows `:540` and `:562`. |
| Adv M11 (`evidence-validated` never sealed) | CLOSED: `:218` re-run. New issue: NM3. |
| Adv M12 (reader migration owner) | Routed to row `:542` (correct-course, no owning story yet). |
| Adv M13 (OQ8 evidence vs the split component) | Routed to row `:542` ("`oq8-postgresql-v1` requalification"). Feasible without a reseal: closure checks bind the `deploy/dapr/statestore-postgresql.yaml` blob at `COMPLETED_V1_CLOSURE_COMMIT` (`tools/validate-oq8-platform-evidence.py:4951`), and only a fresh capture hashes the worktree file (`:1433-1434`). The profile name stays inside the subject. |

## Task 2 - AD-26 ratification subject digest

| Run | Subject SHA-256 | Lines and bytes |
| --- | --- | --- |
| Own implementation (`scratchpad/closure_digest.py`, `python3 -I`; bytes-level, no shared code with the reference) | `7075e76a6977628670eedc6d488229c600f7d343e2cd27bd81b4fe6bce8c62b7` | 10 lines, 7,156 bytes |
| Reference (`scratchpad/ad26_subject_digest.py`) | `7075e76a6977628670eedc6d488229c600f7d343e2cd27bd81b4fe6bce8c62b7` | 10 lines, 7,156 bytes |
| Same definition on the pre-gate-fix bytes (`d3e5fcc5…`), for information | `70a846b0…9014` | 6,314 bytes |

**Match.** The subject is AD-5 `:150` plus AD-26 `:337-345`, with the 3 interior empty lines kept.

**Readings on the current bytes.** The subject is ASCII-only, with 0 trailing-whitespace lines, 0 whitespace-only lines, 0 universal-newline characters, no CR, and no BOM.

| Alternative reading | Result |
| --- | --- |
| `str.splitlines` line split | Same digest |
| A whitespace-only line counts as empty | Same digest |
| Stop at any `#{1,3}` heading | Same digest |
| Stop at any heading, including `####` | Same digest |
| Tag removed without its leading space | `1f09de4d…` |
| Blank separator line between the two parts | `11779da0…` |
| Interior empty lines dropped | `d4e3eb07…` |
| No LF after the final line | `abc7dadd…` |

The wording excludes each of the four differing readings: the parenthetical spells the tag with its space, and the text says "with no separator", "with the empty lines between them included", and "each ending in exactly one LF". **The wording admits one value on the current bytes.**

Latent hazards on future bytes:

- The definition does not say what happens if a second line begins with either anchor (L-level).
- Rendered Markdown hides the leading space in "` [ASSUMPTION]`" (NL4).

**Ratification flip.** Changing only the heading tag from ` [ASSUMPTION]` to ` [ADOPTED]` changes the file SHA-256 to `4e8ea360…14a1` and leaves the subject digest unchanged (`7075e76a…62b7`, `unchanged=True`). After the flip, `:111` and row `:539` become stale. Both are outside the subject, so they are editable without invalidating a record.

**Anchor fact (evidence, not a determination).** The subject value `7075e76a…` first reached `main` in `4b1377a7`. The Activity API records that push at `2026-10-07T10:56:02Z`. Under `:343`, no AD-26 record issued before about `2026-10-08T10:56:02Z` (12:56:02 +02:00) can be valid, provided the subject does not change again. This supersedes the "no earlier than 2026-10-08 09:34 +02:00" note in the routing proposal.

## Task 3 - Invariant checks

| Check | Result |
| --- | --- |
| AD-16 unchanged vs `HEAD` | **Pass.** The AD-16 section SHA-256 is `1891d1f5…c2b4` at session-start `HEAD` `4e4ee858`, at `48ef7171`, at `4b1377a7`, and in the working tree. |
| AD-26 target still `[ASSUMPTION]` | **Pass.** `:337` "### AD-26 - Production Runs Only On A Proven Fail-Closed Profile [ASSUMPTION]". `:341` "binds only through the Ratification below". |
| `status: draft` | **Pass.** `:8`. |
| No text implies AD-26 is ratified | **Pass.** Every "ratif*" occurrence (`:111, 210, 218, 341, 343, 345, 397, 419, 539, 545, 553, 567`) is conditional or prospective ("awaits", "binds only once", "after AD-26 ratification", "while … ratification is missing"). `[ADOPTED]` never qualifies AD-26. |

## Task 3 - New divergences in the gate-fix text

### High

#### NH1 - The Story 4.16 falsification of the physical-target exclusion cannot fail, and it skips the only Dapr-level second writer (AD-5 Append race `:150`)

- **Unit A: Story 4.16, built to** "Story 4.16 attempts the write from every other scoped app."
  - Under one reading the set is empty, because the component is "scoped to it alone".
  - Under the other reading, every other app ID attempts an actor-state write. Dapr builds actor keys from the caller's own app ID, and the state API rejects any key containing `||` (technology review Q1). Every attempt therefore fails **whatever the component configuration**.
  - The falsification is green by construction. It still passes if a reader component shares the actor table.
- **Unit B: G-APPEND, built to** "each mechanism must be falsifiable" and "no other component, namespace, or environment addresses that physical target".
  - The real second writer is a sidecar with the **same** app ID in another namespace or environment configured against the same target. "Every other scoped app" never exercises it.
  - One profile cannot see other environments at all.
  - The connective "so" also makes the exclusion read as a consequence of the `keyPrefix` fact rather than a requirement.
- **Closing wording (AD-5):** "Dapr `keyPrefix` never applies to actor state, so separation is by physical target: no other component, namespace, or environment may address it, and the AD-24 contract grants that target's database credentials to that app ID in one namespace of one environment only. Story 4.16 falsifies this (a) by writing a sentinel through each other app's components and observing through the owner component's Dapr Query API that it never lands in the owner's target, and (b) by starting a sidecar with the owner's app ID in another namespace against that target and observing denial. Each attempt runs beside a positive control in which the separation is removed and the attempt lands. An attempt that Dapr key construction prevents regardless of configuration is not a falsification."
- **Note:** this text is inside the ratification subject, so fix it before any record is issued.

#### NH2 - "AD-10 profiles … bind that scope" contradicts the workload profile, which forbids tenant claims (AD-27 `:351` vs AD-10 `:186`, AD-36 `:409`)

- **Unit A: Story 2.14, built to AD-27** "AD-10 profiles and AD-28 contexts bind that scope, and a request carrying both fails". Its scope includes "the Tenants runtime".
  - At the Tenants domain-service boundary, it requires the validated credential to bind the request tenant.
  - EventStore's dispatch to `/process` and `/query` carries only an AD-36 workload assertion, so every command and query to a domain service is denied.
- **Unit B: Story 5.5 / 5.11, built to AD-10 and AD-36.** The workload profile has "tenant, role, and administrator claims forbidden" and "never carries tenant, role, or administrator claims", so the credential cannot bind a tenant.
- **Second divergence:** "carrying both" does not say whether `eventstore:tenant` grants count. A platform-operation implementer that counts them denies every operator who holds any tenant grant.
- **Closing wording (AD-27):** "The human-bearer profile's tenant grants and a resource-bound family's tenant binding must cover that scope, and an AD-28 context binds it. A workload assertion carries no scope; the receiver takes the scope only from the request and checks it against the route and the AD-28 context. A request fails when its route, body, or headers name both a tenant and the platform-operation namespace; `eventstore:tenant` grants in a credential are not a request scope."

#### NH3 - The restore posture is proven for actor-state components only, but AD-5 moves checkpoints and other read state into separately scoped components (AD-26 Production proof `:345`, row `:563`)

- **Unit A: Story 7.22, built to** `:345` "restore posture for every actor-state component and scheduler state are each proven" and row `:563` "for every actor-state component and scheduler state". It drills restoring `statestore`, the Operations sink store, and the reminder stores to a point T0.
- **Unit B: Stories 5.7 and 3.17, built to** `:150` "State that any other app reads lives in separately scoped components". Projection checkpoints, read models, command status, and admin indexes move out of the actor-state component, so they are not restored.
- **Divergence:**
  - After a drilled restore, aggregates re-issue sequence numbers that the un-restored checkpoints have already passed, now with new `MessageId`s.
  - AD-8 defines no outcome for that case. A sequence guard that treats the event as already applied silently skips it, and the read model, which is the user-visible success evidence, diverges.
  - The AD-31 sink also has no consistency point against the subscriptions it acknowledged (adversarial M6, unaddressed).
  - This does not fail closed.
- **Closing wording (AD-26 Production proof and row `:563`):** "restore posture for every component the profile binds (actor-state, reader, and AD-31 sink components) and scheduler state, with one consistency point or a declared per-component restore order under which no checkpoint, acknowledgement, or command-status record is restored ahead of the actor state it describes". This text is inside the subject, so fix it before any record is issued.

### Medium

| ID | Divergence (Unit A vs Unit B) | Closing wording |
| --- | --- | --- |
| NM1 | Window anchor (`:343`) vs AD-11 `:210`. "First push to `main` of a commit whose spine yields the current subject digest" re-opens an old value. After an A→B→A revert, the window anchors at the first A push, so a record can follow the last authored change by less than 24 hours, which AD-11 forbids. "CI platform's recorded time" also names no record: the Activity API push (10:56:02Z here) or the push-triggered run (10:56:05Z here, and none at all for a `[skip ci]` push). | "starts at the time the repository's push-activity record gives for the latest push to `main` that changed the subject digest to its current value; for AD-26 records this is AD-11's 'last authored change'." |
| NM2 | AD-11 `:210` (unchanged) says every "ratification … binds its required and achieved" level. AD-12 `:218` now says a record "binds its required level and attestation evidence" and the consuming sealed run "computes and labels its achieved level". A Story 3.19 or 9.3 validator built from AD-11 rejects AD-12-shaped records as unlabelled. | Amend AD-11 `:210` (outside the subject): "…binds its required level and, for an owner attestation or ratification, the achieved level the consuming sealed run computes (AD-12)…". |
| NM3 | AD-12 re-runs the predecessor validator "inside that transition's sealed run" at the transition's head SHA. AD-11 `:208` authority records bind "Story 3.15 validator identity/result". After any change to that validator, such as a Story 4.15 reseal, the re-run identity differs from the bound one: one validator rejects and the other passes. | "…is re-run inside that transition's sealed run with the validator identity its record binds, retrieved from that identity's pinned commit; a mismatch voids the predecessor." |
| NM4 | AD-17 `:252` says "every consumer reads it from that codec", but `RestApi.Generators` is a `netstandard2.0` Roslyn component and `Contracts` is `net10.0` (`Directory.Build.props:60`), so the generator cannot call the codec at compile time. One implementer re-implements the codec in the generator, which is forbidden; another emits code that computes the digest at host startup, so it is not "embedded". AD-33 `:389` "each generated API host" also has no enumerating source (profile app IDs, catalog, or inventory). | "A generated host computes its declaration digests at startup through the `Contracts` codec; the AD-26 profile's app-ID list enumerates the generated API hosts that activation waits for." |
| NM5 | AD-5 `:150` "A domain service that hosts actors reaches only its own actor-state component." Reading (i): its only *actor-state* component. Reading (ii): its only *state* component, which forbids the `IReadModelStore` component (`Client/Projections/DaprReadModelStore.cs` saves through the Dapr state API) and the reader components that the next sentence requires for state other apps read. | "A domain service that hosts actors reaches no other app's actor-state component; its other state components, such as `IReadModelStore` read models, are separately scoped under the reader rule." |
| NM6 | AD-5 binds "database and schema" into the AD-26 digest. The production template keeps the database inside `connectionString`, and AD-24 sends it through `secretKeyRef`. Story 3.19's value-free profile then binds a logical secret name, while Story 4.16 needs the database identity. Dapr 1.18 accepts plain `host`, `port`, and `database` fields (components-contrib `release-1.18` `common/authentication/postgresql/metadata.go:36-42`). Separately, "in every profile" applies PostgreSQL-only target fields to the Redis Development profile. | "Each actor-state component declares `host`, `port`, `database`, `tableName`, and `metadataTableName` as plain metadata and takes only credentials through `secretKeyRef`; a component whose `connectionString` is a secret cannot be bound. For Redis profiles, the physical target is instance and `redisDB`." |
| NM7 | AD-33 `:387` "the entry names the resource-bound family it requires". Hop-scoped message and projection entries never carry one: trusted-effect submission and projection provenance enter at uncataloged ingress or channel-admitted delivery. The memlog interim "ingress kind is endpoint metadata (Story 5.14)" is absent from the spine (0 matches), while row `:540` supersedes 5.14's either-kind routes. Story 5.12 and Story 5.14 can each put the field in a different place. | Add to AD-33: "Until the owner decides ingress cataloging, an ingress endpoint's kind, operation, and required resource-bound family are endpoint metadata (Story 5.14); no message or projection entry names a resource-bound family." |
| NM8 | Row `:540`'s trigger "before the Story 5.12 spec freezes" does not gate Stories 5.7, 5.14, or 7.22, none of which depends on 5.12 (`epics.md` dependencies). Its blanket "superseded and do not bind" also removes Story 2.15's declaration-derived-manifest clause, whose substance the owner adopted in decision #5. | "…before the spec of any listed story freezes. Story 2.15's clause is superseded only in its 'until' framing; its manifest stays derived from the `Contracts` declarations." |
| NM9 | Story 9.3 "rejects … open `[ASSUMPTION]`" (`epics.md` Story 9.3 AC). The subject now permanently contains the literal "` [ASSUMPTION]`" in the canonicalization rule at `:343`. A text-scan validator can never pass G-BASELINE without a subject edit, which forces re-ratification and a new 24-hour window. | Add to the Invariants intro (outside the subject): "An `[ASSUMPTION]` is open only as the trailing tag of an AD heading; a quoted mention is not a tag." Route to the Story 9.3 extension in row `:541`. |
| NM10 | Adversarial H8 residual. To keep "no SDK or host default names another app's component", the `statestore` defaults in `EventStoreReminderOptions`, `EventStoreDataProtectionOptions`, `EventStoreDomainEventsOptions`, `ProjectionOptions`, `AdminServerOptions`, and the Aspire `AddDaprComponent("statestore", …)` must change. Those are behavior changes in released packages, and row `:542` does not route them through Story 3.18. | Append to row `:542`: "…and the SDK/host default-name changes, NFR12-classified through Story 3.18". |

### Low

- **NL1 (`:218`).** "Required means …" is unscoped, while the same paragraph's "required fixture job that blocks merges" uses the ruleset meaning. Scope the definition to the transition workflow.
- **NL2 (`:345` vs rows `:544` and `:563`).** The subject says "before any `production-promoted` record". The rows say "a prerequisite of the Story 3.19 issue step", which comes earlier. Align the rows.
- **NL3 (`:357` vs `:351`).** AD-28 contexts are "bound to tenant and operation". AD-27 says they bind "that scope" (a tenant or the namespace). Global actors such as `IGlobalPositionActor` have no tenant (pre-existing).
- **NL4 (`:343`).** Rendered Markdown hides the leading space in "` [ASSUMPTION]`". A reader of the rendered page gets `1f09de4d…`.
- **NL5 (`:409`).** AD-36 still lists McpCli among the sidecar-routed delegated-user callers, while AD-35 routes it over public edges (adversarial L1, open).
- **NL6 (row `:545`).** Only the 2026-09-23 packet is disqualified. The 2026-10-07 handoff packet also binds superseded bytes (rubric L5, open).
- **NL7 (`:357`).** "so no verifier can mint one" is false for self-hosted actors, where verifier and issuer are the same app. Say "no verifier other than the issuer".

## Disposition

- This file and the scratch script are the only writes. No spine, memlog, epics, PRD, tracker, or source edit was made, and no Git command mutated state.
- The closing wordings are inputs to the owner's next spine change. None is adopted here.
- NH1 and NH3 sit inside the AD-26 ratification subject. Applying either changes the subject digest and restarts the 24-hour window. They should land before any AD-26 record is issued.
- AD-26 stays `[ASSUMPTION]` and the spine stays `draft`. Closing these findings ratifies nothing.
