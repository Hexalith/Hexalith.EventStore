namespace Probe.Undeclared;

/// <summary>Writes an owned marker proving an unmanifested managed dependency executed.</summary>
public static class UndeclaredEffect
{
    /// <summary>Appends one marker to the effect path supplied by the isolated runner.</summary>
    public static void Write(string effectsPath)
        => File.AppendAllText(effectsPath, "managed:undeclared\n");
}
