# Story 6.6 Dapr-only transition evidence — 2026-10-05

## Owner decision and source changes

The owner directed implementation to stay within the Dapr abstraction. The
[current amendment](../../story-6-6-dapr-only-amendment.md) replaces direct
PostgreSQL/provider-attestation requirements for Story 6.6. The earlier
approved AD-13 digest and feasibility observations remain historical records.

The previously committed `Server/Control` PostgreSQL transaction kernel,
its SQL schema, dedicated PostgreSQL test project and contract tests were
removed. The Server Npgsql reference and its test assembly visibility were
removed. The sole Npgsql central pin in the root-declared Builds submodule
was removed without changing other package versions. Existing V1 writes and
V2 admission fences remain in place. New V1 writes now carry an application
digest of the original logical payload and addressed metadata; publication
preserves it. No Dapr-only V2 path is claimed yet.

An unregistered `DaprLogicalEventReader` reads an addressed event through
`IActorStateManager`, unprotects its logical payload, checks the application
digest when present, and resolves allow-listed V1 aliases or V2 metadata
through one Client upcaster. It retains a private effective view. It has not
been wired into the existing replay, projection, subscription, reconstruction
or inspection paths. Older V1 events without a digest remain readable.

## Local verification

| Command | Result |
| --- | --- |
| `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --no-restore` | Failed before testing this change: `CS1704` for duplicate `Hexalith.Commons.UniqueIds` assemblies (`2.30.1` package and project-built `1.0.0`/`3.112.0`). This project-reference override is not the repository's normal package graph. |
| `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -m:1` | Passed, zero warnings/errors after restore; Server and Server.Tests compiled without Npgsql. |
| `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug --no-build --filter FullyQualifiedName~EventPersisterTests` | Passed 38/38, zero skips. |
| `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py` | Passed the historical Story 6.5 artifact; it reports approved digest `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`. It does not approve or verify the new amendment. |
| `git diff --check` and `git -C references/Hexalith.Builds diff --check` | Passed. |
| Search of `src`, `tests` and restored Server/Server.Tests assets for `Npgsql`/`PostgreSqlControl` | No matches. |
| Client resolver and upcaster focused test command below | Final local rerun: 17/17 passed, zero skips. |
| Server reader, persistence, publication, envelope and legacy-reader focused test command below | Final local rerun after null-omission change: 103/103 passed, zero skips. |
| `dotnet build src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj --configuration Release -p:UseHexalithProjectReferences=false -m:1 -warnaserror` | Final local rerun: passed, zero warnings/errors. |

Story 6.6 remains in progress. Actor-head and aggregate-route binding, consumer
integration, startup registry attestation, live Dapr same-save/lost-ack/cancellation
evidence, compatibility evidence and production activation gates remain open.
The digest is an application integrity check, not provider attestation.

The exact final focused commands were:

```sh
dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~EventLogicalViewResolverTests|FullyQualifiedName~EventUpcastChainExecutorTests'
dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~DaprLogicalEventReaderTests|FullyQualifiedName~EventPersisterTests|FullyQualifiedName~EventPublisherTests|FullyQualifiedName~EventEnvelopeTests|FullyQualifiedName~EventStreamReaderTests'
```
