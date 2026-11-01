using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Uses real process and read-only daemon observations to preserve completed child ownership ledgers.</summary>
public sealed class QualificationRuntimeLedgerTests
{
    /// <summary>Only actual disposed children can contribute their complete commands; copying twice is refused.</summary>
    [Fact]
    public async Task CompletedChildImportsItsActualLedgerAfterIndependentDisposal()
    {
        await using var parent = new QualificationRuntime();
        var child = new QualificationRuntime();
        try
        {
            await child.CaptureSharedContainersAsync();
            await child.RequireAsync("python3", ["-c", "print('actual-closed-child-ledger')"]);
            Should.Throw<InvalidOperationException>(() => parent.ImportCompletedRuntime(child));
            await child.DisposeAsync();
            parent.ImportCompletedRuntime(child);
            parent.Commands.ShouldContain(command => command.Command.WorkingDirectory == child.Root
                && command.StandardOutput.Trim() == "actual-closed-child-ledger" && command.ExitCode == 0);
            parent.CleanupPasses.ShouldContain(pass => pass.Owner == child.Id && pass.Root == child.Root && pass.Failures == 0);
            Should.Throw<InvalidOperationException>(() => parent.ImportCompletedRuntime(child));
            Should.Throw<InvalidOperationException>(() => parent.ImportCompletedRuntime(parent));
        }
        finally { await child.DisposeAsync(); }
    }
}
