# Story 17.2: Stream Subcommand — Query, List Events, State

Status: done

Size: Medium — ~8 new files, 5 task groups, 11 ACs, ~26 tests (~8-10 hours estimated). Replaces the `stream` stub from story 17-1 with six sub-subcommands (`list`, `events`, `event`, `state`, `diff`, `causation`) that call the existing Admin API stream endpoints. Reuses output formatting, `AdminApiClient`, `GlobalOptionsBinding`, and exit code infrastructure from 17-1.

**Dependency:** Story 17-1 must be complete. This story builds on the CLI scaffold, global options, output formatting, `AdminApiClient`, and exit code conventions established there.

## Definition of Done

- All 11 ACs verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- All six `stream` sub-subcommands return formatted output with correct exit codes
- `eventstore-admin stream list` returns a table of recently active streams
- `eventstore-admin stream event <tenant> <domain> <id> <seq>` returns event detail JSON

## Story

As a **platform operator or CI/CD pipeline engineer**,
I want **`eventstore-admin stream` sub-subcommands to list streams, browse event timelines, inspect individual events, reconstruct aggregate state at a position, diff state between two positions, and trace causation chains**,
so that **I can diagnose event sourcing issues, verify aggregate behavior, and script stream inspection in CI/CD pipelines without needing the Web UI (FR79, FR80, NFR42)**.

## Acceptance Criteria

1. **`stream list` subcommand** — `eventstore-admin stream list [--tenant <id>] [--domain <name>] [--count <n>]` calls `GET /api/v1/admin/streams?tenantId={t}&domain={d}&count={n}` and displays a table of `StreamSummary` records. Default count: 1000. Columns: Status, Tenant, Domain, AggregateId, Events, Last Sequence, Last Activity, Snapshot. Exit code `0` on success, `2` on error. Empty result set returns exit code `0` with "No streams found." message to stderr. Note: this endpoint always returns `PagedResult<StreamSummary>` (never 404) — an empty `Items` list means no matching streams.

2. **`stream events` subcommand** — `eventstore-admin stream events <tenant> <domain> <aggregateId> [--from <seq>] [--to <seq>] [--count <n>]` calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/timeline` and displays a table of `TimelineEntry` records. Default count: 100. Columns: Seq, Timestamp, Type (Command/Event/Query), TypeName, CorrelationId, UserId. Three positional arguments (tenant, domain, aggregateId) are required — missing arguments produce `System.CommandLine`'s built-in error with usage help. Note: this endpoint always returns `PagedResult<TimelineEntry>` (never 404) — an empty `Items` list means no timeline entries. The server may cap `count` at `AdminServerOptions.MaxTimelineEvents`; the pagination hint (AC 8) handles this transparently.

3. **`stream event` subcommand** — `eventstore-admin stream event <tenant> <domain> <aggregateId> <sequenceNumber>` calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/events/{sequenceNumber}` and displays `EventDetail`. For `table` format: key/value pairs (Tenant, Domain, AggregateId, Seq, EventType, Timestamp, CorrelationId, CausationId, UserId, PayloadJson) — use explicit `ColumnDefinition` list with `MaxWidth = 80` for the PayloadJson property to prevent arbitrarily long JSON from blowing out the table layout; the full payload is visible in `--format json`. For `json` format: full `EventDetail` serialized as JSON. For `csv` format: single-row CSV with all fields. HTTP 404 prints "Event not found at sequence {seq} in stream {tenant}:{domain}:{aggregateId}." to stderr, exit code `2`.

4. **`stream state` subcommand** — `eventstore-admin stream state <tenant> <domain> <aggregateId> --at <sequenceNumber>` calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/state?sequenceNumber={seq}` and displays `AggregateStateSnapshot`. `--at` is a required option (not positional). For `table` format: key/value pairs (Tenant, Domain, AggregateId, Seq, Timestamp, StateJson) — use explicit `ColumnDefinition` list with `MaxWidth = 80` for StateJson (same rationale as AC 3 PayloadJson; full state visible via `--format json`). For `json` format: full snapshot as JSON. HTTP 404 prints "Aggregate state not found at sequence {seq}." to stderr, exit code `2`.

5. **`stream diff` subcommand** — `eventstore-admin stream diff <tenant> <domain> <aggregateId> --from <seq> --to <seq>` calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/diff?fromSequence={from}&toSequence={to}` and displays `AggregateStateDiff`. Table format columns: FieldPath, OldValue, NewValue. `--from` and `--to` are required options. HTTP 404 prints "Aggregate state not found for the specified range." to stderr, exit code `2`.

