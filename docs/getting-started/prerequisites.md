[← Back to Hexalith.EventStore](../../README.md)

# Prerequisites

Before you start the quickstart, you need three tools installed: the .NET 10 SDK to build and run the application, Docker Desktop to provide local infrastructure containers, and the DAPR CLI to manage the infrastructure abstraction layer that Hexalith.EventStore uses for state storage, messaging, and actor management.

## .NET 10 SDK

Hexalith.EventStore targets .NET 10. You need the .NET 10 SDK (version 10.0.103 or later) to build and run the project.

**Install:** Use the official installation guidance for your platform:

- [Windows (.NET 10)](https://learn.microsoft.com/en-us/dotnet/core/install/windows?tabs=net10)
- [macOS (.NET 10)](https://learn.microsoft.com/en-us/dotnet/core/install/macos?tabs=net10)
- [Linux (.NET 10)](https://learn.microsoft.com/en-us/dotnet/core/install/linux?tabs=net10)

**Verify installation:**

```bash
$ dotnet --version
```

Expected output: `10.0.103` or later (any 10.0.x patch version works).

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
$ dotnet --version    # 10.0.103 or later
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

If `dotnet --version` shows a version earlier than 10.0.103, download the latest .NET 10 SDK from the [.NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Multiple SDK versions can coexist on the same machine.

### Windows: WSL 2 not enabled

Docker Desktop on Windows requires WSL 2. If Docker fails to start, run `wsl --install` in an elevated PowerShell prompt, restart your machine, and try again. See the [WSL 2 installation guide](https://learn.microsoft.com/en-us/windows/wsl/install) for details.

## Next Steps

- **Next:** [Quickstart Guide](quickstart.md) — clone the repo and run the sample in under 10 minutes
- **Related:** [README](../../README.md), [Choose the Right Tool](../concepts/choose-the-right-tool.md)
