# Current-Source UX Reconciliation — 2026-10-07

## Scope and evidence boundary

This record supports the Update of [DESIGN.md](DESIGN.md) and [EXPERIENCE.md](EXPERIENCE.md). It records source extraction and substantive reconciliation, not a new product authority, approval, implementation claim, or validation verdict. The UX spines retain their own authority over illustrative artifacts. No upstream planning document, story, dependency, source digest pin, or Git history is changed by this record.

The reviewed repository base is full HEAD `c60503c13069fde13f329f2166ff39c90c3661c4`, observed on 2026-10-07 at approximately 13:47 UTC and verified unchanged at 13:51 UTC. The working tree also contained the active UX memlog update and an untracked concurrent correct-course proposal; the captured sources below were hashed directly from their working-tree bytes. The final upstream snapshot includes the concurrent epics correction observed at 13:51 UTC. The commit alone is not an approved atomic planning baseline. The detailed UX and downstream handoff hashes describe their 13:47 UTC state before this Update's active distillation and are not expected to remain current after that work.

## Authority and current statuses

| Source | Authority and observed status |
|---|---|
| [PRD](../../prd.md) | Product intent, scope, FR/NFR and readiness authority. `status: final`, `document_status: final`; implementation readiness remains `blocked` / `reject`, last assessed 2026-10-06. Document finality supplies no implementation or readiness permission. |
| [Architecture](../../architecture.md) | Current system decisions, including AD-1 through AD-36. `status: draft`, updated 2026-10-07. AD-26 production target remains unratified; adopted decisions are requirements, not delivery evidence. |
| [Epics](../../epics.md) | Implementation slicing and acceptance responsibility. Its five `inputDocumentDigests` are stale. Matching or repinning hashes alone never establishes reconciliation or approval. |
| [Brownfield architecture](../../../../docs/brownfield/architecture.md) | Observed legacy runtime and package scope. It is not target UX, production, or readiness authority. Its bytes are unchanged from the UX's prior captured snapshot. |
| [2026-10-07 correct-course proposal](../../sprint-change-proposal-2026-10-07.md) | All five groups approved by the owner and applied. Section 5.7 routes the two UX assumptions for reconciliation. It defines neither a new UX-DR ID nor an automatic choice to delete provisioning. |
| [2026-10-07 architecture-routing proposal](../../sprint-change-proposal-2026-10-07-architecture-routing.md) | All seven groups plus option 4b approved and applied. It supplies named owners and staged architecture-ID propagation, without changing gate results. |
| [Concurrent 9.2/NFR1 supplement](../../sprint-change-proposal-2026-10-07-story-9-2-nfr1.md) | Observed during final hash verification, with its header recording Administrator approval and two applied epics edits. It propagates current PRD anonymous-exception and transition-seal rules; current PRD remains the product authority. |
| [Implementation-readiness assessment](../../implementation-readiness.md) | Current 2026-10-06 FAIL evidence. It does not replace the PRD or grant implementation permission. |
| [Bound PRD validation](../../prds/prd-eventstore-2026-07-05/validation-report.md) | Historical snapshot: run 2026-09-10T08:31:03+02:00, baseline `293c69c42d35dee26682d42f05c943c0b65786f4`, grade Poor, Reject posture. Individual historical findings do not override current source decisions. |
| [Dapr-only 6.6 amendment](../../../implementation-artifacts/story-6-6-dapr-only-amendment.md) | Current owner-directed implementation constraint, 2026-10-05; Story 6.6 remains in progress. It supersedes earlier direct-provider requirements, without repinning historical approval bytes. |
| [Trusted-code 6.6 amendment](../../../implementation-artifacts/story-6-6-trusted-code-amendment.md) | Approved implementation policy, 2026-10-06. Admission/activation remains fenced until qualification; Story 6.6 remains in progress. |
| [Historical source-safety UX validation](validation-report-source-safety-update-2026-09-09.md) | Review evidence for the reopened September draft. Its correction findings are useful inputs, not a fresh verdict on this Update. |

Both approved October proposals and the implementation amendments are already reachable from the bound PRD/architecture sources. They were followed as declared sources, not independently selected as equal authority. The official Fluent UI V5 site remains the inherited component-system reference; no new web research, theme, or component version authorization is claimed.

