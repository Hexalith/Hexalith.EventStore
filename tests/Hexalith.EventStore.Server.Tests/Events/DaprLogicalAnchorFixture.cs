using Dapr.Actors;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Uses the real aggregate actor save boundary with an independently serialized durable pair and actual completed operation.</summary>
internal sealed class DaprLogicalAnchorFixture : IAsyncDisposable
{
    private readonly Dictionary<string, byte[]> _staged = [];
    /// <summary>Creates an actual aggregate actor over the same addressed manager used by the source.</summary>
    internal DaprLogicalAnchorFixture()
    {
        Snapshot = new DaprLogicalSnapshotFixture();
        Actor = new AggregateActor(ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new ActorId(DaprLogicalReplayFixture.Identity.ActorId) }), Substitute.For<ILogger<AggregateActor>>(), Substitute.For<IDomainServiceInvoker>(), Substitute.For<ISnapshotManager>(), new NoOpEventPayloadProtectionService(), Substitute.For<ICommandStatusStore>(), Substitute.For<IEventPublisher>(), Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), Substitute.For<IDeadLetterPublisher>());
        ActorStateManagerTestHelper.SetStateManager(Actor, Snapshot.Replay.Source.SourceState);
        Snapshot.OnCacheClear = token =>
        {
            _staged.Clear();
            return CacheClear?.Invoke(token) ?? Task.CompletedTask;
        };
        _ = Snapshot.Replay.Source.SourceState.SetStateAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Stages++;
            string key = call.ArgAt<string>(0);
            byte[] value = call.ArgAt<byte[]>(1);
            SdkAliases.Add(value);
            _staged[key] = value;
            if (OnStage is not null)
            {
                await OnStage(key, value, call.Arg<CancellationToken>());
            }
        });
        _ = Snapshot.Replay.Source.SourceState.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Saves++;
            call.Arg<CancellationToken>().ThrowIfCancellationRequested();
            if (SaveMode != "no-commit")
            {
                foreach ((string key, byte[] value)in _staged)
                {
                    if (SaveMode != "mixed" || key == Snapshot.Owner.StorageKey)
                    {
                        Snapshot.Durable[key] = value.ToArray();
                    }
                }
            }

            _staged.Clear();
            if (OnSave is not null)
            {
                await OnSave(call.Arg<CancellationToken>());
            }

            if (SaveMode is "commit-throw" or "no-commit" or "mixed")
            {
                throw new IOException("acknowledgement unavailable");
            }
        });
    }

    /// <summary>Gets the actual origin/read-only candidate fixture.</summary>
    internal DaprLogicalSnapshotFixture Snapshot { get; }
    /// <summary>Gets the actual aggregate actor that alone calls SaveStateAsync.</summary>
    internal AggregateActor Actor { get; }
    /// <summary>Gets saved durable boundaries.</summary>
    internal int Saves { get; private set; }
    /// <summary>Gets individual actor SDK stage callbacks.</summary>
    internal int Stages { get; private set; }
    /// <summary>Gets or sets normal, commit-throw, no-commit or mixed save behavior.</summary>
    internal string SaveMode { get; set; } = "normal";
    /// <summary>Gets the SDK-retained exact private staging aliases.</summary>
    internal List<byte[]> SdkAliases { get; } = [];
    /// <summary>Gets or sets an actual asynchronous SDK staging callback.</summary>
    internal Func<string, byte[], CancellationToken, Task>? OnStage { get; set; }
    /// <summary>Gets or sets an actual asynchronous save acknowledgement callback.</summary>
    internal Func<CancellationToken, Task>? OnSave { get; set; }
    /// <summary>Gets or sets an actual asynchronous cache-release callback after staging references are dropped.</summary>
    internal Func<CancellationToken, Task>? CacheClear { get; set; }

    /// <summary>Completes actual replay history before preparing an absent or already matching persisted pair.</summary>
    internal async Task PrepareAsync(bool retainPair = false)
    {
        await Snapshot.PrepareAsync();
        if (!retainPair)
        {
            Snapshot.Durable.Clear();
        }
    }

    /// <summary>Invokes the dormant actual-actor issuer with its exact source, origin, model, budget and local serialized fence.</summary>
    internal Task<DaprReplayCommitOutcome> IssueAsync(EventBufferBudget budget, CancellationToken token = default, int maximumStateBytes = 32) => Actor.IssueLogicalSnapshotAsync(DaprLogicalSnapshotCodec.SnapshotModel, Snapshot.OriginBinding, Snapshot.Replay.Source.Source, Snapshot.Replay.Source.Trust, Snapshot.Replay.Binding, maximumStateBytes, Snapshot.AcquireOriginAsync, Snapshot.SerializeOwnerAsync, budget, token);
    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Snapshot.DisposeAsync();
}
