using System.Text;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises finite SAVE retries and cancellation deterministically without Redis, Docker or a topology.</summary>
public sealed class QualificationRedisSavePolicyTests
{
    /// <summary>Only exact transient conflicts can precede the one required acknowledgement.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ExactBusyRepliesRequireOneFinalRealAcknowledgement(int busyCount)
    {
        var receipts = new List<CommandObservation>();
        var delays = new List<TimeSpan>();
        var tokens = new List<CancellationToken>();
        TimeSpan elapsed = TimeSpan.Zero;
        await QualificationRedisSavePolicy.RequireAcknowledgementAsync(token =>
        {
            tokens.Add(token);
            elapsed += TimeSpan.FromMilliseconds(20);
            var receipt = Receipt(receipts.Count < busyCount ? QualificationRedisSavePolicy.BusyReply + "\n\n" : "OK\n");
            receipts.Add(receipt);
            return Task.FromResult(receipt);
        }, TestContext.Current.CancellationToken, () => elapsed, (duration, token) =>
        {
            token.ThrowIfCancellationRequested();
            delays.Add(duration); elapsed += duration;
            return Task.CompletedTask;
        });
        receipts.Count.ShouldBe(busyCount + 1);
        receipts.Take(busyCount).ShouldAllBe(receipt => receipt.StandardOutput.Trim() == QualificationRedisSavePolicy.BusyReply);
        receipts[^1].StandardOutput.Trim().ShouldBe("OK");
        delays.Count.ShouldBe(busyCount);
        delays.ShouldAllBe(duration => duration == TimeSpan.FromMilliseconds(100));
        tokens.ShouldAllBe(token => token == tokens[0]);
    }

    /// <summary>Failed, incomplete and unrecognized replies never earn another attempt or acknowledgement.</summary>
    [Theory]
    [InlineData("ERR permission denied", 0, false, "", null)]
    [InlineData("ERR Background save already in progress: other failure", 0, false, "", null)]
    [InlineData("ERR background save already in progress", 0, false, "", null)]
    [InlineData("OK\nOK", 0, false, "", null)]
    [InlineData("", 0, false, "", null)]
    [InlineData("ERR Background save already in progress", 1, false, "", null)]
    [InlineData("OK", 1, false, "", null)]
    [InlineData("ERR Background save already in progress", 0, true, "", null)]
    [InlineData("OK", 0, true, "", null)]
    [InlineData("ERR Background save already in progress", 0, false, "ERR permission denied", null)]
    [InlineData("OK", 0, false, "ERR permission denied", null)]
    [InlineData("OK", 0, false, "", "cancelled")]
    public async Task EveryOtherReplyOrFailedObservationStopsWithoutRetry(string output, int exit, bool truncated, string error, string? interruption)
    {
        int attempts = 0;
        await Should.ThrowAsync<InvalidOperationException>(() => QualificationRedisSavePolicy.RequireAcknowledgementAsync(_ =>
        {
            attempts++;
            return Task.FromResult(Receipt(output) with { ExitCode = exit, OutputTruncated = truncated, StandardError = error, Interruption = interruption });
        }, TestContext.Current.CancellationToken));
        attempts.ShouldBe(1);
    }

    /// <summary>Busy replies cannot renew the original budget indefinitely.</summary>
    [Fact]
    public async Task BusyOnlyRepliesExhaustTheOriginalBudgetWithoutAnotherAttempt()
    {
        int attempts = 0; TimeSpan elapsed = TimeSpan.Zero;
        await Should.ThrowAsync<TimeoutException>(() => QualificationRedisSavePolicy.RequireAcknowledgementAsync(_ =>
        {
            attempts++; elapsed += TimeSpan.FromSeconds(70);
            return Task.FromResult(Receipt(QualificationRedisSavePolicy.BusyReply));
        }, TestContext.Current.CancellationToken, () => elapsed, (duration, _) => { elapsed += duration; return Task.CompletedTask; }));
        attempts.ShouldBe(3);
    }

    /// <summary>The final polling delay cannot consume a fresh retry budget.</summary>
    [Fact]
    public async Task FinalDelayUsesOnlyTheRemainingOriginalBudget()
    {
        int attempts = 0; TimeSpan elapsed = TimeSpan.Zero, delayed = TimeSpan.Zero;
        await Should.ThrowAsync<TimeoutException>(() => QualificationRedisSavePolicy.RequireAcknowledgementAsync(_ =>
        {
            attempts++; elapsed = QualificationRedisSavePolicy.Budget - TimeSpan.FromMilliseconds(50);
            return Task.FromResult(Receipt(QualificationRedisSavePolicy.BusyReply));
        }, TestContext.Current.CancellationToken, () => elapsed, (duration, _) =>
        {
            delayed = duration; elapsed += duration; return Task.CompletedTask;
        }));
        attempts.ShouldBe(1); delayed.ShouldBe(TimeSpan.FromMilliseconds(50));
    }

