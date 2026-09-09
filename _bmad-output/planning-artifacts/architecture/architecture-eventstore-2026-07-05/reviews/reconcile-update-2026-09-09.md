# Architecture Update Input Reconciliation — 2026-09-09


> **Anchor note (added 2026-09-09, code review Story 4.15 Group F).** The line numbers originally cited in
> this file were computed against a pre-final draft of the spine and are offset by a non-uniform amount (AD-8 by
> 21 lines, AD-16 by 25, AD-26 by 32), so they resolve to the wrong decision. Citations that could be mapped
> unambiguously have been re-anchored to **AD identifiers**, which are stable. Any residual bare `:NNN`
> reference in this file is unreliable — resolve it by the AD or section named in the surrounding prose, not by
> the number. `ARCHITECTURE-SPINE.md` is a symlink to `_bmad-output/planning-artifacts/architecture.md`.

## Verdict

**CHANGES REQUIRED before the reviewer gate.** The update resolves both prior critical findings and all eleven prior high-severity defects at the level of their intended remedies, preserves `AD-1` through `AD-25`, and adds contiguous `AD-26` through `AD-33`. However, condensation introduced four conflicts or omissions against the current PRD and one brownfield seed omission. The spine should remain `status: draft` until these are corrected.

| Severity | Count |
| --- | ---: |
| Critical | 0 |
| High | 4 |
| Medium | 4 |
| Low | 1 |

## Prior validation finding reconciliation

### Critical and high findings

| Finding | Disposition | Evidence in updated spine |
| --- | --- | --- |
| C1 production runtime/provider authority | Resolved safely | AD-26 selects self-managed Kubernetes, PostgreSQL `state.postgresql` v1, OQ8 `oq8-postgresql-v1`, an approved durable broker, OpenBao, and proof gates; it prohibits release/deployment until the profile is proven (`ARCHITECTURE-SPINE.md` AD-26). The Deferred table keeps broker, restore, multi-region, OpenBao, and provider-fence gaps fail-closed (`:374-388`). |
| C2 tenant authority/normalization | Resolved | AD-27 places one lowercase canonicalizer in `Contracts`, requires one explicit tenant, fixes the grammar, rejects missing/conflicting values, and reserves `system`/wildcards (AD-27). |
| H1 inbound sidecar credential | Resolved | AD-28 binds `APP_API_TOKEN` / `dapr-api-token`, shared validation middleware, readiness failure, and non-authoritative caller app IDs (AD-28). |
| H2 deny-by-default HTTP policy | Partly resolved; see R1 | AD-16 now requires an authenticated fallback policy and metadata enumeration (AD-16), but its anonymous allowlist conflicts with PRD NFR1. |
| H3 Admin mutation attribution | Resolved | AD-29 binds authenticated human/service attribution and a versioned resumable mutation/audit unit (`:237-241`). |
| H4 key rotation/fleet catalog | Resolved | AD-24 requires operational acknowledgement plus zero live references; AD-25 defines a shared `(Domain, CommandType)` catalog, content digest, readiness comparisons, key generations, and retirement blockers (`:203-217`). |
| H5 correlation contract/status identity | Resolved | AD-32 supplies the shared grammar, first-boundary minting, propagation, and `traceparent` separation; AD-17 makes `MessageId` the sole status identity (`:161-165,255-259`). |
| H6 erasure authority/order | Resolved with safe deferral | AD-30 names domain policy as orchestration authority and orders fenced mechanics while separating completion facets (AD-30); physical/broker/backup/legal-hold completion remains explicitly deferred (`:388`). |
| H7 `ProjectionVersion` semantics | Resolved | AD-15 makes it an opaque scoped equality token and AD-20 places progress/rebuild equivalence on persisted checkpoints and output (`:149-153,179-183`). |
| H8 Operations/dead-letter ownership | Resolved | AD-31 owns capture/replay, fixes app ID, prohibits production use pending topology/release/auth/audit/capture proof, and forbids successful acknowledgement of unretained data (AD-31). The seed and topology now show the existing but unwired project (`:320,346-359`). |
| H9 phantom projection result types | Resolved | AD-19 ratifies shipped `ProjectionDispatchResponse` / `ProjectionDispatchOutcome` and keeps boolean coordinators internal (`:173-177`). |
| H10 stale .NET baseline | Resolved | Stack distinguishes repository pins (`10.0.400`, package family `10.0.11`) from current security evidence (`10.0.12` / SDK `10.0.401`) and requires a tested catalog refresh (the Stack table). |
| H11 public unfenced route | Resolved architecturally | AD-5 explicitly rejects null/empty/stale fences, restricts unfenced seams outside Development, and retains the independent provider-write fence gate (`:87-91`). Implementation remains non-conforming until the cited route is changed. |

