# Architecture Update Input Reconciliation: Proposals 2026-09-26 to 2026-09-30

> **Anchors.** Spine `_bmad-output/planning-artifacts/architecture.md` was read at SHA-256
> `ca7898c7d0bd22d9f5b0260f6986b49bb4a0153bc1fae6d6fe0149f81d900cab`, clean against HEAD `27ac3c62`.
> Line numbers (`:NNN`) refer to that byte state only. A concurrent update run may move them, so resolve
> every citation by the AD or section that the prose names. `ARCHITECTURE-SPINE.md` is a symlink to the spine.
>
> | Input | SHA-256 |
> | --- | --- |
> | `sprint-change-proposal-2026-09-26-solo-maintainer-assurance.md` | `d1b3e28b…be55d` |
> | `sprint-change-proposal-2026-09-26.md` | `8eaf9d55…a18f` |
> | `sprint-change-proposal-2026-09-27.md` (EventStore copy: the Story 6.5 split) | `ee7bab7d…6068` |
> | `references/Hexalith.Platform/_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-27.md` (the McpCli course correction) | `8290a1b6…174a` |
> | `sprint-change-proposal-2026-09-30.md` | `b54af18e…c6d916` |

## Scope and method

- **What was read.** Each input was read in full. Directives that fix an invariant two independently built units could otherwise choose incompatibly were extracted. That covers boundaries, ownership, mutation paths, authentication, topology, release and authority semantics, and the owner-role and assurance vocabulary that ADs use.
- **What was ignored.** Story slicing, sequencing, and tracker details were ignored unless an AD names the story or uses the affected wording.
- **What each directive was compared against.** The spine, its memlog (including the uncommitted 2026-10-07 entries appended by the concurrent update run), the current PRD glossary and §11.4 rows, the McpCli spine, and the AD-26 handoff record.
- **Constraint applied throughout.** The project has exactly one human. No recommendation names a second reviewer, and no recommendation labels solo work `independent`.

### Input-identity finding (P1)

The input listed as "3. `sprint-change-proposal-2026-09-27.md` (McpCli course correction)" is **two different documents**:

- **The EventStore file at that path** is *"Split and compact Story 6.5"*, approved on 2026-09-27. Its `git log` subject is `02cf007c`.
- **The McpCli course correction** is the Platform proposal *"Unified Hexalith MCP and CLI ownership"*. It lives only at `references/Hexalith.Platform/_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-27.md`. EventStore received its effect through `4fcb2b5c`, which edited the spine, the PRD, and epics with no local proposal file.

**The concurrent memlog entry miscites this.** It reads "Recover as adopted (owner 2026-09-27, sprint-change-proposal-2026-09-27): Hexalith.McpCli…", which points at the Story 6.5 split. That entry should cite the Platform path. Both documents are reconciled below, as 3a and 3b.

## Verdict

**CHANGES REQUIRED. The spine is not faithful to these inputs.**

- **The solo-maintainer Assurance Control is absent from every AD whose records are named as its downstream consumers:**
  - AD-11 `release-available` and `production-promoted`
  - AD-22 consumer removal
  - AD-26 ratification and promotion
- **The McpCli direction exists only as a floating paragraph.** It omits the EventStore-side ownership boundary. Its last sentence is broad enough to demote AD-21, AD-29, and AD-3.
- **No other input contradicts an AD.**

| Status | Count |
| --- | ---: |
| LANDED | 13 |
| PARTIAL | 11 |
| MISSING | 7 |
| CONTRADICTED | 2 |
| N/A (not architecture-level) | 1 |

The two input "Architecture: none" dispositions only partly hold:

- **Input 1 §2** says "No AD mentions independence or non-authorship". That is true of human review. But the control's label-propagation clause explicitly names records whose schemas AD-11 and AD-22 enumerate. The spine's validator rejection lists and record field lists are therefore incomplete against the PRD.
- **Input 2 §4.F** ("no edit") holds, with one exception: the 3.15 wording under AD-1.

