---
review: reconcile-closure
run: architecture spine update 2026-10-07
reviewer-label: tool-persona
reviewer-mode: read-only (only this report written; no git mutation, build, or test)
created: 2026-10-07
---

# Reconcile-Closure Review: 2026-10-07 Spine Update vs Its Five Input-Reconciliation Reports

> **Assurance label.** This review is `tool-persona` evidence under the PRD Assurance Control. It is not an
> approval and is not `independent`. The owner-role registry names one human.

## Anchors

| Item | Value |
| --- | --- |
| Review subject | `_bmad-output/planning-artifacts/architecture.md`, SHA-256 `e15b079f475c03603ac895213bfdd8ce0f68d0fdb90985d23e38d7874ac3f6cf` (553 lines, `status: draft`, `updated: 2026-10-07`) |
| Pre-update baseline | session scratchpad `architecture.pre-update-2026-10-07.md`, SHA-256 `ca7898c7d0bd22d9f5b0260f6986b49bb4a0153bc1fae6d6fe0149f81d900cab` (488 lines) |
| Memlog | `.memlog.md` lines 151-172 (2026-10-07 entries; diff is append-only plus the `updated:` header) |
| Inputs reconciled | `reconcile-update-2026-10-07-proposals-0926-0930.md` (P), `-dapr-boundary.md` (D), `-correct-course.md` (C), `-story-5-5.md` (S), `-stack-reality.md` (K) |
| Numbering map | P "AD-34" = spine **AD-35** (McpCli); D "AD-34" = spine **AD-34** (Dapr-qualified); S "AD-34" = spine **AD-36** (workload assertions) |

Line numbers `:NNN` below refer to the subject bytes above.

## Verdict

**LANDED WITH DRIFT — small corrective edits recommended; no high-severity drop.** Every high finding in the
five reports (D H1-H4, C H1-H5, S C1-C4, P A8/M8) is landed or explicitly deferred. Six items drifted
(two medium), two source-list additions were dropped, and three baseline fail-closed clauses were weakened in
the restructure (each still inherited by reference to PRD §8.4, the Dapr-only amendment, or the AI.Tools
baseline, so no effective authority was granted).

| Disposition | Count |
| --- | ---: |
| LANDED | 127 |
| DEFERRED-OK | 13 |
| DRIFTED | 6 |
| DROPPED | 2 |
| Out of spine scope (routing not recorded) | 4 |

## DRIFTED and DROPPED (action list)