### Medium and low tail

| Findings | Disposition |
| --- | --- |
| M1 route ownership | Resolved by the one-digest route catalog and readiness failure in AD-33. |
| M2-M3 stack/topology drift | Resolved by dated Stack facts and separate current/target topology (`:283-302,329-360`). |
| M4 direct Admin state reads | Resolved by the named, tenant-authorized, support-safe, platform-owned read-adapter exception in AD-3 (`:75-79`). |
| M5 structural omissions | Prior named omissions are resolved, but the tracked/released Gateway project is newly omitted; see R5. |
| M6 operational/telemetry envelope | Resolved by AD-10's data prohibition and owner/trigger rows for restore, promotion, multi-region, OpenBao, and observability (`:117-121,374-390`). |
| M7 payload-spec lifecycle | Resolved: AD-23 records `approved-authorized`, exact digest, and `AR-20260801-01` while retaining successor gates (AD-23). |
| M8-M9 volatile detail and duplicate conventions | Resolved: story/evidence procedure is removed and conventions mostly point to ADs (`:267-281`). |
| M10 production secret-template drift | Safely gated by AD-26 and the Implementation Status And Production Gates table (`:219-223,383`), but AD-24 lost exact PRD secret wiring; see R3. |
| M11 OQ8 locator | Resolved with repository, path, commit, digest, custody, and fail-closed availability (`:44`). |
| M12 memlog continuity | Resolved: the memlog was appended to, never rewritten. (Line count corrected 2026-09-09, code review Story 4.15 Group F: the original row said "grew from 105 to 127 lines"; the file was 133 lines at that commit. The append-only conclusion holds; the arithmetic did not.) |
| L1-L2 dependency currency | Resolved/deferred through dated repository pins, explicit preview posture, and the tested refresh gate (`:283-302,390`). |
| L3 RFC/absolute `Location` | RFC citation is corrected, but the rule now weakens FR12; see R2. |
| L4 editorial token | Resolved. |
| L5 self-companion | Resolved (`:34-35`). |
| L6 capability-map overbinding | Resolved by area-to-AD mapping rather than unsupported direct FR17/FR18 claims (`:362-372`), though AD-16 itself still has unrelated `Binds` entries; see R8. |

## Accepted proposal constraints

- The 2026-08-20 dependency/submodule constraint lands in AD-11 and Stack: Builds remains the sole version authority, source mode is explicit, coupled families move together, and version availability does not authorize an update (`ARCHITECTURE-SPINE.md` AD-11, the Stack table; proposal `sprint-change-proposal-2026-08-20.md:50-58,90-94`). The terse generic wording appropriately avoids reintroducing volatile Story 3.16 audit counts.
- The 2026-08-29 reconciliation lands: both proposals are sources (`ARCHITECTURE-SPINE.md` the frontmatter `sources:`), the obsolete deployed-parity handoff block and deferred row are absent, and immutable evidence—not planning status—governs release/parity through AD-11 and AD-22 (`:123-129,191-195`; proposal `sprint-change-proposal-2026-08-29.md:38-45`).
- Stable IDs are preserved exactly: 33 contiguous decisions, `AD-1` through `AD-25` unchanged in identity and `AD-26` through `AD-33` newly allocated.

## Residual findings and exact fixes

### R1 — High: AD-16 broadens the anonymous surface beyond PRD NFR1

AD-16 allows cataloged static public assets and OIDC callbacks to carry `AllowAnonymous` (`ARCHITECTURE-SPINE.md` AD-16). The current PRD says the **only** anonymous exceptions are `/health`, `/alive`, and `/ready` (`prd.md:317`). This creates two authoritative security contracts.

**Fix:** change AD-16 to permit `AllowAnonymous` only on those three support-safe probes. If anonymous static assets or OIDC callbacks are required, first amend NFR1 through the governed PRD process; do not let the architecture silently widen it.

### R2 — High: AD-17 weakens the absolute `Location` contract

AD-17's title says absolute, but its Rule accepts an RFC 9110 URI-reference and makes absolute form merely preferred (`ARCHITECTURE-SPINE.md` AD-17). FR12 requires an absolute gateway-authoritative URI when a valid target is supplied and omission otherwise (`prd.md:215`). RFC 9110 permitting a URI-reference does not relax Hexalith's stricter product contract.

**Fix:** state: “On `202`, emit an absolute `Location` URI built from trusted gateway configuration when a valid target exists; otherwise omit it. This is Hexalith's stricter contract within RFC 9110.”

### R3 — High: AD-24 no longer fixes the canonical OpenBao wiring