6. **`stream causation` subcommand** — `eventstore-admin stream causation <tenant> <domain> <aggregateId> --at <sequenceNumber>` calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/causation?sequenceNumber={seq}` and displays `CausationChain`. For `table` format, two sections (same dual-format pattern as `health` command):
   - **Section 1 — Chain overview:** Key/value pairs: Originating Command Type, Originating Command ID, Correlation ID, User ID, Event Count, Affected Projections (comma-separated list).
   - **Section 2 — Events table:** Columns: Seq, EventType, Timestamp.
   For `json`: full `CausationChain` serialized. For `csv`: events table only.

7. **Positional arguments** — `tenant`, `domain`, and `aggregateId` are `Argument<string>` (positional, required) on all sub-subcommands except `list`. `sequenceNumber` is `Argument<long>` (positional, required) on `event`. `--at`, `--from`, `--to` are `Option<long>` (named, required where specified). Missing required arguments/options produce `System.CommandLine`'s built-in error message and usage help.

8. **Paginated results** — `stream list` and `stream events` return `PagedResult<T>`. Display `Items` collection via `FormatCollection`. When `TotalCount > Items.Count`, print "Showing {Items.Count} of {TotalCount} results." to stderr (no specific guidance on how to get more — the server may cap results via `AdminServerOptions.MaxTimelineEvents` independently of `--count`). `ContinuationToken` is not exposed in CLI v1 (future paging story).

9. **Error handling** — All sub-subcommands reuse `AdminApiClient` error handling from story 17-1 for HTTP 401/403/5xx, connection refused, timeout, and JSON deserialization failures. For HTTP 404 specifically: `AdminApiClient` must expose a way for command handlers to detect 404 separately from other errors (e.g., a `TryGetAsync<T>` returning `(T? result, bool notFound)`, or throwing a typed `NotFoundException` that handlers catch). This allows subcommand-specific 404 messages (ACs 3-6) instead of the generic "Endpoint not found" message from story 17-1. Note: `stream list` and `stream events` never receive 404 (they return empty `PagedResult`). All errors go to stderr, exit code `2`.

10. **Test coverage** — Unit tests cover: each sub-subcommand's output formatting (table, JSON, CSV), exit code mapping (success/error), HTTP 404 handling, positional argument parsing, required option validation, paginated result display, enum serialization as strings (not integers), URL encoding of special characters in arguments, and `AdminApiClient` integration via `MockHttpMessageHandler`.

11. **Parent `stream` command help** — Running `eventstore-admin stream` with no subcommand prints help listing all six sub-subcommands (`list`, `events`, `event`, `state`, `diff`, `causation`) with their descriptions. `System.CommandLine` provides this automatically for parent commands with no handler — verify this works.

## Tasks / Subtasks

- [x] **Task 1: Replace `stream` stub with parent command, shared arguments, and `TryGetAsync<T>`** (AC: 7, 9)
  - [x] 1.1 Create `Commands/Stream/StreamCommand.cs` — replaces the `stream` stub from story 17-1. Creates a parent `Command("stream", "Query, list, and inspect event streams")` with no handler (parent only). Registers all six sub-subcommands. Pattern:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;
    public static class StreamCommand
    {
        public static Command Create(GlobalOptionsBinding binding)
        {
            var command = new Command("stream", "Query, list, and inspect event streams");
            command.AddCommand(StreamListCommand.Create(binding));
            command.AddCommand(StreamEventsCommand.Create(binding));
            command.AddCommand(StreamEventCommand.Create(binding));
            command.AddCommand(StreamStateCommand.Create(binding));
            command.AddCommand(StreamDiffCommand.Create(binding));
            command.AddCommand(StreamCausationCommand.Create(binding));
            return command;
        }
    }
    ```
  - [x] 1.2 Update `Program.cs` — replace `StubCommands.Create("stream", ...)` with `StreamCommand.Create(binding)`. Keep all other stubs unchanged.
  - [x] 1.3 Create `Commands/Stream/StreamArguments.cs` — shared helper creating the three common positional arguments:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;
    public static class StreamArguments
    {
        public static Argument<string> Tenant() => new("tenant", "Tenant identifier");
        public static Argument<string> Domain() => new("domain", "Domain name");
        public static Argument<string> AggregateId() => new("aggregateId", "Aggregate identifier");
    }
    ```
  - [x] 1.4 Add `TryGetAsync<T>` method to `AdminApiClient` — returns `null` on HTTP 404 instead of printing generic "Endpoint not found" error. All other HTTP errors (401, 403, 5xx, connection refused, timeout, JSON errors) are handled identically to `GetAsync<T>`. Used by the `event`, `state`, `diff`, and `causation` sub-subcommands for subcommand-specific 404 messages. Pattern:
    ```csharp
    public async Task<T?> TryGetAsync<T>(string path, CancellationToken ct) where T : class
    {
        // Identical to GetAsync<T> but:
        // - On HTTP 404: return null (do NOT print error)
        // - On all other errors: same behavior as GetAsync<T>
    }
    ```

- [x] **Task 2: `stream list` subcommand** (AC: 1, 8)
  - [x] 2.1 Create `Commands/Stream/StreamListCommand.cs`:
    - Options: `--tenant <id>` (alias `-T`, optional), `--domain <name>` (alias `-d`, optional), `--count <n>` (alias `-c`, default 1000). Note: `-t` is taken by global `--token`, so use `-T` for tenant.
    - Handler: resolves global options via `binding.Resolve(context)`, creates `AdminApiClient`, calls `GET /api/v1/admin/streams?tenantId={t}&domain={d}&count={n}`.
    - Deserializes `PagedResult<StreamSummary>`. Formats `Items` via `FormatCollection` with column definitions: Status (StreamStatus), Tenant (TenantId), Domain, AggregateId, Events (EventCount), Last Seq (LastEventSequence), Last Activity (LastActivityUtc), Snapshot (HasSnapshot).
    - When `Items` is empty: print "No streams found." to stderr, exit code `0`.
    - When `TotalCount > Items.Count`: print "Showing {Items.Count} of {TotalCount} results." to stderr.
    - JSON format: serialize the entire `PagedResult<StreamSummary>`.
    - CSV format: `Items` collection only (same as table).

- [x] **Task 3: `stream events` and `stream event` subcommands** (AC: 2, 3, 8)
  - [x] 3.1 Create `Commands/Stream/StreamEventsCommand.cs`:
    - Three positional arguments via `StreamArguments`. Options: `--from <seq>` (optional), `--to <seq>` (optional), `--count <n>` (default 100).
    - Handler: calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/timeline?fromSequence={from}&toSequence={to}&count={count}`.
    - Deserializes `PagedResult<TimelineEntry>`. Columns: Seq (SequenceNumber), Timestamp, Type (EntryType), TypeName, CorrelationId, UserId.
    - Empty result: "No timeline entries found." to stderr, exit code `0`.
    - When `TotalCount > Items.Count`: print "Showing {Items.Count} of {TotalCount} results." to stderr. Note: the server may cap results at `AdminServerOptions.MaxTimelineEvents` independently of `--count`.
    - JSON format: serialize the entire `PagedResult<TimelineEntry>`.
  - [x] 3.2 Create `Commands/Stream/StreamEventCommand.cs`:
    - Three positional arguments via `StreamArguments` plus `Argument<long>("sequenceNumber", "Event sequence number")`.
    - Handler: calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/events/{sequenceNumber}`.
    - Deserializes `EventDetail`. Table format: single-object key/value via `formatter.Format(eventDetail, columns)` with explicit `ColumnDefinition` list. Include all fields (TenantId, Domain, AggregateId, SequenceNumber, EventTypeName, Timestamp, CorrelationId, CausationId, UserId, PayloadJson). Set `MaxWidth = 80` on PayloadJson column to prevent layout blowout.
    - HTTP 404: use `TryGetAsync<EventDetail>` — on null result, print subcommand-specific error message.

- [x] **Task 4: `stream state`, `stream diff`, `stream causation` subcommands** (AC: 4, 5, 6)
  - [x] 4.1 Create `Commands/Stream/StreamStateCommand.cs`:
    - Three positional arguments via `StreamArguments`. Required option: `--at <seq>` (alias `-a`).
    - Handler: calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/state?sequenceNumber={at}`.
    - Deserializes `AggregateStateSnapshot`. Table: single-object key/value via `formatter.Format(snapshot, columns)` with explicit `ColumnDefinition` list. Set `MaxWidth = 80` on StateJson column.
    - HTTP 404: use `TryGetAsync<AggregateStateSnapshot>` — on null result, print "Aggregate state not found at sequence {seq}." to stderr.
  - [x] 4.2 Create `Commands/Stream/StreamDiffCommand.cs`:
    - Three positional arguments via `StreamArguments`. Required options: `--from <seq>`, `--to <seq>`.
    - Handler: calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/diff?fromSequence={from}&toSequence={to}`.
    - Deserializes `AggregateStateDiff`. Table: collection of `FieldChange` with columns: FieldPath, OldValue, NewValue. Also prints "Diff from sequence {from} to {to}" header to stderr.
    - JSON format: full `AggregateStateDiff`.
    - CSV format: `ChangedFields` rows only.
    - HTTP 404: use `TryGetAsync<AggregateStateDiff>` — on null result, print "Aggregate state not found for the specified range." to stderr.
  - [x] 4.3 Create `Commands/Stream/StreamCausationCommand.cs`:
    - Three positional arguments via `StreamArguments`. Required option: `--at <seq>` (alias `-a`).
    - Handler: calls `GET /api/v1/admin/streams/{tenant}/{domain}/{aggregateId}/causation?sequenceNumber={at}`.
    - Deserializes `CausationChain`. Table format (dual-section, same pattern as `HealthCommand`):
      - Section 1: construct overview object with: Originating Command Type, Originating Command ID, Correlation ID, User ID, Event Count (`Events.Count`), Affected Projections (`string.Join(", ", AffectedProjections)`). Render via `formatter.Format(overview)`.
      - Section 2: render `Events` collection with columns: Seq (SequenceNumber), EventType (EventTypeName), Timestamp. Render via `formatter.FormatCollection(chain.Events, columns)`.
      - Blank line between sections.
    - JSON format: full `CausationChain` serialized.
    - CSV format: `Events` table only.
    - HTTP 404: use `TryGetAsync<CausationChain>` — on null result, print "Causation chain not found." to stderr.

- [x] **Task 5: Unit tests** (AC: 10)
  - [x] **Stream list tests:**
  - [x] 5.1 `StreamListCommand_ReturnsStreamTable` — Mocked response with 2 `StreamSummary` items renders table with correct columns (Status, Tenant, Domain, AggregateId, Events, Last Seq, Last Activity, Snapshot).
  - [x] 5.2 `StreamListCommand_EmptyResult_PrintsNoStreamsFound` — Empty `Items` list prints "No streams found." to stderr, exit code `0`.
  - [x] 5.3 `StreamListCommand_PaginatedResult_PrintsHint` — `TotalCount > Items.Count` prints "Showing {N} of {Total} results." to stderr.
  - [x] 5.4 `StreamListCommand_JsonFormat_ReturnsValidJson` — JSON output deserializes back to `PagedResult<StreamSummary>`.
  - [x] 5.5 `StreamListCommand_WithFilters_SendsQueryParameters` — `--tenant acme --domain counter --count 50` sends correct query string.
  - [x] **Stream events tests:**
  - [x] 5.6 `StreamEventsCommand_ReturnsTimelineTable` — Mocked timeline response renders table with correct columns.
  - [x] 5.7 `StreamEventsCommand_WithSequenceRange_SendsFromTo` — `--from 5 --to 20` sends correct query parameters.
  - [x] **Stream event tests:**
  - [x] 5.8 `StreamEventCommand_ReturnsEventDetail` — Mocked response renders key/value table with all `EventDetail` fields.
  - [x] 5.9 `StreamEventCommand_NotFound_PrintsError` — HTTP 404 prints subcommand-specific error, exit code `2`.
  - [x] 5.10 `StreamEventCommand_JsonFormat_ReturnsFullEventDetail` — JSON includes `PayloadJson` field.
  - [x] **Stream state tests:**
  - [x] 5.11 `StreamStateCommand_ReturnsSnapshot` — Renders `AggregateStateSnapshot` as key/value table.
  - [x] 5.12 `StreamStateCommand_NotFound_PrintsError` — HTTP 404 prints "Aggregate state not found" message.
  - [x] 5.13 `StreamStateCommand_RequiresAtOption` — Missing `--at` produces error and usage help.
  - [x] **Stream diff tests:**
  - [x] 5.14 `StreamDiffCommand_ReturnsFieldChanges` — Renders `AggregateStateDiff.ChangedFields` as table with FieldPath, OldValue, NewValue columns.
  - [x] 5.15 `StreamDiffCommand_RequiresFromAndTo` — Missing `--from` or `--to` produces error.
  - [x] **Stream causation tests:**
  - [x] 5.16 `StreamCausationCommand_ReturnsDualSectionOutput` — Table output contains overview section with computed values (Event Count matching `Events.Count`, Affected Projections as comma-separated list matching `AffectedProjections`) AND events table with correct columns (Seq, EventType, Timestamp).
  - [x] 5.17 `StreamCausationCommand_JsonFormat_ReturnsFullChain` — JSON output deserializes back to `CausationChain`.
  - [x] 5.18 `StreamCausationCommand_CsvFormat_ReturnsEventsOnly` — CSV contains only events rows.
  - [x] **Argument parsing tests:**
  - [x] 5.19 `StreamSubcommands_MissingPositionalArgs_ReturnsError` — Missing tenant/domain/aggregateId produces error.
  - [x] 5.20 `StreamEventCommand_ParsesSequenceNumberArgument` — `event acme counter 01J 42` parses all four positional arguments correctly.
  - [x] **Enum serialization tests:**
  - [x] 5.21 `StreamListCommand_JsonFormat_EnumsSerializeAsStrings` — `StreamStatus.Active` serializes as `"active"` (not `0`) and `StreamStatus.Tombstoned` as `"tombstoned"` in JSON output. Verifies `JsonStringEnumConverter` is active for stream DTOs (same risk as story 17-1 test 6.27 for `HealthStatus`).
  - [x] 5.22 `StreamEventsCommand_JsonFormat_EntryTypeSerializesAsString` — `TimelineEntryType.Command` serializes as `"command"` (not `0`).
  - [x] **URL encoding tests:**
  - [x] 5.23 `StreamCommands_SpecialCharsInArgs_AreUrlEncoded` — Positional arguments with special characters (e.g., tenant `acme/corp`, domain `my domain`) are URL-encoded in the HTTP request path via `Uri.EscapeDataString()`.
  - [x] **CSV format tests for collections:**
  - [x] 5.24 `StreamListCommand_CsvFormat_ReturnsHeaderAndRows` — CSV output has header row (Status, TenantId, Domain, AggregateId, EventCount, ...) and data rows with correct values. Enum values render as strings.
  - [x] 5.25 `StreamEventsCommand_CsvFormat_ReturnsTimelineRows` — CSV output has header and data rows for `TimelineEntry` records.
  - [x] **Parent command help test:**
  - [x] 5.26 `StreamCommand_NoSubcommand_PrintsHelp` — Running `stream` with no subcommand prints help listing all six sub-subcommands.
  - [x] **Error handling tests:**
  - [x] 5.27 `StreamCommands_Http401_PrintsAuthError` — Reuses `AdminApiClient` error handling.
  - [x] 5.28 `StreamCommands_ConnectionRefused_PrintsConnectError` — Connection refused error.

  Test file locations:
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamListCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamEventsCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamEventCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamStateCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamDiffCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamCausationCommandTests.cs`

  **Test pattern:** Reuse the `MockHttpMessageHandler` from story 17-1's test project. Construct `AdminApiClient` with `new HttpClient(handler)` internal constructor. Mock responses as `HttpResponseMessage` with JSON-serialized DTOs from `Admin.Abstractions`.

