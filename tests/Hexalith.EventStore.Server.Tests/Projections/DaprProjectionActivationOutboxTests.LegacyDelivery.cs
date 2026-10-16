using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class DaprProjectionActivationOutboxTests
{
    [Fact]
    public async Task FrozenLegacyIntent_PersistsAcrossRestartAndNewAppendWithoutCompletingNewerActivation()
    {
        var state = new Dictionary<string, ProjectionActivationLedger>(StringComparer.Ordinal);
        var dapr = CreateStatefulDaprClient(state);
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var outbox = new DaprProjectionActivationOutbox(dapr, Options.Create(new ProjectionOptions()), clock);
        var identity = new AggregateIdentity("tenant-a", "counter", "aggregate-a");
        await outbox.EnsureAsync(identity).ConfigureAwait(true);
        ProjectionActivationWorkItem original = (await outbox.GetAsync(identity).ConfigureAwait(true)).ShouldNotBeNull();
        var intent = new ProjectionLegacyDeliveryIntent("01ARZ3NDEKTSV4RRFFQ69G5FAV", "counter-app", "v1", 257,
            "01ARZ3NDEKTSV4RRFFQ69G5FAW");
        ProjectionActivationWorkItem frozen = (await ((IProjectionLegacyDeliveryOutbox)outbox)
            .TryBindLegacyDeliveryAsync(original, intent, CancellationToken.None).ConfigureAwait(true)).ShouldNotBeNull();
        await outbox.EnsureAsync(identity).ConfigureAwait(true);

        var restarted = new DaprProjectionActivationOutbox(dapr, Options.Create(new ProjectionOptions()), clock);
        ProjectionActivationWorkItem newer = (await restarted.GetAsync(identity).ConfigureAwait(true)).ShouldNotBeNull();
        newer.LegacyDelivery.ShouldBe(intent);
        newer.Revision.ShouldBe(original.Revision + 1);
        await restarted.CompleteAsync(newer).ConfigureAwait(true);
        (await restarted.GetAsync(identity).ConfigureAwait(true)).ShouldBe(newer);
        (await ((IProjectionLegacyDeliveryOutbox)restarted).TryCompleteLegacyDeliveryAsync(frozen,
            CancellationToken.None).ConfigureAwait(true)).ShouldBeTrue();
        await restarted.CompleteAsync(original).ConfigureAwait(true);
        ProjectionActivationWorkItem remaining = (await restarted.GetAsync(identity).ConfigureAwait(true)).ShouldNotBeNull();
        remaining.LegacyDelivery.ShouldBeNull();
        remaining.Revision.ShouldBe(newer.Revision);
    }

    [Fact]
    public async Task FrozenLegacyIntent_RejectsRetargetingAndAlreadyBoundCandidateChanges()
    {
        var state = new Dictionary<string, ProjectionActivationLedger>(StringComparer.Ordinal);
        var outbox = new DaprProjectionActivationOutbox(CreateStatefulDaprClient(state), Options.Create(new ProjectionOptions()),
            new FakeTimeProvider(DateTimeOffset.UtcNow));
        var identity = new AggregateIdentity("tenant-a", "counter", "aggregate-a");
        await outbox.EnsureAsync(identity).ConfigureAwait(true);
        ProjectionActivationWorkItem original = (await outbox.GetAsync(identity).ConfigureAwait(true)).ShouldNotBeNull();
        IProjectionLegacyDeliveryOutbox delivery = outbox;
        var intent = new ProjectionLegacyDeliveryIntent("01ARZ3NDEKTSV4RRFFQ69G5FAV", "counter-app", "v1", 256,
            "01ARZ3NDEKTSV4RRFFQ69G5FAW") { ProjectionType = "counter", SourceFingerprint = "source", CandidateFingerprint = "candidate" };
        ProjectionActivationWorkItem bound = (await delivery.TryBindLegacyDeliveryAsync(original, intent,
            CancellationToken.None).ConfigureAwait(true)).ShouldNotBeNull();
        foreach (ProjectionLegacyDeliveryIntent changed in new[] { intent with { HeadSequence = 257 },
            intent with { AppId = "other-app" }, intent with { HeadMessageId = "other-message" },
            intent with { ProjectionType = "other" }, intent with { CandidateFingerprint = "changed" },
            intent with { SourceFingerprint = "changed" } })
        {
            (await delivery.TryBindLegacyDeliveryAsync(bound, changed, CancellationToken.None).ConfigureAwait(true)).ShouldBeNull();
        }
        // An old pre-freeze read must not clear the original work via ordinary activation completion.
        await outbox.CompleteAsync(original).ConfigureAwait(true);
        (await outbox.GetAsync(identity).ConfigureAwait(true)).ShouldBe(bound);
    }

    [Fact]
    public async Task FrozenLegacyIntent_RejectsOversizedReferencesWithoutPersistingThem()
    {
        var state = new Dictionary<string, ProjectionActivationLedger>(StringComparer.Ordinal);
        var outbox = new DaprProjectionActivationOutbox(CreateStatefulDaprClient(state), Options.Create(new ProjectionOptions()),
            new FakeTimeProvider(DateTimeOffset.UtcNow));
        var identity = new AggregateIdentity("tenant-a", "counter", "aggregate-a");
        await outbox.EnsureAsync(identity).ConfigureAwait(true);
        ProjectionActivationWorkItem original = (await outbox.GetAsync(identity).ConfigureAwait(true)).ShouldNotBeNull();
        var intent = new ProjectionLegacyDeliveryIntent("01ARZ3NDEKTSV4RRFFQ69G5FAV", new string('x', 4096), "v1", 1, "event-1");
        await Should.ThrowAsync<ArgumentException>(() => ((IProjectionLegacyDeliveryOutbox)outbox)
            .TryBindLegacyDeliveryAsync(original, intent, CancellationToken.None)).ConfigureAwait(true);
        (await outbox.GetAsync(identity).ConfigureAwait(true)).ShouldBe(original);
        JsonSerializer.Serialize(original, JsonSerializerOptions.Web).ShouldNotContain("legacyDelivery");
    }
}