---

## Input 1: Solo-maintainer Assurance Control (2026-09-26)

| # | Directive (locator) | Status | Spine evidence | Proposed amendment |
| --- | --- | --- | --- | --- |
| A1 | The required level is computed from the owner-role registry. It is `single-maintainer-attested` while the registry names exactly one human and `independent` at two or more. It switches back automatically, with no PRD change (§3 Decision bullets 3–4; §3 "Required level"). | PARTIAL | AD-11 Publication lifecycle (`:180`) binds "role-registry identity". AD-22 (`:255`) binds "owner-role registry identity". Neither computes a level or states the switch. | AD-11 new **Assurance level** paragraph (wording in §Consolidated amendments). |
| A2 | Tool personas (`bmad:*`) and CI identities never count as human identities. Adversarial review is `tool-persona` evidence only and never an approval identity (§3 "Required level", "Optional adversarial review"). | MISSING | AD-11 accepts "three packet-bound receipts" for `evidence-validated` (`:180`). One of those receipts is from `bmad:murat`, a tool persona (Input 2 §1 table). The spine is silent on whether personas count. | Same AD-11 paragraph: "tool personas and CI identities never count as human approvers". |
| A3 | Sealed CI validation means the validator result is retrieved from the CI platform for a required, blocking run on the exact head SHA and workflow-file digest, under the authenticated CI identity. It is never an author-supplied file (§3 item 1). | MISSING | AD-12 requires persisted evidence. AD-11 says "A validator must bind …" (`:302`). Neither fixes where a validator result comes from, so a record producer and a validator could disagree, one using a file and the other a CI-fetched result. | AD-12: add one sealed-result sentence. AD-11 rejects unsealed results. |
| A4 | The owner attestation is bound to the subject digest and created ≥24 h after the last authored change to that subject (§3 item 2). | MISSING | AD-11 records bind "issuance, expiry" but no last-authored-change time and no separation rule. | AD-11 rejection list: "attestation created < 24 h after the last authored change to its subject". |
| A5 | Label propagation. The gate result and every downstream record (`READY`, `release-available`, `production-promoted`, consumer removal) carry the label. A downstream record takes the **lowest** level of its inputs (§3 item 3). | MISSING | The AD-11 record field list (`:180`) and the AD-22 receipt field list (`:255`) have no assurance-level field. The AD-26 production proof (`:302`) and the gate-table row "Publication authority transitions" (`:475`) are also silent. | AD-11 paragraph. AD-22 bind list adds "required and achieved assurance level". AD-26 production proof adds the same. |
| A6 | Validators reject: an overstated label (`independent` with one human, self-approval, or an author alias); a level below the required one; an attestation inside the window; a missing seal (§3 "Validators reject"). | PARTIAL | AD-11 rejects "missing, duplicate, unknown, skipped, expired, revoked, wrong-role, or mismatched records". AD-22 rejects "Booleans, free-form or self-declared approval…". Neither list covers any assurance failure. | Extend the AD-11 rejection list. AD-22 and AD-26 reference it. |
| A7 | No record may call single-maintainer work `independent`. `independent` becomes a reserved assurance label (§3 Decision bullet 2). | PARTIAL | Spine ADs use "independent" in non-assurance senses that now collide with the reserved label: AD-11 Release evidence (`:178`), "the **independent** lifecycle and AD-26 gates below"; AD-26 metadata handoff (`:298`), "no **independent** physical-byte or historical-generation receipt is claimed"; AD-26 Rule (`:296`), "**independent** DAPR sidecars" (technical, low). | Reword `:178` "independent lifecycle" → "separate publication-lifecycle". Reword `:298` "no independent" → "no separate". Optionally reword `:296` → "per-application DAPR sidecars". |
| A8 | Same rule (A7) applied to companion records that the AD-26 ratification consumes. | **CONTRADICTED** | `reviews/phase-4-architecture-handoff-2026-09-23.md:41` reads "The **independent** 2026-09-23 rubric, technology/reality, and adversarial reviews". `:54` reads "Three **independent** reviewer lenses above returned PASS". Those lenses are tool-persona reviews. The memlog repeats the label at "(event) Independent update-input reconciliation…" (memlog line 128) and in the 2026-09-23 closing event (line 150). | Append a dated memlog correction: these reviews are `tool-persona` evidence, not approval identities. Supersede the handoff (do not rewrite it) with a solo-aware handoff (see A10). |
| A9 | Owner-role records stay per-role. One person may hold every role, and an approval by identity alone is not evidence of independent review (PRD Owner Roles; Input 1 §8 for G5). | PARTIAL | AD-11 requires "a **separate** authenticated release-owner record" and "a **separate** authenticated deployment-owner record" (`:180`). AD-22 requires an "authenticated Consumer owner" receipt (`:255`). These are compatible only if "separate" means separate records, not separate humans. AD-22's "**self-declared** approval … confer[s] no removal authority" can be read as barring the sole owner's receipt. The Assurance Control permits that receipt at `single-maintainer-attested`. | AD-11 paragraph: "one authenticated human may issue a separate record for each role they hold". AD-22: "free-form or self-declared approval" → "free-form, unauthenticated, or registry-unbound approval". |
| A10 | The AD-26 ratification is an owner decision that consumes this control. It must not require a second human or recorded dissent while the registry names one (implied by §3 and §5 success criterion 1). | PARTIAL | The gate-table row (`:473`) says "Architecture and Platform deployment owners, explicitly ratify or replace AD-26". The handoff it points to requires "both deciding owners" (`:21`) and "Security/Release/Test review dispositions **and any dissent**" (`:21`, `:45`). With one human, that wording implies a second identity. | Gate row `:473` and AD-26: the ratification record names the roles held by the one authenticated owner, binds the architecture digest, and is `single-maintainer-attested`, created ≥24 h after the last authored change to that digest. Tool-persona reviews are bound as evidence only. |
| A11 | The decision changes no gate result and grants no `READY`, release, promotion, deployment, migration, or consumer-removal authority (§3 Decision bullet 4). | LANDED | AD-11 (`:180`): "neither later state is authorized". AD-26 production proof: "remain prohibited while any record … is missing". The gate-table preamble (`:468`). | None. |
| A12 | §8 Story 8.11: G5 uses the control. The Story 8.1 normative bytes and digest `de9ba886…` are unchanged. Its §16.4 "independent … evidence" means independent toolchains, not human independence. | LANDED | AD-23 (`:261`) pins normative SHA-256 `de9ba886…72b4e`. AD-23 states no G5 approver roles and has no conflicting wording. | None. Do not edit inside the 8.1 normative markers. |

