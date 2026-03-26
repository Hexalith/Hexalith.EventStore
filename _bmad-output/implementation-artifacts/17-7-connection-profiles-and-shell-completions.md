# Story 17.7: Connection Profiles and Shell Completions

Status: done

Size: Medium-Large — 12 new source files (7 config subcommands, 2 profile infrastructure, 3 completion), 8 test files, 3 modified files (GlobalOptionsBinding, GlobalOptions, Program.cs), 5 task groups, 14 ACs, ~45 tests (~8-10 hours estimated). Replaces the `config` stub from story 17-1 with profile management subcommands and shell completion script generation. Adds `--profile` global option and profile-aware resolution to `GlobalOptionsBinding.Resolve()`. Profile loading is synchronous (< 1KB file) — no existing command files need modification. Reuses output formatting, `AdminApiClient`, `GlobalOptionsBinding`, exit code, and `JsonDefaults` infrastructure from 17-1.

**Dependency:** Story 17-1 must be complete (done). This story builds on the CLI scaffold, global options, output formatting, `AdminApiClient`, and exit code conventions established there. Stories 17-2 through 17-5 are done. No changes to existing command files are required — `Resolve` remains synchronous and its signature is unchanged.

## Definition of Done

- All 14 ACs verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- All `config` sub-subcommands return formatted output with correct exit codes
- Profile resolution integrates seamlessly with existing global options
- Shell completion scripts generate correctly for bash, zsh, PowerShell, and fish
- Existing commands continue to work identically when no profile is active

## Story

As a **platform operator managing multiple EventStore environments** (dev, staging, production),
I want **named connection profiles that store URL and token per environment, a `--profile` flag to switch between them, and shell completion scripts for bash, zsh, PowerShell, and fish**,
so that **I can quickly switch between environments without retyping `--url` and `--token` on every command, and I get tab-completion for subcommands, options, and profile names, reducing errors and improving CLI productivity (FR80, UX-DR53, UX-DR54, NFR42)**.

## Acceptance Criteria

1. **Profile storage** — Profiles are stored in `~/.eventstore/profiles.json` (Linux/macOS) or `%USERPROFILE%\.eventstore\profiles.json` (Windows) as a JSON object with schema `{ "version": 1, "activeProfile": "string?", "profiles": { "<name>": { "url": "string", "token": "string?", "format": "string?" } } }`. The `"version": 1` field enables future schema migrations without breaking existing installs — on load, if `version` is missing or `1`, proceed normally; if `version > 1`, print `Profile store version {n} is newer than this CLI supports. Please upgrade eventstore-admin.` to stderr and exit code `2`. The directory and file are created on first `config profile add`. File permissions: on Unix, the directory is created with `700` and the file with `600` (owner-only, since tokens may be stored). On Windows, default ACLs apply. The file uses `JsonDefaults.Options` serialization (camelCase, indented). **Home directory resolution:** use `Environment.GetFolderPath(SpecialFolder.UserProfile)` with fallback to `HOME` environment variable (for minimal Linux containers like Alpine/scratch where `UserProfile` returns empty). If both are empty, print `Cannot determine home directory. Set the HOME environment variable.` to stderr, exit code `2`.

2. **`config profile add` subcommand** — `eventstore-admin config profile add <name> --api-url <url> [--api-token <token>] [--default-format <format>]` creates or overwrites a named profile. `<name>` is a positional `Argument<string>("name", "Profile name")`. `--api-url` is a required `Option<string>("--api-url") { IsRequired = true, Description = "Admin API base URL" }` — deliberately NOT aliased `-u` to avoid collision with the global `--url` / `-u` option (System.CommandLine throws on ambiguous aliases with `Recursive = true`). `--api-token` is `Option<string?>("--api-token")` (optional) — avoids collision with global `--token`. `--default-format` is `Option<string?>("--default-format")` (optional, accepts "json", "csv", "table") — avoids collision with global `--format`. Prints `Profile '<name>' saved.` to stderr on success. Exit code `0` on success, `2` on file I/O error. If the profile name already exists, it is silently overwritten (upsert). Profile name validation: must be 1-64 characters, alphanumeric plus hyphens and underscores (`^[a-zA-Z0-9_-]{1,64}$`); invalid names print `Invalid profile name '<name>'. Use alphanumeric characters, hyphens, and underscores (1-64 chars).` to stderr, exit code `2`.

3. **`config profile list` subcommand** — `eventstore-admin config profile list` displays all saved profiles in table format. Columns: Name (name), URL (url), Token (masked — show first 4 chars + "..." or "(none)"), Format (format or "(default)"), Active (asterisk `*` if active). Exit code `0`. If no profiles exist, prints `No profiles configured. Use 'eventstore-admin config profile add <name> --api-url <url>' to create one.` to stderr, exit code `0`. JSON format: serialize the profiles object (tokens fully visible in JSON output — the operator chose JSON intentionally). CSV format: same columns as table (tokens masked in CSV, same as table).

4. **`config profile show` subcommand** — `eventstore-admin config profile show <name>` displays a single profile's details as key-value pairs in table format: Name, URL, Token (masked), Format, Active (yes/no). Exit code `0`. If profile not found, prints `Profile '<name>' not found.` to stderr, exit code `2`. JSON format: full profile object (token visible). CSV format: key-value pairs (token masked).

5. **`config profile remove` subcommand** — `eventstore-admin config profile remove <name>` removes a named profile. Prints `Profile '<name>' removed.` to stderr on success. If the removed profile was the active profile, clears the active profile and prints `Active profile cleared.` to stderr additionally. Exit code `0` on success. If profile not found, prints `Profile '<name>' not found.` to stderr, exit code `2`.

