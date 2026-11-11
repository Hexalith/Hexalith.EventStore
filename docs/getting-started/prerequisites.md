[← Back to Hexalith.EventStore](../../README.md)

# Prerequisites

Before you start the quickstart, you need three tools installed: the pinned .NET 11 SDK to build the application and the .NET 10 runtime to run it, Docker Desktop to provide local infrastructure containers, and the DAPR CLI to manage the infrastructure abstraction layer that Hexalith.EventStore uses for state storage, messaging, and actor management.

## .NET 11 SDK and .NET 10 runtime

Hexalith.EventStore targets .NET 10. Install the .NET SDK pinned in `global.json` (11.0.100, with `latestPatch` roll-forward) to build the project. Also install the .NET 10 ASP.NET Core runtime to run its applications and tests; the .NET 11 SDK alone does not supply that runtime.

**Install:** Use the official installation guidance for your platform:

- [Windows (.NET 11 SDK)](https://learn.microsoft.com/en-us/dotnet/core/install/windows?tabs=net11)
- [macOS (.NET 11 SDK)](https://learn.microsoft.com/en-us/dotnet/core/install/macos?tabs=net11)
- [Linux (.NET 11 SDK)](https://learn.microsoft.com/en-us/dotnet/core/install/linux?tabs=net11)

**Verify installation:**

```bash
$ dotnet --version
```

Expected output: `11.0.100` or a later patch in the same feature band.

Install the [.NET 10 ASP.NET Core runtime](https://dotnet.microsoft.com/download/dotnet/10.0), then verify it separately:

```bash
$ dotnet --list-runtimes
```

The output must include both `Microsoft.NETCore.App 10.x` and `Microsoft.AspNetCore.App 10.x`.

## .NET Aspire CLI

The quickstart uses the Aspire CLI to launch the full application topology. Install the Aspire CLI as a global .NET tool:

```bash
$ dotnet tool install -g Aspire.Cli
```

**Verify installation:**

```bash
$ aspire --version
```

Expected output: a version string (e.g., `9.2.0` or later).

## Docker Desktop

DAPR uses Docker containers for local development infrastructure — a state store (Redis), a pub/sub broker, and a placement service for actors. Docker Desktop provides the container runtime.

**Install:** Use the official Docker installation page for your platform:

- [Windows](https://docs.docker.com/desktop/setup/install/windows-install/)
- [macOS](https://docs.docker.com/desktop/setup/install/mac-install/)
- [Linux](https://docs.docker.com/desktop/setup/install/linux/)

> **Note:** On Windows, Docker Desktop requires WSL 2. If you haven't enabled it, follow the [WSL 2 installation guide](https://learn.microsoft.com/en-us/windows/wsl/install) first.

**Verify installation:**

```bash
$ docker --version
```

Expected output starts with `Docker version`.

**Verify Docker is running:**

```bash
$ docker info
```

If this returns server information without errors, Docker is running correctly.

## DAPR CLI

DAPR provides the infrastructure abstraction layer — it handles state storage, message delivery, and actor management so your domain code never touches infrastructure directly. You need the DAPR CLI (version 1.16.x or later) to initialize and manage the local DAPR environment.

**Install:** Use the official DAPR CLI installation guidance for your platform:

- [Windows](https://docs.dapr.io/getting-started/install-dapr-cli/#install-using-winget)
- [macOS](https://docs.dapr.io/getting-started/install-dapr-cli/#install-from-homebrew)
- [Linux](https://docs.dapr.io/getting-started/install-dapr-cli/#install-from-terminal)

Or use one of these platform-specific commands:

### Windows (PowerShell)

```bash
$ powershell -Command "iwr -useb https://raw.githubusercontent.com/dapr/cli/master/install/install.ps1 | iex"
```

Or via winget:

```bash
$ winget install Dapr.CLI
```

### macOS

```bash
$ brew install dapr/tap/dapr-cli
```

### Linux

```bash
$ wget -q https://raw.githubusercontent.com/dapr/cli/master/install/install.sh -O - | /bin/bash
```

### Verify DAPR CLI installation

```bash
$ dapr --version
```

Expected output: `CLI version: 1.16.x` or later.

### Initialize DAPR

After installing the CLI, initialize the local DAPR environment. Make sure Docker is running first.

```bash
$ dapr init
```

This pulls and starts three Docker containers:

- `dapr_placement` — actor placement service
- `dapr_redis` — default state store and pub/sub for local development
- `dapr_zipkin` — distributed tracing

**Verify DAPR is initialized:**

```bash
$ dapr --version
```

Expected output shows both CLI and runtime versions:

```bash
CLI version: 1.16.x
Runtime version: 1.16.x
```

You can also verify the containers are running:

```bash
$ docker ps --filter "name=dapr_"
```

You should see `dapr_placement`, `dapr_redis`, and `dapr_zipkin` listed as running.

## Verify Your Environment

Run these commands to confirm everything is set up correctly:

```bash
$ dotnet --version    # 11.0.100 or later patch in the same feature band
$ aspire --version    # Version string (e.g., 9.2.0)
$ docker --version    # Output starts with "Docker version"
$ docker info         # Returns Docker server information (daemon running)
$ dapr --version      # CLI version 1.16.x, Runtime version 1.16.x
$ docker ps --filter "name=dapr_"  # Three dapr containers running
```

If all six commands produce the expected output, your environment is ready for the quickstart.

## Common Issues

### Docker daemon not running

If `docker info` returns an error, start Docker Desktop from your applications menu and wait for it to fully initialize before proceeding.

### DAPR init fails

`dapr init` requires Docker to be running. If it fails, start Docker Desktop first, then retry `dapr init`.

### .NET SDK version mismatch

If `dotnet --version` fails to resolve the SDK or shows a version earlier than `11.0.100`, install SDK `11.0.100` or a later compatible patch in the `11.0.1xx` feature band from the [.NET 11 download page](https://dotnet.microsoft.com/en-us/download/dotnet/11.0). Multiple SDK versions can coexist on the same machine.

### Windows: WSL 2 not enabled

Docker Desktop on Windows requires WSL 2. If Docker fails to start, run `wsl --install` in an elevated PowerShell prompt, restart your machine, and try again. See the [WSL 2 installation guide](https://learn.microsoft.com/en-us/windows/wsl/install) for details.

## Next Steps

- **Next:** [Quickstart Guide](quickstart.md) — clone the repo and run the sample in under 10 minutes
- **Related:** [README](../../README.md), [Choose the Right Tool](../concepts/choose-the-right-tool.md)