| ID | Report locator | Severity | What happened | Proposed fix (one sentence) |
| --- | --- | --- | --- | --- |
| DR-1 | P A3 / P Consolidated 2; C D8 | Medium | AD-12 `:204` puts the whole seal definition inside an inline `[ASSUMPTION]` and omits the PRD-approved "retrieved from the CI platform … under the platform's authenticated CI identity" (PRD glossary `:200`). Only the "which run supplies it" part is an inference; the definition itself is approved. The memlog `:169` mirrors the omission. | Move the definition out of the assumption: "A sealed result is retrieved from the CI platform for a required, blocking run on the exact head SHA and workflow-file digest under the platform's authenticated CI identity, never from an author-supplied file", and keep only "`[ASSUMPTION]` that run is the guarded transition's run" as the inferred clause. |
| DR-2 | D C6 (H4), D proposed AD-13 bullet sentence 1; D 3.4 | Medium | The AD-13 Dapr-only bullet `:211` dropped "Story 6.6 has no AD-34 exception path without a separate owner decision". It also does not restate the amendment's absolute ban (no application SQL, Npgsql, PostgreSQL schema, or provider fork, `story-6-6-dapr-only-amendment.md:23-24`). Baseline AD-1 said "Application code does not access PostgreSQL". New AD-1 `:114` makes the ban conditional (Dapr-supported operation plus exception path), so a 6.6 PostgreSQL exception now reads as available through AD-34. | Append to the AD-13 bullet: "No application SQL, Npgsql connection, PostgreSQL schema, or provider fork is part of Story 6.6, and Story 6.6 has no AD-34 exception path without a separate owner decision." |
| DR-3 | P A10 / P Consolidated 4 | Low-medium | AD-26 Ratification `:328` says "one content-bound ratification … per role" without "authenticated". The proposal had "one authenticated owner", and memlog `:162` says "one authenticated owner record per role". AD-11's authentication rule binds AD-11 authority records, but the ratification is not one of them. | "…records one authenticated, content-bound ratification or approved replacement per role…" |
| DR-4 | D 2.7 (PRD §8.4 `prd.md:477`) | Low | AD-34 `:380` substitutes "never substitute for Dapr-path evidence" for the PRD's "Neither category may expose a bypass to application code". It also omits "deployment/backup administration" and "local pure computation". | Append to AD-34: "…and none of them exposes a bypass to application code." |
| DR-5 | K Gap 5 | Low | The OpenBao server floor `2.7.1` / `2.6.4` appears only in the Stack posture cell `:430` ("the AD-26 production profile … must bind"). The AD-26 production-proof digest list `:330` does not bind it, and the memlog records it only as a `(version)` fact. A new binding requirement now lives in a non-normative table. | Add "OpenBao server version floor" to the AD-26 production-proof digest list, or to the OpenBao HA gate row `:542`, and record it as a memlog decision. |
| DR-6 | C (c) rows `:474`/`:481`/`:483`/new tenant row/`:472`; D proposed gate rows (3.17, 8.6) | Low | The gate-row owner and trigger cells were trimmed against the proposals. They drop the Story 9.1 authorization-record prerequisite (5.11, 4.16, 6.7), "Story 5.10 keeps the reserved-`system` guard", "Story 7.1 production acceptance" (broker), and the "with Architecture" and "with Architecture and Security" co-owners (3.17, 8.6). C §(a) itself classed 9.1 as not architecture-level, so this is low. | Add one sentence to the gate-table preamble `:520`: "Each backlog gate story also requires its Story 9.1 corrective-work record (PRD §0)", and restore the Architecture/Security co-owners in the 3.17 and 8.6 rows. |
| DP-1 | D Ancillary frontmatter | Low | DROPPED: the "Dapr crypto, SignalR binding and state-matrix documentation URLs cited in SCP §1" were not added to `sources`. | Add the three URLs from `sprint-change-proposal-2026-10-05.md` §1. |
| DP-2 | K "Suggested source-list additions" | Low | DROPPED: 2 of 3 suggested URLs were not added: `dapr/dapr` v1.18.2 (the CVE fixes cited in `:427` and `:548`) and `fluentui-blazor` v5.0.0 (the GA claim in `:432`). `setup-postgresql-v1` was added. | Add both URLs to `sources`. |

## (1) Fail-closed clause audit (baseline → subject)

