using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Proves failed observations/accounting still execute every final evidence action.</summary>
public sealed class FaultExperimentTests
{
    /// <summary>Observation failure remains first while later failed accounting cannot skip durable evidence.</summary>
    [Fact]
    public async Task FailedObservationAndAccountingPreserveOriginalAndCaptureRemainingEvidenceAsync()
    {
        var original = new InvalidOperationException("boundary was not observed");
        var actions = new List<string>();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        AggregateException failure = await Should.ThrowAsync<AggregateException>(() => FaultExperiment.RunAsync(
            () => Task.FromException(original), () =>
            {
                actions.Add("break-gate");
                pending.SetException(new IOException("broken"));
                return Task.CompletedTask;
            }, async () =>
            {
                await Should.ThrowAsync<IOException>(() => pending.Task);
                actions.Add("settled-request");
            }, () =>
            {
                actions.Add("sdk-send-count");
                throw new InvalidDataException("duplicate SDK send");
            }, () =>
            {
                actions.Add("provider-accounting-and-instrumentation-removal");
                return Task.CompletedTask;
            }, () =>
            {
                actions.Add("complete-reservation-and-witness");
                return Task.CompletedTask;
            }));
        failure.InnerExceptions[0].ShouldBeSameAs(original);
        failure.InnerExceptions.Count.ShouldBe(2);
        pending.Task.IsCompleted.ShouldBeTrue();
        actions.ShouldBe(new[] { "break-gate", "settled-request", "sdk-send-count", "provider-accounting-and-instrumentation-removal", "complete-reservation-and-witness" });
    }
}
