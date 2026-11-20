# Downstream acceptance and traceability review

**Verdict: Major revisions required.** Three high and two medium findings remain. The revised inventories agree on all 56 requirement definitions. Two acceptance passages still conflict with their governing security contracts, and the clause ledger assigns authority differently from the stories that must supply the evidence.

Reviewed the working PRD at SHA-256 `b0aed43df45d9f82a8cf2e93760bff6edb3ebf9a437c45e3daebea779c532299` and epics at `1010d536bfa52d3d53ee33fee0b35e72f533b942b0ffc967076fc7bd976982db`, against EventStore `607072c68`. Line numbers refer to those files.

## High findings

### 1. The §6.5 acceptance criterion allows a Production authentication override

**Evidence.** PRD line 337 says “production auth rejects insecure modes unless explicitly break-glassed.” The glossary at line 205 identifies the sole break-glass option as `AllowInsecureSymmetricKey`. NFR3 at line 399 requires rejection in Production even with that option enabled. The shared JWT contract implements the unconditional Production rejection. The Server tests verify both Production refusal with the override and Staging acceptance with it. [PRD §6.5 and NFR3](../../../prd.md) · [JWT contract, line 132](../../../../../src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs) · [Environment-specific tests, lines 159–191](../../../../../tests/Hexalith.EventStore.Server.Tests/Authentication/EventStoreAuthenticationOptionsTests.cs)

**Consequence and correction.** A security reviewer using §6.5 alone could accept a Production configuration the product forbids. Require a Production rejection with the flag both enabled and disabled. Keep the separate non-Development, non-Production override case in NFR3. This is a correction to the acceptance text.

### 2. The deferred-route contract can disclose capability before authorization

**Evidence.** FR34-C4 at PRD line 454 and §8.3 at line 506 say every retained endpoint returns HTTP 501. Section 6.7 and NFR15 at lines 362 and 411 also combine UI dispositions and server status. Story 7.4 requires authentication, tenant authorization and bounded request validation before a 501 response. Its negative cases explicitly forbid turning a denial into 501. The final UX requires hidden restore/import actions and read-only backup/compaction history. [PRD §§6.7, 7.1 and 8.3](../../../prd.md) · [Story 7.4, lines 5813–5842](../../../epics.md) · [UX dispositions, lines 63–86](../../../ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md)

**Consequence and correction.** A blanket 501 assertion can expose the existence of a deferred capability to an unauthorized caller. A disabled file picker would also satisfy the PRD summary while violating the intended restore/import experience. Specify the caller outcomes in order. Authentication, scope and validation failures retain their ordinary safe responses. An authorized, valid request to a retained deferred route receives the typed 501 response with no downstream call. Name restore/import as hidden and distinguish read-only tracking context from disabled actions. Keep NFR15 synchronized in both inventories.

### 3. The clause ledger cannot produce the single primary owner its guard requires

**Evidence.** Section 9.1 at PRD line 528 requires one primary owner per stable clause. Section 7.1 assigns multiple primary stories to four single clause IDs. FR4-C2 lists 1.2 and 1.16, FR4-C5 lists 1.16, 2.7 and 2.11, FR34-C10 lists 7.10 and 7.11, and FR34-C11 lists 7.14, 7.19 and 7.20 (lines 424, 427, 460–461). Story 7.10 explicitly owns the FR34 CI slice. Story 7.11 explicitly supports FR34 and owns NFR16 readback. A second mismatch affects NFR17-C1, C3 and C4. Their §7.1 primary stories 7.6, 7.8 and 7.9 are supporting in §11.2 and the respective epic declarations. Story 9.4 requires exactly one primary slice per clause and rejection of duplicate or divergent assignments. The existing numeric-story coverage reader compares the *union* of mapped NFR IDs and cannot detect these primary/supporting disagreements. [PRD §§7.1, 9.1 and 11.2](../../../prd.md) · [Story declarations at 5928, 6073, 6136, 6204 and 6267, and Story 9.4 at 8102–8107](../../../epics.md) · [Coverage reader, lines 110–147](../../../../../tools/phase_4_story_completion.py)

**Consequence and correction.** Evidence from a supporting story could be treated as closure authority. A guard faithfully implementing Story 9.4 would reject the PRD even if every owner produced passing evidence. Preserve the stable clause IDs, name one accountable story per clause and list contributing stories separately. Where one clause covers distinct deliveries, enumerate the required contributions. Align the relevant story declarations and §11 role columns with the approved ownership decision. Gate G-CLAUSE should continue to require evidence.

## Medium findings

### 4. The epic list omits all four newly approved audit stories

**Evidence.** PRD §1 at line 137 adds 2.18, 2.19, 5.15 and 7.23. The story headings and tracker entries exist. The Epic List still gives the ranges 2.1–2.17, 5.1–5.14 and 7.1–7.22 (epics lines 340, 366 and 382). It also describes 2.14 and 2.15 as backlog, while the tracker lists them as in progress and review, respectively. [PRD audit scope](../../../prd.md) · [Epic List and detailed stories](../../../epics.md) · [Sprint tracker](../../../../implementation-artifacts/sprint-status.yaml)

**Consequence and correction.** A planning handoff based on the epic overview drops approved query, tenant isolation and Admin activity work. Update the three ranges. Cite the current tracker for lifecycle or date any historical labels. This leaves all four new stories in backlog as approved.

### 5. UJ1 makes a Tenants reuse metric a closure condition for unrelated requirements

**Evidence.** Section 3.3 at PRD line 190 states that any residual host wiring leaves FR1–FR10 and SM8 open. SM8 at line 579 measures duplicated Tenants plumbing and is tied to FR9. FR6 independently governs cursor validation. FR10 independently governs the publishable package inventory. The epic declarations at lines 621 and 903 assign those requirements to 1.5 and 1.12. The Tenants host still registers several of the named services, so the nonzero SM8 observation itself is supported. [PRD UJ1, FR6, FR10 and SM8](../../../prd.md) · [Stories 1.5 and 1.12](../../../epics.md) · [Tenants host registration](../../../../../references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs)

**Consequence and correction.** A release reviewer could hold a separately proven cursor or packaging requirement open solely because Tenants adoption is incomplete. Keep UJ1 as a cross-feature journey. Apply residual wiring to the affected adoption and guardrail criteria and SM8, then evaluate each remaining FR against its own owner and evidence.

## Earlier findings and retained boundaries

The 14 earlier corrections are present. FR33 covers historical GUID reads, the FR/NFR inventories agree, and the three repaired NFR mappings resolve. G-RUNTIME-PARITY dates its historical result, G-PUBLICATION-AUTH identifies the working evaluator, G-NFR18 names the pending re-attestation, and the UX baseline register records all five input-digest differences. The Epic 6 and 8.12 handoffs, Epic 9 scope, subscriber identities, OR25/OR26, dated validation identity and SM3/SM4 descriptions match the reconciliation record. The three historical numeric-story refusals and four letter-suffixed documents retain their stated dispositions. [Original findings and reconciliation](../reconcile-validation-2026-11-20.md)

The read-only Type Catalog route, hidden restore/import controls and gated Create Tenant flow are documented in the UX handoff. Remaining UI implementation, production qualification, the AOT re-attestation and owner approval of a renewed baseline are tracked delivery or approval work. They do not add findings to this review. [UX handoff](../../../ux.md) · [Detailed experience](../../../ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md)
