# Story 17.8: .NET Tool Packaging and Distribution

Status: done

Size: Small-Medium — 3 modified source files (.csproj, Program.cs, ci.yml), 2 modified config files (release.yml, .config/dotnet-tools.json), 2 new test files, 4 task groups, 10 ACs, 6 new tests (~4-6 hours estimated). Finalizes the Admin CLI as a production-ready .NET global tool by adding CLI-specific NuGet metadata, a `--version` flag, CI/CD pipeline coverage for Admin.Cli.Tests, a tool-install smoke test, and local tool manifest entry.

**Dependency:** Stories 17-1 through 17-6 must be done (all done). Story 17-7 (connection profiles and shell completions) is a **soft dependency** — 17-8 can be implemented in parallel since it does not modify any command registrations or add new CLI commands. The only interaction point is `Program.cs`, where 17-8's changes (rootCommand.Name + version check before the try block) are spatially separate from 17-7's changes (config command registration + profile option). This story ensures the existing tool is production-ready for distribution via `dotnet tool install`.

## Definition of Done

- All 10 ACs verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- `dotnet pack` produces a valid `.nupkg` with `DotnetTool` package type and CLI-specific metadata
- `eventstore-admin --version` prints the assembly informational version
- CI pipeline runs Admin.Cli.Tests as part of Tier 1
- Release pipeline runs Admin.Cli.Tests before publishing
- Tool installs and invokes successfully via `dotnet tool install`

## Story

As a **platform operator installing EventStore admin tooling**,
I want **`eventstore-admin` packaged as a production-ready .NET global tool with proper NuGet metadata, version reporting, and CI/CD test coverage**,
so that **I can discover the tool on NuGet.org, install it with `dotnet tool install -g Hexalith.EventStore.Admin.Cli`, verify the installed version, and trust that every release has been tested before publication (FR80, NFR42)**.

## Acceptance Criteria

1. **CLI-specific NuGet package metadata** — The `Hexalith.EventStore.Admin.Cli.csproj` overrides the root `Directory.Build.props` defaults with CLI-specific values: `Description` set to `"Hexalith EventStore administration CLI — manage streams, projections, snapshots, backups, tenants, and health checks from the terminal. Supports JSON/CSV/table output, exit codes for CI/CD, connection profiles, and shell completions."`, `PackageTags` set to `"eventsourcing;dapr;cqrs;eventstore;dotnet;cli;admin;devops"`. The existing `PackAsTool`, `ToolCommandName`, and `IsPackable` settings remain unchanged. The generated `.nupkg` contains `packageType = DotnetTool` (already true from `PackAsTool=true`).

2. **`--version` flag** — Running `eventstore-admin --version` or `eventstore-admin -v` prints the assembly informational version (e.g., `1.2.3`) to stdout and exits with code `0`. The version value comes from `AssemblyInformationalVersionAttribute`, which MSBuild populates from the `-p:Version=` parameter during `dotnet pack` in the release pipeline. During local development, it shows `1.0.0` (the default). Implementation approach is specified in Task 1.2.

3. **CI pipeline includes Admin.Cli.Tests** — The `ci.yml` workflow's "Unit Tests (Tier 1)" step includes `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/` with the same flags as existing test projects (`--no-build --configuration Release --logger "trx;LogFileName=test-results.trx"`). The test summary Python script's `tier1_suites` dictionary includes `'Admin.Cli.Tests': 'tests/Hexalith.EventStore.Admin.Cli.Tests/TestResults/**/test-results.trx'`.

4. **Release pipeline includes Admin.Cli.Tests** — The `release.yml` workflow's "Run All Tests (Tier 1 + 2)" step includes `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/ --configuration Release --logger "trx;LogFileName=admin-cli-results.trx"` alongside existing test projects. Tests run BEFORE `semantic-release` — a failing test blocks the release.

