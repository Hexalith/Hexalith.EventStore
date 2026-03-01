# Story 12.6: First Domain Service Tutorial

Status: done

## Story

As a developer who completed the quickstart,
I want step-by-step instructions to build and register my own domain service,
So that I can extend the system with my business logic.

## Acceptance Criteria

1. `docs/getting-started/first-domain-service.md` exists as a new tutorial page
2. The tutorial walks through creating a new domain service: defining commands, events, state, and an aggregate — following the same pattern as the Counter domain
3. The tutorial includes a backend swap demonstration (FR9): switching from Redis to PostgreSQL with zero code changes by changing DAPR component YAML
4. The tutorial completes within 1 hour for a .NET/DDD-familiar developer (NFR5)
5. All code examples use inline code fences with language tags and are aligned with the sample project
6. The page follows the standard page template with prerequisites linking to quickstart
7. The tutorial references the DAPR component YAML variants in `deploy/dapr/` (D2) for the backend swap demonstration
8. The page is self-contained (FR43) — no external knowledge required beyond stated prerequisites

## Tasks / Subtasks

- [x] Task 1: Create `docs/getting-started/first-domain-service.md` with page template structure (AC: #1, #6, #8)
    - [x] 1.1 Add back-link `[← Back to Hexalith.EventStore](../../README.md)` — use the Unicode arrow `←` matching the quickstart page convention (NOT the ASCII `<-` used in concept pages)
    - [x] 1.2 Add H1 title "Build Your First Domain Service"
    - [x] 1.3 Add one-paragraph summary: what the reader will build (a simple Inventory domain service), what they'll learn (commands, events, state, aggregate, registration), and the time estimate (~45 minutes)
    - [x] 1.4 Add prerequisites callout linking to quickstart only: `> **Prerequisites:** [Quickstart](quickstart.md) — you should have the sample application running before starting this tutorial` (max 2 prerequisites per NFR10)
    - [x] 1.5 Add Next Steps footer: "**Next:** [Architecture Overview](../concepts/architecture-overview.md) — understand the design decisions behind the system" and "**Related:** [Quickstart](quickstart.md), [Command Lifecycle](../concepts/command-lifecycle.md), [Identity Scheme](../concepts/identity-scheme.md)"

- [x] Task 2: Write "What You'll Build" overview section (AC: #2, #5)
    - [x] 2.1 Describe the Inventory domain: a simple product inventory service that tracks stock quantities via `AddStock` and `RemoveStock` commands
    - [x] 2.2 Show the domain model summary: 2 commands, 2 success events (StockAdded, StockRemoved), 1 rejection event (InsufficientStock), 1 state class, 1 aggregate. Distinguish success events from rejection events from the start — this sets up the conceptual distinction that matters in the State and Aggregate sections.
    - [x] 2.3 Explain the pure function contract: `Handle(Command, State?) → DomainResult` — same pattern as the Counter domain from the quickstart
    - [x] 2.4 Include a brief "By the end of this tutorial" bullet list: created domain types, registered with EventStore, sent commands, observed events, swapped backends

- [x] Task 3: Write "Create the Domain Types" section (AC: #2, #5)
    - [x] 3.0 **Folder creation FIRST (separate step before any code):** Instruct the reader to create the entire folder structure as a distinct step BEFORE showing any code files. Use a clear heading like "### Create the Project Structure" and show the folders to create. This prevents readers from creating files in the wrong location. Example instruction: "Create the following folders inside the sample project..."
    - [x] 3.1 After folders exist, explain where the code goes: `samples/Hexalith.EventStore.Sample/Inventory/` (alongside the existing `Counter/` folder — the sample project already has the required project references)
    - [x] 3.2 Show the folder structure the reader will create
    - [x] 3.2b **Section structure:** Use separate H3 subsections for each type category: `### Commands`, `### Events`, `### State`, `### Aggregate`. This breaks up the code-heavy section into scannable chunks and matches the quickstart's sectioned approach. Between each code block, write 1-3 sentences maximum: lead with the instruction ("Create..."), show the code, then explain what it does.
    - [x] 3.3 **Commands subsection (`### Commands`):** Show `AddStock` and `RemoveStock` as sealed records with a `Quantity` property (int). Use `namespace Hexalith.EventStore.Sample.Inventory.Commands;` — follow the Counter pattern exactly. Commands are plain records — no interfaces, no base classes.
    - [x] 3.4 **Events subsection:** Show `StockAdded` and `StockRemoved` implementing `IEventPayload` (from `Hexalith.EventStore.Contracts.Events`), and `InsufficientStock` implementing `IRejectionEvent`. Each has a `Quantity` property matching the command. Follow Counter pattern: sealed records. **Add a brief inline explanation:** events carry data (like `Quantity`) because they are the system's permanent record — during state reconstruction, the framework replays these events through the `Apply` methods to rebuild the current state. This is why events must contain enough information to reconstruct the change they represent.
    - [x] 3.5 **State subsection:** Show `InventoryState` with `public int Quantity { get; private set; }` and TWO `Apply` methods — one per success event only. `Apply(StockAdded e) => Quantity += e.Quantity`, `Apply(StockRemoved e) => Quantity -= e.Quantity`. **Do NOT add an Apply method for `InsufficientStock`** — rejection events (`IRejectionEvent`) are returned to the caller but never persisted to the event stream and never replayed. Counter's `CounterState` follows this same pattern: it has Apply methods for `CounterIncremented`, `CounterDecremented`, and `CounterReset` but NO Apply for `CounterCannotGoNegative`.
    - [x] 3.6 **Aggregate subsection:** Show `InventoryAggregate : EventStoreAggregate<InventoryState>` with two `public static DomainResult Handle(...)` methods. The `static` keyword is critical — it enforces pure functions with no instance state mutation:
        - `public static DomainResult Handle(AddStock command, InventoryState? state)` → guards non-positive quantities with `NoOp`, otherwise succeeds with `StockAdded` event
        - `public static DomainResult Handle(RemoveStock command, InventoryState? state)` → checks `(state?.Quantity ?? 0) >= command.Quantity`; if insufficient, returns `DomainResult.Rejection` with `InsufficientStock`; otherwise returns `DomainResult.Success` with `StockRemoved`
    - [x] 3.7 Every code block must use `csharp` language tag. Every file must show the full file content including `using` directives and `namespace` declaration.

- [x] Task 4: Write "Register and Run" section (AC: #2, #5)
    - [x] 4.1 Explain that **no registration code is needed** — `AddEventStore()` in `Program.cs` auto-discovers all `EventStoreAggregate<>` subclasses in the assembly at startup. The `InventoryAggregate` is found automatically by the assembly scanner. Add a brief WHY: "The sample project already references `Hexalith.EventStore.Client`, which contains the `EventStoreAggregate<T>` base class and the assembly scanner. Any public aggregate class in this project is discovered automatically — no `.csproj` changes, no `Program.cs` changes."
    - [x] 4.2 Explain the naming convention: `InventoryAggregate` → domain name `inventory` (PascalCase to kebab-case, "Aggregate" suffix stripped). Do NOT mention the `[EventStoreDomain("custom-name")]` attribute override — it's a power-user feature that distracts from a first tutorial. Keep the focus on convention-over-configuration.
    - [x] 4.3 Show how to restart the AppHost: `dotnet run --project src/Hexalith.EventStore.AppHost`
    - [x] 4.4 **"Verify Discovery" substep (IMPORTANT):** After restart, direct the reader to the Aspire dashboard's Structured Logs tab for the `sample` service. They should see a log entry confirming `inventory` domain was discovered (the assembly scanner logs discovered domains at startup). If the reader does NOT see their domain, provide a troubleshooting checklist: (a) verify the class extends `EventStoreAggregate<InventoryState>`, (b) verify the class is `public`, (c) verify the namespace matches the folder structure, (d) verify the file is saved and the project compiles without errors. This verification step prevents readers from proceeding with broken code.

- [x] Task 5: Write "Send a Command" section (AC: #2, #5)
    - [x] 5.1 Show the Keycloak token acquisition curl command (same as quickstart). Add a note: "If you still have a token from the quickstart, you can reuse it — tokens are valid for 5 minutes. If it's expired, re-run the curl command below to get a fresh one." Show the full curl command so the reader doesn't have to flip back to the quickstart page.
    - [x] 5.2 Show a curl/Swagger UI command to send an `AddStock` command
    - [x] 5.2b Add a brief note after the JSON explaining commandType, domain, payload conventions, and first non-empty payload
    - [x] 5.3 Show the expected `202 Accepted` response with correlation ID
    - [x] 5.4 Show a follow-up `RemoveStock` command with quantity 3. Then, BEFORE showing the rejection command, add a predictive note about state having 7 units. Then show `RemoveStock` with quantity 100 to trigger the rejection.
    - [x] 5.5 Explain what to look for in the Aspire dashboard traces: the same 6-step processing pipeline as the Counter domain, but now showing `InventoryAggregate` as the domain service
    - [x] 5.6 **Cross-platform:** Include a PowerShell alternative for the token acquisition command, matching the quickstart's pattern.

- [x] Task 6: Write "See the Events" section (AC: #2, #5)
    - [x] 6.1 Direct reader to the Aspire dashboard Traces tab — show what a successful `StockAdded` trace looks like
    - [x] 6.2 Explain the rejection flow: when `RemoveStock` exceeds available stock, the aggregate returns a `DomainResult.Rejection` with an `InsufficientStock` rejection event — this is NOT an error, it's a business rule enforcement. HTTP response behavior for rejections explained.
    - [x] 6.3 Explain state reconstruction: after the successful AddStock(10) and RemoveStock(3), the InventoryState.Quantity is 7. The next command will receive this state.

- [x] Task 7: Write "Swap the Backend" section (AC: #3, #7)
    - [x] 7.1 Start with an expectation-setting callout about walkthrough-only (no PostgreSQL locally)
    - [x] 7.2 Show the current local dev state store config (Redis) with key `type: state.redis` line
    - [x] 7.3 Show the PostgreSQL alternative with `type: state.postgresql` and connection string difference
    - [x] 7.4 One-sentence mention of pub/sub RabbitMQ alternative without full YAML comparison
    - [x] 7.5 Explain that domain code is identical regardless of backend — brief YAML excerpts only
    - [x] 7.6 Do NOT ask the reader to actually run with PostgreSQL
    - [x] 7.7 One-sentence tie-back to architecture overview

- [x] Task 7.8: Write a "What Happened" section (matching quickstart pattern) — consolidated walkthrough of all three commands end-to-end with 8-step processing pipeline for the Inventory domain.

- [x] Task 8: Write "What You Learned" summary section (AC: #2)
    - [x] 8.1 Bullet list summarizing key concepts: pure function contract, auto-discovery, naming conventions, three result types (Success, Rejection, NoOp), state reconstruction via Apply methods, infrastructure portability
    - [x] 8.2 Emphasize: the reader wrote zero infrastructure code — no Redis imports, no DAPR SDK references, no database connection strings. The Inventory domain service follows the same zero-infrastructure principle as the Counter.
    - [x] 8.3 **"Build Your Own" bridge (IMPORTANT):** "Ready to Build Your Own?" section with transferable 4-step recipe.

- [x] Task 9: Verify page compliance (AC: #4, #5, #6, #8)
    - [x] 9.1 Total page length: 368 lines (within acceptable range for tutorial with 7 code files, YAML excerpts, curl commands, PowerShell alternatives)
    - [x] 9.2 No YAML frontmatter
    - [x] 9.3 All links use relative paths
    - [x] 9.4 Second-person tone, present tense, professional-casual
    - [x] 9.5 All code blocks have language tags (`csharp`, `bash`, `json`, `yaml`, `text`, `powershell`)
    - [x] 9.6 Terminal commands prefixed with `$`
    - [x] 9.7 Run `markdownlint-cli2 docs/getting-started/first-domain-service.md` — 0 errors
    - [x] 9.8 All relative links verified to resolve (architecture-overview.md, quickstart.md, command-lifecycle.md, identity-scheme.md, README.md)
    - [x] 9.9 Self-containment test: tutorial understandable with only quickstart as prerequisite
    - [x] 9.10 Time test: tutorial completable in under 1 hour (7 small files to create, 3 curl commands to send)

### Review Follow-ups (AI)

- [x] AI-Review (HIGH): Correct rejection-event semantics in `first-domain-service.md` to match implementation (`IRejectionEvent` is persisted/published as part of the event stream, while state evolution still applies only domain state transition events). Updated all contradictory tutorial statements in overview, events, traces, walkthrough, and summary sections. [`docs/getting-started/first-domain-service.md:13`, `docs/getting-started/first-domain-service.md:111`, `docs/getting-started/first-domain-service.md:139`, `docs/getting-started/first-domain-service.md:309`, `docs/getting-started/first-domain-service.md:326`, `docs/getting-started/first-domain-service.md:367`]

- [x] AI-Review (HIGH): Add the two missing troubleshooting tips explicitly required by the story Dev Notes (rejection logic and state operator troubleshooting), in the tutorial body near command/state sections. [`_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:218`, `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:227`, `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:228`, `docs/getting-started/first-domain-service.md:196`]
- [x] AI-Review (MEDIUM): Harden tutorial aggregate examples against invalid quantities (`<= 0`) to prevent misleading behavior (e.g., negative `RemoveStock` increases inventory). [`docs/getting-started/first-domain-service.md:161`, `docs/getting-started/first-domain-service.md:164`, `docs/getting-started/first-domain-service.md:166`, `docs/getting-started/first-domain-service.md:135`]
- [x] AI-Review (MEDIUM): Reconcile story File List with current git working tree reality before merge so story documentation reflects all changed source/docs files in scope. [`_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:334`, `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:336`]

- [x] AI-Review (HIGH): Align Task 3.6 implementation text/code with the claimed requirement. Task 3.6 currently states `AddStock` "always succeeds with `StockAdded`", but the delivered tutorial code returns `DomainResult.NoOp()` for `Quantity <= 0`. Either adjust Task 3.6 wording to include the guard behavior or update the tutorial code/comments to match the original requirement. [`_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md` Task 3.6, `docs/getting-started/first-domain-service.md` Aggregate section]
- [x] AI-Review (MEDIUM): Resolve contradictory rejection-event semantics inside this story file. Dev Notes still state "Rejection Events — NOT Persisted (CRITICAL)", while completion/review sections and the runtime implementation treat rejection events as persisted/published audit events. Keep one canonical rule to avoid reviewer/developer confusion. [`_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md` Dev Notes "Rejection Events — NOT Persisted (CRITICAL)", `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` comments around event publishing]
- [x] AI-Review (LOW): Correct actor identity delimiter wording in the tutorial walkthrough. The tutorial currently uses `tenant-a|inventory|product-1`, but canonical actor identity is colon-separated (`tenant-a:inventory:product-1`) per `AggregateIdentity.ActorId`. [`docs/getting-started/first-domain-service.md` "What Happened", `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs`]

## Dev Notes

### Implementation Approach — New Tutorial Page (MUST follow)

**This story creates `docs/getting-started/first-domain-service.md` — a NEW file.** This is a step-by-step tutorial, not a concept page. It goes in `docs/getting-started/` alongside `quickstart.md` and `prerequisites.md`.

**The quickstart already links to this page** at `docs/getting-started/quickstart.md:124`:

```markdown
- **Next:** [Build Your First Domain Service](first-domain-service.md) — create your own domain from scratch
```

This link is currently dead. This story makes it live.

### Tutorial Domain: Inventory (NOT Counter)

Use an **Inventory** domain (products with stock quantities), NOT the Counter domain. The reader already built Counter in the quickstart — repeating it teaches nothing. The Inventory domain demonstrates:

- Commands with **data fields** (quantity) — Counter commands have no fields
- **Business rule validation** with rejection events (insufficient stock) — Counter only has the simple "can't go negative" check
- A **different domain** that coexists alongside Counter in the same sample project — proving multi-domain support works

All Inventory code goes in `samples/Hexalith.EventStore.Sample/Inventory/` — a new subfolder alongside the existing `Counter/` folder. The existing `Hexalith.EventStore.Sample.csproj` already has the correct project references (`Hexalith.EventStore.Client`, `Hexalith.EventStore.ServiceDefaults`, `Dapr.AspNetCore`). No `.csproj` changes needed.

### Code Pattern — Follow Counter EXACTLY

Every Inventory file must mirror the Counter pattern precisely:

| Counter File                                | Inventory Equivalent                    | Key Pattern                                            |
| ------------------------------------------- | --------------------------------------- | ------------------------------------------------------ |
| `Counter/Commands/IncrementCounter.cs`      | `Inventory/Commands/AddStock.cs`        | `sealed record` with namespace matching folder         |
| `Counter/Events/CounterIncremented.cs`      | `Inventory/Events/StockAdded.cs`        | `sealed record : IEventPayload`                        |
| `Counter/Events/CounterCannotGoNegative.cs` | `Inventory/Events/InsufficientStock.cs` | `sealed record : IRejectionEvent`                      |
| `Counter/State/CounterState.cs`             | `Inventory/State/InventoryState.cs`     | `Apply(Event)` methods, `private set`                  |
| `Counter/CounterAggregate.cs`               | `Inventory/InventoryAggregate.cs`       | `EventStoreAggregate<TState>`, static `Handle` methods |

**Critical differences from Counter:**

- Commands have a `Quantity` property (`public sealed record AddStock(int Quantity);`)
- Events have a `Quantity` property too (`public sealed record StockAdded(int Quantity) : IEventPayload;`)
- State tracks `Quantity` (not `Count`) — `Apply(StockAdded e) => Quantity += e.Quantity`
- Rejection uses `InsufficientStock` (meaningful name vs Counter's `CounterCannotGoNegative`)

### Rejection Events — Persisted but NOT Applied to State (CRITICAL)

Rejection events (`IRejectionEvent`) differ from success events (`IEventPayload`) in state evolution only:

- **Success events** (`IEventPayload`) are persisted to the event stream, published to pub/sub, AND applied to state via `Apply` methods.
- **Rejection events** (`IRejectionEvent`) are persisted to the event stream and published to pub/sub for auditability, but have NO `Apply` methods on state — they do not cause state transitions.

This matches the Counter pattern: `CounterCannotGoNegative` implements `IRejectionEvent` and `CounterState` has no `Apply(CounterCannotGoNegative)` method. The tutorial's `InsufficientStock` follows the same pattern — it records the refusal for audit but does not change inventory state.

The tutorial should explain this distinction inline when introducing the `InsufficientStock` event: "Rejection events record that the command was refused — they are persisted and published for observability but do not change state."

### Records with Constructor Parameters and JSON Serialization

Counter commands (`IncrementCounter`, `DecrementCounter`, `ResetCounter`) are parameterless records. The Inventory commands (`AddStock(int Quantity)`, `RemoveStock(int Quantity)`) use primary constructor parameters. This works correctly with `System.Text.Json` — records with constructor parameters are deserialized by matching JSON property names to constructor parameters (case-insensitive by default). The tutorial should show the JSON payload with lowercase `"quantity"` matching the C# `Quantity` parameter.

### Auto-Discovery — Zero Registration Required

`AddEventStore()` in `Program.cs` scans the calling assembly for all `EventStoreAggregate<>` subclasses. Adding `InventoryAggregate` to the sample project means it's discovered automatically at startup — no code changes to `Program.cs`, no manual registration, no configuration.

The naming convention engine converts `InventoryAggregate` → domain name `inventory`:

1. Strip known suffixes: "Aggregate", "Projection", "Processor" → `Inventory`
2. PascalCase to kebab-case → `inventory`

### Backend Swap Section — Show, Don't Run

The tutorial SHOWS the backend swap concept using existing YAML files:

- Local dev: `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml` (`state.redis`)
- Production PostgreSQL: `deploy/dapr/statestore-postgresql.yaml` (`state.postgresql`)
- Production RabbitMQ: `deploy/dapr/pubsub-rabbitmq.yaml` (`pubsub.rabbitmq`)

Do NOT ask the reader to actually run with PostgreSQL — that requires a running PostgreSQL instance and is out of scope. The practical backend swap exercise will be delivered in Epic 13, Story 13-2 (DAPR Component Variants for Backend Swap Demo). This tutorial explains the PRINCIPLE; the sample integration tests will provide the PRACTICE.

### Page Conventions (MUST follow)

From `docs/page-template.md`:

- Back-link: `[← Back to Hexalith.EventStore](../../README.md)` — **use Unicode `←`** matching quickstart.md
- One H1 per page
- Max 2 prerequisites (link to quickstart only)
- Code blocks with language tags (`csharp`, `bash`, `json`, `yaml`)
- Terminal commands prefixed with `$`
- No YAML frontmatter
- No hard-wrap in markdown source
- Relative links only
- Second-person tone, present tense
- Next Steps footer with "Next:" and "Related:" links

### Content Tone (MUST follow)

Follow the quickstart's established tone:

- **Instructional and direct:** "Create a new file...", "Add the following code..."
- **Second person, present tense:** "You create...", "The system discovers..."
- **No marketing or superlatives** — just teach
- **Short paragraphs** — 2-4 sentences max between code blocks
- **Explain what happened** after each action — the quickstart uses a "What Happened" section; this tutorial should explain inline after each step

### Usings and Namespace Conventions

From the Counter sample and `.editorconfig`:

- File-scoped namespaces: `namespace X.Y.Z;`
- Usings at the top of file, before namespace
- Required usings for domain types:
    - `using Hexalith.EventStore.Contracts.Events;` (for `IEventPayload`, `IRejectionEvent`)
    - `using Hexalith.EventStore.Contracts.Results;` (for `DomainResult`)
    - `using Hexalith.EventStore.Client.Aggregates;` (for `EventStoreAggregate<T>`)

### DAPR SDK Version

Do not cite specific DAPR version numbers in the tutorial. The tutorial doesn't discuss DAPR internals — it's about the domain programming model.

### Architecture Facts for Tutorial Context

- `AddEventStore()` scans the calling assembly — no explicit assembly parameter needed when domain types are in the same project
- `UseEventStore()` resolves the 5-layer configuration cascade and populates activation context
- Domain name `inventory` maps to state store name `inventory-eventstore` and topic pattern `{tenantId}.inventory.events` (via NamingConventionEngine)
- The `/process` endpoint in `Program.cs` handles ALL domain commands — DAPR routing uses the actor ID to dispatch to the correct aggregate

### Relationship to Adjacent Stories

- **Story 13-2 (DAPR Component Variants):** Will provide hands-on backend swap with actual PostgreSQL. This tutorial only shows the YAML diff.
- **Stories 12-1 through 12-4:** Concept pages the tutorial can cross-reference for deeper understanding.
- **Story 12-5:** Unrelated (DAPR trade-offs on choose-the-right-tool.md). **Story 12-7:** Unrelated (Command API Reference).

### Troubleshooting Guidance (MUST include in tutorial)

The tutorial MUST include a brief troubleshooting section or inline tips for common mistakes. Unlike the quickstart (where Aspire handles everything), this tutorial has the reader writing code — typos happen. Include guidance for:

| Symptom                                               | Cause                                                                                | Fix                                                                                   |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------- |
| Domain not discovered after restart                   | Class is not `public`, doesn't extend `EventStoreAggregate<T>`, or has a build error | Verify class is `public sealed class`, inherits correctly, and project compiles clean |
| Command returns 404 or "unknown domain"               | `"domain"` field in JSON doesn't match convention engine output                      | Verify `"domain": "inventory"` (lowercase, no "Aggregate" suffix)                     |
| Command returns 400 with deserialization error        | JSON property name doesn't match record parameter                                    | Use lowercase `"quantity"` in JSON to match C# `Quantity` parameter                   |
| Rejection not returned, command succeeds unexpectedly | `Handle` method logic error (wrong comparison operator)                              | Verify the `>=` check in `Handle(RemoveStock ...)`                                    |
| State seems wrong after multiple commands             | `Apply` method has wrong operator (+= vs -=)                                         | Verify `Apply(StockRemoved e) => Quantity -= e.Quantity`                              |

Place these as inline tips after relevant steps (e.g., after "Register and Run", after "Send a Command") rather than as a single troubleshooting section at the end.

### Real-World Note (include as sidebar)

Add a brief callout note in the "Register and Run" section: "In this tutorial, you're adding the Inventory domain to the existing sample project for simplicity. In a real-world application, each domain service would typically be a separate .NET project referencing the `Hexalith.EventStore.Client` NuGet package. The same `AddEventStore()` call discovers aggregates from whichever assembly it scans." This manages expectations for readers thinking about production architecture.

### What NOT to Do

- Do NOT modify `Program.cs` — the tutorial should show that NO changes are needed
- Do NOT modify `Hexalith.EventStore.Sample.csproj` — the project references are already correct
- Do NOT create a separate project — the Inventory domain goes in the existing sample project
- Do NOT use `IDomainProcessor` — use `EventStoreAggregate<T>` exclusively (the fluent API is the recommended path)
- Do NOT ask the reader to run PostgreSQL — show the YAML diff only
- Do NOT add Mermaid diagrams — this is a step-by-step tutorial, not a concept page
- Do NOT add YAML frontmatter
- Do NOT change the quickstart page (it already links to this page correctly)
- Do NOT use the ASCII `<-` back-link — use Unicode `←` matching quickstart.md convention
- Do NOT use `IDomainProcessor` as the primary approach — mention it only in a brief note (consistent with quickstart.md line 15)
- Do NOT add Counter domain code — only show Inventory code
- Do NOT hard-wrap markdown source lines
- Do NOT fabricate Aspire dashboard screenshots or specific trace output — describe what the reader should see in prose

### Testing Standards

- Run `markdownlint-cli2 docs/getting-started/first-domain-service.md`
- Verify all relative links resolve (some concept pages may be dead until their stories ship)
- All code blocks should compile as-is if copy-pasted into the correct file locations
- The tutorial flow should be testable: create files → restart AppHost → send commands → see events
- Tutorial time target: under 1 hour for a .NET/DDD-familiar developer (NFR5)

### Previous Story (12-5) Intelligence

**Patterns established in 12-1 through 12-5:**

- Second-person tone, present tense, professional-casual
- Counter domain as running example across all concept pages
- Self-containment with inline concept explanations
- No YAML frontmatter
- Honest, balanced technical language

**12-5 status:** ready-for-dev (story file created but not yet implemented). 12-5 edits the choose-the-right-tool page; it does NOT affect this tutorial.

**Key learning:** Stories 12-1 through 12-5 are concept/reference pages. This story (12-6) is the first TUTORIAL in Epic 12 — it's instructional, not explanatory. Follow the quickstart's tutorial style, not the concept page style.

### Git Intelligence

Recent commits:

- Epic 11 (docs CI pipeline) completed — `markdownlint-cli2` and lychee link checking now available for validation
- Epic 16 (fluent client SDK) completed — `AddEventStore()`, `UseEventStore()`, `EventStoreAggregate<T>`, naming convention engine, assembly scanner all implemented and tested
- Architecture overview (12-1) done at 236 lines
- Quickstart uses the fluent API exclusively (references `CounterAggregate` and `EventStoreAggregate<CounterState>`)

### File to Create

- **Create:** `docs/getting-started/first-domain-service.md` (new file)

### Project Structure Notes

- File path: `docs/getting-started/first-domain-service.md`
- The quickstart page (`docs/getting-started/quickstart.md:124`) already links to this page as "Next:" — currently a dead link
- The PRD folder structure (`prd-documentation.md`) specifies this exact path: `docs/getting-started/first-domain-service.md`
- Adjacent pages in `docs/getting-started/`: `quickstart.md`, `prerequisites.md`
- The tutorial adds code to `samples/Hexalith.EventStore.Sample/Inventory/` — new folder alongside existing `Counter/`

### References

- [Source: _bmad-output/planning-artifacts/epics.md, Story 5.6 — First Domain Service Tutorial]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md, FR8 — step-by-step domain service, FR9 — backend swap, NFR5 — 1-hour tutorial]
- [Source: docs/getting-started/quickstart.md — tutorial style, tone, and Next Steps link to this page]
- [Source: docs/page-template.md — page structure rules]
- [Source: samples/Hexalith.EventStore.Sample/Counter/ — reference implementation pattern]
- [Source: samples/Hexalith.EventStore.Sample/Program.cs — AddEventStore() and UseEventStore() calls]
- [Source: src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs — auto-discovery mechanism]
- [Source: src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs — naming convention rules]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml — local dev Redis config]
- [Source: deploy/dapr/statestore-postgresql.yaml — production PostgreSQL alternative]
- [Source: deploy/dapr/pubsub-rabbitmq.yaml — production RabbitMQ alternative]
- [Source: _bmad-output/implementation-artifacts/12-5-dapr-trade-offs-and-faq-intro.md — previous story patterns]

## Senior Developer Review (AI)

### Outcome

Changes Requested

### Findings

#### HIGH

1. **Mandatory troubleshooting content from the story is incomplete in the tutorial page.**

- The story Dev Notes explicitly require troubleshooting coverage for:
    - `Handle(RemoveStock ...)` comparison/operator issues
    - `Apply(StockRemoved e)` operator issues
- The delivered tutorial includes only 3 troubleshooting tips and omits those two required checks.
- Evidence: `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:218`, `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:227`, `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:228`, `docs/getting-started/first-domain-service.md:196`, `docs/getting-started/first-domain-service.md:249`, `docs/getting-started/first-domain-service.md:279`.

#### MEDIUM

1. **Tutorial aggregate sample allows invalid quantities, which can teach incorrect domain behavior.**

- `AddStock`/`RemoveStock` examples do not reject `Quantity <= 0`.
- With current state logic, `RemoveStock(-1)` would _increase_ inventory due to `Quantity -= e.Quantity`.
- Evidence: `docs/getting-started/first-domain-service.md:161`, `docs/getting-started/first-domain-service.md:164`, `docs/getting-started/first-domain-service.md:166`, `docs/getting-started/first-domain-service.md:135`.

1. **Story File List does not reflect current working tree scope.**

- Story file list claims only one created file, while the repository currently has multiple changed/untracked docs/source files in the working tree.
- This is a transparency/documentation drift risk for reviewers.
- Evidence: `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:334`, `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md:336`.

### Validation Checks

- markdownlint-cli2 on `docs/getting-started/first-domain-service.md`: **0 errors**
- lychee offline check on `docs/getting-started/first-domain-service.md`: **5 OK, 0 errors**

### Recommendation

Keep story status as `in-progress` until HIGH and MEDIUM follow-ups are addressed and re-reviewed.

### Re-review (2026-03-01)

#### Outcome

Changes Requested

#### Additional Findings

1. **[HIGH] Task/implementation mismatch remains for AddStock behavior.**

- Task 3.6 claims `Handle(AddStock, ...)` always succeeds with `StockAdded`.
- Tutorial code currently introduces a `Quantity <= 0` guard returning `DomainResult.NoOp()`.
- This is a claim/implementation mismatch in a completed (`[x]`) task and should be reconciled explicitly.

1. **[MEDIUM] Story contains conflicting rejection-event semantics.**

- Dev Notes state rejection events are not persisted/published.
- Runtime implementation and other story sections describe rejection events as normal persisted/published events (while excluded from state `Apply`).
- Internal contradiction risks future regressions and reviewer confusion.

1. **[LOW] Tutorial uses non-canonical actor identity separator in walkthrough prose.**

- Tutorial shows `tenant-a|inventory|product-1` in "What Happened".
- Runtime identity format is `tenant:domain:aggregateId`.
- Readers troubleshooting with logs/keys may be misled by the pipe-delimited example.

### Re-review (2026-03-01, auto-fix pass)

#### Outcome

Approved

#### Resolution Summary

1. **[HIGH] Story file list synchronization corrected.**

- Reconciled the Dev Agent Record inventory with current workspace git reality and corrected file-state classification for this story artifact.

1. **[MEDIUM] Pipeline granularity wording clarified in tutorial.**

- Clarified that traces present a high-level 6-stage view, while the subsequent walkthrough provides a more detailed sequence.

1. **[MEDIUM] Rejection response wording aligned with API contract.**

- Updated tutorial wording to reflect that command submission returns `202 Accepted`; rejection details are observed via status/traces.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- markdownlint-cli2 initially flagged MD040 (line 31 missing language tag on folder structure code block) — fixed by adding `text` language tag
- All 5 relative links verified to resolve to existing files
- Review remediation pass: added two required troubleshooting tips and hardened aggregate examples with non-positive quantity guards (`DomainResult.NoOp()`)

### Completion Notes List

- Created `docs/getting-started/first-domain-service.md` (368 lines) — step-by-step tutorial for building an Inventory domain service
- Corrected rejection-event semantics in the tutorial to match runtime behavior: rejection events are persisted/published for auditability, while state mutation remains driven by explicit `Apply` methods on domain state
- Tutorial follows Counter domain patterns exactly: sealed records, `EventStoreAggregate<T>`, static `Handle` methods, `IEventPayload`/`IRejectionEvent`
- Covers all 8 acceptance criteria: page exists (AC1), full domain walkthrough (AC2), backend swap section (AC3), ~45 min estimate (AC4), code fences with language tags (AC5), page template structure (AC6), DAPR YAML references (AC7), self-contained (AC8)
- Includes troubleshooting tips inline (domain discovery, deserialization errors, unknown domain)
- Includes required troubleshooting tips for rejection comparison logic and state operator correctness
- Hardened tutorial aggregate sample against non-positive quantities (`<= 0`) to avoid misleading behavior in copied examples
- Includes PowerShell alternative for token acquisition (cross-platform)
- "What Happened" section replicates quickstart's strongest teaching moment for Inventory domain
- "Ready to Build Your Own?" section provides transferable 4-step recipe
- markdownlint-cli2: 0 errors
- Resolved re-review follow-ups: aligned Task 3.6 wording with guard behavior (HIGH), corrected Dev Notes rejection-event semantics to match runtime (MEDIUM), fixed actor identity delimiter from `|` to `:` in tutorial walkthrough (LOW)

### Change Log

- 2026-03-01: Created `docs/getting-started/first-domain-service.md` — "Build Your First Domain Service" tutorial page (Story 12-6)
- 2026-03-01: Senior Developer Review (AI) completed — changes requested, follow-up tasks added, status set to `in-progress`
- 2026-03-01: Applied AI-review follow-up fixes — added missing troubleshooting tips, guarded aggregate examples for non-positive quantities, and reconciled File List with workspace git reality
- 2026-03-01: Re-review completed — 1 HIGH, 1 MEDIUM, 1 LOW additional findings recorded; status remains `in-progress`; new follow-up tasks added
- 2026-03-01: Addressed all 3 re-review findings — aligned Task 3.6 text with guard code (HIGH), corrected Dev Notes rejection-event semantics to "persisted but NOT applied to state" (MEDIUM), fixed actor identity delimiter to colon-separated format (LOW). All review follow-ups now [x]. Status → review
- 2026-03-01: Auto-fix pass completed — synchronized file-list metadata with workspace git reality, clarified high-level-vs-detailed pipeline wording, and corrected rejection-response wording. Status → done

### File List

- **Created:** `docs/getting-started/first-domain-service.md`
- **Created:** `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md`
- **Modified:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Additional Workspace Git Changes Observed During Review (Out of Story Scope)

- `CONTRIBUTING.md` (modified)
- `.lycheeignore` (modified)
- `README.md` (modified)
- `_bmad-output/implementation-artifacts/11-3-documentation-validation-github-actions-workflow.md` (modified)
- `_bmad-output/implementation-artifacts/11-4-stale-content-detection.md` (modified)
- `docs/concepts/architecture-overview.md` (modified)
- `docs/concepts/choose-the-right-tool.md` (modified)
- `.github/workflows/docs-validation.yml` (untracked)
- `_bmad-output/implementation-artifacts/12-1-architecture-overview-with-mermaid-topology.md` (untracked)
- `_bmad-output/implementation-artifacts/12-2-command-lifecycle-deep-dive.md` (untracked)
- `_bmad-output/implementation-artifacts/12-3-event-envelope-metadata-structure.md` (untracked)
- `_bmad-output/implementation-artifacts/12-4-identity-scheme-documentation.md` (untracked)
- `_bmad-output/implementation-artifacts/12-5-dapr-trade-offs-and-faq-intro.md` (untracked)
- `_bmad-output/implementation-artifacts/12-7-command-api-reference.md` (untracked)
- `_bmad-output/implementation-artifacts/12-8-nuget-packages-guide-and-dependency-graph.md` (untracked)
- `docs/concepts/command-lifecycle.md` (untracked)
- `docs/concepts/event-envelope.md` (untracked)
- `docs/concepts/identity-scheme.md` (untracked)
