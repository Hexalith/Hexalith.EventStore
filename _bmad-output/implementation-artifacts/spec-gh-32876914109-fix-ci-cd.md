---
title: 'Fix CI: declare bUnit renderer info for FluentUI v5'
type: 'bugfix'
created: '2026-08-25'
status: 'done'
route: 'one-shot'
---

# Fix CI: declare bUnit renderer info for FluentUI v5

## Intent

**Problem:** `main` CI has failed on every run since 2026-08-22 with the same 24 `MissingRendererInfoException` failures in `Hexalith.EventStore.Admin.UI.Tests`; the `Hexalith.Builds` bump to FluentUI `5.0.0-rc.5-26219.1` made `FluentLayoutHamburger` read `ComponentBase.RendererInfo`, which bUnit leaves unset by default.

**Approach:** Declare the interactive Server renderer once from the shared `AdminUITestContext` by overriding every bUnit render entry point, applied lazily on first render because touching `BunitContext.Renderer` seals `Services` that derived contexts and tests still register into.

## Suggested Review Order

- The whole fix: one lazy latch, so `Services` stays open for derived contexts.
  [`AdminUITestContext.cs:161`](../../tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs#L161)

- `override`, not `new` — bUnit's own overloads dispatch virtually into this one.
  [`AdminUITestContext.cs:118`](../../tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs#L118)

- Hiding `SetRendererInfo` latches too, so a test's explicit choice survives.
  [`AdminUITestContext.cs:146`](../../tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs#L146)

- Seam for the static prerender pass; production runs `InteractiveServer`.
  [`AdminUITestContext.cs:114`](../../tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs#L114)

- Honest note: this line is real insurance but no test can go red on it.
  [`AdminUITestContext.cs:132`](../../tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs#L132)

- Guards the lazy property itself — fails if the latch moves to the constructor.
  [`AdminUITestContextRendererInfoTests.cs:23`](../../tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContextRendererInfoTests.cs#L23)

- Guards the entry point that no existing test exercised.
  [`AdminUITestContextRendererInfoTests.cs:44`](../../tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContextRendererInfoTests.cs#L44)

- Guards the no-clobber contract; fails against the pre-review behaviour.
  [`AdminUITestContextRendererInfoTests.cs:51`](../../tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContextRendererInfoTests.cs#L51)