---

## Input 2: Story 3.15 bounded closure and Epic 9 gate-control ownership (2026-09-26)

| # | Directive (locator) | Status | Spine evidence | Proposed amendment |
| --- | --- | --- | --- | --- |
| B1 | 3.15 is `done` only for FR36-C2 technical evidence validation, and establishes only `evidence-validated`. It closes no G-RUNTIME-PARITY, G-HIGH-RISK, FR36, or SM6 result. All four authority flags stay `false` (§3 Decision ¶1–2). | LANDED (AD-11) | AD-11 (`:180`): "Story 3.15's exact-subject validator and three packet-bound receipts may establish only `evidence-validated`". | None in AD-11. |
| B2 | Same directive, as it appears in the paragraph under AD-1. | PARTIAL | The trailing paragraph under AD-1 (`:103–106`) still says "Epic 3 encodes … Story 3.15 as **positive parity closure**". A reader can take that as closure of the parity gate, which the 2026-09-26 decision explicitly denies. The paragraph also sits inside AD-1, while memlog line 134 calls it a "Design Paradigm rule". | Replace with "Story 3.15 as bounded FR36-C2 deployed-runtime evidence validation (`evidence-validated` only; G-RUNTIME-PARITY stays blocked pending the G-HIGH-RISK Assurance Control)". Consider moving the paragraph to Design Paradigm or AD-11. |
| B3 | Identity caveat. Both owner roles map to `github:jpiquot`, and the Test Architect record is self-attested. The three receipts are not three-party review (§3 Decision ¶3). | LANDED | AD-11 says "three packet-bound receipts", with no multi-party claim. A2 makes the persona status explicit. | Covered by A2. |
| B4 | Reopen triggers: the retained validator exits non-zero; the subject is re-minted; a G-HIGH-RISK evaluation rejects (§3 Decision ¶5). | LANDED | AD-11: "any subject change restarts at `built`"; the validator rejects mismatches. | None. |
| B5 | A story label or local documentation edit cannot change a gate result (§1 table, PRD §11.4). | LANDED | The paragraph under AD-1 (`:105`) says planning or story status never authorizes release. The Implementation-status callout (`:84`) says the same. | None. |
| B6 | Epic 9 owns G-HIGH-RISK (Story 9.2) and the OR28 corrective-work authorization record and validator (Story 9.1) (§2, §4.E). | N/A, not architecture-level | §4.F says governance changes no component, AD, or contract. That holds: no AD names G-HIGH-RISK or OR28. The architecture-level part of G-HIGH-RISK is its Assurance Control vocabulary, which is handled in Input 1. | None, beyond Input 1. |

