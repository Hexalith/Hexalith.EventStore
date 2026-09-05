---
status: blocked
---

# BMad Build Auto Result

Status: blocked
Blocking condition: dirty working tree
Command: `git add --refresh -- . && git status --short --branch`
Observed dirty paths:

- `references/Hexalith.FrontComposer`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorInfrastructureFailureTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs`
