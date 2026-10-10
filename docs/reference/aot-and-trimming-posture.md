[← Back to Hexalith.EventStore](../../README.md)

# Native AOT and IL Trimming Posture

This reference explains the release package compatibility posture for maintainers and .NET consumers, and identifies the runtime conventions that currently depend on reflection.

## Current Posture

**Posture:** Native AOT and IL trimming are not targets for Hexalith.EventStore release packages.

The platform discovers domain types and methods at runtime, invokes them through reflection, and deserializes payloads using reflection-based System.Text.Json metadata. Trimming can remove members these conventions need, and Native AOT cannot assume their dynamic behavior is available. A successful build or publish with either feature enabled does not establish that an EventStore application will work correctly.

## Reflection Convention Inventory

| Convention | Where | Reflection or dynamic behavior used |
| --- | --- | --- |
| Event `Apply` methods and snapshots | Client `Aggregates/ApplyMethodResolver.cs`, `AggregateReplayer.cs`, `EventStoreProjection.cs`, `Handlers/DomainProcessorStateRehydrator.cs`; Testing `Compliance/TerminatableComplianceAssertions.cs` | Find public `Apply` methods with `GetMethods` or `GetMethod`, invoke them through `MethodInfo`, deserialize payloads by runtime `Type`, and inspect snapshot properties with `GetProperties`. |
| Command `Handle` methods | Client `Aggregates/EventStoreAggregate.cs` | Discover handlers with `GetMethods`, read a command contract's static `CommandType` property, invoke the selected method, and inspect asynchronous results through reflection. |
| Domain assembly and naming conventions | Client `Discovery/AssemblyScanner.cs`, `Registration/EventStoreServiceCollectionExtensions.cs`, `Conventions/NamingConventionEngine.cs` | Scan assemblies and attributes, use `Assembly.GetCallingAssembly`, and construct discovered types with `ActivatorUtilities.CreateInstance`. |
| Event upcasters | Client `Registration/EventPayloadEvolutionRegistration.cs`, `Events/EventPayloadEvolutionRegistry.cs`, `Events/RegisteredEventUpcaster.cs` | Discover upcaster types with `GetTypes`, instantiate them with `Activator`, and read `Assembly.Location` to bind an implementation file. |
| Domain event subscriptions | Client `Subscriptions/EventStoreDomainEventProcessor.cs`, `Registration/EventStoreDomainEventsServiceCollectionExtensions.cs`, `Registration/EventStoreHostExtensions.cs` | Scan subscriber types, construct domain instances, close generic dispatch methods with `MakeGenericMethod`, and invoke them dynamically. |
| Domain-service handlers and admin metadata | DomainService `EventStoreDomainServiceExtensions.cs`, `AdminOperationalIndexMetadata.cs` | Scan assemblies for handler implementations and inspect methods and types to report operational metadata. |
| JSON payload binding | Contracts `Serialization/EventStorePayloadSerialization.cs`, `Security/RecoverableAnchoredState.cs`; Client replay, dispatch, and projection paths; Server `Security/RetainedIdentityHistorySourceReader.cs`; ServiceDefaults `Extensions.cs`; Testing `Fakes/FakeEventPersister.cs`; Testing.Integration `Benchmarking/BenchmarkDatasetBuilder.cs`; RestApi.Generators `RestApiControllerEmitter.cs`; Admin.Cli `Client/AdminApiClient.cs`, `Profiles/ProfileManager.cs`, `Formatting/JsonOutputFormatter.cs`, and `JsonDefaults.cs` | Use reflection-mode System.Text.Json for generic state, health data by runtime `Type`, test events and benchmark state by runtime `Type`, generated command and query payloads, and CLI requests, responses, profiles, and output; no `JsonSerializerContext` supplies a complete closed payload graph. |
| Platform hosting and output | Server actor interfaces and `Hexalith.EventStore.Server.csproj`; Gateway controllers compiled from `src/Hexalith.EventStore/Controllers` and `Filters/ValidateModelFilter.cs`; Admin.Server MVC controllers and `Services/DaprConsistencyCommandService.cs`, `DaprStorageCommandService.cs`, `DaprProjectionCommandService.cs`, `DaprBackupCommandService.cs`, `DaprDeadLetterCommandService.cs`; generated REST controllers; Admin.Cli formatters | Dapr actor remoting emits dynamic proxies, MVC discovers controllers, the gateway constructs generic validators and reads tenant properties, admin services inspect result and exception status properties at runtime, and CLI table and CSV formatters inspect output properties with `GetProperties`. |
| Governance evidence validation | Server `Security/GovernanceScopeGuardOwner.cs` | Recursively inspect evidence object properties with `GetProperties` and `GetValue` while validating strings. |

The release package sources have no `RequiresUnreferencedCode`, `RequiresDynamicCode`, or `DynamicallyAccessedMembers` annotations that would make these conventions a supported trimming contract. `EventStorePayloadSerialization` explicitly retains the reflection serializer resolver.

## Consumer Guidance

Use the release packages with the normal managed .NET runtime and without trimming. Do not treat a consumer application's `PublishAot`, `PublishTrimmed`, or analyzer settings as evidence that these packages support either mode. Keep reflection-dependent domain types, handlers, upcasters, and payload contracts available to the runtime; a consumer-specific workaround does not change the package posture.

## Release Guard

Story 6.7 owns `AotTrimmingPostureTests` in the blocking `contracts` CI job. The guard reads `tools/release-packages.json` and evaluates each listed project through MSBuild in Release package-reference mode. It fails if effective `IsAotCompatible` or `IsTrimmable` is `true` (case-insensitively), or if this page loses its posture marker. Seeded tests exercise explicit properties and the SDK's implied `IsTrimmable` value.

Maintainers can run the focused guard after building the Contracts.Tests project:

```bash
$ dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false
$ dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.AotTrimmingPostureTests
```

Before changing this posture, replace or explicitly preserve each reflection convention with a verified AOT and trimming design, including payload metadata, discovery and dispatch, Dapr actor remoting, MVC and generated controllers, and CLI formatting. Then establish compatible package and application publish evidence, review the resulting contract with the owner, and update this page and the guard together. The current guard does not claim that setting either MSBuild property is sufficient proof of compatibility.

## Next Steps

- **Next:** [NuGet Packages Guide](nuget-packages.md) — choose packages from the release inventory.
- **Related:** [Architecture Overview](../concepts/architecture-overview.md), [Command API](command-api.md)