6. **`config use` subcommand** — `eventstore-admin config use <name>` sets the active profile. Prints `Active profile set to '<name>'.` to stderr. Exit code `0`. If profile not found, prints `Profile '<name>' not found. Run 'eventstore-admin config profile list' to see available profiles.` to stderr, exit code `2`. Running `eventstore-admin config use --clear` clears the active profile (no profile active). Prints `Active profile cleared.` to stderr, exit code `0`. If neither `<name>` nor `--clear` is provided, print the System.CommandLine built-in error message and usage help, exit code `2`. Implementation: `name` argument has `Arity = ArgumentArity.ZeroOrOne`; the `SetAction` handler checks: if `--clear` is true, clear active profile (ignore `name`); else if `name` is non-null, set active profile; else write usage error to stderr and return `ExitCodes.Error`.

7. **`config current` subcommand** — `eventstore-admin config current` shows the currently active profile and resolved connection settings. Table format shows key-value pairs: Active Profile (name or "(none)"), URL (resolved value + source label), Token (masked + source label), Format (resolved value + source label). Source labels: `(cli)` if user typed the flag, `(env: EVENTSTORE_ADMIN_URL)` if from env var, `(profile: <name>)` if from profile, `(default)` if hardcoded default. Exit code `0`. JSON format: `{ "activeProfile": "...", "url": { "value": "...", "source": "..." }, "token": { "value": "...", "source": "..." }, "format": { "value": "...", "source": "..." } }`. Implementation: use `GlobalOptionsBinding.ResolveWithSources(ParseResult)` which returns `(GlobalOptions Options, Dictionary<string, string> Sources)` — this method shares resolution logic with `Resolve` but also tracks provenance per field. Only `config current` calls `ResolveWithSources`; all other commands continue using `Resolve`.

8. **`--profile` / `-p` global option** — New global option `--profile` (alias `-p`) on the root command. When provided, loads the named profile's settings as defaults. Resolution priority (highest wins): (1) explicit CLI arg (`--url`, `--token`, `--format`), (2) environment variable (`EVENTSTORE_ADMIN_URL`, etc.), (3) named profile values, (4) hardcoded defaults. **CRITICAL — env var detection:** since `DefaultValueFactory` combines env var and hardcoded default into a single implicit value, `Resolve` MUST explicitly check each env var (`Environment.GetEnvironmentVariable(...)`) to determine whether the implicit value came from an env var or the hardcoded default. If the env var is set, its value takes priority over the profile — do NOT let the profile override it. Only when the env var is unset does the profile value apply. When `--profile` is not provided but an active profile is set in `profiles.json`, the active profile is used as the implicit default. `--profile` explicitly naming a profile overrides the active profile. If `--profile` names a non-existent profile, print `Profile '<name>' not found.` to stderr and exit code `2` — do NOT fall through to defaults silently.

9. **Parent `config` command help** — Running `eventstore-admin config` with no subcommand prints help listing all sub-subcommands: `profile` (parent), `use`, `current`, `completion`. `profile` itself lists: `add`, `list`, `show`, `remove`. `System.CommandLine` provides this automatically for parent commands with no handler.

10. **`config completion` subcommand** — `eventstore-admin config completion <shell>` generates a shell completion script to stdout. `<shell>` is a positional `Argument<string>("shell", "Shell type")` accepting "bash", "zsh", "powershell", "fish". Invalid shell names print `Unsupported shell '<name>'. Supported: bash, zsh, powershell, fish.` to stderr, exit code `2`. The generated script registers completions for the `eventstore-admin` command name. Exit code `0` on success. The script is printed to stdout (not stderr) so it can be piped: `eventstore-admin config completion bash >> ~/.bashrc`.

11. **Shell completion content** — Generated completion scripts provide static completions for: all subcommands (`health`, `stream`, `projection`, `tenant`, `snapshot`, `backup`, `config`), all global options (`--url`, `--token`, `--format`, `--output`, `--profile`), format values (`json`, `csv`, `table`), all sub-subcommand names, and profile names (read dynamically from `profiles.json` at completion time). For bash: use `complete -F` with a function that reads `profiles.json` for `--profile` completions. For zsh: use `compdef` with `_arguments` specs. For PowerShell: use `Register-ArgumentCompleter`. For fish: use `complete -c eventstore-admin`. Profile name completion: the generated script includes a helper that reads `~/.eventstore/profiles.json` and extracts profile names at completion time (not at script generation time).

12. **Security — token handling** — Tokens stored in `profiles.json` are plaintext. The `config profile add` command prints a one-time warning to stderr: `Note: Token stored in plaintext at <path>. Restrict file permissions for production use.` This warning appears only when `--api-token` is provided. The `config profile list` and `config profile show` commands mask tokens in table and CSV output (show first 4 chars + "..." or "(none)" for null). JSON output shows full tokens (intentional — operator chose JSON for scripting/export).

13. **Unit tests** — Tests cover: `ProfileManager` CRUD operations (add, list, show, remove, use, current, clear), profile file creation on first add, profile overwrite on duplicate name, active profile clearing on removal, profile name validation (valid names, invalid characters, too long, empty), `GlobalOptionsBinding` profile resolution priority (CLI > env > profile > default), `--profile` flag with non-existent profile error, implicit active profile usage, completion script generation for all four shells, completion script profile name extraction, `ConfigCommand` subcommand wiring, token masking in table/CSV output, full token in JSON output, file permission setting on Unix, graceful handling of missing/corrupt profiles.json.

14. **All existing Tier 1 and Tier 2 tests continue to pass** with zero behavioral change. Existing commands that do not use `--profile` and have no active profile behave identically to before.

## Tasks / Subtasks

