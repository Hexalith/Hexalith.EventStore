---
created: 2026-07-26
story_id: "2.6"
story_key: 2-6-tenants-ui-client-library-alignment-and-ux-evidence
artifact: focused-ux-acceptance-review
reviewer: Sally (UX Designer)
tenants_sha: 11d69920526f9881ad8c2216b28e82e497543c67
tenants_describe: v3.2.18-33-g11d6992
outcome: approved-with-one-deferred-finding
---

# Story 2.6 — Focused UX Acceptance Review (Sally)

Reviewed at Hexalith.Tenants `11d6992` (working tree clean, commit reachable on `origin/main`;
superproject gitlink already points at it). This closes the **UX half** of AC3. It does **not**
close the Tenants maintainer approval gate, which is a separate signature.

## 2026-07-27 Status Amendment (Administrator; not Sally)

This artifact remains the immutable record of Sally's focused review of published baseline `11d6992`
and its explicitly narrowed scope. The 2026-07-27 code-review patches supersede that scope by adding
distinct `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` presentation. Those uncommitted changes
were not reviewed by Sally; this artifact must not be used as their UX approval. Story 2.6 therefore
requires a fresh focused UX decision after a final Tenants SHA exists.

## Scope

Per the **D3 owner decision (2026-07-26)**, Story 2.6's UX obligation is narrowed to the lifecycle
states `ReadModelFreshnessState` can actually express — `Current`, `Stale`, `Unknown` — plus the
denied / loading / unavailable **surface** states the UI already owns. `Rebuilding`, `Degraded`,
`Unavailable`, and `LocalOnly` are not representable at lifecycle level; that platform prerequisite
is deferred to Hexalith.EventStore and is out of scope here.

Per the **ratified 2.11 overlap**, provenance preservation, authoritative lifecycle/header
selection, and the fail-closed `Unknown` fallback are cited from Story 2.11, not signed off here.
This review covers only the *presentation* of an already-classified state.

Authority for the rules applied: `references/Hexalith.AI.Tools/hexalith-ux-instructions.md`.

## Surfaces reviewed

| Surface | File |
| --- | --- |
| Lifecycle badge | `src/Hexalith.Tenants.UI/Components/Shared/TruthStateBadge.razor` (+ `.css`) |
| List/empty/denied/error surfaces | `src/Hexalith.Tenants.UI/Components/Shared/ListSurfaceStates.razor` (+ `.css`) |
| Mutation denial policy | `src/Hexalith.Tenants.UI/State/TenantDetail/TenantLifecycleAvailability.cs` |
| Canonical copy (EN/FR) | `src/Hexalith.Tenants.UI/Resources/TenantsResources[.fr].resx` |

## Verdict per rule

### 1. Component sources — **PASS**

`FluentBadge` and `FluentButton` (Fluent UI Blazor V5) are used throughout; the host composes
through `Hexalith.FrontComposer.Shell`. No third-party or hand-rolled widget substitutes for an
existing Fluent primitive on these surfaces.

### 2. No theme redefinition, no legacy tokens — **PASS on the reviewed surfaces**

Colour is expressed **only** through `BadgeColor` component roles
(`Success` / `Warning` / `Severe` / `Important` / `Informative`) — never a literal. The two
stylesheets backing these surfaces total 23 lines and contain no typography ramp, no `color:`
role, and no Fluent v4 / FAST token. Both non-layout rules in `ListSurfaceStates.razor.css` carry a
documented `fc-css-exception` marker, as the module's conformance guard requires.

> A legacy-token violation **does** exist elsewhere in the UI. It is outside this story's diff and
> outside the lifecycle surfaces — see *Deferred finding* below.

### 3. State is never encoded by colour alone — **PASS** (WCAG 1.4.1)

Every lifecycle state carries three independent channels: a distinct `Size20` icon, a localized
text label, and the badge colour role.

| State | Colour role | Icon | EN | FR |
| --- | --- | --- | --- | --- |
| `Current` | `Success` | `Checkmark` | Current | Actuel |
| `Aging` | `Warning` | `Clock` | Aging | Vieillissant |
| `Stale` | `Severe` | `ClockAlarm` | Stale | Périmé |
| `Unknown` | `Important` | `QuestionCircle` | Unknown | Inconnu |
| *Refreshing* (transient flag) | `Informative` | `ArrowClockwise` | Refreshing | Actualisation |

Pinned by `TruthStateBadgeTests.Freshness_uses_locked_semantics_and_size20_icons`
(`Components/TruthStateBadgeTests.cs:19-40`), which asserts the colour role, the icon **type**, and
the icon **size** per state.

`Aging` has a complete canonical treatment but is unreachable from `ResolveFreshness` today. That
is the existing D6 read-model-freshness handoff plus the ledger's `Aging` mutation-gate entry — the
presentation layer is ready for the platform work and needs no UX change.

### 4. High-contrast / forced-colors — **PASS**

`ListSurfaceStates.razor.css` declares `@media (forced-colors: active)` and pins the card border to
the `CanvasText` system colour, so the state card keeps its boundary when the theme is overridden.

### 5. Accessible announcement and focus — **PASS**

The division of responsibility is deliberate and correct:

- The **badge** carries **no** ARIA role for a static state (asserted null at
  `TruthStateBadgeTests.cs:39`) and `role="status"` **only** while the transient refresh flag is
  set (asserted at `:61`). A badge is not a live region; it must not chatter on every re-render.
