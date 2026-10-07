---
title: Architecture Update 2026-10-07b Review - Rubric Walker
date: 2026-10-07
lens: rubric-walker
subject: architecture.md working tree
assurance: tool-persona evidence only
---

# Architecture Update 2026-10-07b Review - Rubric Walker

> **Evidence class: `tool-persona`.** This review is evidence only. It is not an approval, a ratification, or an attestation, it records no assurance level for any gate, and it changes no gate result. The owner holds every role.

**Verdict: CHANGES REQUIRED. 0 critical, 8 high, 11 medium, 9 low.**

The run records all 11 owner resolutions faithfully. It also names an owner, taken from the routing proposal, in every row that read "no owning story yet". AD-16 is unchanged, the AD-26 target stays `[ASSUMPTION]`, the spine stays `status: draft`, and lint reports 0 findings. Eight high findings remain. Three concern the new AD-26 ratification subject: two people can compute it differently, ratification invalidates its own record, and Story 9.3 contradicts it. The other five are interactions that adopting the assumptions made binding without an owner or a consistent story text:

- the AD-12 seal workflow
- the AD-33 credential-kind key
- per-app actor-state components
- the internal-proof keys
- the stale "assumption boundary" text in Stories 5.12, 2.15, and 5.7

None of these authorizes an unsafe action today, because every production, readiness, and release path still fails closed. That is why none is critical.

## Review boundary and method

