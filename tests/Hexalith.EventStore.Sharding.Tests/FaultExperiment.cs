using System.Runtime.ExceptionServices;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Preserves the experimental failure while running every independent final evidence action.</summary>
internal static class FaultExperiment
{
    /// <summary>Always breaks/drains/captures via the supplied final actions, even when observation fails.</summary>
    internal static async Task RunAsync(Func<Task> observe, params Func<Task>[] finalEvidence)
    {
        var failures = new List<Exception>();
        try { await observe(); }
        catch (Exception exception) { failures.Add(exception); }
        foreach (Func<Task> evidence in finalEvidence)
        {
            try { await evidence(); }
            catch (Exception exception) { failures.Add(exception); }
        }

        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("The experiment failed; all final evidence actions were attempted.", failures);
    }
}