## Dev Notes

### Builds on story 17-1 infrastructure — do NOT recreate

All of the following already exist from story 17-1. Reuse them, do NOT recreate:
- `GlobalOptions` / `GlobalOptionsBinding` — global option parsing and resolution
- `AdminApiClient` — HTTP client with auth header, error handling, shared `JsonSerializerOptions`
- `IOutputFormatter` / `OutputFormatterFactory` / `OutputWriter` — formatting infrastructure
- `ColumnDefinition` / `Alignment` — column metadata for table/CSV
- `ExitCodes` — exit code constants (Success=0, Degraded=1, Error=2)
- `JsonDefaults.Options` — shared `JsonSerializerOptions` with `JsonStringEnumConverter`
- `MockHttpMessageHandler` — test helper for HTTP mocking

### Admin API endpoints (already implemented in Admin.Server)

All six endpoints are implemented and stable in `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`:

| Method | Route | Query Params | Returns |
|--------|-------|-------------|---------|
| GET | `api/v1/admin/streams` | `tenantId`, `domain`, `count` | `PagedResult<StreamSummary>` |
| GET | `api/v1/admin/streams/{t}/{d}/{id}/timeline` | `fromSequence`, `toSequence`, `count` | `PagedResult<TimelineEntry>` |
| GET | `api/v1/admin/streams/{t}/{d}/{id}/events/{seq}` | — | `EventDetail` |
| GET | `api/v1/admin/streams/{t}/{d}/{id}/state` | `sequenceNumber` | `AggregateStateSnapshot` |
| GET | `api/v1/admin/streams/{t}/{d}/{id}/diff` | `fromSequence`, `toSequence` | `AggregateStateDiff` |
| GET | `api/v1/admin/streams/{t}/{d}/{id}/causation` | `sequenceNumber` | `CausationChain` |

