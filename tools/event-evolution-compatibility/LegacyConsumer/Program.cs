using System.Runtime.CompilerServices;

using Hexalith.EventStore.Server.Actors;

/// <summary>Calls the prior constructor and deconstructor without rebuilding against the replacement assembly.</summary>
internal static class Program
{
    private static int Main()
    {
        try
        {
            Probe();
            Console.WriteLine("legacy-twelve-member-consumer: passed");
            return 0;
        }
        catch (MissingMethodException error)
        {
            Console.WriteLine(error.Message);
            return 2;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Probe()
    {
        var failedAt = DateTimeOffset.UnixEpoch;
        var record = new UnpublishedEventsRecord("correlation", 1, 3, 3, "command", true,
            failedAt, 2, "failure", "message", true, failedAt);
        var (correlation, start, end, count, command, rejection, time, retry, failure, message, deadLettered, reminder) = record;
        if (correlation != "correlation" || start != 1 || end != 3 || count != 3 || command != "command"
            || !rejection || time != failedAt || retry != 2 || failure != "failure" || message != "message"
            || !deadLettered || reminder != failedAt)
        {
            throw new InvalidOperationException("The prior record contract returned different values.");
        }
    }
}