5. **Tool install smoke test in CI** — The `ci.yml` workflow adds a step after "Unit Tests (Tier 1)" named "Tool Install Smoke Test" that: (a) packs the CLI project: `dotnet pack src/Hexalith.EventStore.Admin.Cli/ --no-build --configuration Release --output ./smoke-test-nupkgs`, (b) installs as global tool from local source: `dotnet tool install --global Hexalith.EventStore.Admin.Cli --add-source ./smoke-test-nupkgs --version "1.0.0"`, (c) runs `eventstore-admin --version` and verifies exit code 0, (d) runs `eventstore-admin --help` and verifies it lists expected subcommands (`health`, `stream`, `projection`, `tenant`, `snapshot`, `backup`, `config`), (e) **negative test:** runs `eventstore-admin nonexistent-command` and verifies non-zero exit code, (f) uninstalls: `dotnet tool uninstall --global Hexalith.EventStore.Admin.Cli`. The step uses `continue-on-error: false` (default) — a broken tool package fails the build.

6. **Local tool manifest entry** — `.config/dotnet-tools.json` adds the `Hexalith.EventStore.Admin.Cli` tool entry with `"version": "1.0.0"` and `"commands": ["eventstore-admin"]`. This enables contributors to run `dotnet tool restore` after cloning. The version is a placeholder — contributors can update it or use `--global` install from NuGet.org for the latest release. `"rollForward": false` to match the existing `defaultdocumentation.console` entry style.