AD-24 identifies the component type but no longer fixes component name `openbao`, `auth.secretStore: openbao`, `secretKeyRef`, DAPR Secrets API access, or the Kubernetes bootstrap-only exception (`ARCHITECTURE-SPINE.md` AD-24). FR34 and NFR17 make those cross-unit choices normative (`prd.md:282,333`). Independent overlay, application, and component work can again choose incompatible secret paths.

**Fix:** add the exact component name and consumer wiring to AD-24, require application access through DAPR Secrets API, and state that Kubernetes Secrets may contain only documented bootstrap material when no approved mounted/projected mechanism exists. Keep the value-free overlay details and environment values deferred.

### R4 — High: AD-23 dropped the payload format and NFR19 contract

AD-23's `Binds` omits NFR12 and NFR19 and its Rule reduces the engine to an unspecified optional “format” (`ARCHITECTURE-SPINE.md` AD-23). FR37 fixes `pdenc-v2`, byte-stable authenticated data, backward readers, extension seams, a production backend, parity, and rollback; NFR19 fixes typed fail-closed outcomes, key-buffer zeroing, cache invalidation, and rollout/historical-read evidence (`prd.md:303-311,328,335`). Those are the very cross-unit cryptographic invariants AD-23 exists to prevent from diverging.

**Fix:** restore NFR12/NFR19 to `Binds` and name `pdenc-v2` + byte-stable AAD, preserved `json+pdenc-v1` / `json-redacted` / legacy / snapshot reads, `IPersonalDataPolicy`, `IErasureStateProvider`, and the non-development backend/parity/rollback gate. Detailed schema bytes may remain in the approved specification.

### R5 — Medium: Structural Seed omits the tracked Gateway package

The seed lists the EventStore host but omits `src/Hexalith.EventStore.Gateway/` (`ARCHITECTURE-SPINE.md` the Structural Seed). That project is tracked and is an explicit package in `tools/release-packages.json:43-46`; its project file describes the reusable HTTP gateway surface. It is absent from `Hexalith.EventStore.slnx`, which is a repository drift worth exposing rather than erasing from the seed.

**Fix:** add `Hexalith.EventStore.Gateway/` as the reusable HTTP gateway/package seam. Do not claim solution inclusion; separately leave the solution/manifest mismatch for implementation governance.

### R6 — Medium: NFR18 is neither decided nor deferred

Frontmatter claims NFR1-NFR19, but no AD or Deferred row carries NFR18's explicit no-AOT/trimming posture and owed document (`prd.md:334`).

**Fix:** add NFR18 to AD-13 and state that AOT/trimming remains out of target while reflection conventions are load-bearing, or add a Deferred row owned by the platform maintainer and triggered before NFR18 coverage/readiness.

### R7 — Medium: Two exact UI/delivery guards were over-compressed

AD-8 says only “bounded” SignalR metadata, losing NFR5's interoperable 16-entry / 2048-byte ceiling (`ARCHITECTURE-SPINE.md` AD-8; `prd.md:321`). AD-21 omits NFR15's rule that unavailable Admin operations are hidden/disabled or return `501` (`ARCHITECTURE-SPINE.md` AD-21; `prd.md:331`).

**Fix:** restore both exact rules in AD-8 and AD-21 respectively; they are small cross-unit invariants, not volatile procedure.

### R8 — Medium: Some `Binds` entries are semantically unrelated

AD-16 binds FR18 (DAPR ETag timeout), FR20 (Keycloak resource name), NFR11 (package inventory), and NFR15 (Admin unavailable operations), none of which its Rule addresses (`ARCHITECTURE-SPINE.md` AD-16; `prd.md:229-233,327,331`). This masks rather than closes traceability gaps.

**Fix:** remove unrelated bindings from AD-16. Carry FR18 through code/spec authority or Deferred, FR20 through AD-9/current topology, NFR11 through AD-11, and NFR15 through the corrected AD-21.

### R9 — Low: New inferred decisions are all labelled adopted

AD-26 through AD-33 are all `[ADOPTED]`, but the Fast-path response accepted the update/run scope, not each newly inferred production, protocol, and ownership choice. Several are strongly evidence-backed; AD-26's production target in particular is a new selection rather than current delivered reality.

**Fix:** until explicit review acceptance, mark genuinely inferred choices `[ASSUMPTION]` (at minimum AD-26 and any other non-ratified new target); remove the tag when accepted. Existing shipped seams can remain `[ADOPTED]`.

## Handoff

Apply R1-R8, triage R9, rerun deterministic lint, and then proceed to the configured reviewer gate. No edit to the spine was made by this reconciliation pass.
