using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.DurableTasks;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Provides an authoritative synthetic owner on the fixture's persistent store.</summary>
/// <param name="client">The owner's sidecar connection.</param>
/// <param name="component">The fixture store component.</param>
/// <param name="task">The exact task authorized by this fixture owner.</param>
/// <param name="withholdReply">Whether to keep the caller waiting after commit.</param>
internal sealed class DurableTaskFixtureOwner(
    DaprClient client, string component, CommittedDurableTaskAdmission task, bool withholdReply = false) : IDurableTaskStepOwner
{
    private readonly DaprReadModelStore _store = new(client);

    /// <summary>Gets the owner calls from this worker process in order.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Returns the owner's distinct storage key for a step.</summary>
    public static string RecordKey(DurableTaskStep step) => "fixture-owner-v1-" + step.Key;

    /// <inheritdoc/>
    public async Task<DurableTaskOwnerReceipt?> LookupAsync(DurableTaskStep step, CancellationToken cancellationToken)
    {
        Calls.Add("lookup");
        step.Task.ShouldBe(task);
        DurableTaskFixtureOwnerState? state = await ReadAsync(RecordKey(step), cancellationToken);
        return state is null ? null : state.Receipt.ShouldNotBeNull();
    }

    /// <inheritdoc/>
    public async Task<DurableTaskOwnerReceipt> CommitAsync(DurableTaskStep step, CancellationToken cancellationToken)
    {
        Calls.Add("submit");
        step.Task.ShouldBe(task);
        var receipt = new DurableTaskOwnerReceipt(step, DurableTaskAdmission.Hash(JsonSerializer.SerializeToUtf8Bytes(step)));
        var state = new DurableTaskFixtureOwnerState([step], receipt);
        string key = RecordKey(step);
        if (!await _store.TrySaveAsync(component, key, state, string.Empty, cancellationToken))
        {
            DurableTaskOwnerReceipt existing = (await ReadAsync(key, cancellationToken)).ShouldNotBeNull().Receipt;
            existing.Step.ShouldBe(step);
            return existing;
        }

        if (withholdReply)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        return receipt;
    }

    private async Task<DurableTaskFixtureOwnerState?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        (DurableTaskFixtureOwnerState? state, string etag) = await client.GetStateAndETagAsync<DurableTaskFixtureOwnerState?>(
            component, key, ConsistencyMode.Strong, cancellationToken: cancellationToken);
        if (state is null && string.IsNullOrEmpty(etag))
        {
            return null;
        }

        etag.ShouldNotBeNullOrWhiteSpace();
        return state.ShouldNotBeNull();
    }
}
