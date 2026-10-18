# Contributing to Hexalith.EventStore

Thank you for your interest in contributing to Hexalith.EventStore! Whether you're fixing a typo, improving documentation, or adding a feature, your contributions are appreciated. This guide walks you through the contribution workflow and project conventions.

## How to Contribute

### Fork and Branch

1. [Fork the repository](https://github.com/Hexalith/Hexalith.EventStore/fork) to your GitHub account.
2. Clone your fork locally:

    ```bash
    git clone https://github.com/<your-username>/Hexalith.EventStore.git
    cd Hexalith.EventStore
    ```

3. Create a feature branch from `main` using one of these naming conventions:

    | Prefix               | Use for                      |
    | -------------------- | ---------------------------- |
    | `feat/<description>` | New features or enhancements |
    | `fix/<description>`  | Bug fixes                    |
    | `docs/<description>` | Documentation changes        |

    ```bash
    git checkout -b feat/my-new-feature
    ```

### Commit Messages

Install the repository-pinned Node.js tools after cloning. `npm ci` runs the
Husky `prepare` script and installs the tracked `commit-msg` hook:

```bash
npm ci
```

Every commit header must follow Conventional Commits because semantic-release
uses the type to determine release impact:

```text
<type>[optional scope][!]: <description>
```

Start the description with a lowercase letter and omit a trailing period. Use
imperative mood as a repository authoring convention. Keep the entire header,
each body line, and each footer line at 200 characters or fewer, and prefer a
concise header near 50 characters. Use `feat` for a minor release, `fix` or
`perf` for a patch release, and `docs`, `test`, `refactor`, `build`, `ci`,
`revert`, or `style` when product behavior does not change. Use `!` or a
`BREAKING CHANGE:` footer for a major release.

Do not use `chore`; use the specific non-release type that describes the work
(for example `build(deps)` for dependency maintenance).

Commitlint mechanically enforces the header format, allowed types, description
case, trailing punctuation, and length. Imperative mood and choosing the type
that accurately reflects release impact remain author and reviewer
responsibilities.

The hook validates the message automatically before Git creates a commit. You
can also validate a prepared message or the latest commit explicitly:

```bash
npx commitlint --edit <message-file> --verbose
npx commitlint --last --verbose
```

Do not bypass the hook with `--no-verify`. For example, a valid documentation
commit is `docs: complete projection/query parity proof`.

### Submit a Pull Request

1. Commit your changes with a Conventional Commit message.
2. Push your branch to your fork:

    ```bash
    git push origin feat/my-new-feature
    ```

3. Open a pull request against the `main` branch of the upstream repository.
4. Reference a related issue in the PR description if one exists (e.g., "Closes #42").
5. Wait for CI checks to pass and a maintainer to review your PR.

## Development Setup

For detailed installation instructions for each tool, see the [Prerequisites](docs/getting-started/prerequisites.md) page.

### Required Tools

- **.NET 10 SDK** (10.0.402 or later patch in the same feature band)
- **Node.js** (`^22.14.0` or `>=24.10.0`) and **npm** (for repository tooling
  and Git hook installation)
- **Docker Desktop** (for local DAPR infrastructure)
- **DAPR CLI** (1.18.x or later)

### Build and Test

```bash
# Clone the repository
git clone https://github.com/Hexalith/Hexalith.EventStore.git
cd Hexalith.EventStore

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run Tier 1 unit tests (run projects individually)
dotnet test tests/Hexalith.EventStore.Client.Tests
dotnet test tests/Hexalith.EventStore.Contracts.Tests
dotnet test tests/Hexalith.EventStore.Sample.Tests
dotnet test tests/Hexalith.EventStore.Testing.Tests
```

The solution file is `Hexalith.EventStore.slnx`.

### Run and Debug in the Platform Workspace

Run and debug EventStore as a module from the
[Hexalith.Platform](https://github.com/Hexalith/Hexalith.Platform) workspace, where this repository is the
direct submodule `references/Hexalith.EventStore`. Prerequisites:

- The Platform-pinned `hexalith-module` tool, restored with `dotnet tool restore` in the Platform root.
- Aspire CLI `13.6.1` exactly; the tool refuses any other version with `HXR015`.
- A running Docker engine.
- `HEXALITH_DAPR_HOME` set to an absolute, isolated Dapr home: Dapr CLI `1.18.0` at `tools/dapr` and runtime
  `1.18.2` at `.dapr/bin/{daprd,placement,scheduler}`. The Platform README shows how to prepare one from the
  official release assets.
- The checkout's `HEAD` equal to the gitlink staged in the Platform repository. After a local commit here, run
  `git add references/Hexalith.EventStore` in the Platform root before starting a run.

From the Platform root:

```bash
export HEXALITH_DAPR_HOME="$HOME/.hexalith/dapr/1.18.2"
dotnet tool run hexalith-module -- debug --manifest references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json
```

Pass the owner manifest under `references/`, never the Platform's copy in
`src/Hexalith.Platform.Composition/Declarations/`. `debug` builds this checkout in Debug, starts only the
`eventstore` gateway with its Dapr sidecar, the Dapr control plane and Redis, waits for the declared `/ready`
endpoint, and reports the process id of the `dotnet run` launcher. The gateway is the launcher's child process:

```bash
ps --ppid <launcher-pid> -o pid,args
```

Attach to the child running `src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore`, for example with
VS Code ("Attach to a .NET process"). Breakpoints in this checkout's sources bind. When you finish, stop only
that run with:

```bash
dotnet tool run hexalith-module -- down --manifest references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json
```

`debug` does not print the run id: it is the file name of the run state `<runId>.json` under
`${XDG_DATA_HOME:-$HOME/.local/share}/hexalith-builds/g4-runs/` on Linux. When an earlier run used the same manifest,
`down --manifest` refuses with `HXR024`. Inspect the state files and select the one whose `projectProcessIds`
contains the launcher id reported by this `debug` invocation. Confirm its `manifestHash` matches
`sha256sum references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json` (ignoring hexadecimal letter case),
then pass its `runId` with `--run-id`. File modification time alone cannot identify your run, and an explicit
`--run-id` is not cross-checked against the manifest:

```bash
dotnet tool run hexalith-module -- down --manifest references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json --run-id <runId>
```

`src/Hexalith.EventStore.AppHost` serves only this repository's own tests. A run of it is never evidence that
EventStore runs in the Platform composition.

## Documentation Contributions

### Where Documentation Lives

- **`docs/`** — Main documentation, organized by section:
    - `getting-started/` — Prerequisites, quickstart guide
    - `concepts/` — Architecture overview, decision aids
    - `guides/` — Deployment and operational guides
    - `reference/` — API reference, configuration
    - `community/` — Community resources
- **Root-level docs** — `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, `CODE_OF_CONDUCT.md`
- **Page template** — `docs/page-template.md` (use this as a starting point for new pages)

### Markdown Formatting Conventions

- **Heading hierarchy**: Use H1 through H4 with no skipped levels (H1 > H2 > H3 > H4, never H1 > H3).
- **Code blocks**: Always specify a language tag (`bash`, `csharp`, `json`, etc.).
- **Links**: Use relative links between pages within the repository.
- **Tone**: Professional-casual, developer-to-developer. Use second person ("you") rather than third person ("the developer").

### Run Docs Validation Locally

Before opening a PR for documentation changes, run the full validation suite
that mirrors the CI pipeline:

**Bash (Linux / macOS / Git Bash on Windows):**

```bash
./scripts/validate-docs.sh
```

**PowerShell (Windows):**

```powershell
.\scripts\validate-docs.ps1
```

The scripts run three stages in order: markdown linting, link checking, and
sample build/test. Prerequisites: Node.js (for markdownlint-cli2), [lychee](https://lychee.cli.rs/)
(link checker), and .NET SDK.

### Triaging Documentation CI Failures

When the **Docs** CI pipeline fails on the `sample-build` job, the sample no longer builds
or tests cleanly. Treat this as a stale documentation signal.

**How to triage:**

1. Check the CI failure output — identify whether `dotnet build` or `dotnet test` failed
2. Map the failure to documentation pages:
    - `samples/Hexalith.EventStore.Sample/` build failure → review `docs/getting-started/quickstart.md` and `README.md` code examples
    - `tests/Hexalith.EventStore.Sample.Tests/` test failure → review `docs/getting-started/quickstart.md` and `README.md` behavior notes
3. Update the affected documentation pages to match the new code/behavior
4. Push fixes and verify the Docs CI passes.

Additional sample-to-documentation mappings are maintained in comments inside `docs-validation.yml`.

## Code Contributions

### Coding Standards

- Follow the existing C# coding conventions in the codebase.
- Keep changes focused — one feature or fix per pull request.

### Test Requirements

The project uses a three-tier testing structure:

| Tier   | Scope                            | Location    |
| ------ | -------------------------------- | ----------- |
| Tier 1 | Unit tests                       | `tests/**/` |
| Tier 2 | Integration tests with DAPR      | `tests/**/` |
| Tier 3 | Aspire end-to-end contract tests | `tests/**/` |

- **New features should include Tier 1 unit tests at minimum.**
- Integration tests (Tier 2) are encouraged when your changes interact with DAPR components.

### Pull Request Expectations

- Your PR must pass CI checks: `dotnet build --configuration Release` followed by tests across tiers.
- Include a clear description of what you changed and why.
- Keep PRs small and reviewable when possible.

## Good First Issues

New to Hexalith.EventStore? Look for issues labeled [`good first issue`](https://github.com/Hexalith/Hexalith.EventStore/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22). These are curated, beginner-friendly tasks — typically documentation fixes, small enhancements, or test improvements — designed to help you get familiar with the project without needing deep domain knowledge.

## Community Guidelines

- Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).
- Use [GitHub Discussions](https://github.com/Hexalith/Hexalith.EventStore/discussions) for questions, ideas, and community support.
- Use the [Issue Tracker](https://github.com/Hexalith/Hexalith.EventStore/issues) for bug reports and feature requests.

We're glad you're here — happy contributing!