All endpoints require `ReadOnly` auth policy + `AdminTenantAuthorizationFilter`. All return `ProblemDetails` on error.

### Existing Admin.Abstractions models to reuse — do NOT recreate

All DTOs are defined in `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/`:

- `StreamSummary(TenantId, Domain, AggregateId, LastEventSequence, LastActivityUtc, EventCount, HasSnapshot, StreamStatus)` — stream list item
- `StreamStatus` enum: `Active`, `Idle`, `Tombstoned`
- `TimelineEntry(SequenceNumber, Timestamp, EntryType, TypeName, CorrelationId, UserId)` — timeline row
- `TimelineEntryType` enum: `Command`, `Event`, `Query`
- `EventDetail(TenantId, Domain, AggregateId, SequenceNumber, EventTypeName, Timestamp, CorrelationId, CausationId, UserId, PayloadJson)` — single event
- `AggregateStateSnapshot(TenantId, Domain, AggregateId, SequenceNumber, Timestamp, StateJson)` — state at position
- `AggregateStateDiff(FromSequence, ToSequence, ChangedFields: IReadOnlyList<FieldChange>)` — state delta
- `FieldChange(FieldPath, OldValue, NewValue)` — single field change
- `CausationChain(OriginatingCommandType, OriginatingCommandId, CorrelationId, UserId, Events: IReadOnlyList<CausationEvent>, AffectedProjections: IReadOnlyList<string>)` — trace
- `CausationEvent(SequenceNumber, EventTypeName, Timestamp)` — event in chain
- `PagedResult<T>(Items, TotalCount, ContinuationToken)` — paginated wrapper

