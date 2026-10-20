using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Owns a disposable two-host, two-sidecar Dapr Redis Streams candidate profile.
/// The component files are test-only and do not change the shipped AppHost or deployment profile.
/// </summary>
public sealed class SignalRDaprDistributionFixture : IAsyncDisposable
{
    private readonly string _componentsPath;
    private int _subscriptionCount;

    private SignalRDaprDistributionFixture(string repositoryRoot)
    {
        RepositoryRoot = repositoryRoot;
        _componentsPath = Path.Combine(Path.GetTempPath(), $"eventstore-2-13-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_componentsPath);
        First = new SignalRDaprDistributionNode("first");
        Second = new SignalRDaprDistributionNode("second");
    }

    /// <summary>Gets the repository root containing the product host binary.</summary>
    public string RepositoryRoot { get; }

    /// <summary>Gets the first product host and sidecar.</summary>
    public SignalRDaprDistributionNode First { get; }

    /// <summary>Gets the second product host and sidecar.</summary>
    public SignalRDaprDistributionNode Second { get; }

    /// <summary>Gets the SHA-256 of the actual test-only pub/sub component bytes.</summary>
    public string PubSubComponentSha256 => HashFile(Path.Combine(_componentsPath, "pubsub.yaml"));

    /// <summary>Gets the SHA-256 of the actual test-only state component bytes.</summary>
    public string StateComponentSha256 => HashFile(Path.Combine(_componentsPath, "statestore.yaml"));

    /// <summary>Gets the SHA-256 of a generated exact-topic subscription's bytes.</summary>
    public string SubscriptionSha256(int index)
        => HashFile(Path.Combine(_componentsPath, $"subscription-{index}.yaml"));

    /// <summary>Starts both real host/sidecar pairs with the same app identity.</summary>
    public static async Task<SignalRDaprDistributionFixture> StartAsync(
        IReadOnlyList<string> topics,
        CancellationToken cancellationToken,
        bool enablePubSubProjectionTransport = false,
        string? aggregateActorTypeName = null,
        int redisPort = 6379)
    {
        ArgumentNullException.ThrowIfNull(topics);
        if (topics.Count == 0)
        {
            throw new ArgumentException("At least one exact projection topic is required.", nameof(topics));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(redisPort, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(redisPort, 65_535);

        string root = FindRepositoryRoot();
        var fixture = new SignalRDaprDistributionFixture(root);
        try
        {
            await fixture.WriteComponentsAsync(topics, redisPort, cancellationToken).ConfigureAwait(false);
            await fixture.First.StartAsync(
                root, fixture._componentsPath, cancellationToken,
                enablePubSubProjectionTransport, aggregateActorTypeName).ConfigureAwait(false);
            await fixture.Second.StartAsync(
                root, fixture._componentsPath, cancellationToken,
                enablePubSubProjectionTransport, aggregateActorTypeName).ConfigureAwait(false);
            if (fixture.First.ApplicationProcessId == fixture.Second.ApplicationProcessId
                || fixture.First.SidecarProcessId == fixture.Second.SidecarProcessId)
            {
                throw new InvalidOperationException("Qualification requires two independent host and sidecar processes.");
            }

            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Restarts the second sidecar after a bounded outage.</summary>
    public Task RestartSecondSidecarAsync(CancellationToken cancellationToken)
        => Second.StartSidecarAsync(_componentsPath, cancellationToken);

    /// <summary>Adds an exact topic to the disposable catalog; sidecars must reload it before delivery is claimed.</summary>
    public async Task<int> AddExactTopicAsync(string topic, CancellationToken cancellationToken)
    {
        int index = _subscriptionCount;
        await WriteSubscriptionAsync(topic, index, cancellationToken).ConfigureAwait(false);
        _subscriptionCount++;
        return index;
    }

    /// <summary>Reloads both fixture sidecars while leaving product hosts and their SignalR connections running.</summary>
    public async Task ReloadSidecarsAsync(CancellationToken cancellationToken)
    {
        await Second.StopSidecarAsync().ConfigureAwait(false);
        await First.StopSidecarAsync().ConfigureAwait(false);
        await First.StartSidecarAsync(_componentsPath, cancellationToken).ConfigureAwait(false);
        await Second.StartSidecarAsync(_componentsPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Restarts the second complete replica with its original application and component configuration.</summary>
    public async Task RestartSecondAsync(CancellationToken cancellationToken)
    {
        await Second.RestartApplicationAsync(cancellationToken).ConfigureAwait(false);
        await Second.StartSidecarAsync(_componentsPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Retains exact component bytes, redacted process logs, and source/binary identities when requested.</summary>
    public async Task CaptureEvidenceAsync(
        string scenario,
        DateTimeOffset startedUtc,
        object observations,
        CancellationToken cancellationToken)
    {
        string? destination = Environment.GetEnvironmentVariable("HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        string directory = Path.Combine(
            Path.GetFullPath(destination, RepositoryRoot),
            $"{scenario}-{startedUtc:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (string component in Directory.GetFiles(_componentsPath, "*.yaml").Order(StringComparer.Ordinal))
        {
            File.Copy(component, Path.Combine(directory, Path.GetFileName(component)), overwrite: false);
        }

        await File.WriteAllTextAsync(Path.Combine(directory, "first.log"), First.SnapshotRedactedLogs(), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(directory, "second.log"), Second.SnapshotRedactedLogs(), cancellationToken).ConfigureAwait(false);
        foreach (SignalRDaprDistributionNode node in new[] { First, Second })
        {
            JsonElement metadata = await node.GetRuntimeMetadataAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(directory, $"{node.Name}-sidecar.json"),
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }) + "\n",
                cancellationToken).ConfigureAwait(false);
        }
        string[] sources = [
            "tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionFixture.cs",
            "tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionNode.cs",
            "tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprNetworkFaultProxy.cs",
            "tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionRuntimeProofTests.cs",
            "tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprProjectionDispatchProofTests.cs",
            "src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs",
            "src/Hexalith.EventStore/Authentication/ProjectionNotificationProvenanceVerifier.cs",
            "src/Hexalith.EventStore/Validation/ProjectionChangedNotificationValidator.cs",
            "src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs",
            "src/Hexalith.EventStore/SignalRHub/SignalROptions.cs",
            "src/Hexalith.EventStore/SignalRHub/ValidateSignalROptions.cs",
            "src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs",
            "src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs",
            "src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofIdentity.cs",
            "src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofBroadcastRequest.cs",
            "src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofBroadcastResult.cs",
            "src/Hexalith.EventStore/SignalRHub/SignalRProjectionChangedBroadcaster.cs",
            "src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs",
            "src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs",
            "src/Hexalith.EventStore.Server/Actors/ProjectionState.cs",
            "src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs",
            "src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.Notifications.cs",
            "src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.ReplayPublication.cs",
            "src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs",
            "src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.LegacyReplay.cs",
            "samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs",
            "tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionDispatchContractTests.cs",
            "tests/Hexalith.EventStore.Server.Tests/Actors/ProjectionStateTests.cs",
            "tests/Hexalith.EventStore.Server.Tests/Actors/LegacyProjectionState.cs",
            "tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.Notifications.cs",
            "tests/Hexalith.EventStore.Sample.Tests/Counter/Projections/CounterProjectionHandlerTests.cs",
            "src/Hexalith.EventStore.Contracts/Projections/ProjectionChangedMetadataLimits.cs",
            "src/Hexalith.EventStore.Server/Configuration/ProjectionChangeNotifierOptions.cs",
            "src/Hexalith.EventStore.Server/Configuration/ValidateProjectionChangeNotifierOptions.cs",
            "src/Hexalith.EventStore.Server/Configuration/ProjectionChangeTransport.cs",
            "src/Hexalith.EventStore/Hexalith.EventStore.csproj",
            "src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj",
            "src/Hexalith.EventStore.AppHost/Program.cs",
            "src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml",
            "deploy/dapr/subscription-projection-changed.yaml",
            "_bmad-output/implementation-artifacts/spec-2-13-dapr-notification-distribution-qualification.md",
            "_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md",
            "_bmad-output/planning-artifacts/architecture.md",
            "references/Hexalith.Builds/Props/Directory.Packages.props",
        ];
        string[] binaries = [
            "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.dll",
            "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.Gateway.dll",
            "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.deps.json",
            "tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests.dll",
        ];
        foreach (string source in sources)
        {
            string retained = Path.Combine(directory, "sources", source);
            Directory.CreateDirectory(Path.GetDirectoryName(retained)!);
            File.Copy(Path.Combine(RepositoryRoot, source), retained, overwrite: false);
        }

        var manifest = new
        {
            SchemaVersion = 1,
            Scenario = scenario,
            StartedUtc = startedUtc,
            CompletedUtc = DateTimeOffset.UtcNow,
            Result = "passed-scenario-assertions-only",
            Runtime = Environment.Version.ToString(),
            AppId = "eventstore",
            FirstHostProcessId = First.ApplicationProcessId,
            SecondHostProcessId = Second.ApplicationProcessId,
            FirstSidecarProcessId = First.SidecarProcessId,
            SecondSidecarProcessId = Second.SidecarProcessId,
            FirstHostPort = First.AppPort,
            SecondHostPort = Second.AppPort,
            Sources = sources.ToDictionary(path => path, path => HashFile(Path.Combine(RepositoryRoot, path)), StringComparer.Ordinal),
            Binaries = binaries.ToDictionary(path => path, path => HashFile(Path.Combine(RepositoryRoot, path)), StringComparer.Ordinal),
            Artifacts = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                .ToDictionary(path => Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/'), HashFile, StringComparer.Ordinal),
            Observations = observations,
        };
        await File.WriteAllTextAsync(
            Path.Combine(directory, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Second.DisposeAsync().ConfigureAwait(false);
        await First.DisposeAsync().ConfigureAwait(false);
        try
        {
            Directory.Delete(_componentsPath, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private async Task WriteComponentsAsync(IReadOnlyList<string> topics, int redisPort, CancellationToken cancellationToken)
    {
        string state = $"""
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: statestore
            spec:
              type: state.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "127.0.0.1:{redisPort}"
                - name: redisPassword
                  value: ""
                - name: actorStateStore
                  value: "true"
                - name: keyPrefix
                  value: "none"
            scopes:
              - eventstore
            """;
        string pubsub = $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "127.0.0.1:{{redisPort}}"
                - name: redisPassword
                  value: ""
                - name: consumerID
                  value: "{uuid}"
            scopes:
              - eventstore
            """;
        await File.WriteAllTextAsync(Path.Combine(_componentsPath, "statestore.yaml"), state + "\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(_componentsPath, "pubsub.yaml"), pubsub + "\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < topics.Count; index++)
        {
            await WriteSubscriptionAsync(topics[index], index, cancellationToken).ConfigureAwait(false);
        }
        _subscriptionCount = topics.Count;
    }

    private async Task WriteSubscriptionAsync(string topic, int index, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topic) || topic.Contains('"') || topic.Contains('\n') || topic.Contains('\r'))
        {
            throw new ArgumentException("Topics must be nonempty YAML-safe exact names.", nameof(topic));
        }

        string subscription = $"""
                apiVersion: dapr.io/v2alpha1
                kind: Subscription
                metadata:
                  name: projection-qualification-{index}
                spec:
                  pubsubname: pubsub
                  topic: "{topic}"
                  routes:
                    default: /projections/changed
                scopes:
                  - eventstore
                """;
        await File.WriteAllTextAsync(
            Path.Combine(_componentsPath, $"subscription-{index}.yaml"),
            subscription + "\n", Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("EventStore repository root was not found.");
    }

    private static string HashFile(string path)
        => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