## Retained user decisions

- Evolve `src/Hexalith.EventStore.Admin.UI` in place as `eventstore-admin-ui`; exactly one FrontComposer module `event-store-admin`, labelled **Event Store Admin**.
- Preserve the ten ordered dashboard tabs, their existing route groups, and separate Sample/Tenants consumer modules.
- Preserve FrontComposer and Blazor Fluent UI V5 inheritance, compact operational density, the 4px spacing contract, dense evidence grids, and theme-role behavior in light/dark/system/forced colors. Captured blue-bar and white-canvas images are composition references, not fixed theme values.
- Preserve the explicitly selected `/types` placement under Streams & Events, with events, commands and aggregates inner views.
- Preserve Overview and Commands as the complete key-screen visual set. All other surfaces remain spine-only unless the user requests more visual coverage.
- Preserve evidence-based success, fail-closed mutation gates, no invented freshness horizon, no automatic mutation resubmission, support-safe microcopy, responsive access, accessibility and localization requirements.

These decisions come from the existing [memlog](.memlog.md) and spines. The source changes below refine safety, source routing and closure; no new visual direction or dashboard is required.

## Selected resolution of the two open assumptions

### Restore and import

Retain the established deferred and non-actionable policy. Current code has **Import Stream** and **Restore** actions within `/backups`, including an import file picker and restore workflow. Their presence is evidence of legacy debt, not delivered capability: [Backups.razor](../../../../src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor), lines 43–45, 199–202, 323–422 and 475 onward in the reviewed snapshot.

