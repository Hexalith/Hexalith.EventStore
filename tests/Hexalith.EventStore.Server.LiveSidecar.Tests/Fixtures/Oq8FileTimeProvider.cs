using System.Globalization;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Reads the OQ8 wall clock from a fixture-owned file shared by child processes.</summary>
internal sealed class Oq8FileTimeProvider : TimeProvider
{
    /// <summary>Names the environment variable containing the shared clock file.</summary>
    public const string ClockFileEnvironmentVariable = "HEXALITH_OQ8_CLOCK_FILE";

    private readonly string _clockFile = Environment.GetEnvironmentVariable(ClockFileEnvironmentVariable)
        ?? throw new InvalidOperationException("The OQ8 clock file is not configured.");

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        string value = File.ReadAllText(_clockFile).Trim();
        long ticks = long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}