Import these from the Abstractions project reference. Do NOT define new DTOs.

### SEC-5: PayloadJson and StateJson contain sensitive data

`EventDetail.PayloadJson` and `AggregateStateSnapshot.StateJson` are opaque JSON that may contain sensitive domain data. The `ToString()` overrides on these records redact these fields. In CLI output:
- JSON format: include the raw JSON as-is (user explicitly requested the data)
- Table format: include as-is (user is inspecting)
- CSV format: include as-is
- The CLI does NOT add its own redaction — the user chose to run the command

### `AdminApiClient.GetAsync<T>` — extending for query parameters and 404 handling

Story 17-1's `AdminApiClient` has `GetAsync<T>(string path, CancellationToken ct)`. The stream subcommands need two extensions:

**1. Query string construction:** Build the query string in the command handler and pass the full path: `client.GetAsync<PagedResult<StreamSummary>>($"api/v1/admin/streams?tenantId={tenant}&domain={domain}&count={count}", ct)`. Use `Uri.EscapeDataString()` for all interpolated values.

**2. 404-aware GET method:** Story 17-1's `AdminApiClient` maps HTTP 404 to a generic "Endpoint not found" error. The `event`, `state`, `diff`, and `causation` subcommands need subcommand-specific 404 messages. Add a `TryGetAsync<T>` method that returns `null` on 404 instead of throwing:
```csharp
public async Task<T?> TryGetAsync<T>(string path, CancellationToken ct) where T : class
{
    // Same as GetAsync<T> but returns null on 404 instead of printing generic error
    // All other HTTP errors (401, 403, 5xx) still throw/print as before
}
```
Command handlers then check `if (result is null)` and print their own 404 message. The `list` and `events` subcommands continue using `GetAsync<T>` (they never receive 404).

