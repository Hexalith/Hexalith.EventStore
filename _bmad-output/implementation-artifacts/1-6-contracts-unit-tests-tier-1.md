# Story 1.6: Contracts Unit Tests (Tier 1)

Status: done

## Story

As a **developer**,
I want comprehensive unit tests for the Contracts package covering EventEnvelope creation, AggregateIdentity derivation, CommandStatus transitions, and IRejectionEvent detection,
So that the foundational types are validated and regression-protected before any dependent code is built.

## Acceptance Criteria

1. **EventEnvelope construction** - Given the Contracts.Tests project exists, When I run `dotnet test` on Contracts.Tests, Then EventEnvelope construction with all 11 fields is validated.

2. **AggregateIdentity derivation** - AggregateIdentity correctly derives actor ID, event stream key, pub/sub topic, and queue session from `tenant:domain:aggregate-id`.

3. **AggregateIdentity validation** - AggregateIdentity rejects malformed identity tuples (missing components, empty strings, injection characters).

4. **IRejectionEvent detection** - IRejectionEvent marker interface is correctly detected on implementing types.

5. **Zero DAPR dependency** - All tests pass with zero DAPR dependency.

## Tasks / Subtasks

### Task 1: Verify existing test coverage (AC: #1, #2, #3, #4, #5)

- [x] 1.1 Verify EventEnvelope tests cover all 11 metadata fields construction and validation
- [x] 1.2 Verify EventMetadata tests cover sequence number validation and 11-field property access
- [x] 1.3 Verify AggregateIdentity tests cover ActorId, EventStreamKeyPrefix, MetadataKey, SnapshotKey, PubSubTopic, QueueSession derivations
- [x] 1.4 Verify AggregateIdentity tests cover null/empty/whitespace, invalid characters, length enforcement, lowercase normalization
- [x] 1.5 Verify IRejectionEvent/IEventPayload interface hierarchy tests exist
- [x] 1.6 Verify DomainResult tests cover success, rejection, no-op, and mixed event type rejection
- [x] 1.7 Verify CommandStatus enum tests cover value count, explicit integers, and lifecycle order
- [x] 1.8 Verify CommandStatusRecord tests cover all terminal and non-terminal states
- [x] 1.9 Verify IdentityParser tests cover Parse, TryParse, and ParseStateStoreKey
- [x] 1.10 Verify CommandEnvelope tests cover constructor validation, AggregateIdentity derivation, extensions defensive copy

### Task 2: Run tests and verify results (AC: #5)

- [x] 2.1 Run `dotnet test` — 147 tests pass, zero errors, zero warnings
- [x] 2.2 Verify no DAPR runtime dependency in test project (only references Contracts and Testing packages)

## Dev Notes

### Implementation Notes

Story 1.6 tests were implemented incrementally during Stories 1.2-1.4 as part of TDD development. Each story added comprehensive tests for the types it introduced:

- **Story 1.2** (Contracts Package): Created EventEnvelopeTests, EventMetadataTests, EventPayloadTests, AggregateIdentityTests, IdentityParserTests, CommandStatusTests, CommandStatusRecordTests, CommandEnvelopeTests, DomainResultTests
- **Story 1.4** (Testing Package): Added additional test infrastructure via Testing package (builders, assertions)

### Test Coverage Summary

| Test File | Test Count | Coverage |
|-----------|-----------|----------|
| AggregateIdentityTests | 31 | Constructor validation, key derivation, format validation, length limits, character restrictions |
| DomainResultTests | 13 | Success/rejection/no-op states, mixed type rejection, immutability |
| CommandEnvelopeTests | 11 | Constructor validation, AggregateIdentity derivation, defensive copying |
| EventEnvelopeTests | 10 | Constructor validation, extensions handling, record equality, byte[] comparison |
| IdentityParserTests | 11 | Parse/TryParse/ParseStateStoreKey, round-trip, invalid input handling |
| EventMetadataTests | 6 | 11-field constructor, property count, sequence validation, equality |
| EventPayloadTests | 6 | Marker interface hierarchy, IRejectionEvent detection |
| CommandStatusRecordTests | 5 | Terminal/non-terminal status records |
| CommandStatusTests | 3 | Enum value count, explicit integers, lifecycle order |
| **Total** | **147** | **All Contracts public API surface covered** |

### Key Testing Patterns

- **Eager validation**: All constructors throw on invalid input — tests verify every validation path
- **Defensive copying**: Extensions dictionaries are copied — tests verify mutation isolation
- **Record equality**: Tests verify value equality semantics for record types
- **Identity derivation**: Tests verify all 6 derived key formats from canonical tuple
- **Interface hierarchy**: Tests verify IEventPayload / IRejectionEvent marker interface detection
- **Boundary testing**: Max lengths, single characters, edge cases for all identity components

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Completion Notes List

- All 147 Contracts.Tests pass with zero errors and zero warnings
- Tests were implemented during Stories 1.2-1.4 as part of TDD workflow
- No additional tests needed — coverage is comprehensive across all acceptance criteria
- Zero DAPR dependency verified (test project references only Contracts and Testing packages)

### Change Log

- 2026-02-13: Verified Story 1.6 — all tests already implemented and passing (147 tests)

### File List

- `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandEnvelopeTests.cs` — Verified: 11 tests
- `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs` — Verified: 3 tests
- `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusRecordTests.cs` — Verified: 5 tests
- `tests/Hexalith.EventStore.Contracts.Tests/Events/EventEnvelopeTests.cs` — Verified: 10 tests
- `tests/Hexalith.EventStore.Contracts.Tests/Events/EventMetadataTests.cs` — Verified: 6 tests
- `tests/Hexalith.EventStore.Contracts.Tests/Events/EventPayloadTests.cs` — Verified: 6 tests
- `tests/Hexalith.EventStore.Contracts.Tests/Identity/AggregateIdentityTests.cs` — Verified: 31 tests
- `tests/Hexalith.EventStore.Contracts.Tests/Identity/IdentityParserTests.cs` — Verified: 11 tests
- `tests/Hexalith.EventStore.Contracts.Tests/Results/DomainResultTests.cs` — Verified: 13 tests