| Baseline clause | Subject location | Result |
| --- | --- | --- |
| AD-1: Dapr required boundary; "Apply PRD §8.4 to all … packages and hosts" | AD-1 `:114` (extended to samples, linked source, generated-host inputs) | Preserved, strengthened |
| AD-1: unavailable operation needs "evidence-backed, owned, exact-path" exception | AD-1 `:114` plus the AD-34 register fields `:380` | Preserved, strengthened (decide-before-introduce) |
| AD-1: unknown suitability unresolved; generic SQL binding is not portability and cannot bypass actor ownership | AD-1 `:114` | Preserved |
| AD-1: "select the highest applicable Dapr abstraction **and qualify its actual correctness, security, compatibility, and operational guarantees**" | AD-1 keeps "highest applicable"; AD-34 `:380` says only "its required guarantees" | **Weakened (LC-1).** The four qualification dimensions (PRD `:473`) are no longer named in the spine. They are inherited only through "under PRD §8.4". Fix: AD-34 "…with its required correctness, security, compatibility, and operational guarantees…". |
| AD-1: actor-owned mutations solely `IActorStateManager`; no private actor keys/tables/caches; domains get no persistence authority | AD-1 `:114` | Preserved |
| AD-1: "Other coordinator state may use Dapr state/actor APIs after capability qualification" | AD-34 (every operation qualified); AD-13 `:211` (6.6 control record) | Preserved |
| AD-1: "Application code does not access PostgreSQL" | AD-1 conditional rule; AD-13 by reference | **Weakened (LC-2 = DR-2).** |
| AD-1 trailing Epic 3 paragraph: planning/story status never authorizes release, deployment, consumer removal, or `v3.94.1` closure | AD-11 Epic 3 lineage `:184` | Preserved; 3.15 corrected to bounded `evidence-validated`. The B2 qualifier "pending the G-HIGH-RISK Assurance Control" was omitted, but "G-RUNTIME-PARITY stays blocked" remains. |
| AD-13 loader bullet: immutable declared artifact/pin admission; detection with capability loss; no activation or confinement | AD-13 `:212` | Preserved, strengthened (pins, before callbacks, undeclared loads, in-flight refusal) |
| AD-26 deleted ¶1 (metadata handoff): historical evidence only; no SQL authority; Dapr-only governs control records; ETag/transaction qualified at the API; no physical-byte receipt; proof gate stays | AD-13 `:211` (no authority "to any story"; logical readback only); AD-34 `:380`; AD-26 Production proof `:330` unchanged | Preserved, strengthened |
| AD-26 deleted ¶2 (qualification): per-profile guarantees/versions/evidence; per-component ETag/TTL/ordering/failure; no shared-backend transactions; requalify on backend change; 3.17/2.13/8.6 ownership; "Retained direct Redis is not an accepted exception"; "AD-26 ratification and production gates still fail closed" | AD-34 `:380-382`; gate rows `:536-538` | Preserved, all seven clauses |
| McpCli preamble: target surface; Admin.Cli/Mcp obsolete "**including for infrastructure administration**"; destructive/stream/subscription/cluster ops need generic contract, authorization, and parity before removal | AD-35 `:388`; gate row `:550` | **Mostly preserved (LC-3).** The "including for infrastructure administration" scope qualifier was dropped. Fix: append it after "obsolete migration sources". The removal precondition now allows owner-approved withdrawal; this follows the approved source (AI.Tools baseline, Platform §4 item 13), so it is not a loss. The demoting last sentence was deleted by design (P M8). |
| AD-5: "Physical provider write-once enforcement remains an unsatisfied NFR7 gate; the current fence is not a substitute" | AD-5 Append race `:140`; gate row `:544` | Preserved under the envelope-first owner decision. The class stays unmet, and neither the fence, Dapr routing, nor risk acceptance substitutes. |
| AD-8: dedup, scoped guards, gaps do not advance, metadata-only SignalR, user success needs read-model evidence, Delivery-failure paragraph | AD-8 `:158-162` | Preserved. The bound was re-scoped to PRD NFR5 (`prd.md:364`: detail metadata, 2048 total UTF-8 bytes), which aligns with the PRD. The blanket at-least-once clause was split per owner decision. |
| AD-28: token validation, constant-time, missing config fails readiness, token alone is not caller identity, mTLS/ACL/app-ID never mint admin/tenant claims, AD-24 rotation | AD-28 `:342` | Preserved. Caller attribution became deny-only (owner decision); catalog/ACL authorization is retained for service invocation; channel-only routes confer no authority. |

No baseline digest changed: the five pinned digests (`de9ba886…`, `542f0b6e…`, `0f841d5a…`, `1a55b030…`,
`a9cfea91…`) are identical.

## (2) Memlog 2026-10-07 entries vs spine