- The **surface** owns the announcement: `ListSurfaceStates` maps `Error` and `Degraded` to
  `role="alert"` + `aria-live="assertive"`, and every other state to `role="status"` +
  `aria-live="polite"` (`ListSurfaceStates.razor:63-65`).
- `IconLabel` is bound to the same string as the visible label, and the test asserts that equality
  (`TruthStateBadgeTests.cs:38`) — so the icon cannot announce something different from what is
  read on screen.
- Each state card leads with a semantic `<h2>` and offers exactly the one recovery affordance its
  state warrants (`Reset` on `FilteredEmpty`, `Refresh` on `Stale`), not a generic button row.

### 6. Support-safe copy — **PASS**

All seven list-state strings were read in both cultures. None leaks a stack trace, exception type,
internal host name, or store key; each names the operator-visible situation and the next action.
Resource parity is exact — **1184 EN keys / 1184 FR keys, zero missing, zero orphaned** — and the
French strings are properly accented (*Périmé*, *identité*, *n'est pas authentifiée*).

Note the deliberate distinction the copy preserves: `Empty` reads *"This is an authorized empty
result, not a failure"* while `Unauthorized` reads *"Sign in required"*. An authorized-but-empty
result must never be presented as a denial, and here it isn't.

### 7. Fail-closed denial UX — **PASS**

`TenantLifecycleAvailabilityInput.Evaluate` blocks every lifecycle mutation when freshness is
`Stale` **or** `Unknown`, and when the surface is `Unauthorized`, `Unavailable`, `Unknown`, or
`Degraded` (`TenantLifecycleAvailability.cs:42-52`). Each denial returns a localized
`SafeMessageKey`, a `FocusTarget` so keyboard focus lands on the remedy (`Refresh` for staleness,
`Lifecycle` for permission), and an explicit `LiveRegionPoliteness` (`Assertive` for a block,
`Polite` for an expected same-state rejection). Denial is a designed state here, not a disabled
button with no explanation.

This is also where AC3's `Degraded` and `Unavailable` obligations are honestly met **at the surface
layer** even though the lifecycle enum cannot express them: both surface kinds have canonical
copy, canonical treatment, and fail-closed mutation blocking.

### 8. Page-section accordion rule — **PASS**

Six page-like surfaces group their sibling titled sections in `FluentAccordion`
(`TenantsWorkspace`, `TenantDetailPage`, `TenantAuditPage`, `GlobalAdministratorsPage`,
`TenantConfigurationView`, `UserMembershipLookupPanel`). `PageLayoutDeclarationTests` pins the
workspace layout declaration inside the FrontComposer shell.

## Render evidence

Built at Tenants `11d6992`, Debug source mode
(`-p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false`)
— **Build succeeded, 0 warnings, 0 errors**.

| bUnit class | Result |
| --- | --- |
| `TruthStateBadgeTests` | 5/5 |
| `TenantLifecycleActionAvailabilityTests` | 12/12 |
| `TenantListSurfaceTests` | 43/43 |
| `TenantDetailSurfaceTests` | 56/56 |
| `MyTenantsSurfaceTests` | 14/14 |
| `GlobalAdministratorsPageTests` | 29/29 |
| `SupportSafeCopyButtonTests` | 43/43 |
| `PageLayoutDeclarationTests` | 2/2 |
| **Focused UX subtotal** | **204/204** |
| Full `Hexalith.Tenants.UI.Tests` assembly | **1060/1060**, 0 failed, 0 skipped |

## Deferred finding (1) — outside Story 2.6's diff

**[MEDIUM] Four legacy Fluent v4 / FAST tokens survive in three UI stylesheets, so their "accent"
treatment never tracks the active theme.**

`hexalith-ux-instructions.md` forbids `--accent-*` and `--neutral-foreground-*` outright: they
belong to the previous major version and do not resolve under Fluent V5. Each occurrence therefore
always falls through to its system-colour fallback, which means the callouts render `LinkText` /
`GrayText` in every theme and the intended accent is silently absent.

- `Components/Pages/TenantAuditPage.razor.css:20` — `var(--accent-stroke-rest, LinkText)`
- `Components/Pages/TenantAuditPage.razor.css:27` — `var(--neutral-foreground-hint, GrayText)`
- `Components/Tenants/Members/RemoveTenantMemberFlow.razor.css:22` — `var(--accent-fill-rest, LinkText)`
- `Components/Tenants/Configuration/SetTenantConfigurationFlow.razor.css:49` — `var(--accent-fill-rest, LinkText)`

All four carry an `fc-css-exception` marker, but each marker justifies the **layout** (border and
padding Fluent has no primitive for) — none of them declares or justifies the **token** choice, so
the module's conformance guard passes them while the theme-tracking rule is still broken. The UX
instruction's own escape hatch requires exactly this: files still using legacy tokens must be
tracked as an explicit, allowlisted migration backlog rather than silently exempted.

**Not attributable to Story 2.6.** None of the three files is in this story's File List, its
production change is 29 lines confined to `TenantQueryGateway.cs`, and none of the lifecycle-state
surfaces this story governs is affected — `TruthStateBadge` and `ListSurfaceStates` are clean.
Recorded in `deferred-work.md`; owned by the Hexalith.Tenants maintainer.

## Outcome

**UX acceptance: approved** for the narrowed D3 scope. The canonical support-safe and accessible
treatment for `Current`, `Stale`, and `Unknown`, and for the denied / loading / unavailable surface
states, is implemented, localized in both cultures, and pinned by 204 passing render tests.

Remaining AC3 gate: **Tenants maintainer approval** of the exact SHA. That signature is not Sally's
to give.
