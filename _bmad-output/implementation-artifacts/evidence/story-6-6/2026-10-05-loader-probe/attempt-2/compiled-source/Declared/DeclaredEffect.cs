namespace Probe.Declared;

/// <summary>Writes the positive declared managed dependency's owned fixture effect.</summary>
public static class DeclaredEffect
{
    /// <summary>Appends one marker to the effect path supplied by the isolated runner.</summary>
    public static void Write(string effectsPath)
        => File.AppendAllText(effectsPath, "managed:declared\n");
}
