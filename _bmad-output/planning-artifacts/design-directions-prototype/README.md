# Hexalith.EventStore Design Directions — ARCHIVED PROTOTYPE

**Archived:** 2026-04-15 (Story 21-10, Epic 21 closeout).

This folder is an **archived prototype** and is **not** part of `Hexalith.EventStore.slnx`. The code targets Fluent UI Blazor **v4** and is frozen reference material for the Epic 21 design decisions. Do not attempt to build this project against Fluent UI Blazor v5 — the v4 API surface (e.g., `Appearance.Accent`, `MessageIntent`, `FluentProgress`, `FluentProviders @rendermode`) intentionally remains here for historical comparison with the shipped `samples/Hexalith.EventStore.Sample.BlazorUI/` project.

## Do not edit this prototype in-place

If you need to revive it, create a new story to unfreeze and bring it to current package versions **before** making any code change here. In-place edits against v5 will produce inconsistent state and silently break the historical reference.

## Scope reference

- Sprint change proposal: [`../sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md`](../sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md)
- Epic 21 plan: [`../epics.md`](../epics.md) (search for "Epic 21")
- Closing story: [`../../implementation-artifacts/21-10-sample-blazorui-alignment.md`](../../implementation-artifacts/21-10-sample-blazorui-alignment.md)
