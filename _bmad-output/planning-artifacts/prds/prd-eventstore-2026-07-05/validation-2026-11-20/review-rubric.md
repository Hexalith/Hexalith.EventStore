# PRD quality review for Hexalith.EventStore Phase 4

Reviewed on November 20, 2026 against the BMAD PRD quality rubric. The canonical PRD has SHA-256 `709069aa88cb92b08f9b51252f3ce63bef0aa351b904f6f72e5e64beef4d1d9e`. Evidence includes the canonical and historical PRD memlogs, the sprint tracker, owning story specifications, repository files and the live risk-matrix CLI. Line numbers refer to that PRD revision.

## Overall verdict

**Fair.** The product thesis, MVP boundary and seven November audit cases give developers and operators concrete outcomes. The document still treats several historical failures as present facts and one retired verification command as a current technical pass. Those errors can send owners to redo completed work or to rely on evidence they cannot reproduce. Phase 4 readiness remains blocked on independent grounds.

## Decision-readiness — thin

The status of Stories 2.14 and 2.15 is clear. The PRD gives the tested behavior, remaining assurance obligations and the owner of the next decision. The live matrix independently reported missing seals, missing attestations and below-required assurance for both gates. Other gate rows need the same treatment.

- **High · Runtime parity cites a retired command as current proof.** Sections 6.8, 10 and 11 (lines 377, 573, 693 and 730) carry the historical three-receipt result into the present. The exact command in the G-RUNTIME-PARITY row exited 2 because `tools/validate-corrected-deployed-runtime-parity.py` is absent. The live matrix reports `BINDING_UNBOUND` as well as missing assurance. Preserve the September technical decision with its date and subject. Identify current revalidation as unavailable and give Story 3.15’s owner the task of defining its present evidence path under AD-12. Keep FR36 and release, deployment and removal authority blocked. [Evidence simplification](../../../../implementation-artifacts/spec-evidence-storage-simplification.md) · [Gate matrix](../../../../implementation-artifacts/phase-4-gate-risk-matrix.json)
- **High · The publication row reports completed validator construction as missing.** G-PUBLICATION-AUTH and OR29 (lines 731 and 786) still call for the validator and describe parent Story 3.19 as backlog. The schema and CLI exist, the CLI exposes structural and evaluation modes, and the tracker marks 3.19 in progress. The construction slice is done. Record that result and state what still blocks issuance, including the approved profile, subject-bound prerequisites and authenticated consumption. A structural fixture pass supplies no publication authority. [Construction specification](../../../../implementation-artifacts/spec-3-19-publication-authority-validator.md) · [Sprint tracker](../../../../implementation-artifacts/sprint-status.yaml)

## Substance over theater — strong

The strongest acceptance cases let a reader predict an observable result. With a changing projection, Story 2.18 requires HTTP 200 and the original untagged payload. A second delivery of one of two distinct events leaves exactly two durable effects. An Admin read whose tenant scope cannot be established returns unavailable. These cases identify failures that an HTTP status check or a passing build alone could miss.

## Strategic coherence — strong

The product thesis ties reuse by domain developers to operational trust (lines 159–163). SM8–SM12 measure duplicated plumbing, controller placement, protected surfaces, silent loss and persisted end-state evidence. Counter-metrics address foreseeable shortcuts. The post-MVP payload-protection work has its own explicit G5 gate, and the November findings support the existing MVP.

- **Low · The grouping mislabels a failed metric.** The heading “Planning-recovery metrics (achieved)” includes SM3, explicitly “not met” (lines 563–566). Use a status-neutral group heading or move SM3. A reader scanning the group should see that high-risk coverage remains open.

## Done-ness clarity — thin

The clause ledger gives engineers useful observable consequences and preserves existing IDs. Contradictory statements about authorization, historical replay and documentation prevent several requirements from being accepted consistently.

