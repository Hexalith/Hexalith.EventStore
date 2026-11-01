using System.Text;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Mutates the complete independent backup/restore fixture to enforce every SAVE receipt and its original deadline.</summary>
public sealed class HistoricalMatrixBackupRetryTests
{
    /// <summary>Valid single or retried acknowledgements retain all original byte, restore and destruction checks.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OwnedBusyRepliesThenOneOkPreserveTheFullMeasuredBackupChain(int busyCount)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        AddBusyAttempts(fixture, busyCount);
        fixture.RewriteSummary(); fixture.Validate();
    }

    /// <summary>A final OK never hides earlier failed, foreign, overlapping or renewed-budget receipts.</summary>
    [Theory]
    [InlineData("busy-only")]
    [InlineData("unrelated-error")]
    [InlineData("busy-suffix")]
    [InlineData("prior-nonzero")]
    [InlineData("prior-truncation")]
    [InlineData("prior-stderr")]
    [InlineData("final-nonzero")]
    [InlineData("final-truncation")]
    [InlineData("final-stderr")]
    [InlineData("cancelled")]
    [InlineData("timeout")]
    [InlineData("duplicate-ok")]
    [InlineData("busy-after-ok")]
    [InlineData("overlap")]
    [InlineData("foreign-ack")]
    [InlineData("copy-before-ack")]
    [InlineData("renewed-deadline")]
    [InlineData("renewed-timeout")]
    [InlineData("missing-deadline")]
    [InlineData("attempt-crosses-window")]
    [InlineData("total-span-over-budget")]
    public void EveryInvalidAttemptSequenceRefusesTheOtherwiseCompleteBackupChain(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int first = AddBusyAttempts(fixture, 2), final = first + 2;
        var prior = fixture.Commands[first]; var acknowledgement = fixture.Commands[final];
        switch (mutation)
        {
            case "busy-only": fixture.Commands[final] = WithOutput(acknowledgement, QualificationRedisSavePolicy.BusyReply); break;
            case "unrelated-error": fixture.Commands[first] = WithOutput(prior, "ERR permission denied\n"); break;
            case "busy-suffix": fixture.Commands[first] = WithOutput(prior, QualificationRedisSavePolicy.BusyReply + " extra error"); break;
            case "prior-nonzero": fixture.Commands[first] = prior with { ExitCode = 1 }; break;
            case "prior-truncation": fixture.Commands[first] = prior with { OutputTruncated = true }; break;
            case "prior-stderr": fixture.Commands[first] = WithOutput(prior, prior.StandardOutput, "ERR permission denied"); break;
            case "final-nonzero": fixture.Commands[final] = acknowledgement with { ExitCode = 1 }; break;
            case "final-truncation": fixture.Commands[final] = acknowledgement with { OutputTruncated = true }; break;
            case "final-stderr": fixture.Commands[final] = WithOutput(acknowledgement, acknowledgement.StandardOutput, "ERR permission denied"); break;
            case "cancelled": fixture.Commands[first] = prior with { Interruption = "cancelled" }; break;
            case "timeout": fixture.Commands[first] = prior with { Interruption = "timeout" }; break;
            case "duplicate-ok": fixture.Commands[first] = WithOutput(prior, "OK\n"); break;
            case "busy-after-ok":
                fixture.Commands[first] = WithOutput(prior, "OK\n");
                fixture.Commands[final] = WithOutput(acknowledgement, QualificationRedisSavePolicy.BusyReply); break;
            case "overlap": fixture.Commands[first + 1] = fixture.Commands[first + 1] with { Started = prior.Started }; break;
            case "foreign-ack": fixture.Commands[final] = acknowledgement with
                { Command = acknowledgement.Command with { Arguments = ["exec", "foreign-provider", "redis-cli", "SAVE"] } }; break;
            case "copy-before-ack":
                fixture.Commands[final + 1] = fixture.Commands[final + 1] with { Started = acknowledgement.Started }; break;
            case "renewed-deadline": fixture.Commands[first + 1] = fixture.Commands[first + 1] with { DeadlineStarted = acknowledgement.Started }; break;
            case "renewed-timeout": fixture.Commands[first + 1] = fixture.Commands[first + 1] with
                { Command = prior.Command with { Timeout = QualificationRedisSavePolicy.Budget + TimeSpan.FromSeconds(1) } }; break;
            case "missing-deadline": fixture.Commands[first] = prior with { DeadlineStarted = null }; break;
            case "attempt-crosses-window": fixture.Commands[first] = prior with
                { Started = fixture.Observations.Single(item => item.Scenario == "post-upgrade-restore" && item.Case == "3.128.2").Started!.Value.AddTicks(-1), ExitCode = 1 }; break;
            case "total-span-over-budget":
                int row = fixture.Observations.FindIndex(item => item.Scenario == "post-upgrade-restore" && item.Case == "3.128.2");
                fixture.Observations[row] = fixture.Observations[row] with { Ended = acknowledgement.Ended + QualificationRedisSavePolicy.Budget };
                fixture.Commands[final] = acknowledgement with { Ended = acknowledgement.Ended + QualificationRedisSavePolicy.Budget }; break;
            default: throw new InvalidOperationException("Unknown control.");
        }
        fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    private static int AddBusyAttempts(HistoricalMatrixSyntheticFixture fixture, int count)
    {
        var row = fixture.Observations.Single(item => item.Scenario == "post-upgrade-restore" && item.Case == "3.128.2");
        int index = fixture.Commands.FindIndex(command => command.Started >= row.Started && command.Ended <= row.Ended
            && command.Command.Arguments.LastOrDefault() == "SAVE");
        var acknowledgement = fixture.Commands[index];
        DateTimeOffset began = acknowledgement.Started.AddMilliseconds(-count * 200);
        fixture.Commands[index] = acknowledgement with { DeadlineStarted = began };
        for (int attempt = 0; attempt < count; attempt++)
        {
            DateTimeOffset started = began.AddMilliseconds(attempt * 200);
            fixture.Commands.Insert(index + attempt, WithOutput(acknowledgement, QualificationRedisSavePolicy.BusyReply + "\n\n") with
                { Started = started, Ended = started.AddMilliseconds(100), DeadlineStarted = began });
        }
        return index;
    }

    private static CommandObservation WithOutput(CommandObservation command, string output, string error = "")
        => command with { StandardOutput = output, StandardError = error, OutputHash = QualificationRuntime.Hash(Encoding.UTF8.GetBytes(output + error)) };
}