- [x] **Task 1: Create ProfileManager infrastructure** (AC: #1, #12)
  - [x]1.1 Create `src/Hexalith.EventStore.Admin.Cli/Profiles/ConnectionProfile.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Profiles;

    /// <summary>
    /// A named connection profile storing Admin API connection settings.
    /// </summary>
    public record ConnectionProfile(string Url, string? Token, string? Format);
    ```
  - [x]1.2 Create `src/Hexalith.EventStore.Admin.Cli/Profiles/ProfileStore.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Profiles;

    /// <summary>
    /// The on-disk profile store schema. Version field enables future migrations.
    /// </summary>
    public record ProfileStore(int Version, string? ActiveProfile, Dictionary<string, ConnectionProfile> Profiles)
    {
        /// <summary>Current schema version written by this CLI.</summary>
        public const int CurrentVersion = 1;
    }
    ```
    - On load: if `Version` is 0 (missing/default) or 1, proceed. If `Version > CurrentVersion`, error with upgrade message (AC #1).
    - On save: always write `Version = CurrentVersion`.
  - [x]1.3 Create `src/Hexalith.EventStore.Admin.Cli/Profiles/ProfileManager.cs`:
    - Static class with **synchronous** methods operating on `profiles.json` (file is < 1KB — sync I/O is standard for CLI config files, same pattern as `kubectl`, `gh`, `az`)
    - All methods accept an optional `string? profilePath = null` parameter — when null, uses the default path. This enables test injection of a temp directory without interfaces or DI.
    - `GetDefaultProfilePath()` — resolves home directory with fallback chain: `Environment.GetFolderPath(SpecialFolder.UserProfile)`, then `Environment.GetEnvironmentVariable("HOME")`, then `Environment.GetEnvironmentVariable("USERPROFILE")`. If all return null/empty, throws `InvalidOperationException("Cannot determine home directory. Set the HOME environment variable.")`. Returns `Path.Combine(home, ".eventstore", "profiles.json")`.
    - `Load(string? profilePath = null)` — reads and deserializes `profiles.json` using `File.ReadAllText`, returns empty `ProfileStore` if file missing or empty. Checks version field: if `> CurrentVersion`, throws `ProfileStoreVersionException`.
    - `Save(ProfileStore store, string? profilePath = null)` — serializes to `profiles.json` with `JsonDefaults.Options` using `File.WriteAllText`, creates directory if needed, sets Unix file permissions (700 dir, 600 file) via `File.SetUnixFileMode` when `!OperatingSystem.IsWindows()`
    - `ValidateProfileName(string)` — returns bool, validates `^[a-zA-Z0-9_-]{1,64}$`
    - `MaskToken(string?)` — returns `"(none)"` for null/empty, first 4 chars + `"..."` otherwise
    - Uses `JsonDefaults.Options` for serialization consistency
    - Graceful handling of corrupt JSON: catch `JsonException`, print warning to stderr, treat as empty store

- [x] **Task 2: Integrate `--profile` global option into GlobalOptionsBinding** (AC: #8)
  - [x]2.1 Modify `src/Hexalith.EventStore.Admin.Cli/GlobalOptionsBinding.cs`:
    - Add `Option<string?> ProfileOption` property to the record
    - In `Create()`: add `Option<string?> profileOption = new("--profile", "-p") { Description = "Connection profile name", Recursive = true }`
    - Default value factory: `_ => null` (no default — active profile resolved at `Resolve` time)
  - [x]2.2 Modify `src/Hexalith.EventStore.Admin.Cli/GlobalOptions.cs`:
    - Add `string? Profile` property: `public record GlobalOptions(string Url, string? Token, string Format, string? OutputFile, string? Profile);`
  - [x]2.3 Modify `Resolve(ParseResult)` in `GlobalOptionsBinding` to be profile-aware:
    - `Resolve` **stays synchronous** — `ProfileManager.Load()` is sync (< 1KB file). No existing command files need changes.
    - Resolution logic:
      1. Get `--profile` value from `parseResult.GetValue(ProfileOption)`
      2. If `--profile` is non-null, call `ProfileManager.Load()` and look up the named profile. If not found, throw `ProfileNotFoundException`.
      3. If `--profile` is null, call `ProfileManager.Load()` and check `ActiveProfile`. If set, use that profile. If `profiles.json` doesn't exist, skip (no profile applied).
      4. For each option (url, token, format), apply the 4-layer priority:
         - **Check if user explicitly typed the option:** `parseResult.FindResultFor(option)` is non-null and `IsImplicit == false` → use CLI value, source = "cli"
         - **Check if env var is set:** `Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_URL")` (or TOKEN/FORMAT equivalent) is non-null → use env value, source = "env: EVENTSTORE_ADMIN_URL". **CRITICAL:** This explicit env var check prevents profiles from overriding env vars — `DefaultValueFactory` combines env+default into one implicit value which `IsImplicit` can't distinguish.
         - **Check if profile has a value:** profile's field is non-null → use profile value, source = "profile: <name>"
         - **Otherwise:** keep `DefaultValueFactory` value (hardcoded default), source = "default"
      5. **Verify `IsImplicit` availability** at implementation time: if the pinned System.CommandLine version doesn't expose `OptionResult.IsImplicit`, use the fallback `result.Token is not null` check.
    - If `--profile` names a non-existent profile, throw `ProfileNotFoundException` (caught in `Program.cs` top-level try/catch)
    - **No existing command files change.** Signature remains `GlobalOptions Resolve(ParseResult parseResult)`.
  - [x]2.4 Add `ResolveWithSources(ParseResult)` method to `GlobalOptionsBinding`:
    - Same resolution logic as `Resolve` but returns `(GlobalOptions Options, Dictionary<string, string> Sources)` where Sources maps field names ("url", "token", "format") to source labels ("cli", "env: EVENTSTORE_ADMIN_URL", "profile: prod", "default")
    - Used ONLY by `ConfigCurrentCommand` — all other commands use `Resolve`
    - Share the resolution logic via a private `ResolveInternal(ParseResult, bool trackSources)` helper that both `Resolve` and `ResolveWithSources` call
  - [x]2.5 Modify `src/Hexalith.EventStore.Admin.Cli/Program.cs`:
    - Add `rootCommand.Options.Add(binding.ProfileOption);`
    - Catch `ProfileNotFoundException` in the top-level try/catch, print message to stderr, return `ExitCodes.Error`
    - **No changes to existing command registrations** — `Resolve` signature is unchanged

- [x] **Task 3: Create config parent command and profile subcommands** (AC: #2, #3, #4, #5, #9)
  - [x]3.1 Create `src/Hexalith.EventStore.Admin.Cli/Commands/Config/ConfigCommand.cs` — parent command with subcommands:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

    public static class ConfigCommand
    {
        public static Command Create(GlobalOptionsBinding binding)
        {
            Command command = new("config", "Manage connection profiles and CLI configuration");
            command.Subcommands.Add(ProfileCommand.Create());
            command.Subcommands.Add(ConfigUseCommand.Create());
            command.Subcommands.Add(ConfigCurrentCommand.Create(binding));
            command.Subcommands.Add(ConfigCompletionCommand.Create());
            return command;
        }
    }
    ```
  - [x]3.2 Create `Commands/Config/ProfileCommand.cs` — parent for profile CRUD:
    ```csharp
    public static class ProfileCommand
    {
        public static Command Create()
        {
            Command command = new("profile", "Manage named connection profiles");
            command.Subcommands.Add(ProfileAddCommand.Create());
            command.Subcommands.Add(ProfileListCommand.Create());
            command.Subcommands.Add(ProfileShowCommand.Create());
            command.Subcommands.Add(ProfileRemoveCommand.Create());
            return command;
        }
    }
    ```
  - [x]3.3 Create `Commands/Config/ProfileAddCommand.cs` (AC: #2):
    - Positional: `Argument<string>("name", "Profile name")`
    - Required option: `Option<string>("--api-url") { IsRequired = true, Description = "Admin API base URL" }` — NO `-u` alias (avoids collision with global `--url` / `-u`)
    - Optional: `Option<string?>("--api-token") { Description = "JWT Bearer token or API key" }` — avoids collision with global `--token`
    - Optional: `Option<string?>("--default-format") { Description = "Default output format" }` with `AcceptOnlyFromAmong("json", "csv", "table")` — avoids collision with global `--format`
    - Validates profile name via `ProfileManager.ValidateProfileName()`
    - Calls `ProfileManager.Load()`, adds/overwrites profile, calls `ProfileManager.Save()`
    - Prints token plaintext warning when `--api-token` is provided
    - Returns `ExitCodes.Success` or `ExitCodes.Error`
  - [x]3.4 Create `Commands/Config/ProfileListCommand.cs` (AC: #3):
    - No arguments or options
    - Loads profiles, formats as table/JSON/CSV using existing `IOutputFormatter` and `ColumnDefinition` patterns
    - Columns: Name, URL, Token (masked), Format, Active
    - Note: this command does NOT use `GlobalOptionsBinding` for format — it always shows table unless `--format` global option is passed. The global `--format` option IS available since it's recursive.
  - [x]3.5 Create `Commands/Config/ProfileShowCommand.cs` (AC: #4):
    - Positional: `Argument<string>("name", "Profile name")`
    - Shows single profile as key-value table
  - [x]3.6 Create `Commands/Config/ProfileRemoveCommand.cs` (AC: #5):
    - Positional: `Argument<string>("name", "Profile name")`
    - Removes profile, clears active if it was the removed profile

- [x] **Task 4: Create config use and current subcommands** (AC: #6, #7)
  - [x]4.1 Create `Commands/Config/ConfigUseCommand.cs` (AC: #6):
    - Positional: `Argument<string?>("name", "Profile name to activate")` with `Arity = ArgumentArity.ZeroOrOne` (nullable since optional when `--clear` is used)
    - Option: `Option<bool>("--clear") { Description = "Clear active profile" }`
    - Handler logic: if `--clear` is true → clear active profile (ignore `name`); else if `name` is non-null → validate profile exists, set as active; else (neither `--clear` nor `name`) → write `"Specify a profile name or use --clear to deactivate."` to stderr, return `ExitCodes.Error`
    - Validates profile exists before setting as active
    - Saves updated `ProfileStore`
  - [x]4.2 Create `Commands/Config/ConfigCurrentCommand.cs` (AC: #7):
    - No arguments
    - Calls `binding.ResolveWithSources(parseResult)` to get both resolved values and per-field source labels
    - Table format: key-value pairs with source in parentheses, e.g., `URL: http://prod:5002 (env: EVENTSTORE_ADMIN_URL)`
    - JSON format: `{ "activeProfile": "...", "url": { "value": "...", "source": "..." }, ... }`
    - Note: this is the ONLY command that calls `ResolveWithSources` — all others use `Resolve`

- [x] **Task 5: Create shell completion script generation** (AC: #10, #11)
  - [x]5.1 Create `Commands/Config/ConfigCompletionCommand.cs`:
    - Positional: `Argument<string>("shell", "Shell type")` with `AcceptOnlyFromAmong("bash", "zsh", "powershell", "fish")` — note: use `FromAmong` on the argument, or validate manually since `AcceptOnlyFromAmong` is for options
    - Routes to appropriate generator method
    - Writes generated script to stdout (NOT via `OutputWriter` — raw stdout so pipe works)
  - [x]5.2 Create `Commands/Config/CompletionScripts.cs` — static class with generation methods:
    - `GenerateBash()` — bash completion script using `complete -F _eventstore_admin_completions eventstore-admin`:
      - Static completions: subcommands, options, format values
      - Dynamic profile completions: use **portable `grep`/`sed`** to extract profile names from `~/.eventstore/profiles.json` — do NOT assume `jq` is installed. Pattern: `grep -o '"[a-zA-Z0-9_-]*"\s*:' ~/.eventstore/profiles.json | sed 's/[": ]//g'` to extract keys from the profiles object. Guard with `[ -f ~/.eventstore/profiles.json ]` check.
      - Follows standard bash completion patterns (check `COMP_WORDS`, `COMP_CWORD`)
    - `GenerateZsh()` — zsh completion using `compdef`:
      - Uses `_arguments` for options with descriptions
      - `_eventstore_profiles` function using same portable `grep`/`sed` pattern for dynamic profile names
    - `GeneratePowerShell()` — PowerShell using `Register-ArgumentCompleter`:
      - `ArgumentCompleter` scriptblock with parameter-aware completions
      - Profile names from `Get-Content ~\.eventstore\profiles.json | ConvertFrom-Json | Select-Object -ExpandProperty profiles | Get-Member -MemberType NoteProperty | Select-Object -ExpandProperty Name` (PowerShell is always available on Windows; on Linux/macOS, PowerShell users have it installed)
    - `GenerateFish()` — fish using `complete -c eventstore-admin`:
      - Condition-based completions (`-n '__fish_use_subcommand'`, etc.)
      - Dynamic profile function using portable `grep`/`sed` extraction (same as bash)
    - All scripts include a header comment: `# Generated by eventstore-admin config completion <shell>` with generation timestamp
    - All scripts include installation instructions as comments (e.g., `# Add to ~/.bashrc: eval "$(eventstore-admin config completion bash)"` for bash)

- [x] **Task 6: Update Program.cs and write tests** (AC: #13, #14)
  - [x]6.1 Modify `src/Hexalith.EventStore.Admin.Cli/Program.cs`:
    - Replace `StubCommands.Create("config", ...)` with `ConfigCommand.Create(binding)`
    - Add `using Hexalith.EventStore.Admin.Cli.Commands.Config;`
    - Remove `config` from StubCommands import if no longer needed
  - [x]6.2 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/Profiles/ProfileManagerTests.cs`:
    - Test `Load` with missing file (returns empty store with `Version = CurrentVersion`)
    - Test `Load` with valid file (returns parsed profiles)
    - Test `Load` with corrupt JSON (returns empty store, warns to stderr)
    - Test `Load` with `version: 1` (proceeds normally)
    - Test `Load` with missing `version` field (treats as version 1, proceeds)
    - Test `Load` with `version: 2` (throws/errors with upgrade message)
    - Test `Load` with custom `profilePath` parameter (test injection)
    - Test `Save` creates directory and file with `version: 1`
    - Test `Save` overwrites existing file preserving version field
    - Test `Save` sets Unix file permissions on non-Windows (use `[SkippableFact]` for platform)
    - Test `Save` silently skips permissions on Windows
    - Test `GetDefaultProfilePath` with `HOME` fallback when `UserProfile` is empty
    - Test `ValidateProfileName` with valid names (`prod`, `my-env`, `test_1`)
    - Test `ValidateProfileName` with invalid names (empty, spaces, `../hack`, 65+ chars, special chars)
    - Test `MaskToken` with null, empty, short (< 4 chars), normal tokens
    - Use temp directory for file operations (not real `~/.eventstore`)
  - [x]6.3 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ProfileAddCommandTests.cs`:
    - Test add new profile (file created, profile stored)
    - Test overwrite existing profile
    - Test invalid profile name
    - Test with and without `--api-token`
    - Test with `--format` validation
  - [x]6.4 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ProfileListCommandTests.cs`:
    - Test list with no profiles
    - Test list with multiple profiles (active marked)
    - Test token masking in table output
    - Test JSON output shows full tokens
  - [x]6.5 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ProfileRemoveCommandTests.cs`:
    - Test remove existing profile
    - Test remove active profile (clears active)
    - Test remove non-existent profile
  - [x]6.6 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ConfigUseCommandTests.cs`:
    - Test set active profile
    - Test set non-existent profile
    - Test `--clear` flag
  - [x]6.7 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ConfigCurrentCommandTests.cs`:
    - Test with active profile
    - Test with no profile (defaults)
    - Test source attribution
  - [x]6.8 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ConfigCompletionCommandTests.cs`:
    - Test generation for each shell (bash, zsh, powershell, fish)
    - Test invalid shell name
    - Test output goes to stdout
    - Test generated scripts contain expected subcommand names
    - Test generated scripts contain profile reading logic
  - [x]6.9 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/GlobalOptionsBindingProfileTests.cs` — **critical file, minimum 14 test cases:**
    - Test CLI explicit `--url` + profile has url → CLI wins (source = "cli")
    - Test env var `EVENTSTORE_ADMIN_URL` set + profile has url → **env wins** (explicit env var check prevents profile override)
    - Test profile has url + no env + no CLI → profile wins (source = "profile: <name>")
    - Test no profile + no env → hardcoded default wins (source = "default")
    - Test `--profile` flag + active profile set → `--profile` flag wins
    - Test `--profile` flag + no such profile → `ProfileNotFoundException`
    - Test active profile set + no `--profile` flag → active profile used
    - Test no active profile + no `--profile` → no profile applied, defaults used
    - Test profile has url but not token; env has token → merge across sources (url from profile, token from env)
    - Test profile has format "csv"; CLI has `--format json` → CLI wins for format
    - Test corrupt `profiles.json` + `--profile` flag → graceful error, warns to stderr
    - Test missing `profiles.json` + no `--profile` → silent no-op, defaults used (backward compatible)
    - Test `IsImplicit` detection: verify explicit CLI value detected vs DefaultValueFactory value
    - Test `ResolveWithSources` returns correct source labels for each resolution layer
    - **Environment variable test isolation:** Tests that set env vars MUST use `try/finally` to restore original values, or use a test fixture that manages env var cleanup. Parallel test execution can cause flaky tests if env vars leak between tests.
  - [x]6.10 Run all existing tests to verify zero regressions

## Dev Notes

### Architecture Compliance

- **Thin HTTP client pattern** — Profile management is purely CLI-side. No new Admin API endpoints needed. Profiles are local configuration files, not server-side resources.
- **ADR-P4** — CLI remains a thin HTTP client. Profiles just configure WHERE it connects.
- **No new NuGet dependencies** — `System.Text.Json` (already used), `System.Text.RegularExpressions` (already available), `System.IO` (framework). No external config library needed.
- **`JsonDefaults.Options`** — Reuse existing serialization settings for `profiles.json` consistency.

### Critical Design Decisions

**ADR-1: Synchronous Profile Loading (no ResolveAsync migration)**

`profiles.json` is < 1KB. Sync I/O (`File.ReadAllText`) for config files is standard practice — `kubectl`, `gh`, `az`, and ASP.NET Core's `appsettings.json` all read config synchronously. Making `ProfileManager` sync means `Resolve` stays sync, its signature is unchanged, and **zero existing command files need modification**. This eliminates the riskiest task in the story (~20 file migration) with no measurable performance impact. NFR42 (3s startup) is unaffected.

**ADR-2: Explicit env var checks prevent profile override bug**

`DefaultValueFactory` combines env var and hardcoded default into a single value. `IsImplicit` returns `true` for both — it cannot distinguish "value came from env var" from "value is hardcoded default." Without explicit env var checks, profiles would silently override environment variables, violating the documented priority.

**Resolution priority (highest wins):**
1. Explicit CLI flag (user typed `--url http://...`)
2. Environment variable (`EVENTSTORE_ADMIN_URL`, `EVENTSTORE_ADMIN_TOKEN`, `EVENTSTORE_ADMIN_FORMAT`)
3. Profile value (from named or active profile)
4. Hardcoded default (`http://localhost:5002`, `null`, `table`)

**Resolve logic (pseudocode):**
```
profile = LoadProfile(--profile flag OR active profile from profiles.json)

For each option (url, token, format):
  envVarName = option-specific env var name
  result = parseResult.FindResultFor(option)

  if result is not null AND NOT implicit:
    value = CLI value                              # Layer 1: user typed it
    source = "cli"
  elif Environment.GetEnvironmentVariable(envVarName) is not null:
    value = env var value                          # Layer 2: env var set
    source = "env: {envVarName}"
  elif profile is not null AND profile has this field:
    value = profile value                          # Layer 3: from profile
    source = "profile: {profileName}"
  else:
    value = DefaultValueFactory value              # Layer 4: hardcoded default
    source = "default"
```

**IMPORTANT — Verify `IsImplicit` availability:** Before implementing, confirm that the pinned `System.CommandLine` version in `Directory.Packages.props` exposes `OptionResult.IsImplicit`. If the property is unavailable, use the fallback `result.Token is not null` — a non-null `Token` means the option appeared on the command line.

**ADR-3: ResolveWithSources for config current only**

The `config current` command needs per-field source attribution. Rather than changing `Resolve`'s return type (which would break all callers), a separate `ResolveWithSources(ParseResult)` method returns `(GlobalOptions, Dictionary<string, string>)`. Both methods share a private `ResolveInternal` helper. Only `ConfigCurrentCommand` calls `ResolveWithSources`.

### File Structure

```
src/Hexalith.EventStore.Admin.Cli/
  Profiles/
    ConnectionProfile.cs          # NEW - Profile record
    ProfileStore.cs               # NEW - Store schema record
    ProfileManager.cs             # NEW - File I/O operations
    ProfileNotFoundException.cs   # NEW - Exception for missing profiles
  Commands/Config/
    ConfigCommand.cs              # NEW - Parent command
    ProfileCommand.cs             # NEW - Profile parent command
    ProfileAddCommand.cs          # NEW - profile add
    ProfileListCommand.cs         # NEW - profile list
    ProfileShowCommand.cs         # NEW - profile show
    ProfileRemoveCommand.cs       # NEW - profile remove
    ConfigUseCommand.cs           # NEW - use <name>
    ConfigCurrentCommand.cs       # NEW - current
    ConfigCompletionCommand.cs    # NEW - completion <shell>
    CompletionScripts.cs          # NEW - Script generation
  GlobalOptions.cs                # MODIFIED - add Profile property
  GlobalOptionsBinding.cs         # MODIFIED - add ProfileOption, profile-aware Resolve, ResolveWithSources
  Program.cs                      # MODIFIED - replace stub, add ProfileOption

tests/Hexalith.EventStore.Admin.Cli.Tests/
  Profiles/
    ProfileManagerTests.cs        # NEW
  Commands/Config/
    ProfileAddCommandTests.cs     # NEW
    ProfileListCommandTests.cs    # NEW
    ProfileRemoveCommandTests.cs  # NEW
    ConfigUseCommandTests.cs      # NEW
    ConfigCurrentCommandTests.cs  # NEW
    ConfigCompletionCommandTests.cs # NEW
  GlobalOptionsBindingProfileTests.cs # NEW
```

### Existing Code Patterns to Follow

- **Command factory pattern:** Every command class is `static` with `Create(GlobalOptionsBinding binding)` returning `Command`. Use `command.SetAction(async (parseResult, cancellationToken) => { ... })` for the handler.
- **Dual ExecuteAsync pattern:** Public overload creates `AdminApiClient`, internal overload accepts it for testability. Config commands that don't call the API skip this pattern — they just have a single internal `ExecuteAsync`.
- **Output formatting:** Use `IOutputFormatter` + `OutputFormatterFactory.Create(options.Format)` + `OutputWriter` for all formatted output. Table columns defined as `internal static readonly List<ColumnDefinition>`.
- **Error handling:** Catch `AdminApiException` (for API commands) or `IOException`/`JsonException` (for file operations), print to `Console.Error.WriteLine`, return `ExitCodes.Error`.
- **Namespace convention:** `Hexalith.EventStore.Admin.Cli.Commands.Config` for commands, `Hexalith.EventStore.Admin.Cli.Profiles` for profile infrastructure.
- **File naming:** One public type per file, filename = type name.

### Previous Story Intelligence (17-6)

Story 17-6 added `PutAsync` and `DeleteAsync` to `AdminApiClient` and introduced the snapshot/backup command pattern. Key learnings:
- Parent commands (like `SnapshotCommand`) are purely structural — `Create()` adds subcommands, no handler
- Positional arguments defined with `Argument<T>` — keep descriptions concise
- The existing `StubCommands.Create("backup", ...)` pattern shows exactly what to replace
- All commands accessed `GlobalOptionsBinding` format through the resolved `GlobalOptions` record

### Git Intelligence

Recent commits show consistent patterns:
- `f864c8a` (17-5): Tenant subcommand with 6 sub-subcommands
- `02f3a20` (17-4): Health with CI/CD options
- `8543c83` (17-3): Projection subcommand with 5 sub-subcommands

All follow the same command factory pattern and commit message format: `feat: Add <description> for story 17-X`.

### Anti-Patterns to Avoid

1. **DO NOT** add a dependency on any external configuration library (e.g., Microsoft.Extensions.Configuration). Use plain `System.Text.Json` file I/O — the profile store is simple enough.
2. **DO NOT** encrypt tokens in `profiles.json`. Plaintext with file permissions is the standard approach (matches `kubectl`, `az cli`, `gh`, `aws cli`). Warn the user once.
3. **DO NOT** use `System.CommandLine`'s `dotnet-suggest` infrastructure for completions — it requires a separate global tool installation. Generate standalone completion scripts instead (matches `kubectl completion`, `gh completion`, `az completion`).
4. **DO NOT** make profile commands depend on `AdminApiClient` or require a working server connection. Profile operations are purely local file operations.
5. **DO NOT** change the behavior of existing commands when no profile is active and no `--profile` flag is passed. This is the backward compatibility guarantee.
6. **DO NOT** add `--profile` as a positional argument. It must be a named option with `-p` alias.
7. **DO NOT** make `ProfileManager` async or `Resolve` async. The file is < 1KB — sync I/O is the correct choice. Introducing async here would require migrating ~20 existing command files for zero benefit.
8. **DO NOT** rely solely on `IsImplicit` to distinguish env vars from hardcoded defaults. `DefaultValueFactory` combines both into one implicit value. Always check env vars explicitly with `Environment.GetEnvironmentVariable()` before applying profile values.

### Testing Guidance

- **Profile file operations:** Use a temp directory (`Path.GetTempPath()`) for all profile tests. All `ProfileManager` methods accept an optional `string? profilePath` parameter — pass a temp file path in tests. Production callers pass `null` (uses default `~/.eventstore/profiles.json`). No interfaces or DI needed — the optional parameter IS the testing seam.
- **Unix file permissions on Windows CI:** `File.SetUnixFileMode` throws `PlatformNotSupportedException` on Windows. The production code guards with `if (!OperatingSystem.IsWindows())`. Tests must verify BOTH paths: (a) on non-Windows, assert permissions are set to 700/600; (b) on Windows, assert the code silently skips permission setting without error. Use `[SkippableFact]` or runtime `Skip.If(OperatingSystem.IsWindows())` for the Unix-specific assertion, and a separate test that runs on all platforms verifying the Windows skip path works.
- **GlobalOptionsBinding tests:** Mock the profile file path. Test all 12+ resolution priority combinations (see Task 6.9).
- **Completion script tests:** Verify scripts contain expected strings (subcommand names, option names, profile reading logic). Don't try to execute them in tests — just assert content. Consider snapshot-style tests: store expected completion script fragments as string constants and compare key sections, making updates intentional rather than fragile string matching.
- **Alias collision regression test:** Add a test that parses `eventstore-admin config profile add prod --api-url http://localhost:5002` and verifies it does NOT conflict with the global `--url` option. This catches any accidental alias reintroduction.
- **Test helpers:** Reuse existing `MockHttpMessageHandler` and `QueuedMockHttpMessageHandler` for any tests that need HTTP mocking (unlikely for this story since config commands don't call the API).

### NFR42 Compliance

CLI startup must remain under 3 seconds. Profile resolution adds one synchronous file read (`profiles.json`, typically < 1KB, ~0.1ms). This is negligible — same pattern used by `kubectl`, `gh`, `az`, and ASP.NET Core config loading.
- Sync `File.ReadAllText` is the correct choice for < 1KB config files — async overhead would exceed the I/O time
- Do NOT validate the profile against the server (e.g., no HTTP call to verify the URL works during resolution)
- Do NOT load profiles.json multiple times per invocation — load once in `Resolve`, use the result for all fields

### Project Structure Notes

- All new files go under `src/Hexalith.EventStore.Admin.Cli/` and `tests/Hexalith.EventStore.Admin.Cli.Tests/`
- New directories: `Profiles/` (under src), `Commands/Config/` (under src and tests), `Profiles/` (under tests)
- `InternalsVisibleTo` already configured for the test project
- No changes to `.csproj` files expected (no new NuGet packages)

### References

- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR53] Dynamic shell completion
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR54] Connection profiles
- [Source: _bmad-output/planning-artifacts/prd.md#FR80] CLI output formats, exit codes, completions
- [Source: _bmad-output/planning-artifacts/prd.md#NFR42] CLI startup time requirement
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-17] Admin CLI epic
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P4] CLI as thin HTTP client
- [Source: src/Hexalith.EventStore.Admin.Cli/GlobalOptionsBinding.cs] Current global options pattern
- [Source: src/Hexalith.EventStore.Admin.Cli/Commands/StubCommands.cs] Config stub to replace
- [Source: src/Hexalith.EventStore.Admin.Cli/Program.cs:25] Existing config stub registration

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- System.CommandLine beta5: `OptionResult.IsImplicit` not available — used `result.Tokens.Count > 0` fallback
- System.CommandLine beta5: `Argument<T>` no 2-arg constructor — used object initializer with `Description` property
- System.CommandLine beta5: `Option<T>.IsRequired` not available — used `Required = true` property
- Parallel test Console interference: Added `[Collection("ConsoleTests")]` to all tests that manipulate `Console.SetError`/`Console.SetOut`
- Pre-existing `CS0433` error in IntegrationTests (Tier 3) — not caused by this story

### Completion Notes List

- Task 1: Created `ConnectionProfile`, `ProfileStore`, `ProfileManager`, `ProfileNotFoundException`, `ProfileStoreVersionException` in `Profiles/` directory. Sync I/O for < 1KB config files. Regex-based profile name validation. Unix file permissions (700/600) with Windows skip.
- Task 2: Added `--profile`/`-p` global option to `GlobalOptionsBinding`. Implemented 4-layer resolution priority: CLI > env > profile > default. Added `ResolveWithSources` for `config current` command. Used `result.Tokens.Count > 0` to detect explicit CLI values. Internal `ProfilePath` property for test injection.
- Task 3: Created `ConfigCommand`, `ProfileCommand`, `ProfileAddCommand`, `ProfileListCommand`, `ProfileShowCommand`, `ProfileRemoveCommand`. Profile CRUD with token masking, format support, active profile marker.
- Task 4: Created `ConfigUseCommand` (set/clear active profile) and `ConfigCurrentCommand` (resolved settings with source attribution).
- Task 5: Created `ConfigCompletionCommand` and `CompletionScripts` with bash, zsh, PowerShell, fish generators. Dynamic profile name completion using portable grep/sed. Static completions for all subcommands, options, format values.
- Task 6: Replaced config stub in `Program.cs`. Added `ProfileNotFoundException`/`ProfileStoreVersionException` catch handlers. Created 8 test files with 45+ test cases. All 261 CLI tests pass. All Tier 1 tests pass with zero regressions.
- Code review fixes: 10 patch items resolved. `ProfileListCommand.Create()` and `ProfileShowCommand.Create()` now accept `GlobalOptionsBinding` parameter. `ProfileCommand.Create()` propagates binding. Added `ProfileShowCommandTests.cs`. Expanded `ConfigCurrentCommandTests` and `GlobalOptionsBindingProfileTests`. Reserved profile names rejected. Fish completion scoping fixed. 279 CLI tests pass after review fixes.

### Change Log

- 2026-03-26: Implemented story 17-7 — Connection profiles and shell completions
- 2026-03-26: Code review (3-layer adversarial: Blind Hunter, Edge Case Hunter, Acceptance Auditor). 42 raw findings → 16 actionable (1 bad_spec, 10 patch, 5 defer). All 10 patch items fixed:
  - P-1 (HIGH): Config commands now resolve `--format` via `binding.Resolve()` instead of bypassing profile priority
  - P-2 (MEDIUM): Error handling improved in `ResolveInternal` (narrowed IOE catch, added IO/unauthorized catches), `ProfileRemoveCommand`, `ConfigUseCommand`
  - P-3 (MEDIUM): Created `ProfileShowCommandTests.cs` (4 tests)
  - P-4 (MEDIUM): Expanded `ConfigCurrentCommandTests.cs` from 1 to 5 tests
  - P-5 (MEDIUM): Added 3 tests to `GlobalOptionsBindingProfileTests` + 1 reserved name test to `ProfileManagerTests`
  - P-6 (LOW): Empty env var now ignored (`IsNullOrEmpty` check)
  - P-7 (LOW): Fish completion `use` matching now requires both `config` and `use` context
  - P-8 (LOW): Renamed `ProfileAddCommand.ExecuteAsync` to `Execute` (sync method)
  - P-9 (LOW): Profile name validation rejects reserved names (`version`, `activeProfile`, `profiles`, `url`, `token`, `format`)
  - P-10 (LOW): Completion script headers now include generation date
  - BS-1 (bad spec, not fixed): `MaskToken` leaks short tokens (<4 chars) in full — spec amendment needed
  - 5 defer items noted for future: non-atomic writes, grep/sed JSON parsing, no URL validation, bash _init_completion, -p alias reservation
  - All 279 CLI tests pass. All Tier 1 tests pass with zero regressions.

### File List

**New files:**
- src/Hexalith.EventStore.Admin.Cli/Profiles/ConnectionProfile.cs
- src/Hexalith.EventStore.Admin.Cli/Profiles/ProfileStore.cs
- src/Hexalith.EventStore.Admin.Cli/Profiles/ProfileManager.cs
- src/Hexalith.EventStore.Admin.Cli/Profiles/ProfileNotFoundException.cs
- src/Hexalith.EventStore.Admin.Cli/Profiles/ProfileStoreVersionException.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/ConfigCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/ProfileCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/ProfileAddCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/ProfileListCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/ProfileShowCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/ProfileRemoveCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/ConfigUseCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/ConfigCurrentCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/ConfigCompletionCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Config/CompletionScripts.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Profiles/ProfileManagerTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ProfileAddCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ProfileListCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ProfileRemoveCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ConfigUseCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ConfigCurrentCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/ConfigCompletionCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/GlobalOptionsBindingProfileTests.cs

**Modified files:**
- src/Hexalith.EventStore.Admin.Cli/GlobalOptions.cs (added Profile parameter)
- src/Hexalith.EventStore.Admin.Cli/GlobalOptionsBinding.cs (added ProfileOption, profile-aware Resolve, ResolveWithSources)
- src/Hexalith.EventStore.Admin.Cli/Program.cs (replaced config stub, added ProfileOption, catch handlers)
- tests/Hexalith.EventStore.Admin.Cli.Tests/StubCommandsTests.cs (removed config stub test, added Collection attribute)
- tests/Hexalith.EventStore.Admin.Cli.Tests/GlobalOptionsTests.cs (added ProfileOption to BuildRootCommand)