7. **Package and version tests** — Two new test classes in `tests/Hexalith.EventStore.Admin.Cli.Tests/`: (a) `PackageMetadataTests` verifies `AssemblyInformationalVersionAttribute` is present, assembly name is `Hexalith.EventStore.Admin.Cli`, and assembly has an entry point (catches packaging regressions without `dotnet pack`). (b) `VersionFlagTests` invokes the actual CLI binary via `Process.Start("dotnet", "<assembly.dll> --version")` and asserts: stdout matches the expected version string, exit code is 0 for both `--version` and `-v`, and `--help` output contains the root command description `"Hexalith EventStore administration CLI"` (verifies help text works; tool name `eventstore-admin` only appears when installed as a dotnet tool — see AC #8 deviation). Total: 6 tests across both classes.

8. **Root command configuration** — ~~`Program.cs` sets `rootCommand.Name = "eventstore-admin"` explicitly.~~ **Deviation (beta5):** `Symbol.Name` is read-only in System.CommandLine 2.0.0-beta5.25306.1. The tool name is correctly applied via `<ToolCommandName>eventstore-admin</ToolCommandName>` in the `.csproj` when installed as a .NET global tool. The root command description remains `"Hexalith EventStore administration CLI"`.

9. **No changes to existing commands** — All existing subcommands (health, stream, projection, tenant, snapshot, backup, config stub) continue to work identically. The only changes to `Program.cs` are adding `--version` support and setting `rootCommand.Name`.

10. **All existing Tier 1 and Tier 2 tests continue to pass** with zero behavioral change.

## Tasks / Subtasks

- [x] **Task 1: Update CLI project NuGet metadata and add --version support** (AC: #1, #2, #8, #9)
  - [x] 1.1 Modify `src/Hexalith.EventStore.Admin.Cli/Hexalith.EventStore.Admin.Cli.csproj`:
    - Add CLI-specific `<Description>` that overrides root `Directory.Build.props`
    - Add CLI-specific `<PackageTags>` that overrides root `Directory.Build.props`
    - Keep existing `PackAsTool`, `ToolCommandName`, `IsPackable`, `OutputType`, `RootNamespace`, `AssemblyName` unchanged
    ```xml
    <PropertyGroup>
      <OutputType>Exe</OutputType>
      <PackAsTool>true</PackAsTool>
      <ToolCommandName>eventstore-admin</ToolCommandName>
      <IsPackable>true</IsPackable>
      <RootNamespace>Hexalith.EventStore.Admin.Cli</RootNamespace>
      <AssemblyName>Hexalith.EventStore.Admin.Cli</AssemblyName>
      <Description>Hexalith EventStore administration CLI — manage streams, projections, snapshots, backups, tenants, and health checks from the terminal. Supports JSON/CSV/table output, exit codes for CI/CD, connection profiles, and shell completions.</Description>
      <PackageTags>eventsourcing;dapr;cqrs;eventstore;dotnet;cli;admin;devops</PackageTags>
    </PropertyGroup>
    ```
  - [x] 1.2 Modify `src/Hexalith.EventStore.Admin.Cli/Program.cs`:
    - Add `using System.Reflection;` at the top
    - Add `rootCommand.Name = "eventstore-admin";` after constructing the RootCommand
    - Add `--version` / `-v` handling as a simple args check BEFORE the try block (ADR-1):
      ```csharp
      if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
      {
          string version = Assembly.GetExecutingAssembly()
              .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
              ?.InformationalVersion ?? "0.0.0";
          Console.WriteLine(version);
          return ExitCodes.Success;
      }
      ```
    - No other changes to Program.cs. Existing command registrations unchanged.

- [x] **Task 2: Update CI/CD pipelines** (AC: #3, #4, #5)
  - [x] 2.1 Modify `.github/workflows/ci.yml` — "Unit Tests (Tier 1)" step:
    - Add line: `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/ --no-build --configuration Release --logger "trx;LogFileName=test-results.trx"`
  - [x] 2.2 Modify `.github/workflows/ci.yml` — "Test Summary" Python script:
    - Add `'Admin.Cli.Tests': 'tests/Hexalith.EventStore.Admin.Cli.Tests/TestResults/**/test-results.trx'` to the `tier1_suites` dictionary
    - This is a separate subtask because it's easy to forget — buried in the Python block
  - [x] 2.3 Modify `.github/workflows/ci.yml` — Add "Tool Install Smoke Test" step after Unit Tests:
    ```yaml
    - name: Tool Install Smoke Test
      run: |
        dotnet pack src/Hexalith.EventStore.Admin.Cli/ --no-build --configuration Release --output ./smoke-test-nupkgs
        dotnet tool install --global Hexalith.EventStore.Admin.Cli --add-source ./smoke-test-nupkgs --version "1.0.0"
        eventstore-admin --version
        eventstore-admin --help | grep -q "health"
        eventstore-admin --help | grep -q "stream"
        eventstore-admin --help | grep -q "projection"
        ! eventstore-admin nonexistent-command 2>&1
        dotnet tool uninstall --global Hexalith.EventStore.Admin.Cli
    ```
    - Place after Tier 1 tests, before DAPR installation
    - The `! eventstore-admin nonexistent-command` line verifies non-zero exit for unknown commands (bash `!` inverts exit code)
    - On ubuntu-latest, `~/.dotnet/tools` is already on PATH
  - [x] 2.4 Modify `.github/workflows/release.yml` — "Run All Tests (Tier 1 + 2)" step:
    - Add line: `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/ --configuration Release --logger "trx;LogFileName=admin-cli-results.trx"`
    - Position: alongside existing Tier 1 test lines, before Tier 2 (Server.Tests)

- [x] **Task 3: Update local tool manifest** (AC: #6)
  - [x] 3.1 Modify `.config/dotnet-tools.json`:
    - Add entry for `hexalith.eventstore.admin.cli` (lowercase — NuGet tool IDs are case-insensitive but manifest convention is lowercase):
      ```json
      "hexalith.eventstore.admin.cli": {
        "version": "1.0.0",
        "commands": [
          "eventstore-admin"
        ],
        "rollForward": false
      }
      ```
    - **Note:** `"version": "1.0.0"` is a placeholder. After first release, contributors can run `dotnet tool update hexalith.eventstore.admin.cli` to get the latest. Local development uses `dotnet run --project src/Hexalith.EventStore.Admin.Cli/` directly.

- [x] **Task 4: Write packaging validation tests** (AC: #7, #10)
  - [x] 4.1 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/PackageMetadataTests.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Tests;

    using System.Reflection;

    public class PackageMetadataTests
    {
        private static readonly Assembly _cliAssembly = typeof(ExitCodes).Assembly;

        [Fact]
        public void Assembly_HasInformationalVersion()
        {
            string? version = _cliAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            version.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public void Assembly_NameIsCorrect()
        {
            _cliAssembly.GetName().Name.ShouldBe("Hexalith.EventStore.Admin.Cli");
        }

        [Fact]
        public void Assembly_HasEntryPoint()
        {
            // Verify this is an executable assembly (has top-level statements or Main)
            _cliAssembly.EntryPoint.ShouldNotBeNull();
        }
    }
    ```
    - Uses `typeof(ExitCodes).Assembly` to reference the CLI assembly — `ExitCodes` is a public type in the CLI project already exposed via `InternalsVisibleTo`.
    - These tests catch: missing version metadata, wrong assembly name, and accidental conversion from Exe to Library.
  - [x] 4.2 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/VersionFlagTests.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Tests;

    using System.Diagnostics;
    using System.Reflection;

    public class VersionFlagTests
    {
        private static readonly string _expectedVersion = typeof(ExitCodes).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        [Theory]
        [InlineData("--version")]
        [InlineData("-v")]
        public void VersionFlag_PrintsVersionAndExitsZero(string flag)
        {
            // Invoke the actual CLI binary with the version flag
            string cliDll = typeof(ExitCodes).Assembly.Location;
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{cliDll}\" {flag}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            bool exited = process.WaitForExit(5000);
            if (!exited)
            {
                process.Kill();
            }

            exited.ShouldBeTrue("CLI process timed out");
            process.ExitCode.ShouldBe(0);
            output.ShouldBe(_expectedVersion);
        }

        [Fact]
        public void HelpOutput_ContainsToolName()
        {
            // Verify rootCommand.Name = "eventstore-admin" appears in help
            string cliDll = typeof(ExitCodes).Assembly.Location;
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{cliDll}\" --help",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            bool exited = process.WaitForExit(5000);
            if (!exited)
            {
                process.Kill();
            }

            exited.ShouldBeTrue("CLI process timed out");
            process.ExitCode.ShouldBe(0);
            output.ShouldContain("eventstore-admin");
        }
    }
    ```
    - Uses `Process.Start` with `dotnet <assembly.dll>` to invoke the actual CLI binary
    - Verifies `--version` and `-v` both print the correct version string and exit 0
    - Verifies `--help` output includes the tool name `eventstore-admin` (catches missing `rootCommand.Name`)
    - Timeout of 5s aligns with NFR42 (3s startup target)
  - [x] 4.3 Run all existing tests to verify zero regressions:
    - `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/ --configuration Release`
    - Verify all existing tests continue to pass

## Dev Notes

### Architecture Compliance

- **ADR-P4** — CLI remains a thin HTTP client. This story only affects packaging metadata, not CLI behavior.
- **D9 (semantic-release)** — Version is injected by `-p:Version=${nextRelease.version}` during `dotnet pack`. The `AssemblyInformationalVersionAttribute` is automatically populated by MSBuild. No hardcoded versions in source code.
- **D10 (CI/CD)** — Adding Admin.Cli.Tests to both CI and release pipelines aligns with the existing three-tier test pyramid. Admin.Cli.Tests are Tier 1 (no external dependencies).

### Critical Design Decisions

**ADR-1: Simple --version implementation (no System.CommandLine dependency)**

The `--version` flag is handled as a simple args check BEFORE System.CommandLine parsing. This avoids:
- API differences between System.CommandLine beta versions (`RootCommand.Version` property may or may not exist)
- Conflicts with existing global options
- Complexity of adding a boolean option that short-circuits command execution

The pattern (`if args[0] == "--version"`) is used by `dotnet --version`, `node --version`, and other CLI tools. It's simple, reliable, and version-agnostic.

**ADR-2: InformationalVersion includes `+sha` suffix in local builds**

When built locally with `dotnet build`, MSBuild sets `InformationalVersion` to `1.0.0+<commitsha>`. The `--version` flag will print this full string including the suffix. This is expected and matches `dotnet --version` behavior. In production releases, semantic-release injects a clean version via `-p:Version=1.2.3`, so published builds show clean versions. The `VersionFlagTests` compare against `AssemblyInformationalVersionAttribute` which includes the suffix — tests pass in both scenarios because they read the same attribute value that `--version` prints.

**ADR-3: CLI-specific Description overrides Directory.Build.props**

The root `Directory.Build.props` sets `Description = "DAPR-native event sourcing server for .NET"` which is appropriate for library packages (Contracts, Client, Server, etc.) but misleading for the CLI tool package. Overriding in the `.csproj` is the standard MSBuild pattern — last-write-wins for properties.

**ADR-4: No PackageIcon in this story**

A NuGet `PackageIcon` improves discoverability on NuGet.org search results. However, this requires a PNG/SVG asset that doesn't exist in the repo yet. If the project adds a brand icon later, add `<PackageIcon>icon.png</PackageIcon>` to the .csproj and include it as a `<None Include="..." Pack="true" PackagePath="\"/>`. Not a blocker for the initial tool release.

**ADR-5: Tool manifest version is a placeholder**

The `.config/dotnet-tools.json` entry uses `"version": "1.0.0"` as a placeholder. This file is for `dotnet tool restore` in fresh clones. Contributors developing locally use `dotnet run --project` directly. The manifest is useful for CI environments or contributors who want the published tool without building from source.

### File Structure

```
src/Hexalith.EventStore.Admin.Cli/
  Hexalith.EventStore.Admin.Cli.csproj  # MODIFIED - CLI-specific Description, PackageTags
  Program.cs                            # MODIFIED - rootCommand.Name, --version handling

.github/workflows/
  ci.yml                                # MODIFIED - add Admin.Cli.Tests to Tier 1, add smoke test
  release.yml                           # MODIFIED - add Admin.Cli.Tests to pre-release tests

.config/
  dotnet-tools.json                     # MODIFIED - add eventstore-admin tool entry

tests/Hexalith.EventStore.Admin.Cli.Tests/
  PackageMetadataTests.cs               # NEW - assembly metadata validation (3 tests)
  VersionFlagTests.cs                   # NEW - real E2E version/help tests via Process.Start (3 tests)
```

### Existing Code Patterns to Follow

- **Test conventions:** xUnit + Shouldly assertions. One test class per concern. `[Fact]` for single cases, `[Theory]` with `[InlineData]` for parameterized. Namespace matches folder structure.
- **CI patterns:** Each `dotnet test` line in CI uses `--logger "trx;LogFileName=<name>.trx"`. Test summary Python script parses TRX files for GitHub step summary.
- **NuGet metadata:** Properties in `.csproj` override `Directory.Build.props` using standard MSBuild last-write-wins. No `.nuspec` files needed — SDK-style projects generate them automatically.

### Previous Story Intelligence (17-7)

Story 17-7 added connection profiles and shell completions — the last feature story. Key observations:
- `StubCommands.Create("config", ...)` in Program.cs will be replaced by story 17-7 with `ConfigCommand.Create(binding)` — story 17-8 should NOT modify the config command registration
- The `--profile` / `-p` global option will be added by story 17-7 — story 17-8 should NOT conflict with `-v` alias (verify `-v` is not already used by any global option)
- Story 17-7 modified `GlobalOptionsBinding.cs`, `GlobalOptions.cs`, and `Program.cs` — story 17-8's Program.cs changes are minimal and non-conflicting (only adds `rootCommand.Name` and version check before the try block)

**CRITICAL — Alias conflict check:** Existing global options use `-u` (url), `-t` (token), `-f` (format), `-o` (output). Story 17-7 adds `-p` (profile). Using `-v` for `--version` is safe — no conflicts.

### Git Intelligence

Recent commits follow consistent patterns:
- `c928e9f` (17-6): Snapshot/backup subcommands — latest feature commit
- All feature commits use: `feat: Add <description> for story 17-X`
- This story should use: `feat: Add dotnet tool packaging, version flag, and CI/CD coverage for story 17-8`

The release pipeline's `dotnet pack --no-build --configuration Release --output ./nupkgs -p:Version=${nextRelease.version}` already packs ALL packable projects including Admin.Cli. This story does NOT change the pack/publish mechanism — only the metadata and test coverage.

### References

- [Source: architecture.md, lines 206-212] Admin CLI distribution as .NET global tool
- [Source: architecture.md, lines 472-483] D9 semantic-release, D10 CI/CD pipeline
- [Source: prd.md, lines 906-908] FR79 shared Admin API, FR80 CLI output/exit codes/completions
- [Source: prd.md, line 977] NFR42 CLI 3-second startup
- [Source: .releaserc.json] semantic-release pack and publish commands
- [Source: ci.yml] Existing Tier 1 test execution pattern
- [Source: release.yml] Existing pre-release test execution pattern

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- `Symbol.Name` is read-only in System.CommandLine 2.0.0-beta5.25306.1 — cannot set `rootCommand.Name = "eventstore-admin"` as originally planned in AC #8. The tool name is correctly set via `ToolCommandName` in the .csproj when installed as a .NET global tool. The help output test was adapted to verify the description text instead.
- Pre-existing bug in `TenantCompareCommand.Create`: `new ArgumentArity(2, int.MaxValue)` throws `ArgumentException` in beta5. Fixed by capping at 100 (practical limit for tenant comparison). This fix was necessary to unblock the `--help` smoke test.

### Completion Notes List

- **Task 1.1:** Added `Description` and `PackageTags` to `Hexalith.EventStore.Admin.Cli.csproj` overriding root `Directory.Build.props` defaults.
- **Task 1.2:** Added `--version` / `-v` flag to `Program.cs` using `AssemblyInformationalVersionAttribute`. Implemented as a simple args check before System.CommandLine parsing per ADR-1. Removed `rootCommand.Name` assignment (read-only in beta5).
- **Task 2.1:** Added `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/` to CI Tier 1 step.
- **Task 2.2:** Added `Admin.Cli.Tests` to test summary Python script `tier1_suites` dictionary.
- **Task 2.3:** Added "Tool Install Smoke Test" CI step — packs, installs globally, verifies `--version`, `--help`, negative test with unknown command, and uninstalls.
- **Task 2.4:** Added `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/` to release pipeline before Tier 2 tests.
- **Task 3.1:** Added `hexalith.eventstore.admin.cli` tool entry to `.config/dotnet-tools.json`.
- **Task 4.1:** Created `PackageMetadataTests.cs` with 3 tests: informational version, assembly name, entry point.
- **Task 4.2:** Created `VersionFlagTests.cs` with 3 tests: `--version` flag, `-v` flag, help output contains description.
- **Task 4.3:** All 285 Admin.Cli.Tests pass. All Tier 1 tests (1009 total) pass with zero regressions.
- **Bonus fix:** Fixed pre-existing `TenantCompareCommand` `ArgumentArity(2, int.MaxValue)` crash in beta5.

### Change Log

- 2026-03-26: Story 17-8 implementation complete. Added CLI-specific NuGet metadata, --version flag, CI/CD pipeline coverage, tool manifest entry, and 6 new packaging validation tests. Fixed pre-existing ArgumentArity bug in TenantCompareCommand.

### File List

**Modified:**
- `src/Hexalith.EventStore.Admin.Cli/Hexalith.EventStore.Admin.Cli.csproj` — CLI-specific Description, PackageTags
- `src/Hexalith.EventStore.Admin.Cli/Program.cs` — --version/-v flag handling
- `src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantCompareCommand.cs` — Fixed ArgumentArity(2, int.MaxValue) crash
- `.github/workflows/ci.yml` — Admin.Cli.Tests in Tier 1, test summary, smoke test step
- `.github/workflows/release.yml` — Admin.Cli.Tests in pre-release tests
- `.config/dotnet-tools.json` — eventstore-admin tool manifest entry

**New:**
- `tests/Hexalith.EventStore.Admin.Cli.Tests/PackageMetadataTests.cs` — 3 assembly metadata tests
- `tests/Hexalith.EventStore.Admin.Cli.Tests/VersionFlagTests.cs` — 3 version/help E2E tests
