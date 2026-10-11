using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Owns a disposable two-host, two-sidecar Dapr Redis Streams candidate profile.
/// The component files are test-only and do not change the shipped AppHost or deployment profile.
/// </summary>
public sealed class SignalRDaprDistributionFixture : IAsyncDisposable
{
    private readonly string _componentsPath;

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
        string? aggregateActorTypeName = null)
    {
        ArgumentNullException.ThrowIfNull(topics);
        if (topics.Count == 0)
        {
            throw new ArgumentException("At least one exact projection topic is required.", nameof(topics));
        }

        string root = FindRepositoryRoot();
        var fixture = new SignalRDaprDistributionFixture(root);
        try
        {
            await fixture.WriteComponentsAsync(topics, cancellationToken).ConfigureAwait(false);
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

    private async Task WriteComponentsAsync(IReadOnlyList<string> topics, CancellationToken cancellationToken)
    {
        const string state = """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: statestore
            spec:
              type: state.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "127.0.0.1:6379"
                - name: redisPassword
                  value: ""
                - name: actorStateStore
                  value: "true"
                - name: keyPrefix
                  value: "none"
            scopes:
              - eventstore
            """;
        const string pubsub = """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "127.0.0.1:6379"
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
            string topic = topics[index];
            if (string.IsNullOrWhiteSpace(topic) || topic.Contains('"') || topic.Contains('\n'))
            {
                throw new ArgumentException("Topics must be nonempty YAML-safe exact names.", nameof(topics));
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