    /// <summary>A late OK cannot convert an expired SAVE into success.</summary>
    [Fact]
    public async Task AnAcknowledgementAfterTheOriginalBudgetCannotPass()
    {
        TimeSpan elapsed = TimeSpan.Zero;
        await Should.ThrowAsync<TimeoutException>(() => QualificationRedisSavePolicy.RequireAcknowledgementAsync(_ =>
        {
            elapsed = QualificationRedisSavePolicy.Budget;
            return Task.FromResult(Receipt("OK"));
        }, TestContext.Current.CancellationToken, () => elapsed));
    }

    /// <summary>The caller's cancellation remains effective at every retry boundary.</summary>
    [Theory]
    [InlineData("before")]
    [InlineData("attempt")]
    [InlineData("delay")]
    public async Task OriginalCancellationStopsWithoutManufacturingAnAcknowledgement(string phase)
    {
        using var cancellation = new CancellationTokenSource();
        int attempts = 0;
        if (phase == "before") cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => QualificationRedisSavePolicy.RequireAcknowledgementAsync(token =>
        {
            attempts++;
            if (phase == "attempt") cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Receipt(QualificationRedisSavePolicy.BusyReply));
        }, cancellation.Token, delay: (_, token) =>
        {
            cancellation.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask;
        }));
        attempts.ShouldBe(phase == "before" ? 0 : 1);
    }

    /// <summary>A measured command timeout remains a terminal failure.</summary>
    [Fact]
    public async Task ActualCommandTimeoutIsNotRetried()
    {
        int attempts = 0;
        await Should.ThrowAsync<TimeoutException>(() => QualificationRedisSavePolicy.RequireAcknowledgementAsync(_ =>
        {
            attempts++; throw new TimeoutException("actual measured command timed out");
        }, TestContext.Current.CancellationToken));
        attempts.ShouldBe(1);
    }

    /// <summary>Real backend-free processes retain every measured receipt under the unchanged shared deadline.</summary>
    [Fact]
    public async Task MeasuredRunnerRetainsEveryActualAttemptUnderOneOriginalDeadline()
    {
        await using var runtime = new QualificationRuntime();
        DateTimeOffset began = DateTimeOffset.UtcNow;
        int attempts = 0;
        await QualificationRedisSavePolicy.RequireAcknowledgementAsync(token => runtime.RunAsync(
            new("python3", ["-c", "import sys; print(sys.argv[1])", attempts++ < 2 ? QualificationRedisSavePolicy.BusyReply : "OK"],
                runtime.Root, QualificationRedisSavePolicy.Budget), token, executionStarted: began), TestContext.Current.CancellationToken);
        runtime.Commands.Count.ShouldBe(3);
        runtime.Commands.ShouldAllBe(command => command.DeadlineStarted == began && command.Command.Timeout == QualificationRedisSavePolicy.Budget
            && command.ExitCode == 0 && !command.OutputTruncated);
        runtime.Commands.Take(2).ShouldAllBe(command => command.StandardOutput.Trim() == QualificationRedisSavePolicy.BusyReply);
        runtime.Commands[^1].StandardOutput.Trim().ShouldBe("OK");
        Directory.GetFiles(runtime.Root, "command-*.json").Length.ShouldBe(3);
    }

    /// <summary>The production ownership guard runs before any process or retry seam.</summary>
    [Fact]
    public async Task RuntimeRefusesAnUnreservedProviderBeforeLaunchingAnyCommand()
    {
        await using var runtime = new QualificationRuntime();
        await Should.ThrowAsync<InvalidOperationException>(() => runtime.RequireRedisSaveAsync("foreign-provider"));
        runtime.Commands.ShouldBeEmpty();
    }

    private static CommandObservation Receipt(string output)
        => new(new("docker", ["exec", "synthetic-owned-provider", "redis-cli", "SAVE"], "/synthetic", QualificationRedisSavePolicy.Budget),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMilliseconds(20), 0, output, "", QualificationRuntime.Hash(Encoding.UTF8.GetBytes(output)));
}
