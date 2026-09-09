# Architecture Update — Adversarial Closure Review


> **Anchor note (added 2026-09-09, code review Story 4.15 Group F).** The line numbers originally cited in
> this file were computed against a pre-final draft of the spine and are offset by a non-uniform amount (AD-8 by
> 21 lines, AD-16 by 25, AD-26 by 32), so they resolve to the wrong decision. Citations that could be mapped
> unambiguously have been re-anchored to **AD identifiers**, which are stable. Any residual bare `:NNN`
> reference in this file is unreliable — resolve it by the AD or section named in the surrounding prose, not by
> the number. `ARCHITECTURE-SPINE.md` is a symlink to `_bmad-output/planning-artifacts/architecture.md`.

**Date:** 2026-09-09  
**Subject:** remediated `ARCHITECTURE-SPINE.md`  
**Prior report:** `review-update-2026-09-09-adversarial-divergence.md`  
**Verdict:** **PASS** — all prior critical and high findings are closed; no new critical or high issue was found in the focused seams.

## Closure evidence

| Prior finding | Status | Evidence in the remediated spine |
| --- | --- | --- |
| C1 — subscriber poison could be acknowledged before durable capture | **Closed** | AD-8 now applies to every production subscriber, explicitly includes the generic domain-event endpoint, requires a tenant/domain-scoped durable record keyed by stable `MessageId` before acknowledgement, keeps timeout/cancellation/conflict/capture/unretainable/unknown outcomes retryable or durably quarantined, and binds subscription, sink, and catalog fingerprint as one evidence unit (`ARCHITECTURE-SPINE.md` AD-10). AD-31 names Operations as that sink and prohibits success acknowledgement for the unsafe capture outcomes (`:273-277`). This directly supersedes the brownfield drop paths in `EventStoreDomainEventsEndpointExtensions.cs:47-60` and `DeadLetterOperationsEndpointExtensions.cs:110-119`; those paths remain implementation gaps, not architectural permission. |
| H1 — blanket release prohibition conflicted with AD-11 | **Closed** | AD-26 now permits separately authorized immutable candidates solely for evidence, while prohibiting only production promotion, traffic, consumer migration, readiness claims, and production identity until proof passes (AD-30). The Deferred introduction repeats the same distinction and leaves candidate/package/image publication under AD-11 (`:409-411`), consistent with the existing protected artifact workflow. |
| H2 — route/idempotency catalogs lacked one authority and atomic join | **Closed** | AD-33 assigns schema/codec ownership to Contracts, instance ownership to Platform deployment, names one JSON envelope, joins both facets by stable route-entry ID under one root digest/generation, defines retained-byte hashing, and requires prepare/ready/commit with rollback (`:285-291`). AD-25 now consumes the idempotency facet of that same envelope by stable route-entry ID and fails root/facet drift (`:227-239`). The absent artifact is explicitly production-blocking with named owners and trigger (`:417`). |
| H3 — MVP projection erasure and post-MVP full erasure were conflated | **Closed** | AD-7 now confines MVP behavior to typed, scoped, idempotent, read-back-proven projection/checkpoint removal and disclaims broader erasure (AD-9). AD-30 explicitly owns the post-MVP workflow, relates the MVP operation by stable ID, separates all completion facets, and states that Story 1.14, FR5, a projection response, or spec approval cannot authorize or deliver it (`:267-271`). |
| H4 — NFR3 shared JWT contract was not pinned | **Closed** | AD-10 now names `JwtBearerAuthenticationContract` as the single versioned owner for all JWT-binding hosts and freezes issuer/audience/signature/lifetime, 60-second skew, HTTPS metadata, explicit asymmetric algorithm allowlist, HS256-only break-glass bounds, shared consumption, and fingerprint failure (`:131-137`). AD-28 remains a distinct app-channel scheme (`:255-259`). |
| H5 — deployment guidance remained an unsafe alternate authority | **Closed** | AD-9 includes deploy/operator documentation and machine-checkable examples in the topology change unit, makes the spine and content-bound profiles/catalogs authoritative, and requires CI rejection of identity/secret/topology drift (`:125-129`). The known `deploy/README.md` CloudEvent/OpenBao contradictions are recorded as non-authorizing with owner and remediation trigger (`:426`). |
| M1 — feature altitude understated platform authority | **Not closed** | Frontmatter retains `altitude: feature` (the frontmatter), which is correct: the BMad architecture taxonomy defines only `initiative`, `feature`, and `epic`, and `review-update-2026-09-09-rubric-closure.md` M-R1 requires `feature`. Corrected 2026-09-09 (code review, Story 4.15 Group F) — the original row attested a change that was never made. |
| M2 — adopted decision could be mistaken for delivered capability | **Closed** | A legend explicitly says `[ADOPTED]` is acceptance, not implementation/proof, and points to brownfield/production gates (`:71-73`); all AD headings use the same status. Current topology and future production target remain visibly separated (`:358-395`). |
| M3 — reserved `system` tenant was ambiguous | **Closed** | AD-27 prohibits `system` as a managed/provisionable tenant at public boundaries, gives internal platform scope a distinct cataloged namespace, and forbids `system:*` from synthesizing tenant or global-admin grants (AD-31). |

## Remaining implementation truth

The PASS applies to the architecture update, not runtime delivery. The spine correctly leaves the unified catalogs/profile, production broker, Operations wiring, shared app-channel/JWT adoption, poison capture, OpenBao, append fencing, and deployment-guide reconciliation behind explicit readiness or production gates (`ARCHITECTURE-SPINE.md` the Implementation Status And Production Gates table). Current conflicting code and documentation therefore remain actionable brownfield gaps without weakening this architecture decision set.

## Gate decision

No critical or high blocker remains from the adversarial lens. The spine may proceed through the remaining configured reviewers and final lint/status transition.
