# Copilot instructions

This repository is set up to use Aspire. Aspire is an orchestrator for the entire application and will take care of configuring dependencies, building, and running the application. The resources that make up the application are defined in `apphost.cs` including application code and external dependencies.

## General recommendations for working with Aspire
1. Before making any changes always run the apphost using `aspire run` and inspect the state of resources to make sure you are building from a known state.
1. Changes to the _apphost.cs_ file will require a restart of the application to take effect.
2. Make changes incrementally and run the aspire application using the `aspire run` command to validate changes.
3. Use the Aspire MCP tools to check the status of resources and debug issues.

## Running the application
To run the application run the following command:

```
aspire run
```

If there is already an instance of the application running it will prompt to stop the existing instance. You only need to restart the application if code in `apphost.cs` is changed, but if you experience problems it can be useful to reset everything to the starting state.

## Checking resources
To check the status of resources defined in the app model use the _list resources_ tool. This will show you the current state of each resource and if there are any issues. If a resource is not running as expected you can use the _execute resource command_ tool to restart it or perform other actions.

## Listing integrations
IMPORTANT! When a user asks you to add a resource to the app model you should first use the _list integrations_ tool to get a list of the current versions of all the available integrations. You should try to use the version of the integration which aligns with the version of the Aspire.AppHost.Sdk. Some integration versions may have a preview suffix. Once you have identified the correct integration you should always use the _get integration docs_ tool to fetch the latest documentation for the integration and follow the links to get additional guidance.

## Debugging issues
IMPORTANT! Aspire is designed to capture rich logs and telemetry for all resources defined in the app model. Use the following diagnostic tools when debugging issues with the application before making changes to make sure you are focusing on the right things.

1. _list structured logs_; use this tool to get details about structured logs.
2. _list console logs_; use this tool to get details about console logs.
3. _list traces_; use this tool to get details about traces.
4. _list trace structured logs_; use this tool to get logs related to a trace

## Other Aspire MCP tools

1. _select apphost_; use this tool if working with multiple app hosts within a workspace.
2. _list apphosts_; use this tool to get details about active app hosts.

## Playwright MCP server

The playwright MCP server has also been configured in this repository and you should use it to perform functional investigations of the resources defined in the app model as you work on the codebase. To get endpoints that can be used for navigation using the playwright MCP server use the list resources tool.

## Updating the app host
The user may request that you update the Aspire apphost. You can do this using the `aspire update` command. This will update the apphost to the latest version and some of the Aspire specific packages in referenced projects, however you may need to manually update other packages in the solution to ensure compatibility. You can consider using the `dotnet-outdated` with the users consent. To install the `dotnet-outdated` tool use the following command:

```
dotnet tool install --global dotnet-outdated-tool
```

## Persistent containers
IMPORTANT! Consider avoiding persistent containers early during development to avoid creating state management issues when restarting the app.

## Aspire workload
IMPORTANT! The aspire workload is obsolete. You should never attempt to install or use the Aspire workload.

## Official documentation
IMPORTANT! Always prefer official documentation when available. The following sites contain the official documentation for Aspire and related components

1. https://aspire.dev
2. https://learn.microsoft.com/dotnet/aspire
3. https://nuget.org (for specific integration package details)

## Cursor Cloud specific instructions

### System dependencies

The VM environment provides: .NET 10 SDK, Docker, Aspire CLI (`aspire`), Dapr CLI (`dapr`), and Dapr runtime (`daprd`, `placement`, `scheduler`) under `$HOME/.dapr/bin`. The update script handles `dotnet restore` automatically.

### Running the application

1. Ensure Docker is running: `sudo dockerd &>/tmp/dockerd.log &` then `sudo chmod 666 /var/run/docker.sock`.
2. Start Dapr placement and scheduler services manually (slim mode does not auto-start them):
   ```
   $HOME/.dapr/bin/placement --port 50005 &
   $HOME/.dapr/bin/scheduler --port 50006 --etcd-data-dir /tmp/dapr-scheduler-data &
   ```
3. Run with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` to skip the Keycloak container (auth falls back to symmetric key JWT).
4. The CommandAPI listens on `http://localhost:8080`. The Aspire dashboard is at `https://localhost:17017`.

### Testing

- **Unit tests**: `dotnet test tests/Hexalith.EventStore.Client.Tests tests/Hexalith.EventStore.Contracts.Tests tests/Hexalith.EventStore.Sample.Tests tests/Hexalith.EventStore.Testing.Tests` (run individually, not in a single `dotnet test` call).
- **Pre-existing build failure**: `Hexalith.EventStore.Server.Tests` does not build due to CA2007 warnings treated as errors (pre-existing in the repo).
- **Integration tests** (`tests/Hexalith.EventStore.IntegrationTests`) require Docker and a running Aspire environment.

### API authentication (dev mode)

With `EnableKeycloak=false`, JWT tokens are validated against the symmetric key `DevOnlySigningKey-AtLeast32Chars!` (configured in `appsettings.Development.json`). Tokens must include `iss=hexalith-dev`, `aud=hexalith-eventstore`, and claims `tenants` (JSON array) and `permissions` (e.g. `["commands:*"]`) for authorization.

### Known gotchas

- DAPR slim mode does not start placement/scheduler automatically — you must start them before `aspire run` or actors will fail with "did not find address for actor".
- DAPR access control policies (in `DaprComponents/accesscontrol.yaml`) enforce deny-by-default. In slim mode without mTLS, service-to-service invocations from eventstore to domain services (e.g. sample) may be rejected with 403. This does not affect unit tests or actor processing.
- The HTTPS dev certificate cannot be fully trusted in the cloud VM — `aspire run` will warn but continues to work. Use `http://localhost:8080` for API calls.
- Run test projects individually (not via a solution-level `dotnet test`) or use the `.slnx` solution file.