| # | Memlog | Spine | Finding |
| --- | --- | --- | --- |
| MM-1 | `:162` "Ratification is one **authenticated** owner record per role" | AD-26 `:328` "one content-bound ratification … per role" | Mismatch (DR-3) |
| MM-2 | `:155` "application code never accesses PostgreSQL"; `:159` "Prevents units reading the 6.6-scoped PostgreSQL ban as story-local" | No literal PostgreSQL ban; AD-1 is conditional on Dapr support plus an exception path; AD-13 relies on the amendment by reference | Mismatch (DR-2). The memlog claims a stronger spine than exists. |
| MM-3 | `:160` "provider credentials never reach application processes" (unconditional) | AD-34 `:380` "…for an operation Dapr supplies" | Memlog overstates. Either is defensible; align them. |
| MM-4 | `:169` seal `[ASSUMPTION]` without CI identity | AD-12 `:204` same | Consistent with each other, but both drift from the PRD (DR-1). |
| MM-5 | `:172` open questions (2) stale `304` under `Direct`, and (4) channel-only routes indistinguishable from peer invocation | No spine trace (AD-28 only asserts channel-only routes confer no authority) | Not a contradiction. The spine is silent, and downstream readers never see the memlog. Suggest adding both to the safe-posture cell of the AD-36 gate row `:528`. |
| MM-6 | No entry | Spine `:101` inline-`[ASSUMPTION]` convention and the "four inline clauses" count `:524`; 8 new or retitled gate rows; OpenBao floor `:430`; Structural Seed Gateway SignalR note `:463`; vocabulary rewording (`independent lifecycle` → `separate publication-lifecycle`, `independent DAPR sidecars` → `per-application`); diagram relabels | Unrecorded spine changes. Only the OpenBao floor is a new binding (DR-5); the rest are derived. A single `(decision)` entry for the gate-table restructure and the `[ASSUMPTION]` convention would close it. |
| MM-7 | `:153` McpCli removal needs "generic … contract, authorization and parity evidence before removal" | AD-35 `:388` allows replacement or withdrawal | Superseded by `:167`, but `:167` does not say it supersedes `:153`'s removal precondition. Minor. |

All other 2026-10-07 entries (`:151-152`, `:154`, `:156-161`, `:163-168`, `:170-171`) match the spine. The
inline `[ASSUMPTION]` count is consistent: AD-11 `:182`, AD-12 `:204`, AD-17 `:238`, AD-27 `:336`, which is
four, as `:524` states.

## (3) Quiet requirements

| Requirement | Holds? | Evidence |
| --- | --- | --- |
| Never label solo work `independent`; never name a second reviewer | Yes | `independent` occurs only as the reserved label: AD-11 `:196` (two-or-more rule) and Conventions `:404` (never used). No "second human", "second reviewer", or "dissent". "Reviewer closure" `:524` is PRD §11.3 vocabulary. |
| Story 5.5 literals not bound while in review | Yes | No header name, `azp`, `eventstore:*` claim, 300/900 s, Keycloak scope, EventId, `AllowedPublishers`, verifier, or ROPC text (the only "Keycloak" hit is the Stack Aspire row). AD-36 `:396` explicitly defers literals. Residual: the in-review spec is listed in `sources` `:25` unpinned; that is acceptable because AD-36 scopes it. |
| Draft payload amendment stays non-authorizing | Yes | AD-23 `:287` wording unchanged; gate row `:538` "stays draft and non-authorizing"; Structural Seed `:463` now cites PRD §8.4/AD-34 rather than the draft; the draft is absent from `sources`. |
| No digest refresh except via Story 9.3 | Yes | `epics.md:15` and `prd.md:646` still carry `7e3dbc7b…`; the worktree diffs touch no digest; spine pins unchanged; gate row `:524` routes refresh to Story 9.3 and prohibits hash-only refresh. |
| History not rewritten | Yes | Memlog diff is append-only (`@@ -150,0 +151,22`) plus the header. The 2026-09-23 handoff is not edited; AD-26 `:328` declares it unusable instead. |
| No readiness, approval, or production authority implied | Yes | `status: draft`; AD-26 stays `[ASSUMPTION]`; AD-34 `:382` "changes no readiness verdict"; AD-36 `:396` "not evidence that any host has adopted it". |
| 8.1 normative markers untouched | N/A | Not in the spine. AD-23's `de9ba886…` pin is unchanged. |
| AD-ID collision outside the spine | None | No `AD-34`/`AD-35`/`AD-36` reference in `epics.md`, `prd.md`, top-level implementation artifacts, or `docs/`. |

