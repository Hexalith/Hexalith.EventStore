/// <summary>Retains every Boolean check as executed; failures are never discarded.</summary>
internal sealed class ExecutedChecks
{
    private readonly List<(string Id, bool Passed)> _checks = [];

    /// <summary>Records one stable, unique check.</summary>
    internal void Record(string id, bool passed)
    {
        if (_checks.Any(check => check.Id == id))
        {
            throw new InvalidOperationException("Duplicate executed check.");
        }

        _checks.Add((id, passed));
    }

    /// <summary>Returns observations and counters derived from those observations.</summary>
    internal object Read() => new
    {
        checks = _checks.Select(check => new { id = check.Id, passed = check.Passed }).ToArray(),
        assertions = new { attempted = _checks.Count, passed = _checks.Count(check => check.Passed), failed = _checks.Count(check => !check.Passed) },
    };
}