---

## Input 3a: Split and compact Story 6.5 (EventStore `sprint-change-proposal-2026-09-27.md`)

| # | Directive (locator) | Status | Spine evidence | Proposed amendment |
| --- | --- | --- | --- | --- |
| C1 | One content-bound normative AD-13 artifact (`spec-event-versioning-upcasting.md`) and one final approval gate (Story 6.5) authorize Story 6.6. Splitting the backlog implies no approval, and no runtime work starts from a child review or an unapproved draft (§2, §4.1, §5). | LANDED | AD-13 Rule: "require an approved versioned spec and compatibility vectors before runtime work. An approved spec authorizes the next slice". The gate-table row "Projection cost/sequence guard and upcaster details" says "No runtime implementation before approved specs and vectors". | None. |
| C2 | The exact-content human receipt is the only 6.6 authorization. No assistant may supply it (§5, §7). | LANDED | AD-13 names no approver, so nothing conflicts with the sole owner holding that role. | None. |

---

## Input 3b: McpCli course correction (Platform proposal; applied to EventStore by `4fcb2b5c`)

The EventStore-relevant source items are Platform proposal §2 (EventStore row, "Technical impact"), §4 items 8 and 12, and §5. They are restated in `references/Hexalith.AI.Tools/hexalith-llm-instructions.md` ("Hexalith MCP and CLI ownership") and in the McpCli spine (ecosystem-direction paragraph and AD-21).