## Per-report reconciliation

### P: Proposals 2026-09-26 to 2026-09-30 (20 counted items)

| ID | Was | Now | Evidence |
| --- | --- | --- | --- |
| A1 | PARTIAL | LANDED | AD-11 Assurance level `:196` |
| A2 | MISSING | LANDED | AD-11 `:196`; Conventions `:404`; AD-26 `:328` |
| A3 | MISSING | **DRIFTED** | DR-1 |
| A4 | MISSING | LANDED | AD-11 `:196` (24 h rejection) |
| A5 | MISSING | LANDED | AD-11 `:196` (lowest level); AD-22 `:281` binds the level |
| A6 | PARTIAL | LANDED | AD-11 `:196` rejection list |
| A7 | PARTIAL | LANDED | `:192` "separate publication-lifecycle"; AD-26 `:326` "per-application"; the AD-26 "no independent" paragraph was removed |
| A8 | CONTRADICTED | LANDED (spine scope) | Memlog correction `:157`. Re-issuing the solo-aware packet is an owner act: AD-26 `:328`, gate row `:526`. |
| A9 | PARTIAL | LANDED | AD-11 `:194` role separation; AD-22 `:281` "free-form, unauthenticated, or registry-unbound" |
| A10 | PARTIAL | **DRIFTED** | DR-3 |
| B2 | PARTIAL | LANDED | AD-11 Epic 3 lineage `:184` (G-HIGH-RISK qualifier omitted) |
| M1 | PARTIAL | LANDED | AD-35; Design Paradigm `:66`; diagram `:72` |
| M2 | PARTIAL | LANDED | AD-35 "limited to safety and continuity fixes" |
| M3 | MISSING | LANDED | AD-35 server-side checks |
| M4 | PARTIAL | LANDED | AD-35 "reaches no … destructive …" |
| M5 | PARTIAL | LANDED | AD-35 contract and transport decision; gate row `:550` |
| M6 | PARTIAL | LANDED | AD-35 inventory, replacement or withdrawal, never reported as migrated ("owning maintainer" not named, as in the proposal) |
| M7 | MISSING | LANDED | AD-35 UI-only and confirmation-required |
| M8 | CONTRADICTED | LANDED | Preamble deleted; "AD-21 and the Admin Server are unaffected" |
| M10 | MISSING | LANDED | `sources` `:15-26` incl. Platform path `:20`; memlog citation fix `:157` |

Also landed: M9 seed comment `:455` "(AD-35)"; the collateral theme row, capability map, and Conventions
"UI and Admin" `:412`.

### D: Dapr boundary (58 counted items, plus 2 out of scope)