### Explicit `ColumnDefinition` for large JSON fields

`EventDetail.PayloadJson` and `AggregateStateSnapshot.StateJson` are arbitrarily long JSON strings. Without explicit `ColumnDefinition`, the table formatter's auto-discovery will include them as scalar `string` properties and attempt to render the full JSON in a table cell. Use explicit `ColumnDefinition` lists for the `event` and `state` subcommands with `MaxWidth = 80` on the JSON fields to prevent layout blowout. Users who need the full JSON should use `--format json`.

### System.CommandLine patterns — positional arguments

`Argument<T>` creates positional arguments (no `--` prefix). They are order-dependent. Pattern:
```csharp
var tenantArg = new Argument<string>("tenant", "Tenant identifier");
var domainArg = new Argument<string>("domain", "Domain name");
var aggregateIdArg = new Argument<string>("aggregateId", "Aggregate identifier");
command.AddArgument(tenantArg);
command.AddArgument(domainArg);
command.AddArgument(aggregateIdArg);
command.SetHandler(async (InvocationContext context) =>
{
    string tenant = context.ParseResult.GetValueForArgument(tenantArg);
    string domain = context.ParseResult.GetValueForArgument(domainArg);
    string aggregateId = context.ParseResult.GetValueForArgument(aggregateIdArg);
    // ...
});
```

### Required options pattern

`--at`, `--from`, `--to` are required options. In `System.CommandLine`, use `IsRequired = true`:
```csharp
var atOption = new Option<long>("--at", "Sequence number to inspect") { IsRequired = true };
atOption.AddAlias("-a");
```
Missing required options produce `System.CommandLine`'s built-in error message automatically.

### Dual-section table output pattern (from HealthCommand)

The `causation` subcommand uses the same dual-section pattern as `HealthCommand` from story 17-1:
```csharp
// Section 1: overview
var overview = new { ... }; // constructed from CausationChain fields
await writer.WriteAsync(formatter.Format(overview));
await writer.WriteAsync(""); // blank line
// Section 2: events table
await writer.WriteAsync(formatter.FormatCollection(chain.Events.ToList(), columns));
```

### Folder structure for new files

