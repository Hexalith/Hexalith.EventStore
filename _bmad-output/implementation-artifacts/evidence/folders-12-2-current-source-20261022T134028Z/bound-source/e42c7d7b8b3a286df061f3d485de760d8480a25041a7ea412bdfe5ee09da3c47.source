using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Time.Testing;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Crosses a source deadline after installation and the immutable commit decision.</summary>
internal sealed class RetainedDeadlineLiveFaultInjector(FakeTimeProvider clock, DateTimeOffset deadline) : IReadModelBatchFaultInjector
{
    public Task InjectAsync(ReadModelBatchPhase phase, int ordinal, CancellationToken cancellationToken)
    {
        if (phase == ReadModelBatchPhase.AfterCommit) clock.SetUtcNow(deadline);
        return Task.CompletedTask;
    }
}