| # | Directive | Status | Spine evidence | Proposed amendment |
| --- | --- | --- | --- | --- |
| M1 | `Hexalith.McpCli` is the sole target Hexalith-owned CLI/MCP surface. EventStore creates no new proprietary CLI or MCP (§4 items 9 and 12; AI.Tools baseline). | PARTIAL | Stated only in the bold preamble (`:50`), outside any AD. It has no Binds or Prevents and no AD ID for later ADs to cite. Design Paradigm (`:55`) and the context diagram (`:61`) still list a generic "CLI / MCP" client. | New AD-34 (below). Rename the diagram node "McpCli CLI/MCP". |
| M2 | Admin.Cli and Admin.Mcp are obsolete migration sources. They get safety and continuity fixes only, as explicitly versioned, time-bounded compatibility (§2 Technical impact; AI.Tools "Limit them to safety and continuity fixes"). | PARTIAL | The preamble says "obsolete migration sources". It gives no feature-freeze, safety/continuity-only limit. | AD-34 sentence 1. |
| M3 | EventStore owns the administration semantics and security checks. McpCli owns presentation, catalog, and transport (§4 item 12; item 11 pattern). | MISSING | Nothing in the spine assigns admin authorization, confirmation, or audit ownership when the transport moves to McpCli. Two units could each implement, or each assume the other implements, the role and confirmation checks. | AD-34 sentence 2. |
| M4 | Destructive actions keep their confirmation and role gates and stay unavailable through McpCli until proof passes (§4 item 12). | PARTIAL | The preamble only gates *removal* of the legacy tools. It says nothing about McpCli exposing these operations in the meantime. | AD-34 sentence 2. |
| M5 | Gateway-incompatible admin operations need an approved generic McpCli administration contract **and transport decision**, covering contract versioning, authorization class, audit, and generic registration, with no module-specific McpCli branch (§2 Technical impact; §4 item 10; McpCli spine ecosystem paragraph). | PARTIAL | The preamble names "contract, authorization, and parity evidence". It omits the transport decision and audit, and has no link to AD-29. The Implementation Status and Production Gates table has no row for this open decision. | AD-34 sentence 2. Add gate row: *"McpCli generic admin contract and transport decision \| Admin.Cli/Admin.Mcp remain compatibility; no stream/subscription/cluster/destructive operation via McpCli \| McpCli and EventStore maintainers, before any legacy CLI/MCP removal"*. |
| M6 | Every legacy operation is inventoried, with its owning maintainer and a disposition: replacement or owner-approved withdrawal. An unsupported capability is never represented as migrated (§4 item 13; McpCli AD-21 and ecosystem paragraph). | PARTIAL | The preamble lists only four operation classes. It has no withdrawal path and no "not represented as migrated" rule. | AD-34 sentence 3. |
| M7 | UI-only and human-confirmation operations stay denied to McpCli's agent-capable client. Migration must not weaken that (Platform AD-14, §2 Technical impact). | MISSING | AD-21 and AD-29 do not mention McpCli. AD-29 covers any Admin mutation regardless of transport, but it does not cover the confirmation-denial rule. | AD-34 sentence 2. |
| M8 | Scope: the obsolescence applies to Admin **CLI/MCP** only (§4 item 12). | **CONTRADICTED** (latent) | The preamble's last sentence reads: "Existing admin descriptions below are compatibility and historical implementation requirements, not a permanent separate transport target." Read literally, that makes AD-21 (consolidated Admin UI), AD-29 (admin mutation attribution), and AD-3 (Admin read adapters) "historical". The source proposal retires only the CLI/MCP transports. | Delete the preamble once AD-34 lands. AD-34 says "AD-21 and the Admin Server are unaffected". |
| M9 | Structural Seed comment for `Admin.*` (`4fcb2b5c`). | LANDED | `:403`: "consolidated UI; CLI/MCP are obsolete compatibility pending Hexalith.McpCli admin migration". | Optionally append "(AD-34)". |
| M10 | Provenance. | MISSING | The spine frontmatter `sources:` lists nothing after 2026-09-23. Neither the Platform proposal nor any of the four inputs is listed. The memlog miscites the McpCli source (P1). | Add all four EventStore input paths and `references/Hexalith.Platform/_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-27.md` to `sources`. Correct the memlog citation. |

### Recommendation: the McpCli preamble becomes **new AD-34**

**Why a new AD rather than an amendment:**

- **AD-21 does not fit.** It is UI-only ("The Existing Admin UI Is The Consolidated EventStore UI"), so adding CLI/MCP would falsify its title.
- **AD-29 does not fit.** It fixes attribution, not which surface an operation goes through.
- **AD-3 does not fit.** It governs gateway policy, while the open question is the *non-gateway* admin path.
- **The decision is new.** It is a cross-repository surface-ownership decision with its own Prevents, so the memlog's stable-ID rule ("add only new stable IDs", memlog line 106) points to a new number.

**Placement.** Put AD-34 under the theme "Platform core and module boundaries", making that row "AD-1 through AD-4, AD-34".

**Proposed AD-34 text:**