```
src/Hexalith.EventStore.Admin.Cli/
  Commands/
    Stream/                              ← NEW folder
      StreamCommand.cs                   ← Parent command (replaces stub)
      StreamArguments.cs                 ← Shared positional arguments
      StreamListCommand.cs               ← list subcommand
      StreamEventsCommand.cs             ← events subcommand
      StreamEventCommand.cs              ← event subcommand (single event)
      StreamStateCommand.cs              ← state subcommand
      StreamDiffCommand.cs               ← diff subcommand
      StreamCausationCommand.cs          ← causation subcommand

tests/Hexalith.EventStore.Admin.Cli.Tests/
  Commands/
    Stream/                              ← NEW folder
      StreamListCommandTests.cs
      StreamEventsCommandTests.cs
      StreamEventCommandTests.cs
      StreamStateCommandTests.cs
      StreamDiffCommandTests.cs
      StreamCausationCommandTests.cs
```

### Modified files

- `src/Hexalith.EventStore.Admin.Cli/Program.cs` — replace stream stub registration with `StreamCommand.Create(binding)`

### Architecture: CLI is a thin HTTP client (ADR-P4)

Per ADR-P4, the CLI never accesses DAPR directly. All stream queries go through `AdminApiClient` → Admin REST API → DAPR state store. No DAPR SDK, no Aspire dependencies, no DI container.

### Code style — follow project conventions exactly

- File-scoped namespaces: `namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;`
- Allman braces (opening brace on new line)
- Private fields: `_camelCase`
- Async methods: `Async` suffix
- 4 spaces indentation, CRLF line endings, UTF-8
- Nullable enabled
- Implicit usings enabled
- Warnings as errors

### URL encoding for route parameters

Tenant IDs, domain names, and aggregate IDs may contain characters that need URL encoding. Use `Uri.EscapeDataString()` when interpolating into URL paths:
```csharp
string path = $"api/v1/admin/streams/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/timeline";
```

### Previous story intelligence (17-1)

Story 17-1 established:
- `System.CommandLine` package version in `Directory.Packages.props`
- Root command structure in `Program.cs` with `GlobalOptionsBinding` pattern
- `AdminApiClient` with `GetAsync<T>` method and full error handling (HTTP 401/403/5xx, connection refused, timeout, JSON errors). **This story adds `TryGetAsync<T>` to `AdminApiClient`** for 404-aware requests (returns null instead of generic error on 404)
- Output formatting infrastructure (`IOutputFormatter`, `JsonOutputFormatter`, `CsvOutputFormatter`, `TableOutputFormatter`)
- `OutputWriter` for file/stdout routing
- `ExitCodes` static class
- `HealthCommand` as the working reference for how to wire up a subcommand
- `StubCommands` for placeholder commands
- `MockHttpMessageHandler` test helper
- Handler wiring pattern: `command.SetHandler(async (InvocationContext context) => { ... })` with `context.ExitCode = N`

Follow `HealthCommand` as the canonical pattern for all stream sub-subcommands.

### Naming hazard: `stream event` vs `stream events`

`stream event` (singular — single event detail) and `stream events` (plural — timeline list) differ by one letter. Take care with class names (`StreamEventCommand` vs `StreamEventsCommand`), test file names, and endpoint URLs during implementation. Double-check that `StreamEventCommand` calls `.../events/{seq}` (single event) and `StreamEventsCommand` calls `.../timeline` (collection). A mix-up here will compile fine but return wrong data.

### `Format<T>` key/value mode must respect `ColumnDefinition.MaxWidth`

When `IOutputFormatter.Format<T>` renders a single object as key/value pairs (used by `event`, `state` commands), it must respect `ColumnDefinition.MaxWidth` for value truncation. If story 17-1's implementation auto-discovers properties and ignores the `columns` parameter in key/value mode, extend it in this story to honor `MaxWidth` truncation. Without this, `PayloadJson` and `StateJson` render at full length in table format, potentially thousands of characters per cell.

### JSON output field names are camelCase — critical for scriptability

JSON output uses `JsonNamingPolicy.CamelCase` via `JsonDefaults.Options`. Property names in JSON are: `items`, `totalCount`, `continuationToken`, `tenantId`, `domain`, `aggregateId`, `streamStatus`, `eventCount`, `lastEventSequence`, `lastActivityUtc`, `hasSnapshot`, `sequenceNumber`, `entryType`, `typeName`, `correlationId`, `causationId`, `userId`, `payloadJson`, `stateJson`, `fieldPath`, `oldValue`, `newValue`, `originatingCommandType`, `originatingCommandId`, `affectedProjections`. Operators write `jq` filters like `jq '.items[] | select(.streamStatus == "active")'` — camelCase consistency is critical.

### Integration test gap for URL-encoded paths

