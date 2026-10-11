using System.Diagnostics;

using Hexalith.EventStore.DomainService;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Bounded admission for uncooperative synchronous and asynchronous adapter reads.</summary>
[Collection("Process read slots")]
public sealed class ProcessReadDeadlineTests
{
    /// <summary>Timed-out uncooperative workers exhaust a finite pool without blocking later callers in its queue.</summary>
    [Fact]
    public async Task ExhaustedWorkersRejectPromptlyAndRecoverAfterRelease()
    {
        TaskCompletionSource<int> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>[] timedOut = Enumerable.Range(0, 16)
            .Select(_ => ProcessReadDeadline.RunAsync(_ => held.Task, TimeSpan.FromMilliseconds(100), CancellationToken.None))
            .ToArray();
        try
        {
            await Task.Delay(150);
            Stopwatch elapsed = Stopwatch.StartNew();
            await Should.ThrowAsync<ProcessReadAdmissionException>(() =>
                ProcessReadDeadline.RunAsync(_ => Task.FromResult(1), TimeSpan.FromSeconds(1), CancellationToken.None));
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(400));
        }
        finally
        {
            held.SetResult(1);
            try { await Task.WhenAll(timedOut); }
            catch (TimeoutException) { }
        }

        int? recovered = null;
        for (int attempt = 0; attempt < 100 && recovered is null; attempt++)
        {
            try
            {
                recovered = await ProcessReadDeadline.RunAsync(_ => Task.FromResult(1), TimeSpan.FromSeconds(1), CancellationToken.None);
            }
            catch (ProcessReadAdmissionException)
            {
                await Task.Delay(10);
            }
        }
        recovered.ShouldBe(1);
    }
}