| IDs | Now | Evidence |
| --- | --- | --- |
| 1.5, 1.7, 1.8 (L1/L2), 1.9 (M1), 1.10 (M2), 1.11, 1.12 (H3), 1.15 (L6), 1.18 (M4), 1.19 (L7) | LANDED | AD-1 `:114` pointer; AD-34 `:380-382`; diagram `:75`, `:90`, `:93`; gate row `:552`; AD-23 `:287`; AD-12 `:202`; AD-5 `:140` |
| 2.2, 2.3, 2.4, 2.6 (H1), 2.8, 2.10, 2.11 (M6), 2.12, 2.14, 2.16 (L8) | LANDED | AD-1 `:114`; AD-34; gate rows `:536-538`; register path in AD-34 and `sources` `:26`; capability map `:510-516`; Seed `:463` |
| 2.7 | **DRIFTED** | DR-4 |
| 3.2, 3.3, 3.6, 3.7, 3.9, 3.10, 3.11, 3.12 (M3), 3.13 | LANDED | AD-13 `:211`; AD-34; AD-26 `:326`; AD-12 `:202` |
| 4.2-4.8 (incl. M8 4.3/4.4) | LANDED | AD-13 loader bullet `:212` (verbatim proposal) |
| 5.3, 5.4 (L3) | LANDED | AD-1 no-fallback; AD-23 Crypto Scheme v1; Seed `:463` cites PRD §8.4/AD-34 |
| 6.1 (H4), 6.3, 6.4, 6.6 (H1) | LANDED | AD-13 "to any story"; AD-34 readback and cancellation clause; AD-1 no-fallback |
| C1 (H3), C2 (M5), C3 (L1), C4 (M4), C5 (L2), C7 (H2), C8 (H1) | LANDED | AD-26 `:326`; AD-7 `:152` (verbatim); diagram `:90`/`:75`; AD-12; AD-34 `[ADOPTED]` |
| C1b (M7) | DEFERRED-OK | Gate row `:537` "must also settle AD-8/AD-31 poison handling"; memlog Q5 |
| C6 (H4) | **DRIFTED** | DR-2 |
| L4, L5 | LANDED | AD-13 `:212` names the spec; Epic 3 moved to AD-11 |
| Proposed AD-26 edits (delete ¶¶, Control state, digest list) | LANDED | AD-26 `:326`, `:330` |
| Proposed gate rows (3) | LANDED (owner trims under DR-6) | `:536-538` |
| Ancillary: theme, Conventions "Infrastructure access", capability map, `updated`, four `sources` | LANDED | `:105`, `:403`, `:510-516`, `:10`, `:17`, `:23-26` |
| Doc URLs (SCP §1) | **DROPPED** | DP-1 |
| Downstream references (epics 2.13/3.17/4.16/8.6, `docs/concepts/dapr-infrastructure-boundary.md`, 3.17 spec); L9 PRD glossary | Out of scope | The report itself marks these "routed, not edited here", but no memlog entry records the routing |

"Not landed by design" holds: no spec-level values (30 s, 128 MiB, 64 MiB, 65,536) and no draft-only
amendment content in the spine.

### C: Correct-course 2026-10-07 (37 counted items)

| IDs | Now | Evidence |
| --- | --- | --- |
| D3 reviewer closure | DEFERRED-OK | Gate row `:524` "after reviewer closure"; memlog correction `:157`. This review is part of that closure. |
| D4 AD-26 ratification | DEFERRED-OK (owner act) | AD-26 Ratification `:328`; gate row `:526` |
| D5 status final | DEFERRED-OK | `status: draft`; gate row `:524` |
| D6 (H2), D7 | LANDED | AD-5 `:140`; gate row `:544`; AD-26 `:330` envelope binding |
| D8 (H1) | LANDED; PRD conflict DEFERRED-OK | AD-12 `:204`; PM routing in-text and memlog Q6 (the seal drift is DR-1) |
| D9, D10, D11, D12, D15, D17, D19, D20, D18 owner cell, Adjacent AD-10 | LANDED | AD-25 `:318`; AD-11 `:194`; Conventions `:404`; `sources`; Design Paradigm `:68`; AD-8 `:158`; AD-30 `:354`; AD-11 `:184`; gate row `:546`; AD-10 `:176` |
| D13 (H3) | LANDED; carrier `[ASSUMPTION]` | AD-17 `:238`; Conventions Identity `:402` |
| D14 | DEFERRED-OK | AD-11 `:182` inline `[ASSUMPTION]`; NFR12 added to Binds |
| D16 | LANDED; namespace clause `[ASSUMPTION]` | AD-27 `:334-336` (FR12/FR15 added) |
| (c) cell updates: 10 existing rows + 7 new rows | LANDED (trims under DR-6) | `:524-552` |
| (d) readiness findings | DEFERRED-OK | Gate row `:524`; artifact gaps carried by rows `:526`, `:531`, `:546` |
| Residual PM notes (NFR18 stale text, PRD §11.3 digest, G-RUNTIME-PARITY wording) | Out of scope | Only the Assurance conflict is recorded (memlog Q6) |

