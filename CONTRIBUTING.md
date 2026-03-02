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

### Submit a Pull Request

1. Commit your changes with a clear, descriptive commit message.
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

- **.NET 10 SDK** (10.0.102 or later)
- **Docker Desktop** (for local DAPR infrastructure)
- **DAPR CLI** (1.16.x or later)

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