| Item | Observed result |
| --- | --- |
| Subject | `_bmad-output/planning-artifacts/architecture.md` working tree, SHA-256 `d3e5fcc5d0f6961c3976c31546793774f77e24993f704f8a7da663d012cb0b2a`. Base is `HEAD` `4e4ee858`, spine `1ff06c5e…` (+27/−26). The SHA was re-checked at the end of the review and is unchanged. |
| Scope | Only the changes this run made: AD-5 Append race, AD-10 JWT contract, AD-11 Compatibility authority, AD-12 Gate validators, the AD-17 MessageId contract version, the AD-24 Secret contract, AD-26 (Rule, Ratification, and Production proof), AD-27, AD-28, the AD-33 Rule, AD-35, the Invariants intro, the Stack intro and Code coverage row, the Implementation Status preamble, and the changed rows. Unchanged ADs were read only where changed text interacts with them. |
| Decision log | `.memlog.md` lines 186-199, from the "Update run 2026-10-07b opened" entry through the "2026-10-07b spine edits applied" entry. |
| Owner sources | `sprint-change-proposal-2026-10-07-architecture-routing.md` (working tree, applied); `epics.md` SHA-256 `a7474fc1…` (matches the proposal's Application Record); `prd.md` SHA-256 `e1eea265…`. |
| Lint | `python3 -I .claude/skills/bmad-architecture/scripts/lint_spine.py --workspace <run folder>` returned `ok: true`, 0 findings. |
| Subject digest probe | A scratch script (`python3 -I`) computed the AD-26 ratification subject under four readings of the spine sentence (see H1). Lines are LF with no CR and no trailing whitespace, and the subject lines are ASCII-only. |
| Brownfield spot checks | `deploy/dapr/statestore-postgresql.yaml:12-14,34-36`; AppHost `DaprComponents/statestore.yaml:18-20,50-53`; `Server/Commands/IdempotencyExecutionContextProtector.cs:209-223`, `TrustedEffectGatewayProof.cs:25`, `TrustedEffectErasureCapability.cs:29-34` (all on `IIdempotencyDigestKeyProvider`); `IAggregateActor.cs:44`; `IETagActor.cs:22`; `DomainService/ReminderActor.cs` and `EventStoreReminderServiceCollectionExtensions.cs:104` (`AddActors`); `Operations/Program.cs:29` (`AddActors`); ServiceDefaults and Contracts `.csproj` files (neither references the other); the Builds catalog at gitlink `397c94a4`. All reads were read-only. |

## Rubric results

| Checklist item | Result |
| --- | --- |
| Each changed Rule is enforceable and prevents its stated divergence | **Partial.** H1, H2, H4, H5, M1, M2, M6 |
| The text records the owner decisions with no drift, nothing added, and refinements present | **Pass with gaps.** All 11 resolutions and their refinements are present, and no unrequested decision was found. Two items the memlog records are missing from the spine: the reviewer-gate enforcement for AD-26 (M1) and the AD-28 actor-interface NFR12 consequence (H7). |
| Nothing changed lets two units one level down diverge | **Fail.** H3-H8 |
| Named owners exist in `epics.md` with the claimed scope | **Partial.** 3.21, 5.12, 5.13, 5.14, and 7.22 exist, and extended 2.14, 3.19, 4.16, 5.7, and 9.3 carry the routed text. Gaps: Story 9.2 lacks the AD-12 workflow (H4); Story 9.3 lacks the subject digest (H3); Story 5.12 lacks the fields (H8); Story 7.22 lacks per-app actor stores (M9); Story 5.7 lacks broker intake (L4). |
| AD-16 anonymous-endpoint rule unchanged | **Pass.** `:244` is not in the diff. |
| AD-26 target stays `[ASSUMPTION]` and is not presented as ratified | **Pass.** `:337`, `:341`, `:343`, `:539`. The broker row says the owner selects it "only in the AD-26 record". |
| Spine stays `status: draft` | **Pass.** `:8` |
| No contradiction introduced with AD-36, AD-18, AD-11 Assurance level, AD-31, or Design Paradigm | **Partial.** AD-36, AD-18, AD-31, and Design Paradigm are consistent. AD-11 Assurance level vs the AD-12 per-transition seal is underspecified (M3). AD-33 vs AD-33 Activation and AD-25 keys conflict (H5). |
| AD-26 subject canonicalization gives two people the same bytes | **Fail.** H1 (four readings give four digests) and H2 |
| Stack intro and Code coverage row | **Pass.** The Builds gitlink is `397c94a4`, and `Microsoft.Testing.Extensions.CodeCoverage` is `18.12.0` in `Props/Directory.Packages.props:267`. The other rows still match the catalog at that gitlink. |

## Critical

None.

## High

### H1 - The AD-26 ratification subject has several valid byte readings (AD-26 Ratification)

- **Location:** `architecture.md:343`, AD-26 **Ratification**: "the SHA-256 of the UTF-8 bytes of the AD-5 **Append race** paragraph followed by this section, from its heading through its last non-blank line before the next `###` heading, each line terminated by LF."
- **Problem:** "followed by" does not say whether a separator line comes between the paragraph and the heading. "Paragraph" has no stated boundary, and "next `###` heading" also matches a `####` line. The scratch probe over the current bytes produced:
  - `16ab54f4e6a84dca2330df8646d0aabb48904765b06c8a93bca05f88bb2bc436` with direct concatenation.
  - `20a43275641505022255875967b5b207df83f52f50d68491da3a2b85b26a328a` with a blank separator line, as in the source document.
  - `98f2b825…` with non-blank lines only.
  - `f38dc682…` with LF only between lines.

  The first two are both plausible readings. A Story 9.3 or Story 3.19 validator and the owner's record would then disagree, and a valid ratification would be refused. Refusal fails closed, but the rule cannot be applied as written, and the owner chose this rule to make records stable.
- **Closing wording:** Replace the sentence with: "Each record binds the ratification subject digest, the lowercase hexadecimal SHA-256 of the subject bytes S. To build S, split the spine's UTF-8 text at LF and take each line without its terminator and otherwise unchanged. S is the AD-5 line that begins `**Append race (NFR7 class (c)).**` and each following line up to, not including, the first empty line. With no separator line, it continues with the line that begins `### AD-26 `, with its trailing status tag and the preceding space removed, and each following line up to, not including, the next line that begins `### ` or `## `, after trailing empty lines are dropped. Every line of S, including empty lines inside AD-26, ends with exactly one LF. The reference script, and the subject value bound by each record, are kept outside the subject in the Story 9.3 manifest and the run memlog." Under that wording, the current bytes give `70a846b077b555e333ae35d4bad7b88f4aa5c945ad0bf84d989175f481719014`. Record that value outside the subject, never inside AD-26.

### H2 - Ratification and its downstream stories must edit the ratified subject (AD-26 Ratification and Production proof)

- **Location:** `:337`, the heading "### AD-26 - Production Runs Only On A Proven Fail-Closed Profile [ASSUMPTION]". `:341`: "Only the target selection in this Rule … is the assumption". `:345`: "The file is absent, so no profile currently authorizes promotion." `:343`: "An edit outside the subject re-digests the Story 9.3 baseline without invalidating a record". This implies that an edit inside the subject does invalidate a record.
- **Problem:** The subject includes text whose truth changes because of the ratification and its consumers:
  - Ratifying the target means changing the heading tag to `[ADOPTED]` and rewording the "is the assumption" sentence. Story 9.3 rejects any "open `[ASSUMPTION]`" (`epics.md:7615`). Either edit changes the subject, so the record no longer binds it. The owner would need to re-ratify after another 24 hours, and that record would ratify text that already claims to be adopted. "Stays `[ASSUMPTION]` until … records" and "editing the tag invalidates the record" cannot both be satisfied.
  - When Story 3.19 publishes `deploy/dapr/production-profile.yaml` after ratification, "The file is absent" becomes false. Correcting it invalidates the ratification that the `production-promoted` record depends on as a predecessor.
- **Closing wording:** Add to Ratification: "The subject excludes the AD-26 heading's status tag. The only change inside the subject that a valid record survives is the change, in the change that records ratification, of the target clause in the Rule from '`[ASSUMPTION]`' wording to adopted wording, as the record names it. Present-state statements about delivery stay outside the subject in the Implementation Status table." Then move "The file is absent, so no profile currently authorizes promotion." out of `:345` into the "Canonical production-profile identity…" row at `:541`, where it already appears as "No profile currently authorizes promotion".

### H3 - Story 9.3 would reject the AD-26 records this rule keeps valid (AD-26 Ratification vs Story 9.3)

- **Location:** `:343`: "Story 9.3 records the subject digest in its manifest and never redefines it. An edit outside the subject re-digests the Story 9.3 baseline without invalidating a record". `epics.md:7613-7615` (Story 9.3): the manifest "binds the SHA-256 of … `architecture.md`" and rejects "any approval that predates the bytes it binds".
- **Problem:** Story 9.3's acceptance criteria bind the whole-file digest only, and none records a ratification subject digest. A later edit outside the subject, such as the AD-16 amendment that waits on PRD item 7.2 or the AD-36 reconciliation at Story 5.5 `done`, makes every AD-26 record "predate the bytes" the manifest binds. Story 9.3's validator then rejects records that AD-26 says remain valid, and G-BASELINE cannot pass without re-ratifying. This is the outcome the owner's #7 decision was meant to prevent (memlog `:192`). The spine also claims a Story 9.3 scope that `epics.md` does not contain.
- **Closing wording:** Add to Ratification: "Story 9.3's validator checks each AD-26 record against the ratification subject digest and that subject's last authored change, never against the whole-file digest, and its 'approval that predates the bytes it binds' rule applies to an AD-26 record only through the subject." Add the same obligation to the G-BASELINE row (`:539`): "Story 9.3 binds the AD-26 subject digest as a separate manifest field; extending its scope routes through correct-course before its spec freezes."

### H4 - The AD-12 seal workflow is owned by a story that does not contain it, and contradicts the PRD (AD-12 Gate validators)

- **Location:** `:218`: "A seal, under the PRD Assurance Control, is the gate validator's result … for a required run of a dedicated transition workflow … Story 9.2 owns that workflow; each run is triggered for one guarded transition (…readiness, `release-available`, `production-promoted`, or consumer removal), so a truthful FAIL blocks only that transition and never `main`."
- **Problem:** Three units now read three different definitions.
  - The PRD glossary Assurance Control (1), which AD-12 cites as its authority, still reads "a required, blocking run" (`prd.md:201`). The memlog records "The PRD and AD-12 disagree until /bmad-prd applies it" (`:191`), but the spine records this nowhere.
  - Story 9.2's acceptance criteria (`epics.md:7578-7581`) make its validator "a blocking, required check" and say nothing of a dedicated transition workflow. They also cover only the gate-PASS and story-`done` transitions (OR13), not readiness, `release-available`, `production-promoted`, or consumer removal.
  - "Required" is undefined. A branch-protection required check on `main` would block `main` on a truthful FAIL and contradict "never `main`". The owner can bypass the `main` ruleset, so a ruleset-required check is also not a seal.
- **Closing wording:** Add to AD-12: "'Required' means the transition's validator requires a run of that workflow, retrieved from the CI platform; it is never a branch-protection or ruleset check on `main`. A transition recorded without such a run is rejected by the consuming gate validator." Add an Implementation Status row:

  | Item | Safe posture | Owner and trigger |
  | --- | --- | --- |
  | Seal transition workflow (AD-12) | No seal exists. The PRD Assurance Control (1) still reads "required, blocking run", and Story 9.2 defines only a blocking check and two transitions. | Product owner applies PRD item 7.1 option A through `/bmad-prd`; correct-course extends Story 9.2 to the dedicated transition workflow for all six transitions; both before the Story 9.2 spec freezes. |

### H5 - One credential kind per entry, with two operations for a capability both kinds need, collides with the catalog key (AD-33 Rule vs AD-33 Activation and AD-25)

- **Location:** `:387`: "Commands/queries map by `(Domain, MessageType)` … to exactly one app ID, method, and contract version … Each route entry also declares exactly one admitted credential kind … a capability both kinds need is cataloged as two operations." `:389`: "Any duplicate, missing, or ambiguous entry … causes readiness to fail." `:335` (AD-25): the idempotency facet is "keyed by stable route-entry ID and `(Domain, CommandType)`".
- **Problem:** One kind and one operation per entry means a command or query reached both by a human relay (delegated-user) and by a workload, such as a trusted effect or a cross-domain query, needs two entries. Both entries have the same `(Domain, MessageType)` key and the same AD-25 facet key. The Activation rule then fails readiness as a duplicate. Story 5.12's implementers will choose differently: a key extended with kind, one entry with an admission list, or distinct message types. Each choice changes the codec and the AD-25 facet join.
- **Closing wording:** Add to AD-33: "A route entry is keyed by `(Domain, MessageType, credential kind)` for commands and queries and by `(Domain, ProjectionType, credential kind)` for projections. Entries that share `(Domain, MessageType)` must resolve to the same app ID, method, contract version, and AD-17 declaration digest, and the AD-25 idempotency facet joins on route-entry ID with `(Domain, CommandType)` unique per kind. Duplicate and ambiguity checks apply to that key." If the owner prefers one entry with an admission list, the alternative wording is: "one entry per `(Domain, MessageType)` carrying a list of admissions, each pairing exactly one credential kind with one operation from that kind's vocabulary." Either way, this is an owner choice.

### H6 - Per-app actor-state components have no owner, and Story 5.7 contradicts them (AD-5 Append race, AD-26 Rule)

- **Location:** `:150`: "each actor-hosting app ID (EventStore, Operations, and domain services that use typed reminders) has its own `actorStateStore: true` component scoped to it alone in every profile … AD-9 parity is reached by moving the AppHost to that posture". `:341` uses the matching wording, "one stable `state.postgresql` v1 component … per actor-hosting app ID".
- **Problem:** The rule is adopted for every profile, but no Implementation Status row or story owns splitting the components, migrating the readers, or moving the AppHost. Current brownfield and story text contradict it:
  - `deploy/dapr/statestore-postgresql.yaml:34-36` scopes `statestore` to `eventstore` and `eventstore-admin`. The AppHost `statestore.yaml:50-53` adds `tenants`. Both files carry "Domain services have zero state store access (D4)" (`:12-14`, `:18-20`).
  - Story 5.7's acceptance criteria require that "domain services and service-invocation-only workloads retain zero actor-state access" (`epics.md` Story 5.7, state-store AC). Story 5.6 has no per-app actor-store criterion.
  - `DomainService/ReminderActor` and `Operations/DeadLetterDrainActor` host actors (`AddActors` at `EventStoreReminderServiceCollectionExtensions.cs:104` and `Operations/Program.cs:29`). Neither has an actor-state component of its own in any profile.

  Story 5.7 and Story 5.6 will build the opposite of what Story 4.16 proves and Story 3.19 binds.
- **Closing wording:** Add an Implementation Status row:

  | Item | Safe posture | Owner and trigger |
  | --- | --- | --- |
  | Per-app actor-state components (AD-5, AD-26) | `statestore` is shared by `eventstore`, `eventstore-admin`, and (AppHost) `tenants`. Operations and typed-reminder domain services have no actor-state component. Story 5.7 still requires zero actor-state access for domain services. No envelope claim. | Correct-course extends Story 5.6 (AppHost) and Story 5.7 (production) to one actor-state component per actor-hosting app ID, scoped to it alone, and replaces Story 5.7's domain-service criterion. Story 3.17 inventories every other reader of those components with its data migration. Before Story 4.16. |

### H7 - The internal-proof key move and actor execution contexts have no owner, and AD-24 omits an existing proof (AD-24 Secret contract, AD-28)

- **Location:** `:309`: "Internal proofs, namely the trusted-effect gateway proof and the AD-28 execution context, use dedicated keys inventoried here, never the AD-25 digest ring. A digest-key generation retires only after every registered consumer, including any trusted-effect gateway proof not yet moved to its dedicated key, shows no live reference." `:357`: "every actor method that discloses, admits, or mutates validates one EventStore-issued execution context, integrity-protected with a dedicated AD-24 key … for mutating aggregate methods it is the AD-5 fenced context".
- **Problem:**
  - Three committed internal proofs HMAC with the AD-25 ring through `IIdempotencyDigestKeyProvider`: `IdempotencyExecutionContextProtector` (`:209-223`, the AD-5 fenced context from done Story 4.11), `TrustedEffectGatewayProof` (`:25`), and `TrustedEffectErasureCapability` (`:29-34`). "Namely" lists only two of them. The retirement clause protects only gateway proofs, so a ring generation could retire while fenced execution contexts or erasure capabilities still use it.
  - `IAggregateActor.GetEventsAsync(long)` (`:44`) and `IETagActor.RegenerateAsync()` (`:22`) take no context. Changing them is an NFR12 change to the released `Server` package, which memlog `:193` records and the spine does not.
  - No row owns the key move, the actor-interface change, or the AD-24 contract entries. The fallback row assigns only ACLs and the actor-invocation qualification to Story 5.7. Dapr access-control policies govern service invocation, and actor-API invocation is not ACL-scoped in Dapr's documented model; this review did not re-verify that. If so, the qualification is likely to fail, and the execution context becomes the permanent control.
- **Closing wording:** Change `:309` to: "Internal proofs, which are the AD-5 and AD-28 execution context, the trusted-effect gateway proof, the trusted-effect erasure capability, and any later internal proof, use dedicated keys inventoried here, never the AD-25 digest ring. A digest-key generation retires only after every registered consumer, including every internal proof not yet moved to its dedicated key, shows no live reference." Add an Implementation Status row:

  | Item | Safe posture | Owner and trigger |
  | --- | --- | --- |
  | Internal-proof keys and actor execution contexts (AD-24, AD-28) | The execution context, gateway proof, and erasure capability use the AD-25 ring. Disclosing and freshness actor methods take no context. No AD-28 actor-boundary claim. | Owner story assigned by correct-course. The actor-interface change is NFR12-classified through Story 3.18, and the dedicated keys are inventoried in the Story 7.6 OpenBao contract. Before readiness. |

### H8 - The resolved assumptions do not reach the stories that were fenced off from them (Implementation Status preamble and catalog row)

- **Location:** `:535`: "Story 9.3's repin change propagates every new AD ID into the affected constraint lists". `:558`, the catalog row: "Story 5.12 delivers the `Contracts` schema, codec, validator, and Development/test envelope".
- **Problem:** The routing proposal fenced stories off from the 11 assumptions. Those fences are now stale, and no one is assigned to lift them:
  - Story 5.12 "adds none of [the MessageId-version digest, the platform-operation namespace, the credential kind and operation] as a field until the owner resolves" them (`epics.md:4737`). No acceptance criterion requires the fields now that they are resolved.
  - Story 2.15's interim "until the owner resolves the AD-17 carrier assumption" clause.
  - Story 5.7's acceptance criterion: "neither adopts nor rejects the AD-28 `[ASSUMPTION]`" (`epics.md:4524`).

  The spine routes only new AD IDs, AD-34 through AD-36, to Story 9.3. The binding of 5.12 fields lives only in memlog `:187-189`. Story 5.12 can therefore ship a schema without the version, digest, kind, or operation fields, while AD-17 activation (`:252`), Story 2.15, and Story 5.13 depend on them.
- **Closing wording:** Append to `:535`: "The 2026-10-07b owner resolutions of the 11 inline assumptions supersede every story text that defers to them: the Story 5.12 assumption boundary, the Story 2.15 interim clause, and the Story 5.7 AD-28 criterion. Correct-course updates those stories before the Story 5.12 spec freezes." Append to the catalog row: "Its schema carries, per entry, the AD-17 MessageId version and declaration digest, the AD-27 platform-operation namespace entries, and the AD-33 credential kind and operation, each derived from the `Contracts` declarations."

## Medium

### M1 - The "edit AD-26 in the same change" rule names no enforcer (AD-26 Ratification)

- **Location:** `:343`: "so an edit elsewhere that changes what AD-26 requires edits this section in the same change".
- **Problem:** Whether an edit "changes what AD-26 requires" is a judgment. The memlog says "(reviewer gate checks)" (`:192`), but the spine names no check. An edit to AD-24, AD-33, AD-34, or AD-36 that weakens what the profile binds by reference can leave a ratification valid while its meaning changes.
- **Closing wording:** "The `bmad-architecture` reviewer gate records, for every spine change outside the subject, whether it changes what AD-26 requires, and Story 9.3's validator rejects a repin whose run log lacks that record."

### M2 - "Last authored change to the subject" is undefined in a repository with auto-committing loops (AD-26 Ratification, AD-11)

- **Location:** `:343`: "the AD-11 24-hour window runs from the last authored change to the subject".
- **Problem:** This run's edits are uncommitted. Earlier spine bytes reached `main` inside unrelated commits (`48ef7171`, `af2892e8`), and their author dates are not authenticated. Validators will choose different times: the file's mtime, the author date, or the committer date.
- **Closing wording:** "The last authored change is the committer timestamp of the earliest commit reachable from `main` whose spine yields the current subject digest, as reported by the CI platform. A record issued before that commit exists is invalid."

### M3 - The achieved assurance level of an owner record is undefined once seals attach only to transitions (AD-12 vs AD-11 Assurance level)

- **Location:** `:218`: "A seal attaches to a transition, never to an owner record". `:210`: "Every gate result, authority record, ratification, and receipt … binds its required and achieved PRD Assurance Control level".
- **Problem:** At issuance, an AD-26 record has no seal, so its "achieved" level cannot yet be `single-maintainer-attested`.
- **Closing wording:** Add to AD-12: "An owner record binds its required level and its attestation evidence; its achieved level is computed and labelled by the sealed run of the transition that consumes it."

### M4 - AD-33 operation vocabularies live in ServiceDefaults, but `Contracts` declarations must name them (AD-33, AD-10, AD-36)

- **Location:** `:387`: "the operation it requires from that kind's vocabulary (the AD-10 human-bearer operation claim or the AD-36 workload operation) … kind and operation are declared in the `Contracts` route declaration". AD-36 `:409`: ServiceDefaults owns the "operation vocabulary".
- **Problem:** `Contracts` and ServiceDefaults reference neither package, so declarations can name operations only as unchecked strings. AD-10 also never defines the human-bearer operation claim; existing human tokens carry `eventstore:permission`. The deferral (adv M1, "revisit at the Story 5.12 spec freeze") exists only in the memlog.
- **Closing wording:** Add to the catalog row: "Before the Story 5.12 spec freezes, the owner decides the single owner of both operation vocabularies, which `Contracts` can reference, and names the AD-10 human-bearer operation claim type and how a human grant satisfies a cataloged operation."

### M5 - The AD-17 declaration digest has no canonical form or owner (AD-17)

- **Location:** `:252`: "derived from it mechanically and carry its digest, and activation fails when a generated host's embedded digest differs".
- **Problem:** The generator, the catalog, and the Story 3.18 inventory compute "its digest" independently. Different encodings would fail activation on correct hosts.
- **Closing wording:** "The declaration digest is the SHA-256 of the declaration's canonical bytes produced by the Story 5.12 `Contracts` codec, and every consumer reads it from that codec."

### M6 - AD-11's sole compatibility authority is not sequenced against Story 2.15's manifest (AD-11 Compatibility authority)

- **Location:** `:194`: "other stories' classifications, such as Story 2.15's MessageId versions, are written into its schema rather than a second manifest". Story 2.15 (`epics.md`) validates "a versioned contract manifest" that "records an NFR12 compatibility classification", and does not depend on Story 3.18.
- **Problem:** If Story 2.15 finishes first, it creates exactly the second manifest that AD-11 forbids.
- **Closing wording:** "Until the Story 3.18 schema exists, Story 2.15's manifest is a declaration-derived projection with no compatibility authority; Story 3.18 imports it and retires it, and the release lane reads only the Story 3.18 inventory."

### M7 - McpCli's admin path reads two ways (AD-35)

- **Location:** `:403`: "McpCli reaches gateway-ready operations only through decorated Contracts and the EventStore gateway, over the public AD-3 and AD-21 HTTP edges … with admin operations terminating at Admin Server".
- **Problem:** The sentence does not say whether admin operations pass through the EventStore gateway, which then reaches Admin Server, or call Admin Server directly over AD-21. The memlog says "over the public AD-21 edge".
- **Closing wording:** "McpCli reaches gateway-ready domain operations only through decorated Contracts and the EventStore gateway over the AD-3 edge, and admin operations only through Admin Server over the AD-21 edge, in both cases with the end user's AD-10 bearer or an AD-29 delegation."

### M8 - An execution-context key held by non-EventStore actor hosts lets those hosts mint contexts (AD-28)

- **Location:** `:357`: "every actor method that discloses, admits, or mutates validates one EventStore-issued execution context, integrity-protected with a dedicated AD-24 key so no peer can mint one".
- **Problem:** Operations and typed-reminder domain services host actors. If they must validate a context protected by a symmetric dedicated key, as the HMAC pattern today does, they hold the minting key.
- **Closing wording:** "The rule covers EventStore-hosted actors. Any other actor host validates with verification-only key material and cannot mint, or its actor methods are cataloged as channel-only callbacks."

### M9 - Story 7.22's restore scope names only `statestore` (RTO/RPO row, AD-26 Production proof)

- **Location:** `:559`: "Story 7.22 writes and drills the restore posture including scheduler state". `:345` binds "every actor-state component". Story 7.22 lists "actor state in `statestore`".
- **Closing wording:** Change the row to "Story 7.22 writes and drills the restore posture for every actor-state component and scheduler state…".

### M10 - The adopted AD-10 validation profiles are not in Story 5.11's scope (AD-10, Shared JWT row)

- **Location:** `:186`: "versioned validation profiles, each with its own fingerprint". Story 5.11 requires "one versioned JWT contract with a fingerprint", and the memlog says "Story 5.11 builds it".
- **Closing wording:** Add to the `:542` row: "Story 5.11's scope is extended through correct-course to build and fingerprint each validation profile."

### M11 - The new definition of `[ASSUMPTION]` does not say what scope the AD-26 heading tag covers (Invariants intro)

- **Location:** `:111`: "`[ASSUMPTION]` marks a decision that awaits owner ratification; only the AD-26 production target carries it." The tag sits on the whole AD-26 heading, while `:341` limits the assumption to "the target selection".
- **Closing wording:** "…only the AD-26 production target carries it; the tag on the AD-26 heading marks that target alone, and the rest of AD-26 binds as adopted."

## Low

- **L1 (`:544`).** "after which AD-16 is amended" assumes PRD option A. Proposed wording: "after which AD-16 is amended if NFR1 changes".
- **L2 (`:541`).** "with Story 3.21 supplying the runtime-pin evidence" omits the broker (Story 3.21) and the restore posture (Story 7.22), both of which the record must name. Proposed wording: "with Stories 3.21 and 7.22 supplying the runtime-pin, broker, and restore-posture evidence".
- **L3 (`:345`).** "so no `production-promoted` record is issued before them". Story 3.19 also withholds `release-available`. Proposed wording: "no `release-available` or `production-promoted` record".
- **L4 (`:540`).** "Story 5.7 takes the selected component". Story 5.7's text has no Story 3.21 broker dependency, so the intake rests only on Story 3.21's acceptance criteria. Add it at the next correct-course.
- **L5 (`:343`).** Only the 2026-09-23 packet is disqualified. The 2026-10-07 handoff packet binds `1ff06c5e…`, whose AD-26 subject this run changed. Proposed wording: "…and the 2026-10-07 packet bind superseded spine bytes and cannot serve as that record."
- **L6 (outside the spine).** The routing proposal §5 says "no earlier than 2026-10-08 09:34 +02:00". This run's AD-26 edits superseded that window. Append a memlog note that the window restarts at this run's last commit to the subject.
- **L7 (`:194`).** The AD-11 example names only Story 2.15. Memlog #3 also names the Story 2.14 `system` migration and the AD-28 actor-interface change. "Such as" covers them, but naming them would close the audit trail.
- **L8 (`:357`).** "bound to tenant and operation" does not name which operation vocabulary an actor execution context uses.
- **L9 (`:343`).** If the spine is ever saved through a normalizing editor, the subject bytes change silently. Add "no Unicode normalization is applied" to the H1 wording.

## Recorded without findings

- AD-5, AD-10, AD-17, and AD-27 lost only their tags. Their text matches the memlog "as written" decisions.
- AD-33 records #10 and both refinements, including the change from "assertion" to "credential".
- AD-12 records both #4 refinements: the guarded set and the seal attaching to a transition.
- AD-28 records the qualification locus and the dedicated key.
- AD-24 records #6.
- AD-35 records #11.
- AD-26 Rule records the per-app target alignment, and the target stays `[ASSUMPTION]`.
- The Implementation Status preamble cites Story 9.3 Group 5, which matches the Story 9.3 acceptance criterion at `epics.md:7617-7620`.
- The tenant row matches Story 2.14 as extended, including option 4b.
- The codec row matches Story 3.19's codec acceptance criterion.
- The NFR7 row matches Story 4.16's dependencies.
- The fallback row's NFR1 reference correction matches routing item 7.2.
- No changed text contradicts AD-36, AD-18, AD-31, or Design Paradigm.