H4 and H5 are landed: AD-26 `:328` declares the 2026-09-23 packet unusable. AD-26 `:330` and rows `:525`,
`:539`, `:540` record the missing owning stories for the broker, catalog, pin, and restore posture.

### S: Story 5.5 (25 counted items)

| IDs | Now | Evidence |
| --- | --- | --- |
| D1, D1a, D1b, D1c, D2, D3, D4, D6, D7, D9, D10 | LANDED | AD-36 `:394-396`; AD-28 `:342`; AD-10 `:176`; AD-8 `:160`; diagram `:82`; AD-18 `:254-257`; AD-24 `:295`; AD-33 `:372` |
| D5 | LANDED; OQ-5 DEFERRED-OK | AD-36 covers domain-service endpoints; AD-28 channel-only routes; memlog Q4 |
| AD-27, AD-31, AD-33 cross-references | LANDED | `:336`, `:360`, `:372` |
| Diagram, AD-36 gate row, JWT row extension | LANDED | `:78`, `:80`, `:82`; `:528`; `:527` |
| OQ-1, OQ-3 | DEFERRED-OK | AD-36 `:396`; gate row `:537`; memlog Q1 |
| OQ-2 | DEFERRED-OK (memlog only) | Memlog Q2; see MM-5 |
| OQ-4 | DEFERRED-OK | AD-36 binds a contract-owned lifetime ceiling, no number |
| OQ-6 | DEFERRED-OK | Gate row `:529` "No owner yet; route to correct-course" |
| OQ-7 to OQ-12 | DEFERRED-OK | Gate row `:528` "reconcile AD-36 … when Story 5.5 exits review". OQ-12's three NFR12-breaking changes are not routed to the 3.18 row `:532`. |

The AD-10 "and AD-34" suffix was not appended to "AD-28 remains a distinct authentication scheme". That is
correct: AD-36 assertions are validated through the AD-10 contract, so calling AD-36 a distinct scheme would
contradict the AD-10 sentence. Counted as LANDED.

### K: Stack reality (7 counted items)

| IDs | Now | Evidence |
| --- | --- | --- |
| Gap 1 (stale rows), Gap 2 (daprd 1.18.0 / `latest`), Gap 3 (CLI lag), Gap 4 (FrontComposer/Fluent), proposed rows | LANDED | Stack `:418-435`; gate row `:548`. Minor omissions: the `Hexalith.Folders.Aspire` 13.0.0 override, and "no committed component" on the OpenBao row. |
| Gap 5 OpenBao floor | **DRIFTED** | DR-5 |
| Source URLs | **DROPPED** (2 of 3) | DP-2 |
| Gap 6 secondary docs | Out of scope | — |

## Tally method

Counted items are every PARTIAL / MISSING / CONTRADICTED / Mixed row and every distinct proposed amendment
or gate cell. An amendment block that only implements an already-counted row is not counted twice. Totals:
P 18 LANDED / 2 DRIFTED; D 54 LANDED / 1 DEFERRED-OK / 2 DRIFTED / 1 DROPPED; C 32 LANDED / 5 DEFERRED-OK;
S 18 LANDED / 7 DEFERRED-OK; K 5 LANDED / 1 DRIFTED / 1 DROPPED. The cross-cutting DR-6 counts once. The
three lost-clause findings LC-1 to LC-3 come from the baseline audit and are not in the tally.