> ### AD-34 - Hexalith.McpCli Is The Target CLI/MCP Surface; EventStore Keeps Admin Semantics [ADOPTED]
>
> - **Binds:** FR26 (FR26-C4), FR34-FR35, NFR1-NFR4, NFR15-NFR17
> - **Prevents:** a second permanent EventStore CLI/MCP transport, and McpCli re-implementing, weakening, or bypassing EventStore admin authorization, confirmation, and audit.
> - **Rule (3 sentences):** `Hexalith.McpCli` is the sole target Hexalith-owned CLI/MCP surface for EventStore. `Hexalith.EventStore.Admin.Cli` and `.Admin.Mcp` are obsolete migration sources, limited to safety and continuity fixes, and no new EventStore CLI or MCP surface is created. EventStore keeps the administration semantics and the server-side checks: the AD-3 policy edge, AD-10/AD-27 authorization, AD-29 attribution and audit, and destructive-operation confirmation and role gates. McpCli therefore reaches gateway-ready operations only through decorated Contracts and the EventStore gateway. It reaches no stream, subscription, cluster, destructive, UI-only, or confirmation-required operation until an approved generic McpCli administration contract and transport decision preserves those checks. Each legacy operation is removed only after the owner-approved McpCli inventory records its replacement or withdrawal, and positive and negative parity, authorization, and denial evidence passes. An unsupported operation is never reported as migrated, and AD-21 and the Admin Server are unaffected.

**Collateral edits:**

1. Delete the floating preamble (`:50`).
2. Design Paradigm (`:55`): change "CLI, and MCP" to "the Hexalith.McpCli CLI/MCP heads (AD-34)".
3. Diagram node (`:61`): change "CLI / MCP" to "McpCli CLI/MCP".
4. Capability map, row FR26/FR28/FR32: add AD-34.
5. Consistency Conventions, "UI and Admin" row: add AD-34.
6. Add the gate row from M5.

---

## Input 4: Story 6.5d split (2026-09-30)

| # | Directive (locator) | Status | Spine evidence | Proposed amendment |
| --- | --- | --- | --- | --- |
| D1 | AD-13 is unchanged. 6.5d is specification-only. Integration splices and cites, and adds no new record, codec, state, or exit. The receipt stays `UNAPPROVED` (§2 Architecture row, §3 D-SPLIT). | LANDED | AD-13 Rule. The integration-adds-nothing rule is story process, not architecture. | None. |
| D2 | NFR7, no silent loss: every hold and wait state needs an exit, and dropping holds would reintroduce silent loss (§3 MVP-reduction rejection; §4.3 ACs). | LANDED | AD-8 Delivery failure: "silent drop is not the default". AD-31: unretained captures "must not be acknowledged as successful". | None. |
| D3 | D-RESUME re-arms the same committed events under the same MessageId, without re-executing the command (§4.3 ACs). | LANDED, with a watch item | AD-6: "CloudEvent `id` is the persisted event `MessageId`. Duplicate replies preserve the original result." AD-5 allows a recovery effect only under the current fence. | No spine change now, because AD-13 gates runtime work. **Watch at Story 6.5 integration:** the 6.5d candidate's "execution-owner CAS recovery fence" (`spec-6-5d-…md:66`) must be shown to be, or be subordinate to, the AD-25 current fence. Its Admin resume and redrive paths must satisfy AD-29, and Operations-owned holds (`:116`, operations-epoch lease `:127`) must sit within AD-31's non-production gating. |
| D4 | The sole owner approves the exact AD-13 bytes through the six-field receipt (§5 "Owner (sole maintainer)"). | LANDED | Consistent with A9 and the solo-maintainer context. AD-13 states no approver. | None. |

---

## Solo-maintainer conflict check (AD-11, AD-22, AD-26, and companions)

**Spine ADs.** No AD requires an "authenticated second identity", a "second human", "non-authorship", or "independent approval". Several wordings remain collision or ambiguity risks:

- **AD-11:**
  - `:178` uses "independent lifecycle", which is the reserved label used in a different sense (A7).
  - `:180` uses "separate authenticated release-owner/deployment-owner record". It is compatible only as per-role records, so clarify that one human may issue each one (A9).
- **AD-22:** `:255` says "self-declared approval … confer[s] no removal authority", which can be read as barring the sole owner's receipt (A9). Its receipt bind list also lacks an assurance level (A5).
- **AD-26:**
  - `:298` uses "no independent … receipt" (A7).
  - `:296` uses "independent DAPR sidecars", a technical sense (low).
  - Gate row `:473` routes ratification to "Architecture and Platform deployment owners", which is fine as roles. But the handoff it consumes requires "both deciding owners" and recorded "dissent" (A10).

**Companion records (CONTRADICTED, A8).** These records label tool-persona reviews `independent`:

- `reviews/phase-4-architecture-handoff-2026-09-23.md:41`
- `reviews/phase-4-architecture-handoff-2026-09-23.md:54`
- memlog line 128 and the 2026-09-23 closing event (line 150)

**Fix.** Append a memlog correction and issue a superseding handoff. Do not rewrite these historical records.

## Consolidated amendments (proposed wording)

1. **AD-11, new paragraph "Assurance level."** "Every gate result, authority record, and receipt under AD-11, AD-22, and AD-26 binds its required and achieved PRD Assurance Control level. The level is `single-maintainer-attested` while the owner-role registry names exactly one distinct human, and `independent` automatically once it names two or more. Tool personas and CI identities never count as humans, and one human may issue a separate record for each role they hold. A downstream record (`READY`, `release-available`, `production-promoted`, consumer removal) carries the lowest level of its inputs, and single-maintainer work is never labelled `independent`. Validators reject: an overstated or below-required label; an owner attestation created less than 24 hours after the last authored change to its subject; a validator result not retrieved from a sealed AD-12 CI run."
2. **AD-12, append.** "A gate-validator result that feeds an approval is sealed: it is retrieved from the CI platform for a required, blocking run on the exact head SHA and workflow-file digest under its authenticated CI identity, never from an author-supplied file."
3. **AD-22.** Add "required and achieved assurance level (AD-11)" to the receipt bind list. Change "free-form or self-declared approval" to "free-form, unauthenticated, or registry-unbound approval".
4. **AD-26 production proof and gate row `:473`.** "Ratification and each `production-promoted` record bind the AD-11 assurance level. While the registry names one human, one authenticated owner holding both roles ratifies by a `single-maintainer-attested` record bound to the architecture digest, created ≥24 h after its last authored change. Tool-persona reviews are bound as evidence only."
5. **Vocabulary.** Reword AD-11 `:178` "independent lifecycle" → "separate publication-lifecycle". Reword AD-26 `:298` "no independent" → "no separate". Optionally reword AD-26 `:296` → "per-application DAPR sidecars".
6. **The 3.15 paragraph under AD-1 (`:103–104`).** Apply the B2 replacement.
7. **AD-34** as above. Delete the preamble.
8. **Frontmatter `sources`.** Add the four inputs and the Platform McpCli proposal. **Memlog.** Fix the McpCli citation (P1) and add the A8 correction.

## Out-of-scope observations (not counted)

- **PRD G-RUNTIME-PARITY status.** It still reads "TECHNICAL PASS; INDEPENDENT GATE BLOCKED". That string is a guarded Contracts.Tests substring, deliberately kept by Input 2 §3 Risks and §4.C. It is PRD text, not spine text. It now names a level the solo project cannot reach, so a future PRD correct-course may want to retarget it to "ASSURANCE GATE BLOCKED" together with its guard.
- **Inputs not reconciled here.** The 2026-10-05 and 2026-10-07 proposals, and the Story 5.5 owner decision, are update inputs outside this review's scope. Their effects on AD-1, AD-13, AD-23, and AD-26 are recorded in the concurrent memlog entries.
