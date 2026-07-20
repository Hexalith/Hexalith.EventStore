using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

public class TestServiceOverridesTests {
    [Fact]
    public async Task ReplaceCommandRouter_AlsoReplacesMandatoryProjectionActivationOutboxAsync() {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ICommandRouter, ThrowingCommandRouter>();
        _ = services.AddSingleton<IProjectionActivationOutbox, ThrowingProjectionActivationOutbox>();
        var expectedRouter = new FakeCommandRouter();

        TestServiceOverrides.ReplaceCommandRouter(services, expectedRouter);

        await using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<ICommandRouter>().ShouldBeSameAs(expectedRouter);
        provider.GetServices<ICommandRouter>().ShouldHaveSingleItem().ShouldBeSameAs(expectedRouter);
        IProjectionActivationOutbox outbox = provider.GetRequiredService<IProjectionActivationOutbox>();
        outbox.ShouldNotBeOfType<ThrowingProjectionActivationOutbox>();
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        ProjectionActivationWorkItem workItem = ProjectionActivationWorkItem.Create(identity, DateTimeOffset.UtcNow);
        await outbox.EnsureAsync(identity);
        (await outbox.GetAsync(identity)).ShouldBeNull();
        (await outbox.GetDueAsync(DateTimeOffset.UtcNow, 1)).ShouldBeEmpty();
        await outbox.CompleteAsync(workItem);
        await outbox.DeferAsync(workItem, DateTimeOffset.UtcNow.AddMinutes(1));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => outbox.EnsureAsync(identity, cancellation.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => outbox.GetAsync(identity, cancellation.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => outbox.GetDueAsync(DateTimeOffset.UtcNow, 1, cancellation.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => outbox.CompleteAsync(workItem, cancellation.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => outbox.DeferAsync(workItem, DateTimeOffset.UtcNow, cancellation.Token));
    }

    [Fact]
    public void RemoveDaprHealthChecks_RemovesWriterProtocolAndDaprChecksOnly() {
        var services = new ServiceCollection();
        _ = services.AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy())
            .AddCheck("dapr-sidecar", static () => HealthCheckResult.Healthy())
            .AddCheck("projection-delivery-writer-protocol", static () => HealthCheckResult.Healthy());

        TestServiceOverrides.RemoveDaprHealthChecks(services);
        _ = services.AddHealthChecks()
            .AddCheck("dapr-registered-after-override", static () => HealthCheckResult.Healthy());

        using ServiceProvider provider = services.BuildServiceProvider();
        string[] names = [.. provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations
            .Select(static registration => registration.Name)];
        names.ShouldBe(["self"]);
    }

    private sealed class ThrowingCommandRouter : ICommandRouter {
        public Task<CommandProcessingResult> RouteCommandAsync(
            SubmitCommand command,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The production command router was not replaced.");
    }

    private sealed class ThrowingProjectionActivationOutbox : IProjectionActivationOutbox {
        public Task EnsureAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The DAPR projection activation outbox was not replaced.");

        public Task<ProjectionActivationWorkItem?> GetAsync(
            AggregateIdentity identity,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The DAPR projection activation outbox was not replaced.");

        public Task CompleteAsync(
            ProjectionActivationWorkItem workItem,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The DAPR projection activation outbox was not replaced.");

        public Task<IReadOnlyList<ProjectionActivationWorkItem>> GetDueAsync(
            DateTimeOffset dueUtc,
            int maximumCount,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The DAPR projection activation outbox was not replaced.");

        public Task DeferAsync(
            ProjectionActivationWorkItem workItem,
            DateTimeOffset nextDueUtc,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The DAPR projection activation outbox was not replaced.");
    }
}