All unit tests use `MockHttpMessageHandler` which validates request construction but not server-side routing behavior. `Uri.EscapeDataString("acme/corp")` produces `acme%2Fcorp` — ASP.NET routing may decode this differently than expected. When Tier 2 integration tests are created for Admin.Cli (future story), they should cover URL-encoded path segments with special characters (slashes, spaces, Unicode) against a real Admin.Server instance.

### Future ergonomic enhancements (out of scope for 17-2)

The following improvements would significantly improve CLI ergonomics but are deferred to future stories:
- **`EVENTSTORE_ADMIN_TENANT` env var** — default tenant for all stream sub-subcommands, consistent with `EVENTSTORE_ADMIN_URL` pattern. Eliminates typing the tenant argument on every command for operators working in a single tenant.
- **Colon-separated stream identifier** — `acme-corp:counter:01JARX7K9M2T5N` as alternative to three positional arguments. Reduces typing and enables copy-paste from log output.
- **Timestamp-based diff** — `stream diff ... --from-time 2026-03-24T10:00 --to-time 2026-03-24T11:00` for post-incident analysis without knowing sequence numbers.
- **`--quiet` flag** — suppress informational stderr messages (pagination hints, "No streams found") for CI/CD scripts that parse stderr for errors.
- Story 17-7 (Connection Profiles and Shell Completions) addresses dynamic tab-completion and REPL context persistence.

### Git commit patterns from recent work

Recent commits: `feat: <description> for story <story-id>`
Branch naming: `feat/story-17-2-stream-subcommand-query-list-events-state`

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR79: three admin interfaces backed by shared Admin API]
- [Source: _bmad-output/planning-artifacts/prd.md — FR80: JSON/CSV/table output, exit codes, shell completions]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR42: CLI startup + query within 3 seconds]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: CLI is thin HTTP client, no DAPR]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR50: subcommand tree]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR51: global options]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR52: exit codes]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/ — all stream DTOs]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs — service contract]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs — REST API endpoints]
- [Source: _bmad-output/implementation-artifacts/17-1-cli-scaffold-system-commandline-global-options.md — CLI scaffold story]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Initial build failed due to `Argument<T>` constructor API — System.CommandLine beta5 uses single-arg constructor with Description property setter
- 8 test failures in first run: Console.SetOut/SetError parallel capture races, Option.Name includes `--` prefix in beta5, JSON PayloadJson double-encoding
- All resolved by testing formatters directly instead of capturing Console, using `--at`/`--from`/`--to` for option name lookup, and testing JSON round-trip

### Completion Notes List

- Implemented all 6 stream sub-subcommands: `list`, `events`, `event`, `state`, `diff`, `causation`
- Added `TryGetAsync<T>` to `AdminApiClient` for 404-aware GET requests (returns null instead of throwing)
- Extended `TableOutputFormatter.Format<T>` to honor `ColumnDefinition.MaxWidth` in key/value mode (for PayloadJson/StateJson truncation)
- Replaced stream stub in `Program.cs` with `StreamCommand.Create(binding)`
- Created `StreamArguments` shared helper for tenant/domain/aggregateId positional arguments
- All 6 commands follow HealthCommand pattern: static Create, SetAction with ParseResult, internal ExecuteAsync for testability
- URL encoding via `Uri.EscapeDataString()` on all interpolated path segments
- 31 unit tests covering all 28 story-specified tests plus 3 additional coverage tests
- Updated StubCommandsTests to remove "stream" test case (no longer a stub)
- All Tier 1 tests pass (64 CLI tests), zero regressions
- Build produces zero warnings in Release configuration

### Change Log

- 2026-03-25: Implemented story 17-2 — stream subcommand with 6 sub-subcommands, TryGetAsync, and 30 unit tests
- 2026-03-25: Code review fixes — wrapped HttpResponseMessage in using (TryGetAsync), renamed "Last Seq" column to "Last Sequence" (AC#1), added StreamDiffCommand_NotFound test, added empty ChangedFields guard in StreamDiffCommand. 31 tests total, all green.

### File List

New files:
- src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamArguments.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamListCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamEventsCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamEventCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamStateCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamDiffCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamCausationCommand.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamListCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamEventsCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamEventCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamStateCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamDiffCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamCausationCommandTests.cs

Modified files:
- src/Hexalith.EventStore.Admin.Cli/Program.cs (replaced stream stub with StreamCommand)
- src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs (added TryGetAsync<T>)
- src/Hexalith.EventStore.Admin.Cli/Formatting/TableOutputFormatter.cs (MaxWidth in key/value Format<T>)
- tests/Hexalith.EventStore.Admin.Cli.Tests/StubCommandsTests.cs (removed "stream" test case)
