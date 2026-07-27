---
created: 2026-07-27
story_id: "2.6"
story_key: 2-6-tenants-ui-client-library-alignment-and-ux-evidence
artifact: focused-ux-acceptance-review
reviewer: Sally (UX Designer)
tenants_sha: 55e6000a41e7846868ff7512b79e5f7a36464a37
tenants_describe: v3.2.18-34-g55e6000
outcome: approved-with-deferred-out-of-scope-finding
---

# Story 2.6 — Focused UX Acceptance Review (Sally)

Reviewed at Hexalith.Tenants `55e6000a41e7846868ff7512b79e5f7a36464a37` with a clean
Tenants working tree. The commit is published at `origin/main`, is authored and committed by the
administrator `jpiquot`, and is the exact gitlink consumed by Hexalith.EventStore.

## Scope and ownership boundary

This review covers Story 2.6's typed-client/UI-host boundary and the canonical accessible
presentation of already-classified lifecycle evidence. Story 2.11 remains the exclusive owner of
provenance preservation, authoritative lifecycle/header classification, fail-closed `Unknown`
selection, gateway-consumer implementation, and persisted/real-gateway proof. This review cites
those behaviors and does not sign them off.

The UX rules applied are `references/Hexalith.AI.Tools/hexalith-ux-instructions.md`.

## Verdict

### Component sources — PASS

`TruthStateBadge` renders lifecycle evidence with Fluent UI V5 `FluentBadge` and Size20 Fluent
icons. The five operator surfaces reuse that shared component: tenant detail, tenant list,
memberships, global administrators, and audit. The host retains its FrontComposer composition and
does not introduce a second MVC/API surface.

### Honest lifecycle presentation — PASS

`ProjectionLifecycleState` is transported independently from freshness. `Rebuilding`, `Degraded`,
`Unavailable`, and `LocalOnly` therefore remain visible as distinct localized states rather than
collapsing into `Unknown`. A current lifecycle preserves the more specific freshness threshold,
including `Aging`; the transient refreshing flag remains a separate status presentation.

State is not encoded by color alone. Every operational lifecycle badge includes a localized text
label, an accessible label/icon label, a Size20 icon, and a Fluent semantic color role. The focused
bUnit theory renders and asserts all four newly representable states.

| Lifecycle | Fluent role | Icon | EN | FR |
| --- | --- | --- | --- | --- |
| `Rebuilding` | `Informative` | `ArrowClockwise` | Rebuilding | Reconstruction en cours |
| `Degraded` | `Warning` | `ClockAlarm` | Degraded | Dégradé |
| `Unavailable` | `Severe` | `QuestionCircle` | Unavailable | Indisponible |
| `LocalOnly` | `Important` | `Clock` | Local only | Local uniquement |

### Support-safe action denial — PASS

Every authoritative non-current lifecycle (`Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and
`LocalOnly`) blocks lifecycle mutations even when legacy freshness is `Current`. The UI supplies
localized support-safe copy, keeps the operator in read-only mode, and directs focus to refresh the
projection evidence. Story 2.11 retains ownership of the separate `Unknown` provenance-selection
policy.

### Localization and theme conformance — PASS for Story 2.6 surfaces

English and French resources have exact parity at 1,189 keys each. Story-owned surfaces contain no
hard-coded colors and no legacy Fluent v4/FAST tokens. The lifecycle implementation adds no custom
CSS or theme redefinition.

## Fresh validation at the reviewed SHA

- Debug/source build: succeeded with 0 warnings and 0 errors after the prescribed serialized retry
  for a transient output-file lock.
- Lifecycle badge/action tests: 31/31 passed.
- Structural UI composition tests: 29/29 passed.
- Gateway transport evidence cited from Story 2.11: 191/191 passed.
- Focused UX render classes: 209/209 passed.
- Full UI assembly: 1,090/1,090 passed in both Debug/source and Release/package modes.
- Release/package UI build: succeeded with 0 warnings and 0 errors.
- Full configured unit regression lane: 1,473/1,473 passed.
- Server regression lane: 738/738 passed.
- DAPR/Aspire integration lane: 167 passed, 1 scheduled performance test skipped, 0 failed.
- Root and Tenants working-tree whitespace checks: recorded with the story completion update.

## Deferred finding — outside Story 2.6

Six legacy Fluent v4/FAST token occurrences remain in four stylesheets outside Story 2.6's owned
surfaces. They are not introduced or consumed by this lifecycle-state presentation and remain
separately deferred; they do not change this review's outcome.

## Outcome

**UX acceptance: approved** for Hexalith.Tenants
`55e6000a41e7846868ff7512b79e5f7a36464a37`. The committed implementation provides canonical,
localized, non-color-only presentation for the supplied lifecycle states across all five required
operator surfaces and fails closed for non-current lifecycle actions. With the separately verified
maintainer authority chain for this exact SHA, Story 2.6 may advance to `review`.
