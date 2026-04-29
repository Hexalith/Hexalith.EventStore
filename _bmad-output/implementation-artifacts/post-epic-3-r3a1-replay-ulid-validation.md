# Post-Epic-3 R3-A1: Replay ULID Validation

Status: done

## Story

As a platform operator,
I want replay correlation IDs to follow the platform correlation ID contract instead of GUID-only validation,
So that replay accepts archived commands whose correlation IDs are ULID or other non-GUID platform IDs.

## Acceptance Criteria

1. Replay no longer rejects non-GUID correlation IDs solely because they are not GUIDs.
2. Replay still rejects empty or whitespace correlation IDs with HTTP 400.
3. Existing replay behavior for tenant authorization, archive lookup, status validation, and replay submission remains unchanged.
4. Focused replay controller tests pass.

## Implementation Notes

- Replaced the replay path parameter `Guid.TryParse` check with a whitespace-only validation guard.
- Updated the 400 detail text from GUID-specific wording to a platform-neutral empty/whitespace validation message.
- Added a regression test proving a non-GUID ULID-shaped correlation ID can replay successfully.
- Added a regression test proving whitespace correlation IDs still return HTTP 400.

## File List

- `src/Hexalith.EventStore/Controllers/ReplayController.cs`
- `tests/Hexalith.EventStore.Server.Tests/Commands/ReplayControllerTests.cs`
- `_bmad-output/implementation-artifacts/post-epic-3-r3a1-replay-ulid-validation.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Verification

Command:

```powershell
dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter FullyQualifiedName~ReplayControllerTests
```

Result:

- Passed: 21
- Failed: 0
- Skipped: 0

## Change Log

- 2026-04-28: Removed replay GUID-only validation and added focused replay regression tests.
