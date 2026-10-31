namespace Hexalith.EventStore.ServiceDefaults.Tests.GeneratedRest;

/// <summary>The actual typed query used by the generated HTTP boundary proof.</summary>
public sealed record GeneratedRestProbeQuery
{
    /// <summary>The owner domain.</summary>
    public static string Domain => "generated-probe";
    /// <summary>The stable query discriminator.</summary>
    public static string QueryType => "read-probe";
    /// <summary>The actual projection identity.</summary>
    public static string ProjectionType => "probe";
}
