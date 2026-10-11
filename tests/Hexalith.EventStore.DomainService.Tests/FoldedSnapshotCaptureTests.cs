using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exercises the qualified request-to-wire folded-state capture seam.</summary>
public sealed class FoldedSnapshotCaptureTests
{
    [Fact]
    public async Task RequestedQualifiedCaptureUsesOnceAdmittedBytesAndDetachedApplyInput()
    {
        var processor = new SnapshotCaptureTestProcessor();
        using ServiceProvider provider = BuildProvider(processor);
        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: true, "test-profile"));

        wire.Events.ShouldHaveSingleItem();
        Encoding.UTF8.GetString(wire.Events[0].Payload).ShouldBe("{\"amount\":1}");
        processor.ReturnedEvent.ShouldNotBeNull().Amount.ShouldBe(99);
        using JsonDocument document = JsonDocument.Parse(wire.PostCommandStateJson.ShouldNotBeNull());
        document.RootElement.GetProperty("count").GetInt32().ShouldBe(1);
        document.RootElement.TryGetProperty("events", out _).ShouldBeFalse();

        using var output = new MemoryStream();
        await BoundedV1WireResultResponse.WriteAsync(output, wire, CancellationToken.None);
        DomainServiceWireResult? parsed = JsonSerializer.Deserialize<DomainServiceWireResult>(
            output.ToArray(), JsonSerializerOptions.Web);
        parsed.ShouldNotBeNull().PostCommandStateJson.ShouldBe(wire.PostCommandStateJson);
    }

    [Theory]
    [InlineData(false, "test-profile")]
    [InlineData(true, "wrong-profile")]
    public async Task MissingOrMismatchedQualificationOmitsStateWithoutChangingEvents(bool include, string profileId)
    {
        var processor = new SnapshotCaptureTestProcessor();
        using ServiceProvider provider = BuildProvider(processor);
        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include, profileId));

        wire.PostCommandStateJson.ShouldBeNull();
        Encoding.UTF8.GetString(wire.Events[0].Payload).ShouldBe("{\"amount\":1}");
    }

    [Fact]
    public async Task CaptureApplyFailureLeavesTheOnceAdmittedEventAndReturnedGraphUnchanged()
    {
        var processor = new SnapshotCaptureTestProcessor();
        using ServiceProvider provider = BuildProvider(processor);
        DomainServiceRequest request = CreateRequest(include: true, "test-profile") with
        {
            CurrentState = new SnapshotCaptureTestState { Count = 4, ThrowOnApply = true },
        };

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(provider, request);

        wire.PostCommandStateJson.ShouldBeNull();
        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
        processor.ReturnedEvent.ShouldNotBeNull().Amount.ShouldBe(99);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider,
            request with { IncludePostCommandState = false });
        wire.Events[0].Payload.ShouldBe(baseline.Events[0].Payload);
        wire.IsRejection.ShouldBe(baseline.IsRejection);
        wire.ResultPayload.ShouldBe(baseline.ResultPayload);
    }

    [Fact]
    public async Task PersistedRejectionUsesItsOnceAdmittedEventBytesForTheFold()
    {
        var processor = new SnapshotCaptureTestProcessor { Reject = true };
        using ServiceProvider provider = BuildProvider(processor);

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: true, "test-profile"));

        wire.IsRejection.ShouldBeTrue();
        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":3}"u8.ToArray());
        processor.ReturnedRejectionEvent.ShouldNotBeNull().Amount.ShouldBe(99);
        using JsonDocument document = JsonDocument.Parse(wire.PostCommandStateJson.ShouldNotBeNull());
        document.RootElement.GetProperty("count").GetInt32().ShouldBe(3);
    }

    private static ServiceProvider BuildProvider(SnapshotCaptureTestProcessor processor)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("d", processor);
        services.AddEventStoreBoundedV1DomainSerialization("d", profile => profile.Add<SnapshotCaptureTestEvent>(
            typeof(SnapshotCaptureTestEvent).FullName!, "json", 64,
            static (_, sink, _) => { sink.Write("{\"amount\":1}"u8); return Task.CompletedTask; })
            .Add<SnapshotCaptureTestRejectionEvent>(typeof(SnapshotCaptureTestRejectionEvent).FullName!, "json", 64,
                static (_, sink, _) => { sink.Write("{\"amount\":3}"u8); return Task.CompletedTask; }));
        services.AddEventStoreFoldedSnapshotQualification<SnapshotCaptureTestState>(
            "d", "aggregate", "test-profile", "test-parity-evidence", SnapshotCaptureTestLimits.Value);
        return services.BuildServiceProvider();
    }

    private static DomainServiceRequest CreateRequest(bool include, string profileId)
        => new(new CommandEnvelope("01HZX6FKA8B3YTX6MBQ3AYT3PT", "tenant", "d", "aggregate-id",
            "Create", [], "01HZX6FKA8B3YTX6MBQ3AYT3PT", null, "user", null), null)
        {
            IncludePostCommandState = include,
            SnapshotCaptureProfileId = profileId,
        };
}
