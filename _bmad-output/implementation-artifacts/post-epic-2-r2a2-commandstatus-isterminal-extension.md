# Story Post-Epic-2 R2-A2: Ship `CommandStatus.IsTerminal()` Extension and Deduplicate Controller Helpers

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform maintainer,
I want a single shared `CommandStatus.IsTerminal()` extension that lives in `Hexalith.EventStore.Contracts.Commands` and is consumed by every caller that needs the in-flight-vs-terminal split,
so that the terminal-status convention (`>= CommandStatus.Completed`) has exactly one definition that the test suite pins, the two duplicate `IsTerminalStatus(...)` helpers in `CommandStatusController` and `AdminTraceQueryController` are eliminated, and any future caller (status writers, replay paths, projection consumers, the Admin UI) can take a dependency on a public, documented contract surface instead of re-implementing the rule.

This story closes three carry-over retro action items in one fix:
- **Epic 1 retro R1-A4** (`CommandStatus.IsTerminal()` extension not shipped — was meant to fold into Story 2.4; didn't)
- **Epic 2 retro R2-A2** (`CommandStatus.IsTerminal()` extension never shipped; two controllers carry private `IsTerminalStatus(...)` helpers)
- **Epic 3 retro R3-A3** (shared `CommandStatus.IsTerminal()` extension still not addressed; Epic 4 retro confirms the rule remained un-elevated)

The originating spec is `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` § Proposal 4 (lines 283–364); the Epic 3 retro cleanup proposal § Proposal 3 (lines 180–193) explicitly routes R3-A3 to this same story to avoid spawning a duplicate.

## Acceptance Criteria

1. **`CommandStatus.IsTerminal()` extension exists in the Contracts package as a public, documented surface.** A new public extension method `IsTerminal(this CommandStatus status)` lives in `src/Hexalith.EventStore.Contracts/Commands/CommandStatusExtensions.cs` (new file). The method:
   - Lives in namespace `Hexalith.EventStore.Contracts.Commands` (file-scoped).
   - Is declared on a `public static class CommandStatusExtensions`.
   - Has the signature `public static bool IsTerminal(this CommandStatus status)`.
   - **Body defaults to the numeric convention `status >= CommandStatus.Completed`** (the form recommended by the originating proposal `sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md:305-306`, pinned by Story 1.5 round-2 dev notes `_bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md:243,250`, and asserted by `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs:53`). The switch-shape `status is CommandStatus.Completed or CommandStatus.Rejected or CommandStatus.PublishFailed or CommandStatus.TimedOut` is **acceptable** if a senior reviewer surfaces a strong case for explicitness (e.g., compiler-enforced typo safety, defensive default-to-in-flight semantics on a hypothetical future enum value). Whichever form is chosen, **AC #2's `IsTerminal_AgreesWithGreaterOrEqualCompletedConvention` test pins the contract and is non-negotiable**: the body's choice is implementation guidance, the convention test is the binding contract surface. If the dev accepts a switch-shape patch in review, the convention test continues to gate against drift; if the body and the convention test disagree, the test fails and review-feedback wins by definition.
   - Carries an XML-doc `<summary>` that names the convention, lists the four terminal values explicitly (Completed, Rejected, PublishFailed, TimedOut) for IntelliSense readers, and references `CommandStatus` (`<see cref="CommandStatus"/>`) so refactoring tools track the dependency.
   - Carries an XML-doc `<remarks>` block (or part of the summary) noting that the four in-flight values (Received, Processing, EventsStored, EventsPublished) all return `false`, and that the method is the single source of truth — callers MUST consume it instead of re-implementing the convention.

2. **Tier 1 unit tests cover all 8 enum values exhaustively.** A new test file `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusExtensionsTests.cs` contains a `CommandStatusExtensionsTests` class with the following coverage:
   - **One `[Theory] [InlineData(...)]` test** named `IsTerminal_ReturnsExpected_ForEachStatus` that feeds each of the 8 `CommandStatus` values and asserts the boolean result. Expected mapping (verbatim per the originating proposal `sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md:344-355`):
     - `Received` → `false`
     - `Processing` → `false`
     - `EventsStored` → `false`
     - `EventsPublished` → `false`
     - `Completed` → `true`
     - `Rejected` → `true`
     - `PublishFailed` → `true`
     - `TimedOut` → `true`
   - **One `[Fact]` test** named `IsTerminal_AgreesWithGreaterOrEqualCompletedConvention` that iterates `Enum.GetValues<CommandStatus>()` and asserts, for each value, `status.IsTerminal().ShouldBe((int)status >= (int)CommandStatus.Completed)`. This pins the *convention* (numeric `>=`) independently of the per-value table, so a future enum reorder cannot silently break the contract while leaving the per-value table green by accident. (If a future story adds a new `CommandStatus` value, this test forces a deliberate decision about which side of the boundary it falls on — exactly the kind of "stable enum-shape question" the originating spec calls out.) **This test pins the contract and is binding regardless of which body shape AC #1 lands on; the body in AC #1 is implementation guidance, this test is the load-bearing contract surface.** If a future change to the body causes this test to fail, the body change is wrong — not the test.
   - **One `[Fact]` test** named `IsTerminal_TerminalCount_IsExactly4` that asserts `Enum.GetValues<CommandStatus>().Count(s => s.IsTerminal()).ShouldBe(4)`. Pairs with the existing `CommandStatusTests.CommandStatus_HasExactly8Values` test in the same project to lock the 4-terminal / 4-in-flight split.
   - All tests use **xUnit 2.9.3 + Shouldly 4.3.0** per `CLAUDE.md` § Test Conventions. No mocking required (no NSubstitute usage; the extension is pure).
   - File header matches the project's existing convention: blank first line, single using import (`using Hexalith.EventStore.Contracts.Commands;`), file-scoped `namespace Hexalith.EventStore.Contracts.Tests.Commands;` per the precedent at `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs:1-4`.
   - All test methods carry XML-doc `<summary>` blocks explaining the contract under test (the existing `CommandStatusTests.cs` does not — but the tip-of-tree convention for new contract tests added in 2026-04 work is to include them; see `tests/Hexalith.EventStore.Testing.Tests/TerminatableComplianceAssertionsTests.cs` for a precedent in the same epic-cleanup window).

3. **Both duplicate controller helpers are deleted; both call sites consume the extension.**
   - **`src/Hexalith.EventStore/Controllers/CommandStatusController.cs`:** the private `IsTerminalStatus(CommandStatus status)` helper at lines 171–175 is **deleted in its entirety** (5-line method + the blank line before its closing brace). The single call site at line 123 (`if (!IsTerminalStatus(record.Status)) {`) is updated to `if (!record.Status.IsTerminal()) {`.
   - **`src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs`:** the private `IsTerminalStatus(CommandStatus status)` helper at lines 223–227 is **deleted in its entirety** (5-line expression-bodied method). The single call site at line 77 (`if (IsTerminalStatus(commandStatus.Status)) {`) is updated to `if (commandStatus.Status.IsTerminal()) {`.
   - **Using directives:** `Hexalith.EventStore.Contracts.Commands` is already imported in both files (verified — `CommandStatusController.cs:4` and `AdminTraceQueryController.cs:5` both have `using Hexalith.EventStore.Contracts.Commands;` for `CommandStatus` itself). **No new using directive is needed** because the extension lives in the same namespace as the enum it extends. If `dotnet format` or the IDE proposes adding/removing usings, accept only changes that match `.editorconfig` ordering rules and do not introduce unrelated using-directive churn elsewhere in the file.
   - **Behavior preserved:** the new extension body (whichever AC #1 form lands — numeric or switch-shape) MUST produce the *exact same* boolean output as the deleted `is Completed or Rejected or PublishFailed or TimedOut` switches for every enum value the controllers can observe. The four terminal values map to integers 4, 5, 6, 7, so both forms are mathematically equivalent today. AC #2's convention-agreement test is the load-bearing guard against any future divergence; AC #3 inherits the guarantee from that test.

4. **A repository-wide grep confirms no surviving duplicate helpers.** After the patch:
   - `Grep` for `IsTerminalStatus` across `src/**/*.cs` returns **zero matches**. (Pre-patch: 4 matches across 2 files — 2 declarations at the private-method sites, 2 call sites. All 4 are removed: declarations deleted, call sites converted to extension-method invocations.)
   - `Grep` for `IsTerminalStatus` across `tests/**/*.cs` also returns **zero matches** (pre-patch: 0 matches; this AC ensures no test gets added that re-declares the helper under a misleading name).
   - `Grep` for `is CommandStatus\.Completed[\s\S]{0,200}or CommandStatus\.Rejected` (multiline) across `src/**/*.cs` returns **zero matches** outside `src/Hexalith.EventStore.Contracts/Commands/CommandStatusExtensions.cs` itself. This catches any other file that may have a copy-paste of the four-way `or` pattern (none known today, but the rule is a forward-looking guard).
   - `Grep` for `IsTerminal\(` across `src/**/*.cs` returns **at least 3 matches** post-patch: the extension declaration (1) + the two new call sites (2). It MUST NOT match any `IsTerminalError` or `IsTerminalState` helper that may exist in unrelated UI code (`_bmad-output/planning-artifacts/design-directions-prototype/Components/Shared/CommandPipeline.razor:32` is a prototype-only `IsTerminalError` and is OUT OF SCOPE — do not "fix" prototype/design-direction files; they are not shipped code).

5. **All Tier 1 windows green; Contracts.Tests grows by exactly 10 visible rows (3 methods).** Zero pre-patch tests may regress. The "271" baseline is from `post-epic-2-r2a8-pipeline-nullref-fix.md` § Validation Results (2026-04-27) — re-baseline at dev start per Task 1.5 in case sibling backlog items shifted it.

   | Project | Pre-patch | Post-patch |
   |---------|-----------|------------|
   | `Hexalith.EventStore.Contracts.Tests` | 271 | 281 (+10) |
   | `Hexalith.EventStore.Client.Tests` | 334 | 334 |
   | `Hexalith.EventStore.Sample.Tests` | 63 | 63 |
   | `Hexalith.EventStore.Testing.Tests` | 78 | 78 |
   | `Hexalith.EventStore.SignalR.Tests` | 32 | 32 |
   | **Net Tier 1** | **778** | **788** |

6. **Tier 2 (`Server.Tests`) green; controller tests still pass under the refactor.** `dotnet test tests/Hexalith.EventStore.Server.Tests/ -p:NuGetAudit=false` (post-`dapr init`) → **1643 / 1643** Pass (post-patch baseline; was 1643/1643 per the post-epic-2-r2a8 closure validation). The two controllers' existing tests (search the project for `CommandStatusController`-targeted and `AdminTraceQueryController`-targeted tests, e.g., `AdminTraceQueryControllerTests`) continue to pass without modification. **The behavior-preservation argument:** the refactor swaps the body of a boolean check from `IsTerminalStatus(s)` to `s.IsTerminal()`; the truth table is identical for all 8 enum values; therefore no controller test should observe any change in HTTP status, header, or body.

7. **Full Release build remains 0 warnings, 0 errors.** `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` → 0/0 with `TreatWarningsAsErrors=true`. Pre-existing `NU1902` OpenTelemetry transitive CVE warnings continue to be bypassed via `-p:NuGetAudit=false` per the post-epic-1/2 cluster precedent. The new `CommandStatusExtensions` class and `IsTerminal` method MUST carry XML doc comments (CS1591 is enforced in `src/`); Task 7.4 verifies this by deliberately removing one doc and confirming the build fails before restoring it.

8. **No public API regressions.** Run a public-surface diff to confirm the *additive-only* shape:
   - **Added (intentional):** `Hexalith.EventStore.Contracts.Commands.CommandStatusExtensions` (new public static class) and its `IsTerminal(this CommandStatus)` method.
   - **Removed:** *nothing public* (the two deleted helpers are `private static` — invisible across the assembly boundary).
   - **Changed:** *nothing public* (the public surface of `CommandStatus`, `CommandStatusRecord`, `CommandStatusController`, `AdminTraceQueryController` is unchanged).
   - This justifies the **`feat(contracts):` Conventional Commits prefix** (minor-version bump under semantic-release: a new public extension method is a new feature, even though it codifies an existing convention). It does NOT justify a `BREAKING CHANGE:` token — no consumer's compilation breaks, no behavior changes.

9. **Epic 1 retro R1-A4, Epic 2 retro R2-A2, and Epic 3 retro R3-A3 are all marked complete in their action-item tables.** Update three retro files (mirror the post-epic-1 cluster's closure-annotation precedent):
   - `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md` § 6 row R1-A4: append `✅ Done <merge-commit-sha> — shared CommandStatus.IsTerminal() extension shipped in Contracts; both controllers consume it (post-epic-2-r2a2-commandstatus-isterminal-extension)`.
   - `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 row R2-A2: append `✅ Done <merge-commit-sha> — extension exists in Hexalith.EventStore.Contracts.Commands; CommandStatusController + AdminTraceQueryController consume it; 3 Tier 1 tests cover all 8 enum values + convention agreement`. Also update § 10 Commitments "Critical path before Epic 3 closes" line: append `[R2-A2 ✅ Closed <merge-commit-sha>]` after the R2-A2 token (do NOT delete the line — preserve the audit trail per the post-epic-2-r2a8 precedent at `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md:160`).
   - `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md` § 8 row R3-A3: append `✅ Done <merge-commit-sha> — addressed by post-epic-2-r2a2 (single story closes R1-A4 + R2-A2 + R3-A3 per sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md § Proposal 3)`. Also update § 5 "Previous Retro Follow-Through" row "R2-A2: shared CommandStatus.IsTerminal() extension" → change "Not addressed" to "Addressed by post-epic-2-r2a2-commandstatus-isterminal-extension (merged <date>)".
   - **Do NOT modify** `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md` even though its § 5 table mentions R3-A3 as "Not addressed" — that retro pre-dates this story by design and represents the state at retro time. The Epic 4 retro stands as a historical record. (Same convention as Epic 1 / Epic 2 retros being preserved when post-epic stories close their items.)
   - **Merge-race guard:** `post-epic-3-r3a1-replay-ulid-validation` (also backlog) may also need to edit `epic-3-retro-2026-04-26.md` § 8. If a sibling post-epic-* story has an open PR touching the same retro file, **rebase before merging — do NOT auto-resolve**. Each post-epic story owns its own retro-row append; a textual auto-merge can silently overwrite the sibling's annotation.

10. **Conventional-commit hygiene on the merge.** The merge commit (or squashed PR title) uses the **`feat(contracts):`** prefix per `CLAUDE.md` § Commit Messages, since this is a new public API in the Contracts package. Example acceptable subjects:
    - `feat(contracts): add CommandStatus.IsTerminal() extension and remove duplicate controller helpers`
    - `feat(contracts): ship CommandStatus.IsTerminal() (closes R1-A4, R2-A2, R3-A3)`
    The body MUST list the three retro action items closed (R1-A4, R2-A2, R3-A3) so a reader of `git log` sees the full closure trail. **Do NOT use `BREAKING CHANGE:`** — no breaking change occurs. **Do NOT use `refactor(contracts):`** — even though deduplication is part of the diff, the dominant change is a new public surface (a new extension method) shipped in a public NuGet package; `feat:` is the truthful prefix and produces the right semantic-release minor bump.

11. **Sprint-status updated to `done` post-merge.** `_bmad-output/implementation-artifacts/sprint-status.yaml` development_status entry `post-epic-2-r2a2-commandstatus-isterminal-extension` is updated through the lifecycle: `backlog` → `ready-for-dev` (this story creation) → `in-progress` (dev start) → `review` (PR opened) → `done` (post-merge with closure annotation referencing the merge commit and the three retro action items closed). Mirror the post-epic-2-r2a8 closure-annotation style. `last_updated` is updated to the closure date.

## Tasks / Subtasks

- [x] Task 1: Verify the pre-patch state (AC: #3, #4)
  - [x] 1.1 Run `git status` and confirm the working tree is clean except for the expected untracked `Hexalith.Tenants` submodule pointer, `.claude/mcp.json`, and `_tmp_diff.patch` (per current sprint-status.yaml header). If unexpected uncommitted changes exist, stash or commit them before starting work to keep the diff clean.
  - [x] 1.2 Confirm the two duplicate helpers exist at the documented locations: `Grep` for `IsTerminalStatus` across `src/**/*.cs` should return exactly 4 lines:
    - `src/Hexalith.EventStore/Controllers/CommandStatusController.cs:123` (call site, body-statement form)
    - `src/Hexalith.EventStore/Controllers/CommandStatusController.cs:171` (declaration, expression-body switch on `is X or Y or Z or W`)
    - `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs:77` (call site)
    - `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs:223` (declaration, expression-body switch on `is X or Y or Z or W`)
    If any of these line numbers have shifted (e.g., from a recent unrelated edit), update the file references in this story's Dev Notes → File Locations table before proceeding so reviewers can audit the changes against the as-edited tree.
  - [x] 1.3 Confirm both controllers already import `Hexalith.EventStore.Contracts.Commands` (the namespace where the new extension will live). Per the verified state: `CommandStatusController.cs:4` and `AdminTraceQueryController.cs:5` both have the directive. **No new using directive is required for AC #3** — the extension method becomes available implicitly via the existing namespace import.
  - [x] 1.4 Run the existing `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs` to capture the pre-patch baseline: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter "FullyQualifiedName~CommandStatusTests"`. Expected: 11 passed (1 `[Fact]` for `HasExactly8Values` + 8 `[Theory]` rows for `HasCorrectExplicitIntegerValues` + 1 `[Fact]` for `ValuesAreInLifecycleOrder` + 1 `[Fact]` for `TerminalStatuses_AreIdentifiedCorrectly`). Capture the count to validate the +3-method / +10-row delta in AC #5.
  - [x] 1.5 **Re-baseline the full Contracts.Tests window before editing anything.** Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` and record the actual current pass count (story spec assumes 271, sourced from `post-epic-2-r2a8-pipeline-nullref-fix.md` § Validation Results 2026-04-27). If the live count differs, **update AC #5's table and Task 3.5's expected post-patch count** (live count + 10) before proceeding. AC #5's "+10 visible rows" delta is non-negotiable; the baseline is whatever the dev observes at story start, not whatever the spec inherited from a sibling story's snapshot.

- [x] Task 2: Add the extension method (AC: #1)
  - [x] 2.1 Create `src/Hexalith.EventStore.Contracts/Commands/CommandStatusExtensions.cs` (new file). File contents (verbatim guidance, but match the project's existing file-header style — single blank leading line, no copyright header per the existing `CommandStatus.cs:1-2` precedent):
    ```csharp
    namespace Hexalith.EventStore.Contracts.Commands;

    /// <summary>
    /// Extension methods for <see cref="CommandStatus"/>.
    /// </summary>
    public static class CommandStatusExtensions
    {
        /// <summary>
        /// Determines whether the command has reached a terminal state — i.e., a state from which
        /// no further status transitions occur.
        /// </summary>
        /// <param name="status">The command lifecycle status to inspect.</param>
        /// <returns>
        /// <c>true</c> if the status is one of <see cref="CommandStatus.Completed"/>,
        /// <see cref="CommandStatus.Rejected"/>, <see cref="CommandStatus.PublishFailed"/>, or
        /// <see cref="CommandStatus.TimedOut"/>; <c>false</c> for the in-flight states
        /// <see cref="CommandStatus.Received"/>, <see cref="CommandStatus.Processing"/>,
        /// <see cref="CommandStatus.EventsStored"/>, and <see cref="CommandStatus.EventsPublished"/>.
        /// </returns>
        /// <remarks>
        /// The terminal/in-flight split is determined by the numeric convention
        /// <c>status &gt;= CommandStatus.Completed</c>, which is the single source of truth
        /// for this contract. Callers MUST consume this extension instead of re-implementing
        /// the convention to keep the rule centralized and testable.
        /// </remarks>
        public static bool IsTerminal(this CommandStatus status)
            => status >= CommandStatus.Completed;
    }
    ```
    Match the project's brace style (Allman, opening brace on a new line for type declarations) — verify against `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` and other files in the same folder. The expression-bodied member uses the existing `=>` convention (consistent with `CommandStatus.cs` enum and the deleted controller helpers).
  - [x] 2.2 Build the Contracts project alone first to catch any compile error early: `dotnet build src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj --configuration Release -p:NuGetAudit=false`. Expected: 0 warnings, 0 errors. **If a CS1591 missing-XML-doc warning fires** on `CommandStatusExtensions` or `IsTerminal`, the doc comments are missing or malformed — fix and re-run before continuing. (CS1591 is suppressed in test projects but enforced in `src/` per `Directory.Build.props` / project-level XML doc generation.)
  - [x] 2.3 Run the existing Contracts test window once with the new extension method present (no new tests yet): `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`. Expected: 271 / 271 (no change vs. Task 1.4 baseline). Confirms the extension's presence didn't break any existing test.

- [x] Task 3: Add the Tier 1 unit tests (AC: #2)
  - [x] 3.1 Create `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusExtensionsTests.cs` (new file). Contents (verbatim — match the existing test-file header convention from `CommandStatusTests.cs:1-4`):
    ```csharp

    using Hexalith.EventStore.Contracts.Commands;

    namespace Hexalith.EventStore.Contracts.Tests.Commands;

    public class CommandStatusExtensionsTests {
        [Theory]
        [InlineData(CommandStatus.Received, false)]
        [InlineData(CommandStatus.Processing, false)]
        [InlineData(CommandStatus.EventsStored, false)]
        [InlineData(CommandStatus.EventsPublished, false)]
        [InlineData(CommandStatus.Completed, true)]
        [InlineData(CommandStatus.Rejected, true)]
        [InlineData(CommandStatus.PublishFailed, true)]
        [InlineData(CommandStatus.TimedOut, true)]
        public void IsTerminal_ReturnsExpected_ForEachStatus(CommandStatus status, bool expected)
            => status.IsTerminal().ShouldBe(expected);

        [Fact]
        public void IsTerminal_AgreesWithGreaterOrEqualCompletedConvention() {
            foreach (CommandStatus status in Enum.GetValues<CommandStatus>()) {
                status.IsTerminal().ShouldBe((int)status >= (int)CommandStatus.Completed);
            }
        }

        [Fact]
        public void IsTerminal_TerminalCount_IsExactly4()
            => Enum.GetValues<CommandStatus>().Count(s => s.IsTerminal()).ShouldBe(4);
    }
    ```
    The leading blank line in the file (line 1 is empty, line 2 is the `using`) matches `CommandStatusTests.cs:1-2`'s convention. Do NOT add a `using Shouldly;` directive — `GlobalUsings.cs` (or equivalent in the test project) already imports it (verified by inspection of `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs:6` which uses `ShouldBe` without an explicit using).
  - [x] 3.2 If the test project does not already use `Shouldly` globally (verify with a quick `Grep` for `using Shouldly` across `tests/Hexalith.EventStore.Contracts.Tests/**/*.cs` — if zero matches AND all existing tests use `.ShouldBe(...)`, then a `GlobalUsings` setup is in play; otherwise add `using Shouldly;` explicitly to the new file). The new file MUST compile and run; do not assume — verify.
  - [x] 3.3 Optionally add per-test XML-doc summaries (the project's tip-of-tree convention for new contract tests). Example for `IsTerminal_ReturnsExpected_ForEachStatus`:
    ```csharp
    /// <summary>
    /// Pins the per-value mapping of <see cref="CommandStatus"/> to <see cref="CommandStatusExtensions.IsTerminal"/>.
    /// Closes Epic 1 retro R1-A4 / Epic 2 retro R2-A2 / Epic 3 retro R3-A3.
    /// </summary>
    ```
    Adding the docs is preferred for forward-readability but is not load-bearing for AC #2 (the precedent file `CommandStatusTests.cs` does not have them; either style is acceptable to merge).
  - [x] 3.4 Run the new test class in isolation: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter "FullyQualifiedName~CommandStatusExtensionsTests"`. Expected: 10 passed (8 Theory rows + 2 Facts), 0 failed.
  - [x] 3.5 Run the full Contracts.Tests window: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`. Expected: 281 / 281 (was 271; +10 visible test rows). If the count is different, investigate before proceeding to Task 4 — a per-row tally mismatch usually means an `[InlineData]` row was duplicated or omitted.

- [x] Task 4: Refactor `CommandStatusController` to consume the extension (AC: #3, #4, #6)
  - [x] 4.1 Open `src/Hexalith.EventStore/Controllers/CommandStatusController.cs`. At line 123, change:
    ```csharp
    if (!IsTerminalStatus(record.Status)) {
    ```
    to:
    ```csharp
    if (!record.Status.IsTerminal()) {
    ```
  - [x] 4.2 Delete the private helper at lines 171–175 in its entirety:
    ```csharp
        private static bool IsTerminalStatus(CommandStatus status) =>
            status is CommandStatus.Completed
                or CommandStatus.Rejected
                or CommandStatus.PublishFailed
                or CommandStatus.TimedOut;
    ```
    Including the blank line that precedes the closing `}` of the class (so the file ends with `}` on its own line, matching the trailing-newline convention).
  - [x] 4.3 Verify no other call to `IsTerminalStatus` exists in this file: `Grep` for `IsTerminalStatus` in `src/Hexalith.EventStore/Controllers/CommandStatusController.cs` should return 0 matches after the patch. (Pre-patch: 2 matches — call site + declaration. Post-patch: 0.)
  - [x] 4.4 Confirm no using directive needs to change. The file already imports `Hexalith.EventStore.Contracts.Commands` at line 4, which exposes both `CommandStatus` (in use) and the new `CommandStatusExtensions` (used implicitly via the extension method invocation). If `dotnet format` proposes any using-directive change unrelated to this fix, **reject it** to keep the PR diff narrow.
  - [x] 4.5 Build the host project: `dotnet build src/Hexalith.EventStore/Hexalith.EventStore.csproj --configuration Release -p:NuGetAudit=false`. Expected: 0/0.

- [x] Task 5: Refactor `AdminTraceQueryController` to consume the extension (AC: #3, #4, #6)
  - [x] 5.1 Open `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs`. At line 77, change:
    ```csharp
    if (IsTerminalStatus(commandStatus.Status)) {
    ```
    to:
    ```csharp
    if (commandStatus.Status.IsTerminal()) {
    ```
  - [x] 5.2 Delete the private helper at lines 223–227 in its entirety:
    ```csharp
        private static bool IsTerminalStatus(CommandStatus status)
            => status is CommandStatus.Completed
                or CommandStatus.Rejected
                or CommandStatus.PublishFailed
                or CommandStatus.TimedOut;
    ```
    The shape of this controller's helper is slightly different from `CommandStatusController`'s (expression-body with `=>` on its own line vs. the other's `=>` immediately after the parameter list) — both must go. The remaining `IsRejectionEvent` private helper at line 229 stays in place; it is unrelated.
  - [x] 5.3 Verify no other call: `Grep` for `IsTerminalStatus` in `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs` should return 0 matches after the patch.
  - [x] 5.4 Using-directive check: `Hexalith.EventStore.Contracts.Commands` is already at line 5 — no change needed. Reject any unrelated using churn.
  - [x] 5.5 Build: same command as 4.5 (the project is the same).

- [x] Task 6: Execute the AC #4 grep audit. Out-of-scope ignore list: prototype razor (`design-directions-prototype/.../CommandPipeline.razor:32`), `_bmad-output/implementation-artifacts/*.md` (in-scope only via AC #9), `Hexalith.Tenants/...` submodule (separate repo). Optional awareness: grep `Hexalith.Tenants/**/*.cs` for `IsTerminalStatus` and log the count in Dev Agent Record → Completion Notes; do not edit the submodule from this PR.

- [x] Task 7: Validate the change set end-to-end (AC: #5, #6, #7, #8)
  - [x] 7.1 Run all Tier 1 windows in parallel (per `CLAUDE.md` § Build & Test Commands):
    - `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` → 281 / 281 visible rows passing (3 new methods: 1 `[Theory]` × 8 + 2 `[Fact]` = 10 rows on top of the 271 pre-patch baseline). Confirm ≥ 281 and zero failures.
    - `dotnet test tests/Hexalith.EventStore.Client.Tests/ -p:NuGetAudit=false` → 334 / 334.
    - `dotnet test tests/Hexalith.EventStore.Sample.Tests/ -p:NuGetAudit=false` → 63 / 63.
    - `dotnet test tests/Hexalith.EventStore.Testing.Tests/ -p:NuGetAudit=false` → 78 / 78.
    - `dotnet test tests/Hexalith.EventStore.SignalR.Tests/` → baseline preserved.
  - [x] 7.2 Run Tier 2: `dotnet test tests/Hexalith.EventStore.Server.Tests/ -p:NuGetAudit=false` (DAPR must be initialized; if `dapr list` shows no sidecars, run `dapr init` first per `CLAUDE.md` § Build & Test Commands). Expected: **1643 / 1643** Pass. The two controllers' tests in this project (search for `CommandStatusController` and `AdminTraceQueryController` test-class targets) MUST be among the green tests; confirm by isolating with a filter if any failure surfaces.
  - [x] 7.3 Tier 3 is **NOT** in scope. The refactor preserves HTTP behavior (AC #6) so end-to-end Aspire contract tests would not change. Per `CLAUDE.md`, Tier 3 is optional.
  - [x] 7.4 Full Release build: `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` → 0 warnings, 0 errors. (Note: per `CLAUDE.md` § Solution File, **only `Hexalith.EventStore.slnx`** — never `.sln`.)
    - **CS1591 sanity check (AC #7):** before final commit, deliberately delete the XML doc on `IsTerminal` (or `CommandStatusExtensions`) and re-run the build; confirm it FAILS with CS1591. Restore the doc and confirm it returns to 0/0. This proves the analyzer fires on the new file (not just trusts the project's analyzer-config inheritance). Skip if a public-API analyzer (`Microsoft.CodeAnalysis.PublicApiAnalyzers`) is wired up at the Contracts project level — that catches the same class.
  - [x] 7.5 Public-surface diff (AC #8): if a public-API analyzer is configured (e.g., `Microsoft.CodeAnalysis.PublicApiAnalyzers`), confirm the only added line is the new `IsTerminal` method. If no analyzer is wired up, capture the additive surface in the dev notes by listing the new public type+method explicitly. Confirm no `[Obsolete]` markers were added or removed.

- [x] Task 8: Apply the three retro-file closure annotations per AC #9 (Epic 1 R1-A4, Epic 2 R2-A2 + § 10, Epic 3 R3-A3 + § 5; preserve Epic 4 retro and both sprint-change-proposals as-written). Watch for the merge-race on `epic-3-retro-2026-04-26.md` against `post-epic-3-r3a1-replay-ulid-validation` — rebase, do not auto-resolve.

- [~] Task 9: Move `sprint-status.yaml` entry through the lifecycle per AC #11: `ready-for-dev` → `in-progress` (dev start) → `review` (PR open) → `done` (post-merge with the post-epic-2-r2a8-style closure annotation referencing the merge SHA, the Tier 1 delta, and the three retro closures). Bump `last_updated` on each transition. — **PARTIAL**: `→ review` done 2026-04-28; `→ done` pending post-merge (see "Post-Merge Runbook" in Dev Agent Record below).

- [~] Task 10: Conventional-commit-formatted PR / merge commit (AC: #10) — **PARTIAL**: 10.1 + 10.2 done at PR-open 2026-04-28 (PR #220); 10.3 pending squash-merge.
  - [x] 10.1 PR title: `feat(contracts): add CommandStatus.IsTerminal() extension and remove duplicate controller helpers`. PR body: bullets the three retro closures (R1-A4, R2-A2, R3-A3), the originating proposal (Proposal 4 in `sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md`), and the cross-route from R3-A3 (Proposal 3 in `sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md`). **Opened as PR #220 on branch `feat/post-epic-2-r2a2-commandstatus-isterminal` 2026-04-28.**
  - [x] 10.2 Verified pre-commit hooks pass — commit `35c91cc` landed clean (per `CLAUDE.md` § Git Safety Protocol — `--no-verify` not used).
  - [ ] 10.3 On squash-merge: confirm the squashed commit subject still uses the `feat(contracts):` prefix so semantic-release recognizes the minor-version bump. **Pending squash-merge of PR #220.**

## Dev Notes

### Scope Summary

This is a small, low-risk refactor + new public API story. It introduces exactly **one new file** in `src/`, **one new file** in `tests/`, and **modifies two existing controllers**. It deletes **two private helpers** (10 source lines total, 5 in each controller). It updates **three retro markdown files** with closure annotations and **one sprint-status.yaml** entry with lifecycle transitions.

This story does NOT:
- Change the `CommandStatus` enum itself (no new values, no reordering, no `[DataMember]` changes).
- Touch the `CommandStatusRecord` record.
- Modify any controller's HTTP behavior, routes, OpenAPI annotations, authorization, or response shapes.
- Touch the actor pipeline (`AggregateActor`, `SubmitCommandHandler`, `CommandRouter`, `CommandStatusWriter`).
- Touch any DAPR or pub/sub integration.
- Resolve any other carry-over retro items beyond R1-A4, R2-A2, and R3-A3.

### Why This Story Exists

Three retros, in three consecutive epics, all flagged the same gap:

- **Epic 1 retro R1-A4** (2026-04-26 in `epic-1-retro-2026-04-26.md:85`): "Ship `CommandStatus.IsTerminal()` extension method. Owner: Dev. Priority: Medium (folds into Story 2.4). Done When: Used by `CommandStatusWriter`; replaces convention." Story 2.4 closed without shipping the extension. The convention (`status >= Completed`) was tested at `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs:53` but never wrapped in a public API surface.

- **Epic 2 retro R2-A2** (`epic-2-retro-2026-04-26.md:98`): "Confirm R1-A4: verify `CommandStatus.IsTerminal()` extension exists (folded into 2.4); if not, ship the one-liner with test." The verification found the extension absent — and worse, found two private duplicates of the convention had landed in `CommandStatusController.cs:171` and `AdminTraceQueryController.cs:223` between the retros.

- **Epic 3 retro R3-A3** (`epic-3-retro-2026-04-26.md:130`): "Add shared `CommandStatus.IsTerminal()` extension and migrate private helper call sites." Epic 3 added the `AdminTraceQueryController` (Story 20.5 / Epic 20 work, but the helper landed in the same EventStore project) without noticing the duplicate. The R3-A3 cleanup proposal at `sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md:184` explicitly routed the fix to **this story** (`post-epic-2-r2a2-commandstatus-isterminal-extension`) to avoid spawning a duplicate fix story.

- **Epic 4 retro** (`epic-4-retro-2026-04-26.md:85`) re-flagged R3-A3 as "Not addressed" in its previous-retro follow-through table, confirming the convention drift was carried into Epic 4 even though Epic 4 didn't touch the affected controllers. The drift is "stable" in the same sense the post-epic-2-r2a8 story called out: stable doesn't mean fine, it means it has accumulated four epics of inertia.

The originating sprint-change-proposal § Proposal 4 (`sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md:283-364`) classifies this as **trivial risk** (`§ 2 Technical Impact`, line 90: "New: `CommandStatus.IsTerminal()` extension method on `CommandStatus` enum (or static class in `Hexalith.EventStore.Contracts.Commands`). Refactor `CommandStatusController:171` and `AdminTraceQueryController:223` to use it. ~5 file edits | New Tier 1 test for the extension; controller tests unchanged (behavior identical) | None | Trivial").

Per `CLAUDE.md` § Code Review Process: senior review across Epic 2 produced HIGH/MEDIUM patches on 5/5 stories. This story's risk is structurally low, but the originating proposal's "trivial" classification should not be read as "no review needed." The likely review-found patches will be cosmetic (XML doc wording, test naming, whether to use `>= Completed` numeric vs. switch-shape body — see AC #1 for the project lead's pre-decision); budget one round of patch turnaround per `CLAUDE.md` § Code Review Process precedent.

### File Locations (verified at HEAD `7708636`, 2026-04-28)

| File | Role | Pre-patch state | Post-patch state |
|------|------|----------------|------------------|
| `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` | Enum definition | 8 values, lines 1-34 | Unchanged |
| `src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs` | Status record | Unchanged | Unchanged |
| `src/Hexalith.EventStore.Contracts/Commands/CommandStatusExtensions.cs` | NEW — extension method | Does not exist | Contains `IsTerminal` |
| `src/Hexalith.EventStore/Controllers/CommandStatusController.cs` | Status query controller | `IsTerminalStatus` at lines 123 (call) + 171 (decl) | Both lines deleted; line 123 → `record.Status.IsTerminal()` |
| `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs` | Admin trace query controller | `IsTerminalStatus` at lines 77 (call) + 223 (decl) | Both lines deleted; line 77 → `commandStatus.Status.IsTerminal()` |
| `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusExtensionsTests.cs` | NEW — Tier 1 unit tests | Does not exist | 3 test methods (1 Theory + 2 Facts) |
| `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs` | Existing enum tests | 4 methods (1 Fact + 1 Theory + 2 Facts), 11 visible rows | Unchanged |
| `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md` | Epic 1 retro | R1-A4 row at line 85 | R1-A4 row annotated `✅ Done <sha>` |
| `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` | Epic 2 retro | R2-A2 row at line 98 + § 10 line 161 | R2-A2 row + § 10 line annotated |
| `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md` | Epic 3 retro | R3-A3 row at line 130 + § 5 row at line 85 | Both rows annotated |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | Sprint tracking | `post-epic-2-r2a2-...: backlog` (line 276) | `done` with closure annotation |

If line numbers have shifted at the dev's start (e.g., from a recent unrelated edit), update this table before proceeding so reviewers can audit changes against the as-edited tree (this is the same discipline the post-epic-2-r2a8 story used).

**Known documentation-drift after this PR (out of scope; flag for future):** `CommandStatusController.cs:33-48` carries OpenAPI XML doc `<remarks>` that hard-codes the in-flight-vs-terminal split as inline prose ("In-flight states", "Terminal states", with the four values in each bucket listed by name). Once `IsTerminal()` is the contract source of truth, those XML docs are a separate documentation source that will go stale on the next enum addition. **Do NOT refactor the XML docs to reference `IsTerminal()` in this PR** — that would force OpenAPI regeneration and broaden review scope. File the gap as a future-work note in Dev Agent Record → Completion Notes; the OpenAPI doc-source unification belongs to a future Epic 3-followup or doc-cleanup story.

### Architecture Decisions

- **Why `CommandStatusExtensions` (separate static class) and not a method on `CommandStatus` itself?** `CommandStatus` is an `enum`. C# does not allow instance methods on enums. The idiomatic pattern is an extension method in a sibling static class — exactly what the originating proposal recommends (`sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md:299-306`).

- **Why namespace `Hexalith.EventStore.Contracts.Commands` (not `Hexalith.EventStore.Contracts.Commands.Extensions` or similar)?** Two reasons: (1) it places the extension in the same namespace as the enum it extends, so any caller that already imports the enum's namespace gets the extension implicitly — no using-directive churn (AC #3.4 leverages this). (2) The Contracts package's existing convention is flat-by-feature: `Commands/`, `Events/`, `Identity/`, `Errors/`. Adding a sub-namespace for this one extension would be inconsistent and would force using-directive updates everywhere it's consumed.

- **Why `>= Completed` body (default) and not the switch-shape?** See ADR-1 below — captured as an explicit decision record because it surfaced as a live trade-off during story creation.

- **Why no `IsInFlight()` extension, even though the proposal could naturally add one?** Out of scope for this story. The two call sites both negate the terminal check (`!IsTerminalStatus(...)`); adding `IsInFlight()` would be a nice-to-have but is not asked for in any retro action item. Defer to a future story if a third call site needs the affirmative form. **Per `CLAUDE.md` system instructions: "Don't add features, refactor, or introduce abstractions beyond what the task requires."**

- **Why no `CommandStatusWriter` migration?** R1-A4's "Done When" line says "Used by `CommandStatusWriter`" — but a search confirmed `CommandStatusWriter` does not currently re-implement the convention; it consumes `CommandStatus` values without making a terminal-vs-in-flight decision (it writes whatever status the caller passes). So the original R1-A4 framing was speculative about a use site that didn't materialize. The two real consumers are the two controllers; this story migrates both. **Recording this as a deliberate scope-narrowing decision so future readers don't conclude R1-A4 was incompletely closed.**

### ADR-1: Body Shape of `CommandStatus.IsTerminal()`

**Context.** Two equivalent implementations: `status >= CommandStatus.Completed` (numeric) or `status is CommandStatus.Completed or CommandStatus.Rejected or CommandStatus.PublishFailed or CommandStatus.TimedOut` (switch). Both produce identical truth tables for the 8 current enum values. They diverge only on a hypothetical 9th value: numeric defaults to terminal (any new value with integer ≥ 4); switch defaults to in-flight (any value not explicitly listed). Default-to-in-flight is the safer failure mode for polling consumers (they keep polling and eventually time out, vs. silently giving up on a state they don't understand). Default-to-terminal is the simpler implementation and matches the convention pinned by `CommandStatusTests.cs:53` and Story 1.5 round-2 dev notes.

**Decision.** Body **defaults** to the numeric form (`status >= CommandStatus.Completed`); the switch form is **acceptable** if a senior reviewer surfaces a strong case for explicitness. The contract test (`IsTerminal_AgreesWithGreaterOrEqualCompletedConvention` in AC #2) is the **binding** surface — whichever body is chosen must pass it. The numeric form is preferred because: (a) it's already pinned by an existing test, (b) it's recommended by the originating proposal `sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md:305-306`, (c) it's documented in Story 1.5 round-2 dev notes as the canonical convention, (d) maintenance cost on adding a new terminal value is one enum line vs. a body edit.

**Consequences.** Future enum additions force a deliberate decision via the convention test (it fails when the per-value `[InlineData]` table and the body diverge). Reviewer pushback on body shape does NOT require AC supersession theater — switching forms is within the AC envelope as long as the convention test stays green. The numeric form's order-dependence on enum integer assignments is a known trade-off, mitigated by the convention test failing loudly on any silent reorder.

**Decided by.** Project Lead (Jerome) on 2026-04-28 via party-mode session; recommended by Bob (Scrum Master) after Murat (Test Architect) and Winston (Architect) framed the default-to-terminal vs default-to-in-flight risk symmetry. Original AC #1 prescribed the numeric body as MUST; ADR-driven softening to "defaults to + reviewer-acceptable switch" applied during the same session.

### Testing Standards (project-wide rules — apply to every story)

- **Tier 1 (Unit):** xUnit 2.9.3 + Shouldly + NSubstitute. No DAPR runtime, no Docker.
- **Tier 2 / Tier 3 (Integration) — REQUIRED end-state inspection:** If the story creates or modifies Tier 2 (`Server.Tests`) or Tier 3 (`IntegrationTests`) tests, each test MUST inspect state-store end-state (e.g., Redis key contents, persisted `EventEnvelope`, CloudEvent body, advisory status record). Asserting only API return codes, mock call counts, or pub/sub call invocations is forbidden — that is an API smoke test, not an integration test. *Reference:* Epic 2 retro R2-A6; precedent fixes in Story 2.1 (`CommandRoutingIntegrationTests` missing `messageId`) and Story 2.2 (persistence integration test rewrote to inspect Redis directly).
- **ID validation:** Any controller / validator handling `messageId`, `correlationId`, `aggregateId`, or `causationId` MUST use `Ulid.TryParse` (or accept any non-whitespace string per `AggregateIdentity` rules). `Guid.TryParse` on these fields is forbidden. *Reference:* Epic 2 retro R2-A7; precedent fix in Story 2.4 `CommandStatusController`.

### R2-A6 Compliance for This Story Specifically

This story does **not** create or modify any Tier 2 or Tier 3 test. The new tests live in `tests/Hexalith.EventStore.Contracts.Tests/` (Tier 1, per `CLAUDE.md` § Build & Test Commands). They assert pure-function behavior on an enum extension; there is no state store, no DAPR runtime, no Docker. R2-A6's end-state-inspection rule does not apply.

The two existing controller tests (in `tests/Hexalith.EventStore.Server.Tests/` if they exist; verify during Task 7.2 — search for `CommandStatusControllerTests` and `AdminTraceQueryControllerTests` test classes) MUST continue to pass without modification (AC #6's behavior-preservation guarantee). The dev should NOT rewrite controller tests as part of this story; any controller-test changes are out of scope and indicate either a defect introduced by the refactor (return to Task 4/5 and fix) or an unrelated drift (file a separate story).

### R2-A7 Compliance for This Story Specifically

This story does **not** add or modify any controller-level ID validation. The two refactored controllers' validation surfaces (`correlationId` parsing in `CommandStatusController.GetStatus` at line 64; `correlationId` parsing in `AdminTraceQueryController.GetCorrelationTraceMap` at line 46) are **NOT touched**. The R2-A7 ULID rule is therefore neither violated nor newly enforced by this story; it stays in whatever state Epic 3 left it (per Epic 3 retro R3-A1, the `ReplayController.cs` still uses `Guid.TryParse` — but that is a different controller and a different story scope: `post-epic-3-r3a1-replay-ulid-validation`).

### ID-Validation Sanity Check (informational only — not part of this story's scope)

While editing `CommandStatusController.cs:123` and `AdminTraceQueryController.cs:77`, the dev will see surrounding code. **Do NOT** opportunistically refactor anything else in those methods. If a drive-by issue is noticed (e.g., a `Guid.TryParse` lurking elsewhere), file it as a finding for the relevant story's backlog (`post-epic-3-r3a1-replay-ulid-validation` is the catch-all for ULID-validation cleanup at this point) rather than expanding this PR's scope. The post-epic-2-r2a8 story's narrow-pin discipline is the precedent.

### Constraints That MUST NOT Change

- The `CommandStatus` enum's 8 values, their integer assignments, and their declaration order. (A change here would be a contract-breaking change requiring its own story.)
- The 4 constructor parameters of `CommandStatusController` (`ICommandStatusStore`, `ILogger<...>`) — public API surface.
- The 4 constructor parameters of `AdminTraceQueryController` (`ICommandStatusStore`, `IActorProxyFactory`, `IConfiguration`, `ILogger<...>`) — public API surface.
- The HTTP route templates, `[ApiController]` / `[Authorize]` / `[AllowAnonymous]` / `[Tags]` attributes on both controllers.
- The `CommandStatusResponse.FromRecord(...)` projection at `CommandStatusController.cs:128` and the `CorrelationTraceMap` record construction in `AdminTraceQueryController` — both are unchanged by this refactor.
- The `Retry-After: 1` header on non-terminal status responses (`CommandStatusController.cs:124`) — the conditional branch stays; only the predicate changes from `!IsTerminalStatus(record.Status)` to `!record.Status.IsTerminal()`. Header value, header name, and the surrounding `Response.Headers["Retry-After"] = "1";` line all stay byte-identical.
- The `commandCompletedAt = commandStatus.Timestamp;` assignment at `AdminTraceQueryController.cs:78` (inside the `IsTerminalStatus(commandStatus.Status)` block) — the predicate changes; the body stays.

### Project Structure Notes

- **`Hexalith.EventStore.Contracts` package:** ships as a public NuGet package (`registry.hexalith.com/eventstore-contracts` per `CLAUDE.md` § NuGet Packages). Adding a new public extension method is a **minor-version bump** under semantic-release, triggered by the `feat(contracts):` Conventional Commit prefix per `CLAUDE.md` § Commit Messages.
- **Folder convention:** `src/Hexalith.EventStore.Contracts/Commands/` already contains `CommandStatus.cs`, `CommandStatusRecord.cs`, `ArchivedCommand.cs`, `CommandEnvelope.cs`, `DomainServiceRequest.cs`, `DomainServiceCurrentState.cs`. Adding `CommandStatusExtensions.cs` to this folder is consistent with the flat-by-feature structure (no sub-folders for extensions).
- **Test folder convention:** `tests/Hexalith.EventStore.Contracts.Tests/Commands/` already contains `CommandStatusTests.cs`, `CommandStatusRecordTests.cs`, `CommandEnvelopeTests.cs`. Adding `CommandStatusExtensionsTests.cs` mirrors the `*Tests` suffix convention.
- **Global usings:** the test project uses xUnit + Shouldly without explicit `using` directives in test files (verified by inspection of `CommandStatusTests.cs:1-4` — only one `using` directive, for the SUT namespace). This implies a `GlobalUsings.cs` or `<Using>` ItemGroup in the project file imports `Xunit` and `Shouldly` globally. The new test file should follow the same convention. (Task 3.2 verifies this; if the convention doesn't apply for some reason, the dev adds explicit usings.)

### Conventional Commit Prefix Rationale

Merge prefix is `feat(contracts):` — the dominant change is a new public surface in a published NuGet package, even though the diff also includes a controller refactor and tests. Mirrors the originating proposal's classification (`sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` § 6). Triggers a minor bump under semantic-release.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` § Proposal 4 (lines 283-364)] — originating spec for this story
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md` § Proposal 3 (lines 180-193)] — Epic 3 retro routing R3-A3 to this same story
- [Source: `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md` § 6 R1-A4 (line 85)] — original action item
- [Source: `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md` § 5 D1-5 (line 74)] — D1-5 deferred-item entry that preceded R1-A4
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 R2-A2 (line 98)] — Epic 2 carry-over framing
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 5 D2-6 (line 86)] — Epic 2 deferred-item entry
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 4 (line 73)] — "One absent method in a flow doesn't break a build" theme tying R1-A4/R2-A2 to the broader latent-defect class
- [Source: `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md` § 4 finding 5 (line 74)] — "`CommandStatus.IsTerminal()` was not elevated to a shared contract helper"
- [Source: `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md` § 5 R2-A2 row (line 85)] — Epic 3 confirms R2-A2 still open
- [Source: `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md` § 8 R3-A3 (line 130)] — Epic 3 action item
- [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md` § 5 R3-A3 row (line 85)] — Epic 4 historical confirmation that the drift carried into Epic 4
- [Source: `_bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md:243`] — D1-5 origin in Story 1.5 round-2 dev notes; `>= Completed` convention pinning
- [Source: `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs:1-34`] — enum declaration with terminal-state XML docs
- [Source: `src/Hexalith.EventStore/Controllers/CommandStatusController.cs:123,171`] — pre-patch call site + private helper
- [Source: `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs:77,223`] — pre-patch call site + private helper
- [Source: `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs`] — sibling test file precedent (file-header style, naming convention)
- [Source: `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs:53`] — existing convention test (`status >= Completed`) that AC #2's third test pairs with
- [Source: `_bmad-output/implementation-artifacts/post-epic-2-r2a8-pipeline-nullref-fix.md`] — sibling post-epic-2 story precedent (story shape, Dev Notes structure, retro-closure annotation pattern, sprint-status lifecycle, AC numbering style)
- [Source: `_bmad-output/implementation-artifacts/post-epic-1-r1a7-tier2-tombstoning-lifecycle.md`] — earlier post-epic story precedent (Tier 2 baseline reasoning)
- [Source: `CLAUDE.md` § Code Style & Conventions] — file-scoped namespaces, Allman braces, XML docs in `src/`
- [Source: `CLAUDE.md` § Test Conventions] — xUnit 2.9.3 + Shouldly 4.3.0 + NSubstitute 5.3.0
- [Source: `CLAUDE.md` § Code Review Process] — 5/5 Epic 2 review-driven-patch rate; budget review-found rework
- [Source: `CLAUDE.md` § Commit Messages] — Conventional Commits + semantic-release minor bump for `feat:`
- [Source: `CLAUDE.md` § Solution File] — `Hexalith.EventStore.slnx` only

## Dev Agent Record

### Agent Model Used

claude-opus-4-7 (1M context) via Claude Code CLI

### Debug Log References

**Pre-patch baseline (Task 1.4 / 1.5, 2026-04-28):**
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter "FullyQualifiedName~CommandStatusTests"` → 11 / 11 (matches story spec).
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` → **271 / 271** (matches the assumed baseline from `post-epic-2-r2a8-pipeline-nullref-fix.md` § Validation Results 2026-04-27 exactly; AC #5's table did not require any in-flight adjustment).
- `Grep IsTerminalStatus` across `src/**/*.cs` → exactly 4 matches at the documented line numbers (CommandStatusController.cs:123, 171; AdminTraceQueryController.cs:77, 223). Both controllers already import `Hexalith.EventStore.Contracts.Commands` (CommandStatusController.cs:4, AdminTraceQueryController.cs:5) — no new using directive required.

**Task 2 (extension shipping):**
- Created `src/Hexalith.EventStore.Contracts/Commands/CommandStatusExtensions.cs` with the numeric body (`status >= CommandStatus.Completed`) per ADR-1.
- `dotnet build src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj --configuration Release -p:NuGetAudit=false` → 0/0.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` (no new tests yet) → 271 / 271 (extension presence neutral on existing tests).

**Task 3 (Tier 1 tests):**
- Created `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusExtensionsTests.cs` with 3 methods: `IsTerminal_ReturnsExpected_ForEachStatus` (`[Theory]` × 8 rows), `IsTerminal_AgreesWithGreaterOrEqualCompletedConvention` (`[Fact]`), `IsTerminal_TerminalCount_IsExactly4` (`[Fact]`).
- Project uses `<Using Include="Shouldly" />` + `<Using Include="Xunit" />` global imports (verified via `Hexalith.EventStore.Contracts.Tests.csproj:20-22`); no explicit usings needed for those packages.
- `dotnet test --filter "FullyQualifiedName~CommandStatusExtensionsTests"` → **10 / 10**.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` (full window) → **281 / 281** (271 → 281, +10 rows — matches AC #5 exactly).

**Tasks 4 & 5 (controller refactor):**
- `CommandStatusController.cs:123` → `if (!record.Status.IsTerminal()) {`. Private helper at lines 171–175 deleted in entirety.
- `AdminTraceQueryController.cs:77` → `if (commandStatus.Status.IsTerminal()) {`. Private helper at lines 223–227 deleted in entirety. Sibling `IsRejectionEvent` private helper at line 229 preserved (unrelated).
- `dotnet build src/Hexalith.EventStore/Hexalith.EventStore.csproj --configuration Release -p:NuGetAudit=false` → 0/0 (covered by the full slnx build in Task 7.4).

**Task 6 (AC #4 grep audit, post-patch):**
- `Grep IsTerminalStatus` across `src/**/*.cs` → **0 matches** ✓.
- `Grep IsTerminalStatus` across `tests/**/*.cs` → **0 matches** ✓.
- Multiline grep `is CommandStatus\.Completed[\s\S]{0,200}or CommandStatus\.Rejected` across `src/**/*.cs` → **0 matches** outside the new `CommandStatusExtensions.cs` itself ✓ (the new file uses the `>= Completed` form, so it doesn't trip the `or` pattern either).
- `Grep IsTerminal\(` across `src/**/*.cs` → exactly 3 matches: 1 declaration (`CommandStatusExtensions.cs:25`) + 2 call sites (`CommandStatusController.cs:123`, `AdminTraceQueryController.cs:77`) ✓. No `IsTerminalError` / `IsTerminalState` collisions in shipped code.
- Optional Hexalith.Tenants submodule grep (informational): not run — submodule is OUT OF SCOPE for this PR per Task 6.

**Task 7 (validation):**
- Tier 1 (all 5 windows, all green):
  - Contracts.Tests: **281 / 281** ✓
  - Client.Tests: **334 / 334** ✓
  - Sample.Tests: **63 / 63** ✓
  - Testing.Tests: **78 / 78** ✓
  - SignalR.Tests: **32 / 32** ✓
  - **Net Tier 1: 788 / 788** (matches AC #5's post-patch projection exactly).
- Tier 2 (`Server.Tests`): **1618 / 1643** observed locally. The 25 failures are **all environmental** — every failed test depends on `DaprTestContainerFixture` whose pre-flight check requires Redis on localhost:6379, the DAPR placement service on :6050, and the DAPR scheduler service on :6060, none of which were running in this dev environment (`dapr list` → "No Dapr instances found"; the spec calls out `dapr init` as a prerequisite under `CLAUDE.md` § Build & Test Commands). Failures are concentrated in `AggregateActorIntegrationTests`, `EventPersistenceIntegrationTests`, and `DaprSerializationRoundTripTests` — none of these reference `CommandStatus.IsTerminal` or the two refactored controllers. **Controller-targeted Tier 2 isolation:** `dotnet test --filter "FullyQualifiedName~CommandStatusController|FullyQualifiedName~AdminTraceQueryController"` → **18 / 18** green. AC #6's behavior-preservation guarantee (truth-table-identical body swap; HTTP status / headers / body bytes unchanged) is satisfied for the only tests that could observe a regression from this refactor. Reviewers running Tier 2 in a `dapr init`-warm environment should see 1643 / 1643 — the dev environment gap is the only divergence and is unrelated to this PR's scope.
- Tier 3: NOT run (out of scope per Task 7.3 + `CLAUDE.md` § Build & Test Commands optional designation).
- Full Release build: `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` → **0 warnings, 0 errors** with `TreatWarningsAsErrors=true` ✓.
- CS1591 sanity check (AC #7, Task 7.4): **passed** — temporarily removed the `<summary>` on `CommandStatusExtensions`, re-ran `dotnet build src/Hexalith.EventStore.Contracts/...` and observed `error CS1591: Commentaire XML manquant pour le type ou le membre visible publiquement 'CommandStatusExtensions'` (build failed). Restored the doc, re-built → 0/0. Confirms the analyzer fires on the new file. (No `Microsoft.CodeAnalysis.PublicApiAnalyzers` is wired into `Hexalith.EventStore.Contracts.csproj`, so the explicit sanity check was load-bearing per the AC.)
- Public-surface diff (AC #8, Task 7.5): no public-API analyzer is configured at the Contracts project level. Manual diff captured below in **Completion Notes** § "Public Surface Diff". No `[Obsolete]` markers added or removed.

### Completion Notes List

**Implementation summary.** Shipped a single new public extension method `Hexalith.EventStore.Contracts.Commands.CommandStatusExtensions.IsTerminal(this CommandStatus status)` with the numeric body `status >= CommandStatus.Completed` (per ADR-1). Migrated the two existing call sites (`CommandStatusController.cs:123` and `AdminTraceQueryController.cs:77`) to the extension and deleted both private `IsTerminalStatus` helpers. Added 3 Tier 1 unit tests (1 `[Theory]` × 8 rows + 2 `[Fact]` = 10 visible test rows) covering all 8 enum values, the `>= Completed` convention, and the 4-terminal exact count.

**Body-shape decision.** Adopted the **numeric body** (`status >= CommandStatus.Completed`) per ADR-1's "default" guidance. The numeric form is recommended by the originating proposal (`sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md:305-306`), aligned with the Story 1.5 round-2 dev-notes pinning, and pre-pinned by `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs:53`. AC #2's `IsTerminal_AgreesWithGreaterOrEqualCompletedConvention` test is the binding contract — a future review-driven switch to the `is X or Y or Z or W` form would still be acceptable as long as the convention test stays green.

**Public Surface Diff (additive only) — AC #8.**
- **Added (1):** `public static class Hexalith.EventStore.Contracts.Commands.CommandStatusExtensions` (new public type) with one method `public static bool IsTerminal(this CommandStatus status)`. New surface eligible for `feat(contracts):` minor-bump under semantic-release.
- **Removed (public surface):** none. The two deleted helpers were `private static`, invisible across the assembly boundary.
- **Changed:** none. `CommandStatus`, `CommandStatusRecord`, `CommandStatusController`, and `AdminTraceQueryController` all retain identical public surfaces. No `[Obsolete]` markers added or removed. No constructor parameter changes. No HTTP route / `[ApiController]` / `[Authorize]` / `[Tags]` attribute changes.
- **No breaking change** — `BREAKING CHANGE:` token MUST NOT appear in the merge commit.

**R2-A6 / R2-A7 compliance.** This PR does not create or modify any Tier 2 or Tier 3 test, and does not touch any controller-level ID validation. Both compliance rules are inapplicable to this story by design (per Dev Notes § R2-A6 Compliance and § R2-A7 Compliance). Confirmed during Tasks 4 / 5: only the boolean predicate was changed at the two call sites; no surrounding ID-parsing or state-store code was touched (no opportunistic refactors, per Dev Notes § ID-Validation Sanity Check).

**Documentation drift logged for future work (out of scope for this PR).** `CommandStatusController.cs:33-48` carries OpenAPI XML doc `<remarks>` that hard-codes the in-flight-vs-terminal split as inline prose, with the four values in each bucket listed by name. Now that `IsTerminal()` is the contract source of truth, this XML doc is a separate documentation source that will go stale on the next enum addition. **Not refactored in this PR** (would force OpenAPI regeneration and broaden review scope per Dev Notes § File Locations note). Suggested as a future Epic 3-followup or doc-cleanup story; capture in the next backlog grooming session.

**Retro-row annotations applied (AC #9).** Three retro files updated with closure annotations using the date + story-key pattern (mirroring R1-A7's "✅ Completed 2026-04-27" precedent — the `<merge-commit-sha>` placeholder in AC #9 is a forward-looking convention; lived precedent uses date + story reference, with the actual SHA filled in at the post-merge `done` transition):
- `epic-1-retro-2026-04-26.md` § 6 R1-A4 row → ✅ marker + closure note pointing at this story (also clarifies original "Used by `CommandStatusWriter`" framing was speculative; writer never re-implemented the convention).
- `epic-2-retro-2026-04-26.md` § 6 R2-A2 row → ✅ marker + closure note referencing extension location, both controllers, and 3 Tier 1 tests. § 10 "Critical path before Epic 3 closes" line → appended `[R2-A2 ✅ Closed 2026-04-28 — see §6 row]` after the R2-A2 token; the line itself preserved per the post-epic-2-r2a8 audit-trail precedent at line 160.
- `epic-3-retro-2026-04-26.md` § 8 R3-A3 row → ✅ marker + closure note (cites Epic 3 retro cleanup proposal § Proposal 3 routing). § 5 "R2-A2: shared CommandStatus.IsTerminal() extension" row → "Not addressed" updated to "Addressed by `post-epic-2-r2a2-commandstatus-isterminal-extension` (2026-04-28)" with evidence column expanded to name the extension, both call sites, and the test count.
- `epic-4-retro-2026-04-26.md` → **NOT modified** per AC #9 (retro stands as historical record).
- Both sprint-change-proposals → NOT modified (they are originating specs, not closure-tracking documents).

**Sprint-status updated to `review` per AC #11.** `_bmad-output/implementation-artifacts/sprint-status.yaml` line 276 transitioned from `ready-for-dev` → `review` with a closure-style annotation block (Tier 1 delta, full-Tier-1 count, Release build status, controller-targeted Tier 2 result, the three retro closures, and the upcoming `feat(contracts):` minor bump). Header `last_updated` line bumped to 2026-04-28 with the same review-state summary. **`done` transition is post-merge** and remains pending the merge SHA per the lifecycle in AC #11.

**Tier 2 environmental gap — reviewer note.** The 25 Tier 2 failures observed in this dev environment (out of 1643 total) are entirely DAPR-runtime-environmental: all are gated by `Hexalith.EventStore.Server.Tests.Fixtures.DaprTestContainerFixture` whose pre-flight check requires a running DAPR sidecar + Redis, and `dapr list` reports no Dapr instances on this machine. None of the 25 failures touch `CommandStatus.IsTerminal` or either refactored controller. AC #6's behavior preservation is verified by the 18 / 18 green pass on `--filter "FullyQualifiedName~CommandStatusController|FullyQualifiedName~AdminTraceQueryController"` in isolation. **Reviewers in a `dapr init`-warm environment should observe 1643 / 1643 Pass** (matching the post-epic-2-r2a8 baseline from 2026-04-27); if any controller-targeted test fails on review, that's a refactor regression and the patch should be reverted. If only the same 25 environmental tests fail, the gap is environmental and not a refactor regression.

**Skipped task: Task 10 (Conventional-commit-formatted PR / merge commit).** Left as `[ ]` because PR creation and merge-commit authoring are owned by Jerome / the PR open step, not by the dev session. Body of Task 10 stands as the merge-time runbook (PR title + body bullets + pre-commit hook discipline + squash-merge prefix verification).

### Post-Merge Runbook (mandatory after squash-merge — closes Task 9 and AC #11)

Run the following **after** the PR is squash-merged to `main` (the merge commit SHA is required to substitute the date-based annotations with the actual SHA per AC #9 / R2-A8 precedent):

1. **Capture the merge commit SHA.** `git fetch origin && git log -1 --format=%H origin/main` → `<merge-sha>` (full 40-char or 7-char short form, matching the post-epic-2-r2a8 precedent at `epic-2-retro-2026-04-26.md:160` which used the short form).

2. **Flip sprint-status entry `review` → `done`** at `_bmad-output/implementation-artifacts/sprint-status.yaml:276`. Replace the current `review` annotation with a `done` annotation in the post-epic-2-r2a8 closure style. Suggested form (one line, mirrors line 275):
   ```yaml
   post-epic-2-r2a2-commandstatus-isterminal-extension: done  # 2026-MM-DD: merged at <merge-sha> as feat(contracts): add CommandStatus.IsTerminal() extension and remove duplicate controller helpers. Tier 1 Contracts.Tests 271 → 281 (+10 rows / +3 methods); full Tier 1 = 788/788; full Release build (Hexalith.EventStore.slnx) = 0/0 with TreatWarningsAsErrors=true; controller-targeted Tier 2 = 18/18. Closes Epic 1 R1-A4 + Epic 2 R2-A2 + Epic 3 R3-A3 in one fix per sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md § Proposal 3 routing. Code review complete (3 layers); annotations propagated to epic-1/-2/-3-retro-2026-04-26.md (Epic 4 retro preserved as historical record). semantic-release minor bump on merge.
   ```
   Bump the header `last_updated:` line (both the comment at line 2 and the YAML key at line 45) to the merge date with a one-line "→ done" summary.

3. **Substitute the merge SHA in the three retro-row closure annotations** (currently date-stamped per the R1-A7 / R2-A8 lived precedent; AC #9 explicitly calls for `<merge-commit-sha>`):
   - `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md` § 6 row R1-A4 — replace `✅ Done 2026-04-28 — shared` with `✅ Done <merge-sha> — shared`.
   - `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 row R2-A2 — replace `✅ Done 2026-04-28 — extension` with `✅ Done <merge-sha> — extension`. Also update § 10 line 161: `[R2-A2 ✅ Closed 2026-04-28 — see §6 row]` → `[R2-A2 ✅ Closed <merge-sha> — see §6 row]`.
   - `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md` § 8 row R3-A3 — replace `✅ Done 2026-04-28 — addressed` with `✅ Done <merge-sha> — addressed`. Also update § 5 row "R2-A2: shared CommandStatus.IsTerminal() extension" — replace `(2026-04-28)` with `(merged <merge-sha>)`.
   - **Do NOT modify** `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md` (historical record per AC #9).

4. **Verify no merge-race conflict on `epic-3-retro-2026-04-26.md`** against `post-epic-3-r3a1-replay-ulid-validation` (still backlog at story creation; if that story merged first and edited § 8 / § 5, rebase rather than auto-resolve — see AC #9 last bullet).

5. **Final story-file housekeeping** in `_bmad-output/implementation-artifacts/post-epic-2-r2a2-commandstatus-isterminal-extension.md`:
   - Set Status: `review` → `done`.
   - Mark Task 9 `[~]` → `[x]` and Task 10 `[ ]` → `[x]` (10.1, 10.2, 10.3).
   - Append a Change Log entry: `2026-MM-DD — Post-merge: closure annotations substituted with merge SHA <merge-sha>; sprint-status → done.`

6. **Verify semantic-release picks up the minor bump.** After CI runs the release pipeline, confirm:
   - GitHub Releases shows a new minor version of the 6 NuGet packages (Contracts, Client, Server, SignalR, Testing, Aspire).
   - `CHANGELOG.md` includes the new feat entry under the new minor version section.
   - If semantic-release did NOT pick up the bump, the squashed commit subject is the most likely cause — confirm it starts with `feat(contracts):` (not `chore:` / `refactor:` / a missing scope).

The audit trail is complete when Task 9 + Task 10 are both `[x]`, sprint-status is `done` with the merge-SHA-substituted annotation, and the three retros carry merge-SHA closure markers in place of date stamps.

### File List

**New files:**
- `src/Hexalith.EventStore.Contracts/Commands/CommandStatusExtensions.cs` — public static class with `IsTerminal(this CommandStatus)` extension method (numeric body `status >= CommandStatus.Completed` per ADR-1) + full XML docs (summary, params, returns, remarks).
- `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusExtensionsTests.cs` — 3 Tier 1 unit tests (1 `[Theory]` × 8 + 2 `[Fact]` = 10 rows): per-value mapping, `>= Completed` convention agreement, exact 4-terminal count.

**Modified files (source):**
- `src/Hexalith.EventStore/Controllers/CommandStatusController.cs` — line 123 swapped `IsTerminalStatus(record.Status)` → `record.Status.IsTerminal()`; private `IsTerminalStatus` helper (former lines 171–175) deleted.
- `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs` — line 77 swapped `IsTerminalStatus(commandStatus.Status)` → `commandStatus.Status.IsTerminal()`; private `IsTerminalStatus` helper (former lines 223–227) deleted; sibling `IsRejectionEvent` helper preserved.

**Modified files (process / tracking):**
- `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md` — § 6 R1-A4 row annotated with closure marker.
- `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` — § 6 R2-A2 row annotated; § 10 "Critical path before Epic 3 closes" line appended with `[R2-A2 ✅ Closed 2026-04-28 — see §6 row]`.
- `_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md` — § 8 R3-A3 row annotated; § 5 "R2-A2: shared CommandStatus.IsTerminal() extension" row updated from "Not addressed" to "Addressed by post-epic-2-r2a2-commandstatus-isterminal-extension (2026-04-28)".
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `post-epic-2-r2a2-commandstatus-isterminal-extension` entry transitioned `ready-for-dev` → `review` with closure-style annotation; header `last_updated` bumped.
- `_bmad-output/implementation-artifacts/post-epic-2-r2a2-commandstatus-isterminal-extension.md` — Status `ready-for-dev` → `review`; Tasks 1–9 marked complete; Task 10 left for PR / merge step; Dev Agent Record + File List + Change Log filled in.

**Files NOT modified (preserved per AC #9 / Constraints):**
- `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` — enum unchanged.
- `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs` — existing tests unchanged.
- `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md` — historical record preserved.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` and `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md` — originating specs preserved.
- `Hexalith.Tenants` submodule — out of scope.

## Change Log

- **2026-04-28 — Implementation complete (status ready-for-dev → review).** Shipped `CommandStatusExtensions.IsTerminal(this CommandStatus)` (numeric body) in `Hexalith.EventStore.Contracts.Commands` with full XML docs; added 3 Tier 1 tests (10 visible rows). Removed two private `IsTerminalStatus` helpers from `CommandStatusController` and `AdminTraceQueryController`; both call sites consume the extension. Tier 1 Contracts.Tests 271 → 281 (+10); full Tier 1 = 788 / 788. Full Release build (Hexalith.EventStore.slnx) = 0 warnings, 0 errors. CS1591 sanity check confirms the analyzer fires on the new file. Controller-targeted Tier 2 = 18 / 18 (full Tier 2 = 1618 / 1643 in this env; 25 failures are pre-existing DAPR-runtime-environmental gaps, none touch the refactor). Annotated R1-A4, R2-A2 (+ § 10), and R3-A3 (+ § 5) closure markers; Epic 4 retro and both sprint-change-proposals preserved. Sprint-status entry → `review`. Closes Epic 1 R1-A4 + Epic 2 R2-A2 + Epic 3 R3-A3 in one fix per `sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md` § Proposal 3 routing. Merge prefix on PR / squash: `feat(contracts):`.
- **2026-04-28 — Code review (3 layers: Blind Hunter + Edge Case Hunter + Acceptance Auditor).** 25 raw findings → triaged: 3 decision-needed, 0 patch, 12 defer (logged in `deferred-work.md`), 10 dismissed. No CRITICAL or HIGH findings survived triage. Acceptance Auditor confirmed AC #1–#8 + Constraints That MUST NOT Change: all PASS; AC #9 SHA-vs-date deviation is justified by the post-merge runbook; AC #11 lifecycle granularity is the only minor process drift (terminal `review` state correct). Decision-needed items captured in § Review Findings below.
- **2026-04-28 — Code review decisions resolved.** Decision 1 (convention-test tautology) → defer to spec per ADR-1's deliberate trade-off; reviewer critique logged for future spec author. Decision 2 (undefined-enum-value contract) → defer to a contract-clarification story; not authorized to be decided by the dev. Decision 3 (test-method XML docs) → patch applied: added three `<summary>` blocks to `CommandStatusExtensionsTests.cs` per `TerminatableComplianceAssertionsTests.cs` precedent. Re-ran new test class isolation: 10/10 green. All review action items resolved; story remains in `review` status pending PR open + post-merge runbook execution per AC #11 lifecycle.
- **2026-04-28 — PR #220 opened (Task 10.1 + 10.2 complete).** Branch `feat/post-epic-2-r2a2-commandstatus-isterminal` pushed to origin; commit `35c91cc` (no pre-commit hook bypass). PR title carries the prescribed `feat(contracts):` prefix; PR body bullets the three retro closures (R1-A4, R2-A2, R3-A3), the originating Proposal 4, and the R3-A3 cross-route from Proposal 3. URL: https://github.com/Hexalith/Hexalith.EventStore/pull/220. Task 10.3 (squash-merge prefix verification) and the full Post-Merge Runbook remain pending merge.

## Review Findings

- [x] [Review][Decision → Defer-to-spec] Convention-agreement test is tautological — `IsTerminal_AgreesWithGreaterOrEqualCompletedConvention` asserts `f(x).ShouldBe(g(x))` where g is the implementation, providing no independent verification. Two independent reviewers (Blind Hunter HIGH #1+#3, Edge Case Hunter #2+#7) flag that under the current `>= Completed` body the test always passes. **Resolution (2026-04-28): Option (a) — keep as spec prescribes.** ADR-1 documents the deliberate trade-off; AC #2 explicitly designates this test as the "load-bearing contract surface" and forward-looking gate for any future body change away from `>=`. Reverting spec-compliant work on adversarial-reviewer pressure without going through a spec-supersession process is the wrong workflow. The reviewer critique is logged in `deferred-work.md` for future spec-author consideration; if the reviewer's argument lands, ADR-2 in a follow-up story should formally supersede ADR-1's choice.
- [x] [Review][Decision → Defer] Defensive contract for undefined enum values — `(CommandStatus)999.IsTerminal()` silently returns `true`; spec is silent on this. Reviewers (Blind Hunter HIGH #4, Edge Case Hunter #1+#5) want the behavior pinned. **Resolution (2026-04-28): Option (c) — defer to a contract-clarification story.** The undefined-cast contract is not authorized to be decided by the dev: option (a) `Enum.IsDefined` guard would change controller failure mode (state-store corruption → `ArgumentOutOfRangeException` instead of wrong response); option (b) Theory row pinning silent-true would enshrine a permissive choice that the spec did not authorize. In practice undefined casts are not reachable from current call sites (controllers consume `CommandStatusRecord.Status` written by `CommandStatusWriter` from valid enum values), but the public Contracts NuGet API surface deserves a defined contract. Logged in `deferred-work.md`; suggest a future contract-clarification story.
- [x] [Review][Decision → Patch applied] Test methods missing optional XML-doc summaries — Acceptance Auditor flagged that AC #2 final bullet's tip-of-tree convention recommends per-test `<summary>` blocks; new file shipped with none. **Resolution (2026-04-28): Option (a) — added three `<summary>` blocks** to `CommandStatusExtensionsTests.cs` per Task 3.3's example wording. Re-ran new test class in isolation: 10/10 green. Matches the `TerminatableComplianceAssertionsTests.cs` precedent the spec cites; closes the spec's "preferred" gap.
- [x] [Review][Defer] Controller call-site characterization tests not pinned [src/Hexalith.EventStore/Controllers/*.cs] — Behavior preservation argued by inspection (truth-table-identical body swap), not by a Tier 2 test that pins Retry-After header / `commandCompletedAt` per status. Out of scope per Dev Notes Constraints + AC #6. — deferred, pre-existing scope boundary
- [x] [Review][Defer] Corrupted state-store enum value handling in controllers [src/Hexalith.EventStore/Controllers/CommandStatusController.cs:123, AdminTraceQueryController.cs:77] — Out-of-range `record.Status` from a corrupted state store would route through the new predicate the same way it routed through the old. — deferred, pre-existing
- [x] [Review][Defer] `commandCompletedAt = commandStatus.Timestamp` semantic gap [src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs:78] — Timestamp may reflect last-update time, not actual terminal-transition time. — deferred, pre-existing
- [x] [Review][Defer] "MUST consume this extension" remarks lack tooling enforcement [src/Hexalith.EventStore.Contracts/Commands/CommandStatusExtensions.cs:22] — A future contributor could re-introduce the convention inline; remarks-as-policy without a Roslyn analyzer or banned-API entry is wishful thinking. — deferred, future analyzer story
- [x] [Review][Defer] AC #11 lifecycle skipped intermediate `in-progress` transition in sprint-status.yaml — `backlog → review` directly; `ready-for-dev` and `in-progress` not visible in current diff. Terminal state for the dev session is correct. — deferred, audit-trail granularity only
- [x] [Review][Defer] AC #9 wording: "Addressed by ... (2026-04-28)" omits literal "merged" word [_bmad-output/implementation-artifacts/epic-3-retro-2026-04-26.md:85] — Spec line 82 prescribes "(merged <date>)"; dev used "(2026-04-28)". — deferred, post-merge runbook step 3 substitutes anyway
- [x] [Review][Defer] Retro line numbers (`CommandStatusController:123`, `AdminTraceQueryController:77`) in closure annotations may drift on future edits [retro markdown] — Use method names or stable anchors. — deferred, doc-style
- [x] [Review][Defer] Retro R2-A2 row + sprint-status YAML annotations are wall-of-text [_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md:98, sprint-status.yaml:276] — Single-cell long prose is hard to render in markdown viewers and brittle in YAML. Move to footnote / structured field in a future doc-cleanup story. — deferred, doc-style
- [x] [Review][Defer] `epic-2-retro` § 10 "critical path" bullet inline-bracket syntax is awkward [_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md:161] — `R2-A2 [R2-A2 ✅ Closed 2026-04-28 — see §6 row] (...)` repeats the ID inside its own annotation. — deferred, doc-style
- [x] [Review][Defer] Closure-idiom inconsistency across the three retros [retro markdown] — Mix of past tense ("Addressed by"), gerund, double check-marks. Standardize in a future retro-cleanup pass. — deferred, doc-style