- **High · The PRD gives opposite answers on whether Story 6.4 may proceed.** The FR33 ownership cell (line 632) says “6.4 remains unauthorized” because its specification is absent. G-NFR8 (line 727) records the approved specification and explicit authorization. NFR8 and the evidence ledger (lines 405 and 701) also retain pre-approval statements and stale lifecycle values. The tracker records 6.2 done, 6.3 done and 6.4 in progress. State the approved specification and current implementation status consistently. Keep acceptance of the runtime cost bound contingent on its own evidence. [Approved specification](../../../../implementation-artifacts/spec-projection-cost-sequence-guard.md) · [Sprint tracker](../../../../implementation-artifacts/sprint-status.yaml)
- **High · A blanket identity requirement can reject supported historical events.** FR33 (line 344) requires rejection when metadata identity components are “not ULID-safe,” without distinguishing reads from new writes. The approved 6.6 contract explicitly accepts a legacy GUID MessageId on read and separately applies AD-32 correlation and causation rules. An implementer reading FR33 alone could break historical replay. State the field and direction of each rule, with one legacy-read example, and keep the existing stricter write validation. [Story 6.6 constraints and I/O matrix](../../../../implementation-artifacts/spec-6-6-event-versioning-and-upcasting-implementation-3.md)
- **High · G-NFR18 names the wrong missing evidence.** G-NFR18 and OR6 (lines 734 and 765) say the document, owner, evidence and command are absent. The posture reference exists, the tracker and owning spec mark 6.7 done, and the spec records the test command and owner review. It also records a subsequent edit requiring re-attestation. The file currently hashes to `99395b0adf8f86c8c18bf58f57e863c52f91bf03d068eca7f000965972126a61`; the recorded attestation covers `babd1cd1…`. Replace the instruction to create the page with the actual current-byte review obligation. The offline matrix’s standard-control classification does not establish content review. [Current posture](../../../../../docs/reference/aot-and-trimming-posture.md) · [Story 6.7 verification](../../../../implementation-artifacts/spec-6-7-aot-and-trimming-posture-reference.md)

## Scope honesty — adequate

Sections 9.2–9.3 distinguish source availability, production eligibility and future features. Guest-facing UI restrictions, profile authority and per-consumer migration have explicit owners. The payload-protection limits are clear, but its handoff status is out of date.

- **High · The post-MVP handoff still treats the approved 8.12 successor as pending.** Sections 6.9, 8.4, 11.3 and OR31 (lines 392, 519, 697 and 793) call for the Dapr Kubernetes successor’s approval or implementation. The supplement records approved revision 4, the story is done, and the owning spec records 14 of 14 isolated Kubernetes cases passing for its pinned 1.19.1 cohort. Record the completed bounded conformance and the specific work still owned by 8.7, the release owner and G5. Preserve the separate capacity, activation and rollout requirements. [Successor approval](../../../../implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography-amendment.md) · [Story verification and limits](../../../../implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography.md)

## Downstream usability — thin

The FR and NFR identifiers are complete, and the November stories can be traced to the approved proposal. Several entries that read as current still contradict their owning sources.

- **Medium · The UX baseline register sends a reviewer to resolve decisions already recorded.** Line 688 calls DESIGN and EXPERIENCE draft and says the UX handoff digest matches. Both detailed documents have final frontmatter. All five recorded epic digests differ from their current inputs, including the UX handoff (`2927f97d…` recorded, `5dc2d678…` current). G-BASELINE at line 721 already describes the current state correctly. Date the older register row and identify its superseding entry. Retain actual digest reconciliation and owner approval as outstanding. [Approved November 9 reconciliation](../../../sprint-change-proposal-2026-11-09-readiness-recovery.md) · [Epic input digests](../../../epics.md)
- **Medium · SignalR and generator owners receive duplicate assignment instructions.** OR25 and OR26 (lines 782–783) ask for owners to be assigned and call Stories 2.16 and 2.17 backlog. The tracker and both owning specifications mark them done. Use the existing owners and recorded implementation evidence, then identify any separate verification the coverage manifest still owes. [Story 2.16](../../../../implementation-artifacts/spec-2-16-bounded-signalr-detail-metadata.md) · [Story 2.17](../../../../implementation-artifacts/spec-2-17-generated-code-and-generator-build-quality.md)
- **Medium · Subscriber identity needs a boundary in the normative text.** Section 6.4 (line 320) instructs consumers to use the received CloudEvents pair `(source, id)`. NFR6 (line 403) says subscribers deduplicate by `MessageId`. Story 7.1 explicitly distinguishes external subscriber identity from the platform processor’s persisted-message contract. Carry that distinction into both PRD passages, including subscription scope and the publisher’s mapping of MessageId to CloudEvents id. [Story 7.1 and its audit acceptance](../../../epics.md)

## Shape fit — adequate

A capability-led structure suits this brownfield platform. The five journeys lead from named users to platform seams or operational proof. The main usability cost comes from keeping historical review narratives next to undated current results. When implementing the corrections above, keep the dated result near its source and give each gate one current account.

## Mechanical notes

FR1–FR37 and NFR1–NFR19 each occur once in the respective normative requirement table. The five journeys have named protagonists. The last formal implementation-readiness assessment remains dated October 6. This review checked the canonical document and source evidence on November 20. The quality judgment is about using the PRD to make decisions.

**Open findings:** 0 critical, 6 high, 3 medium, 1 low.