[Story 7.4](../../epics.md#story-74-honest-deferred-admin-operations), lines 5498–5573, already requires a closed deferred-capability matrix covering backup creation/validation/restore, stream import/export where unavailable, and other deferred actions. It requires hiding forms/actions/dialogs/routes/palette entries where useful read-only context is absent, or showing honest read-only tracking context where present. No file picker, acknowledgment workflow, submit-deferred control, job, progress, accepted state or synthetic completion remains. Retained authenticated endpoints return the typed `501` after authentication, tenant authorization and bounded validation, with zero mutation or audit admission. Denial is evaluated before capability disclosure.

The target IA adds no separate restore/import route. `/backups` remains the Deferred & Backlog owner. Story 7.4 owns removal or honest unavailable replacement of existing restore/import controls; [Story 7.14](../../epics.md#story-714-admin-shell-and-canonical-route-migration) owns canonical routing and legacy navigation to that single unsupported/read-only owner. Capability promotion requires a separately approved implementation story and coordinated server, typed-client, audit, UI and production evidence.

This closes the unqualified assumption that restore/import are simply absent. The contract now explicitly accounts for the legacy controls and their removal ownership. It does not prescribe a new history surface or claim that removal is implemented. This is the source-compatible resolution allowed by the approved proposal [§5.7](../../sprint-change-proposal-2026-10-07.md#57-route-to-bmad-ux), lines 791–800.

### Tenant provisioning

Replace the assumption that Tenants & Access excludes provisioning with a **gated Create Tenant target flow**. [Story 5.10](../../epics.md#story-510-reserved-system-tenant-provisioning-guard), lines 4641–4699, explicitly requires that dialog and server-authoritative guard; exclusion would silently override the epic. The approved UX routing does not authorize removing this requirement. No new canonical route is needed: the target remains within `/tenants` and uses the existing Operation dialog pattern.

Every available user-facing provisioning adapter uses the shared semantic guard before command construction/submission. The dialog checks the canonical managed-tenant input as a convenience, rejects normalized reserved `system`, displays a concise localized inline Fluent error associated with the tenant-ID field, returns focus there and sends no request. Invalid input does not disclose any tenant/resource existence. Server validation remains authoritative. No command/admission, actor activation, persisted record, status/archive, audit mutation, publication, notification or other downstream effect is permitted for reserved rejection; source acceptance requires persisted zero-effect proof. Valid nearby identifiers follow the unchanged provisioning contract.

Creation becomes runnable only when its exact delivery, authorization, current pre-state/freshness, audit and evidence gates are proven. Valid submission follows the existing frozen-context, submit-once, accepted/evidence-pending and authoritative confirmation contract; a legacy form or command DTO is no delivery claim. Story 5.10 remains backlog and depends on Story 5.2, whose reconciled state is review. A topology or later UI migration story cannot replace the guard. Amelia owns implementation; Winston reviews the managed/platform boundary and Murat reviews zero-state/downstream evidence.

Story 5.10's historical compatibility clause still permits internal platform-owned `system` routing. Current [AD-27](../../architecture.md#ad-27---tenant-identity-has-one-canonical-boundary-contract-adopted) instead requires a distinct cataloged platform-operation namespace, and the architecture assigns its owner-approved NFR12-classified migration to Story 2.14. UX must never expose `system` as a managed/selectable/provisionable tenant or infer scope from a credential. That upstream migration does not authorize a UX-only bootstrap redesign.

## Concrete source-derived deltas

| Concern | Contract correction | Source and delivery/ownership boundary |
|---|---|---|
| Command status identity | `MessageId` alone selects status. Contracts declarations choose v2 canonical 26-character uppercase Crockford ULID with successful parse and byte-identical round trip, or explicit v1 legacy 1–128 ASCII alphanumeric/hyphen with no leading/trailing hyphen and byte preservation. Caller input never chooses/normalizes a version. | PRD §4 MessageId Contract Version; architecture AD-17 (lines 246–252); Stories 2.15 and 5.12 own declaration/catalog delivery. Earlier done labels are bounded history. |
| Command status `Location` | Consume a valid gateway-authored absolute URI when supplied; tolerate omission. Never synthesize, repair, or use correlation as a fallback selector. UI stays a typed-client consumer. | AD-17; PRD UJ2; Story 2.15 and typed Admin client 7.5. |
| Correlation | `X-Correlation-ID` is 1–128 ASCII alphanumeric/hyphen, accepted or minted once at the first public boundary and propagated unchanged. Never GUID/ULID-parse it or use it as status identity. | AD-32 (lines 377–381); identifier presentation via 7.5/7.19. This specific authority wins over generic envelope-identifier advice. |
| Projection version | Optional bounded opaque equality token scoped to tenant/domain/projection/read-model lineage. Equality within that scope only; no parsing, ordering, incrementing, schema/event-position/progress inference. | AD-15 (lines 236–240); AD-20 rebuild output/checkpoint proof. |
| Notification/freshness | Lost, duplicated, reordered or absent callbacks prove neither currentness nor completion. Direct is currently the only production-eligible freshness transport and has no redelivery; PubSub requires resource-bound provenance and remains non-production without an approved issuer/profile. Reconnect/rejoin retrieves authoritative typed evidence. | AD-8 (lines 164–172), AD-36 resource binding; Story 2.13 distribution remains unresolved. The retained direct Redis backplane is no accepted exception. |
| Notification metadata | Contracts owns ceilings of 16 entries and 2,048 total UTF-8 key/value bytes; options may lower only, receiver rejects oversized, broadcaster clips. Discarded metadata does not render or become lifecycle evidence. | AD-8; existing connection-status/unknown patterns. Logging permission in the source is not permission to render opaque detail metadata to operators. |
| Human versus workload calls | UI/Admin relays forward only the human bearer or AD-29 delegation and no workload assertion. Workload identities never satisfy human authorization or carry tenant/role/administrator grants. Mixed credential kinds fail before downstream work. Sidecar token, caller app ID, ACL or mTLS alone supplies no operational authority. | AD-10, AD-28, AD-29 and AD-36 (lines 405–411); ServiceDefaults owns versioned authentication contracts, 5.5/5.11 own closure. |
| Authentication recovery and anonymous exceptions | Keep bounded denied/unavailable handling and safe same-origin canonical return state. Current NFR1 permits probes plus an enumerated set of data-free static framework assets/authentication-protocol callbacks on interactive UI hosts only, each explicitly anonymous, support-safe and metadata-tested. No operational page/data becomes anonymous. Human login remains unavailable; UI hosts stay outside the canonical MVP production profile until it exists. | PRD NFR1 (line 361), §9.2 (line 507), SM10 (line 543), NFR1 coverage (line 609); interactive login remains backlog 7.16. AD-16 and architecture Implementation Status line 549 retain superseded probes-only/pending-decision wording and require upstream reconciliation. Concurrent epics supplement now propagates the PRD rule. |
| Dapr infrastructure boundary | Operational effects/reads use qualified platform/Dapr adapters, never direct providers or private actor keys. Timeout/cancellation/unavailable readback proves no absence and authorizes no second effect. A safety guarantee unavailable through the allowed boundary leaves the operation typed unavailable/hold. | PRD §8.4; AD-1/AD-34; 3.17 qualification owns the guard. No adapter, dependency, exception or infrastructure permission is created by UX. |
| Event evolution | One allow-listed service governs replay, projections, subscriptions, reconstruction and inspection. Missing/ambiguous mapping, corrupt digest, unreadable protection, unsupported version or incomplete contiguous prefix fails closed before domain/checkpoint/publication effects. Preserve immutable retained application payload bytes and reconcile ambiguous actor saves. | AD-13 amendments (lines 225–226); Story 6.6 and its Dapr-only amendment. V2 admission remains fenced. |
| Loader capability loss | Deployer-reviewed trusted catalog code and immutable admitted artifacts are required. Tenant input cannot provide executable code, paths or CLR selection. A policy violation removes affected evolution capability; uncommitted work cannot claim success, while committed truth retains Dapr reconciliation/idempotency. No hostile-code confinement claim. | Approved 2026-10-06 trusted-code amendment, AD-13. |
| Evolution inspection | Type Catalog/streams/replay/diagnostics show only approved stable contract/version, bounded hop/outcome and safe location/sequence. Unknown, malformed, unreadable and cancelled outcomes remain explicit, with actions matching authority. No CLR details, payloads, protected bytes, secrets or cross-tenant identifiers. | Story 6.6 acceptance at epics lines 5262–5265; no new screen or transport. |
| Release display identity | Topology may show approved repository, semantic version, source revision, verification state and shortened/full-copy immutable **OCI index digest**. This permitted identity differs from prohibited idempotency/catalog digests, keys, fences and raw attestations. Mutable tags never prove deployment. | Story 7.9 UX coverage, epics line 5833; AD-11. All exposed identifiers still require allow-listed typed transport and authorization. |
| Publication states | Preserve `built` → `evidence-candidate-published` → `evidence-validated` → `release-available` → `production-promoted`. Candidate/evidence state confers no release/promotion authority. Separate role-bound records and exact canonical profile/subject evidence are required. | PRD §4; AD-11/AD-26; 3.19 owns schema/validator, owner issues records. No new release-control UI or approval is introduced. |
| Assurance | If a visible gate/authority result is supplied, preserve its required/achieved source assurance level. One-human roster uses `single-maintainer-attested`; tool-persona review never becomes an independent human approval. Merge-blocking fixtures, non-required live evidence and a required seal for one guarded transition are separate; the seal belongs to the transition, not an owner record. | PRD Assurance Control; AD-11/AD-12; Story 9.2 as corrected by the concurrent supplement. No UX finalization or screenshot is a gate seal, and a live FAIL never becomes a readiness/release/promotion/removal authority. |
| McpCli migration | McpCli is the sole target proprietary CLI/MCP surface. Admin.Cli/Admin.Mcp remain compatibility sources limited to safety/continuity fixes. EventStore retains server-side checks, confirmation, audit and admin semantics. Generic admin contract/transport and owner-approved inventory/parity precede removal; unsupported operations are not reported migrated. | AD-35 (lines 398–402); epics opening correction. UI and Admin Server identities are unchanged; maintainers own migration. |
| Erasure claims | Projection removal remains typed read-model/checkpoint removal only. Full/GDPR erasure is unavailable until every required facet passes. Existing MVP crypto-shred seams are claim-bounded, with no engine, physical erasure or production key-custody guarantee. | AD-7/AD-30; Story 7.21 primary NFR17-C5; Epic 8 remains separately gated post-MVP. |
| Payload protection | New prerequisite-spec approval identities do not establish engine/backend/package/parity/rollback/G5 delivery. The detached Dapr amendment is draft/unapproved and cannot reuse earlier approvals for changed bytes. | AD-23; Stories 8.2–8.11 retain their actual gated lifecycle; 8.3 is in progress. |
| Restore posture | Production recovery drills and restore scope cover every profile-bound state component plus scheduler state. This does not make an Admin Restore control available or define RTO/RPO. | AD-26; Story 7.22 owns posture/drill; Platform Operations/data owner owns numerical targets. |

The source-authority distinction matters: catalog activation and route generation readiness shown in Topology are safe typed summaries, not raw catalog bytes, signatures, digests or keys. Publication OCI digests are explicitly permitted only as approved release identities. Neither kind authorizes a mutation by display alone.

## Journey scope and names

[PRD §3.3](../../prd.md#33-user-journeys), lines 165–173, now defines five named journeys. Mirror exact source titles where the UX has the corresponding journey:

- **UJ3 - Priya observes projection-confirmed success.** Priya is a Tenants UI maintainer in this source. Retain the detailed consumer success/failure steps and climax; acceptance, local-only, stale and unknown never become success.
- **UJ4 - Nora investigates and recovers a failed operation.** Retain the local incident-triage/recovery detail, audit phases, non-actionable readiness boundary and no blind retry.

Keep local named-protagonist flows for command investigation, deferred discovery, gated tenant creation/access, projection rebuild, topology, storage/snapshots, settings and deep links. Sample accepted submission remains its own supporting scenario: Alex's Sample counter flow must not be relabelled as **UJ1 - Alex adopts the domain-service SDK**, which is a different platform journey. **UJ2 - Morgan exposes a generated external API** and **UJ5 - Riley authorizes a release and consumer removal** similarly establish platform boundaries and traceability, not new Admin screens or release controls. A journey reference must not inflate the Admin IA.

The target Create Tenant flow must land on `/tenants`, show the reserved-input zero-request branch, and make valid creation conditional on the established mutation gates. Deferred discovery must explicitly account for legacy restore/import removal into the single unavailable owner. These changes resolve source conflicts while retaining the existing protagonists and operational vision where upstream names are not defined.

## Traceability and ownership routing

| Concern | Owning source story or role | UX responsibility |
|---|---|---|
| Deferred server/UI behavior | 7.4 | Exact unavailable copy; hidden/non-actionable controls; no fake work. |
| Canonical host/tabs/routes/palette | 7.14 | One host/module/route owner, including retained `/types` placement; legacy restore/import routing. |
| Shared typed Admin transport | 7.5 | Preserve typed authority/outcomes and keep URL/auth/error parsing outside pages. |
| Operational content, mutation states and critical journeys | 7.19 | Evidence display, conditional actions, attribution/audit, reserved/protected failure behavior. |
| Theme/accessibility/localization/responsive conformance | 7.20 | Full semantic/focus/live-region/locale/theme/viewport matrix without changing business meaning. |
| Reserved managed-tenant creation | 5.10, with 5.2 prerequisite | Gated Create Tenant and accessible zero-request validation; no UI-only authority. |
| Canonical public tenant scope and platform namespace migration | 2.14 / G-TENANT | No managed `system`, inferred wildcard or credential-derived scope. |
| Versioned MessageId status identity | 2.15 / G-STATUS-ID | Correct grammar, no correlation fallback, gateway `Location` ownership. |
| Contracts catalog schema/codec and activation | 5.12 / 5.13; Platform deployment owner | Safe generation/readiness summaries; no raw catalog internals or assumed activation. |
| Shared JWT/all-host conformance and ingress protection | 5.11 / G-AUTH-HOSTS; 5.14 | Honest denial/unavailability; enumerate only NFR1's data-free UI asset/callback exceptions; no fabricated login or blanket anonymous shell. UI hosts remain outside the MVP production profile pending human login. |
| SignalR distribution qualification | 2.13; transport owner | Authoritative refetch and honest unavailable/stale states across reconnects/outages. |
| Dapr operation inventory/qualification | 3.17; EventStore maintainer | No direct-provider recovery or guarantee inference. |
| Immutable release authority and consumer removal | 3.19 / G-PUBLICATION-AUTH; 3.20 / G-CONSUMER; respective owners | Safe identity/status only; no authority inferred from tags, packages, UI or tool reviews. |
| MVP crypto-shred claim boundary | 7.21 / NFR17-C5 | Bound wording and per-facet incomplete outcomes. |
| Production restore posture/drill | 7.22; Platform Operations/data owner | Keep Admin restore deferred; no RTO/RPO or recoverability claim. |
| Baseline reconciliation and digest renewal | 9.3 / G-BASELINE, after OR14 | Detailed UX can close its own source conflicts; epics pins/manifests remain stale until owner-bound renewal. |

The October proposals introduce no new UX-DR identifiers. Existing UX-DR1–42 ownership remains: 7.14 shell/routing, 7.19 operations, 7.20 conformance, 7.4 deferred behavior, and Epic 2 consumer flows. Do not turn this derived table into a competing FR/NFR primary-ownership register.

## Historical review inputs and artifact repair

The [September source-safety report](validation-report-source-safety-update-2026-09-09.md) identifies useful contract repairs: inconsistent aside/drawer detail-panel terminology; accessible persistent inline validation and focus; ownership/deduplication of polite versus critical announcements; canonical state IDs versus localized labels; safe identifier presentation; keyboard/reflow/text-spacing profiles; exactly one route heading; and unsafe `system` mock data. Resolve these during distillation through the inherited component contract and source-permitted primitives, without treating the old review as a new approval.

The existing mocks are illustrative. Remove reserved `system` tenant fixture data when refreshing the retained Overview reference. `CreateTenant` command evidence is consistent with the gated target and is no proof of provisioning delivery. Preserve the approved mock set; these corrections require no new brand/layout exploration.

`DESIGN.md` currently puts runtime authority in an extra Contract Scope section outside the canonical visual spine. Move that material to EXPERIENCE Foundation or evidence records while preserving the canonical visual section order. EXPERIENCE should retain an Inspiration & Anti-patterns section because the imported Fluent references trigger it, without copying their theme values or promoting old screenshots to version-bound conformance evidence.

## Unresolved upstream obligations

1. **Route manifest:** Story 7.14's exhaustive list, epics lines 6200–6202, still omits live `/types`. Retain its user-confirmed Streams & Events ownership and inner-tab policy in UX; upstream correction and a closed machine-validated manifest remain necessary. This reconciliation does not edit epics or claim that the omission is fixed.
2. **Baseline pins:** Epics records old PRD/architecture/detailed UX/`ux.md` hashes. PRD §11.3 also records historical digest facts. [Approved proposal §5.8](../../sprint-change-proposal-2026-10-07.md#58-no-digest-refresh), line 801, reserves renewal to Story 9.3 after substantive OR14 reconciliation and approval. Do not blindly repin epics, PRD, architecture or manifests after this UX Update. Architecture contains source references to UX but no detailed UX SHA table requiring a UX-side edit.
3. **Handoff/index truth:** At capture, `ux.md` and the folder index advertise final status and revision `23a722a1ffe29099a9d87df266552be4e3addd82`, while the detailed pair remains draft at `0994c37814c37dac7667a209dbd0659125aac49e`. This Update must align their status/revision/reconciliation links with the resulting pair, retaining historical reviews as historical. These downstream routers must not become hashed upstream authority inside the pair, which would create a hash cycle.
4. **Readiness:** Current PRD readiness remains blocked/reject; architecture stays draft with AD-26 target awaiting ratification. UX document finality cannot satisfy G-BASELINE, gate seals, implementation, release, deployment, migration or readiness. Corrective implementation remains bound to Story 9.1 authorization and Story 9.2 controls; approved planning reconciliation is not that implementation permission. Interactive UI hosts remain outside the canonical MVP production profile until human login exists and must be listed with that scope reason, never counted as NFR1-conformant production surfaces by omission.
5. **Anonymous exception propagation:** Current PRD NFR1 already settles the narrow data-free UI-host framework-asset/authentication-callback exception. AD-16 and architecture Implementation Status still retain the probes-only/pending-product-decision text. Reconcile architecture upstream to the current PRD; this is no longer a new UX product choice. The concurrent approved supplement aligned epics NFR1 and Story 9.2, without updating its stale digest pins or delivering enforcement.
6. **Further product/architecture decisions:** Production broker/profile/issuer, catalog operation-vocabulary ownership, full erasure and quantified UX performance remain with their named upstream owners. UX uses unavailable/unknown/safe disposition until their gates pass and invents no numeric horizon, budget, provider guarantee or runnable operation.

## Exact input snapshot

SHA-256 values below hash the complete local file bytes, without normalization. They are observation evidence, not approval or new pins. Re-hash affected inputs if concurrent work changes their bytes. The epics row records the final 13:51 UTC source correction; its prior 13:47 UTC digest was `a7474fc1edce5212689eed383e4424683ce5e821e08ff7b69cb99e082a19dc03`. That change propagates NFR1's already authoritative asset/callback exception and the transition-seal mechanics, and adds no UI route, visual direction or runnable capability. The last four rows record downstream pre-update state only and are not added as upstream sources of the UX pair.

| Repository-relative path | SHA-256 |
|---|---|
| `docs/brownfield/architecture.md` | `3cddf6eb593fb28d90b8dd9d54562cb28bb4ea2a4f5f15c6a51b77f06bf5a0a3` |
| `_bmad-output/planning-artifacts/prd.md` | `d5632ba71c838ba7f0b8ca61a24889cb21edb4506531e823d8c0dfb12b7e6ae6` |
| `_bmad-output/planning-artifacts/architecture.md` | `7fd805a871883a7594b9df89c9146b1e84a42a86ddab5d6ca500fbe13387fc09` |
| `_bmad-output/planning-artifacts/epics.md` | `0697679b32390edf4d8719e5e1423e8dcbd77d01c6fc7ba50f2d754e11d1565f` |
| `_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md` | `e50d939cc701d9484ce0d23ebcab68a6b76f143ee7f0cd9ebc2d2c1fe9169575` |
| `_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-07.md` | `ab5d5cd4dbdccf7be29e5f470d70c36326d31feab553155a16ade9947baf68b5` |
| `_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-07-architecture-routing.md` | `6855e82e14cd213022c2df80029fda4455fe81ae2a56efeed9f85f41a423c39c` |
| `_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-07-story-9-2-nfr1.md` | `2958068c2c0d5e75c33bc8db12994c8d79cce8beb67b7f1ed167841d12b9521e` |
| `_bmad-output/planning-artifacts/implementation-readiness.md` | `60ea053c326b35ed2ec29aa93ad1b6fdd2c5c8a3e45eee8064822de959c8a249` |
| `_bmad-output/implementation-artifacts/story-6-6-dapr-only-amendment.md` | `2c1dc2e80d032a7f1622ab6f00057e9c0298ec2a1114df23e74a1ce28f5feaef` |
| `_bmad-output/implementation-artifacts/story-6-6-trusted-code-amendment.md` | `2eb79903da2157747aab5833e99826dd636b02ca2c080b4436c643912a90d0b7` |
| `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/validation-report-source-safety-update-2026-09-09.md` | `70061782cf64c6b4e22d5ff9ea78ca5f5707b880221ad63ccc6c0c9b6b0fa9e2` |
| `_bmad-output/planning-artifacts/ux.md` | `4e8eb907b15d5b5badc72b5ec70f93c3671deeb624e80fa33aec868847ef766c` |
| `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/index.md` | `29a8cc558bdef2c91b4cc55ae373a618315097f03d38fb7cd6042819353f1ef9` |
| `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md` | `3f4f0181ea24b5ed7544b6cb8482cd73b1aadba7ddbef47fd135e080a2d8365d` |
| `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md` | `11f754031cb5f7f8c376787573c32ba9ac0c68c4718e6b4b86281f579e8c9cab` |

Four of the five source hashes in the pre-update EXPERIENCE table differ; brownfield architecture still matches. Unlike the previous September epics repin, the current change contains substantive requirement, authority, owner, journey and safety evolution. Metadata changes alone are not used as justification for a new UX behavior.

The concurrent supplement's final approval/application record was rehashed at 13:53 UTC. It confirms the two exact epics replacements and unchanged story IDs, input pins, backlog state and readiness verdict; it introduces no further UX semantic change beyond the NFR1/transition rules above.

## Validation boundary

This extraction reread the bound sources, prior UX decisions, current source-safety report and declared source amendments, and inspected the relevant legacy controls. No qualitative UX, PRD, architecture, browser, accessibility or implementation validation was rerun. Local source/link/hash checks can verify this evidence record's integrity, but cannot establish delivery or a fresh review verdict.
